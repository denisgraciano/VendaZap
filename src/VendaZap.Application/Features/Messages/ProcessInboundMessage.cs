using MediatR;
using Microsoft.Extensions.Logging;
using VendaZap.Application.Common.Interfaces;
using VendaZap.Domain.Common;
using VendaZap.Domain.Entities;
using VendaZap.Domain.Enums;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Application.Features.Messages;

// ─── Inbound Webhook Processing ───────────────────────────────────────────────

public record ProcessInboundMessageCommand(
    Guid TenantId,
    string FromPhone,
    string? ContactName,
    string MessageBody,
    string WhatsAppMessageId,
    MessageType MessageType = MessageType.Text,
    string? MediaUrl = null) : IRequest<Result>;

public class ProcessInboundMessageCommandHandler : IRequestHandler<ProcessInboundMessageCommand, Result>
{
    private readonly IContactRepository _contacts;
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IProductRepository _products;
    private readonly ITenantRepository _tenants;
    private readonly IAutoReplyTemplateRepository _autoReplies;
    private readonly IConversationAIService _ai;
    private readonly IWhatsAppService _whatsApp;
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProcessInboundMessageCommandHandler> _logger;

    private const int ConfidenceThreshold = 70;
    private const int HistoryLimit = 20;

    public ProcessInboundMessageCommandHandler(
        IContactRepository contacts, IConversationRepository conversations,
        IMessageRepository messages, IProductRepository products,
        ITenantRepository tenants, IAutoReplyTemplateRepository autoReplies,
        IConversationAIService ai, IWhatsAppService whatsApp,
        INotificationService notifications, IUnitOfWork uow,
        ILogger<ProcessInboundMessageCommandHandler> logger)
    {
        _contacts = contacts; _conversations = conversations; _messages = messages;
        _products = products; _tenants = tenants; _autoReplies = autoReplies;
        _ai = ai; _whatsApp = whatsApp; _notifications = notifications;
        _uow = uow; _logger = logger;
    }

    public async Task<Result> Handle(ProcessInboundMessageCommand request, CancellationToken ct)
    {
        // Idempotency: skip if already processed
        var existing = await _messages.GetByWhatsAppIdAsync(request.WhatsAppMessageId, ct);
        if (existing is not null) return Result.Success();

        var tenant = await _tenants.GetByIdAsync(request.TenantId, ct);
        if (tenant is null || !tenant.IsActive()) return Result.Failure(Error.NotFound("Tenant"));

        // 1. Upsert contact
        var contact = await _contacts.GetOrCreateAsync(request.TenantId, request.FromPhone, request.ContactName, ct);
        if (contact.IsBlocked) return Result.Success(); // silently ignore blocked contacts

        contact.RecordInteraction();

        // 2. Get or create conversation
        var conversation = await _conversations.GetActiveByContactAsync(request.TenantId, contact.Id, ct);
        bool isNewConversation = conversation is null;

        if (isNewConversation)
        {
            conversation = Conversation.Create(request.TenantId, contact.Id);
            await _conversations.AddAsync(conversation, ct);
        }

        // 3. Persist inbound message
        var inboundMessage = Message.CreateInbound(
            conversation!.Id, request.TenantId, request.MessageBody,
            request.WhatsAppMessageId, request.MessageType, request.MediaUrl);
        await _messages.AddAsync(inboundMessage, ct);
        conversation.AddMessage(inboundMessage);

        await _uow.SaveChangesAsync(ct);
        await _messages.InvalidateConversationCacheAsync(conversation.Id, ct);

        // Mark as read on WhatsApp
        await _whatsApp.MarkMessageAsReadAsync(
            tenant.WhatsAppPhoneNumberId, tenant.WhatsAppAccessToken, request.WhatsAppMessageId, ct);

        // 4. Notify agents via SignalR
        await _notifications.NotifyNewMessageAsync(request.TenantId, conversation.Id, request.MessageBody, ct);

        // 5. Generate response if in Bot mode
        if (conversation.IsBot())
            await HandleBotResponseAsync(tenant, conversation, contact, request.MessageBody, isNewConversation, ct);

        return Result.Success();
    }

    private async Task HandleBotResponseAsync(
        Tenant tenant, Conversation conversation, Contact contact,
        string userMessage, bool isNewConversation, CancellationToken ct)
    {
        try
        {
            await _whatsApp.SendTypingIndicatorAsync(
                tenant.WhatsAppPhoneNumberId, tenant.WhatsAppAccessToken, contact.PhoneNumber, ct);

            // Check auto-reply templates first (keyword-based, fast)
            var autoReplies = await _autoReplies.GetActiveByTenantAsync(tenant.Id, ct);
            var matchedReply = autoReplies
                .OrderByDescending(t => t.Priority)
                .FirstOrDefault(t => t.Matches(userMessage));

            if (matchedReply is not null)
            {
                await SendBotMessageAsync(tenant, conversation, contact, matchedReply.Response, ct);
                return;
            }

            // Send welcome message for brand-new conversations without AI call
            if (isNewConversation)
            {
                var welcome = tenant.WelcomeMessage ?? "Olá! 👋 Como posso te ajudar?";
                await SendBotMessageAsync(tenant, conversation, contact, welcome, ct);
                return;
            }

            // Check for explicit human transfer keywords before hitting AI
            if (IsHumanTransferRequest(userMessage) && tenant.IsHumanTakeoverEnabled)
            {
                await TransferToHumanAsync(tenant, conversation, contact, ct);
                return;
            }

            // Load last 20 messages as AI context
            var recentMessages = await _messages.GetByConversationAsync(conversation.Id, 1, HistoryLimit, ct);
            var history = recentMessages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ConversationMessageContext(
                    Role: m.Direction == MessageDirection.Inbound ? "user" : "assistant",
                    Content: m.Content,
                    SentAt: m.CreatedAt))
                .ToList();

            // Build AI context with tenant and product data
            var products = await _products.GetByTenantAsync(tenant.Id, true, ct);
            var productInfos = products.Select(p =>
                new AiProductInfo(p.Id, p.Name, p.Price.Amount, p.Description, p.IsAvailable())).ToList();

            // Get/create AI thread for conversation tracking
            if (conversation.AiThreadId is null)
            {
                var threadId = Guid.NewGuid().ToString("N");
                conversation.SetAiThreadId(threadId);
                _conversations.Update(conversation);
                await _uow.SaveChangesAsync(ct);
            }

            var aiContext = new AiSalesContext(
                TenantName: tenant.Name,
                ContactName: contact.GetDisplayName(),
                CurrentStage: conversation.Stage.ToString(),
                AvailableProducts: productInfos,
                CartSummary: conversation.CartJson,
                PaymentInfo: BuildPaymentInfoString(tenant),
                CustomInstructions: null);

            // Call the AI engine
            var aiResponse = await _ai.ProcessMessageAsync(tenant.Id, userMessage, history, aiContext, ct);

            _logger.LogInformation(
                "AI response for conversation {ConversationId}: intent={Intent}, confidence={Confidence}, next={NextAction}",
                conversation.Id, aiResponse.Intent, aiResponse.Confidence, aiResponse.NextAction);

            // Fallback: low confidence → transfer to human
            if (aiResponse.Confidence < ConfidenceThreshold && tenant.IsHumanTakeoverEnabled)
            {
                _logger.LogInformation(
                    "Low AI confidence ({Confidence}) for conversation {ConversationId}. Transferring to human.",
                    aiResponse.Confidence, conversation.Id);
                await TransferToHumanAsync(tenant, conversation, contact, ct);
                return;
            }

            // Intent-based routing
            if (aiResponse.Intent is "transferir_humano" && tenant.IsHumanTakeoverEnabled)
            {
                await TransferToHumanAsync(tenant, conversation, contact, ct);
                return;
            }

            // Advance conversation stage based on intent
            AdvanceStageFromIntent(conversation, aiResponse.Intent);

            await SendBotMessageAsync(tenant, conversation, contact, aiResponse.Message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating bot response for conversation {ConversationId}", conversation.Id);
            await SendBotMessageAsync(tenant, conversation, contact,
                "Desculpe, ocorreu um erro. Tente novamente em instantes.", ct);
        }
    }

    private async Task TransferToHumanAsync(
        Tenant tenant, Conversation conversation, Contact contact, CancellationToken ct)
    {
        conversation.TransferToHuman();
        _conversations.Update(conversation);
        await SendBotMessageAsync(tenant, conversation, contact,
            "Ok! Vou chamar um atendente para você. Por favor, aguarde um momento. 🙏", ct);
        await _notifications.NotifyHumanTakeoverRequestAsync(
            tenant.Id, conversation.Id, contact.GetDisplayName(), ct);
    }

    private static void AdvanceStageFromIntent(Conversation conversation, string intent)
    {
        var next = intent switch
        {
            "consulta_produto"  => ConversationStage.BrowsingProducts,
            "interesse_compra"  => ConversationStage.ProductSelected,
            "finalizar_pedido"  => ConversationStage.ConfirmingOrder,
            _                   => (ConversationStage?)null
        };

        if (next.HasValue && next.Value > conversation.Stage)
            conversation.AdvanceStage(next.Value);
    }

    private async Task SendBotMessageAsync(
        Tenant tenant, Conversation conversation, Contact contact, string content, CancellationToken ct)
    {
        var outbound = Message.CreateOutbound(conversation.Id, tenant.Id, content, MessageSource.Bot);
        await _messages.AddAsync(outbound, ct);
        conversation.AddMessage(outbound);
        _conversations.Update(conversation);
        await _uow.SaveChangesAsync(ct);
        await _messages.InvalidateConversationCacheAsync(conversation.Id, ct);

        var waId = await _whatsApp.SendTextMessageAsync(
            tenant.WhatsAppPhoneNumberId, tenant.WhatsAppAccessToken, contact.PhoneNumber, content, ct);

        if (waId is not null) outbound.SetWhatsAppMessageId(waId);
        await _uow.SaveChangesAsync(ct);
    }

    private static bool IsHumanTransferRequest(string message)
    {
        var lower = message.ToLower();
        return lower.Contains("falar com atendente") || lower.Contains("falar com humano") ||
               lower.Contains("falar com pessoa") || lower.Contains("atendimento humano") ||
               lower.Contains("preciso de ajuda") && lower.Contains("atend");
    }

    private static string BuildPaymentInfoString(Tenant tenant) =>
        "Formas de pagamento disponíveis nesta loja.";
}

// ─── WhatsApp Status Webhook ──────────────────────────────────────────────────

public record ProcessMessageStatusCommand(
    string WhatsAppMessageId,
    string Status,
    DateTime Timestamp) : IRequest<Result>;

public class ProcessMessageStatusCommandHandler : IRequestHandler<ProcessMessageStatusCommand, Result>
{
    private readonly IMessageRepository _messages;
    private readonly IUnitOfWork _uow;

    public ProcessMessageStatusCommandHandler(IMessageRepository messages, IUnitOfWork uow)
    {
        _messages = messages; _uow = uow;
    }

    public async Task<Result> Handle(ProcessMessageStatusCommand request, CancellationToken ct)
    {
        var message = await _messages.GetByWhatsAppIdAsync(request.WhatsAppMessageId, ct);
        if (message is null) return Result.Success();

        switch (request.Status.ToLower())
        {
            case "delivered": message.MarkDelivered(); break;
            case "read": message.MarkRead(); break;
            case "failed": message.MarkFailed("WhatsApp delivery failure"); break;
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
