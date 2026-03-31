using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using VendaZap.Application.Features.Messages;
using VendaZap.Domain.Enums;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Infrastructure.Messaging.Consumers;

// ─── Message Contracts ────────────────────────────────────────────────────────

public record OutgoingWhatsAppMessage(
    Guid TenantId,
    string PhoneNumberId,
    string AccessToken,
    string ToPhone,
    string MessageType,
    string? TextBody = null,
    string? ImageUrl = null,
    string? ImageCaption = null,
    IEnumerable<WhatsAppButton>? Buttons = null,
    string? ListButtonText = null,
    IEnumerable<WhatsAppListSection>? ListSections = null);

public record InboundWhatsAppMessage(
    Guid TenantId,
    string FromPhone,
    string? ContactName,
    string MessageBody,
    string WhatsAppMessageId,
    string MessageType = "text",
    string? MediaUrl = null);

public record WhatsAppStatusUpdate(
    string WhatsAppMessageId,
    string Status,
    DateTime Timestamp);

public record AbandonedCartJob(Guid TenantId, Guid ConversationId);

public record FollowUpJob(Guid TenantId, Guid ConversationId, Guid CampaignId, string Message);

// ─── Consumers ────────────────────────────────────────────────────────────────

public class InboundWhatsAppMessageConsumer : IConsumer<InboundWhatsAppMessage>
{
    private readonly IMediator _mediator;
    private readonly ILogger<InboundWhatsAppMessageConsumer> _logger;

    public InboundWhatsAppMessageConsumer(IMediator mediator, ILogger<InboundWhatsAppMessageConsumer> logger)
    {
        _mediator = mediator; _logger = logger;
    }

    public async Task Consume(ConsumeContext<InboundWhatsAppMessage> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Processing inbound WhatsApp message from {Phone} for tenant {TenantId}",
            msg.FromPhone[..4] + "****", msg.TenantId);

        var messageType = msg.MessageType.ToLower() switch
        {
            "image" => MessageType.Image,
            "audio" => MessageType.Audio,
            "video" => MessageType.Video,
            "document" => MessageType.Document,
            _ => MessageType.Text
        };

        var command = new ProcessInboundMessageCommand(
            msg.TenantId, msg.FromPhone, msg.ContactName,
            msg.MessageBody, msg.WhatsAppMessageId, messageType, msg.MediaUrl);

        var result = await _mediator.Send(command, context.CancellationToken);

        if (result.IsFailure)
            _logger.LogWarning("Failed to process inbound message: {Error}", result.Error.Description);
    }
}

public class WhatsAppStatusUpdateConsumer : IConsumer<WhatsAppStatusUpdate>
{
    private readonly IMediator _mediator;

    public WhatsAppStatusUpdateConsumer(IMediator mediator) => _mediator = mediator;

    public async Task Consume(ConsumeContext<WhatsAppStatusUpdate> context)
    {
        var msg = context.Message;
        await _mediator.Send(new ProcessMessageStatusCommand(
            msg.WhatsAppMessageId, msg.Status, msg.Timestamp),
            context.CancellationToken);
    }
}

public class AbandonedCartConsumer : IConsumer<AbandonedCartJob>
{
    private readonly IConversationRepository _conversations;
    private readonly ICampaignRepository _campaigns;
    private readonly Application.Common.Interfaces.IWhatsAppService _whatsApp;
    private readonly ITenantRepository _tenants;
    private readonly ILogger<AbandonedCartConsumer> _logger;

    public AbandonedCartConsumer(
        IConversationRepository conversations, ICampaignRepository campaigns,
        Application.Common.Interfaces.IWhatsAppService whatsApp, ITenantRepository tenants,
        ILogger<AbandonedCartConsumer> logger)
    {
        _conversations = conversations; _campaigns = campaigns;
        _whatsApp = whatsApp; _tenants = tenants; _logger = logger;
    }

    public async Task Consume(ConsumeContext<AbandonedCartJob> context)
    {
        var job = context.Message;
        var conversation = await _conversations.GetByIdAsync(job.ConversationId, context.CancellationToken);
        if (conversation is null || conversation.CartJson == "{}") return;

        var tenant = await _tenants.GetByIdAsync(job.TenantId, context.CancellationToken);
        if (tenant is null || !tenant.IsActive()) return;

        var campaigns = await _campaigns.GetActiveByTriggerAsync(
            job.TenantId, CampaignTrigger.AbandonedCart, context.CancellationToken);

        var campaign = campaigns.FirstOrDefault();
        if (campaign is null) return;

        var message = campaign.InterpolateMessage(
            conversation.Contact?.GetDisplayName() ?? "Cliente");

        await _whatsApp.SendTextMessageAsync(
            tenant.WhatsAppPhoneNumberId, tenant.WhatsAppAccessToken,
            conversation.Contact!.PhoneNumber, message, context.CancellationToken);

        campaign.RecordSent();
        _logger.LogInformation("Sent abandoned cart message for conversation {ConversationId}", job.ConversationId);
    }
}

public class OutgoingWhatsAppMessageConsumer : IConsumer<OutgoingWhatsAppMessage>
{
    private readonly IWhatsAppClient _whatsAppClient;
    private readonly ILogger<OutgoingWhatsAppMessageConsumer> _logger;

    public OutgoingWhatsAppMessageConsumer(IWhatsAppClient whatsAppClient, ILogger<OutgoingWhatsAppMessageConsumer> logger)
    {
        _whatsAppClient = whatsAppClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OutgoingWhatsAppMessage> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Processing outgoing WhatsApp {MessageType} for tenant {TenantId} to {Phone}",
            msg.MessageType, msg.TenantId, msg.ToPhone.Length > 4 ? msg.ToPhone[..4] + "****" : "****");

        string? messageId = msg.MessageType switch
        {
            "text" => await _whatsAppClient.SendTextAsync(
                msg.PhoneNumberId, msg.AccessToken, msg.ToPhone,
                msg.TextBody ?? string.Empty, context.CancellationToken),

            "image" => await _whatsAppClient.SendImageAsync(
                msg.PhoneNumberId, msg.AccessToken, msg.ToPhone,
                msg.ImageUrl!, msg.ImageCaption, context.CancellationToken),

            "interactive_buttons" when msg.Buttons is not null => await _whatsAppClient.SendInteractiveButtonsAsync(
                msg.PhoneNumberId, msg.AccessToken, msg.ToPhone,
                msg.TextBody ?? string.Empty, msg.Buttons, context.CancellationToken),

            "interactive_list" when msg.ListSections is not null => await _whatsAppClient.SendInteractiveListAsync(
                msg.PhoneNumberId, msg.AccessToken, msg.ToPhone,
                msg.TextBody ?? string.Empty, msg.ListButtonText ?? "Selecionar",
                msg.ListSections, context.CancellationToken),

            _ => throw new ArgumentException($"Unsupported message type: {msg.MessageType}")
        };

        if (messageId is null)
            throw new InvalidOperationException($"Failed to send WhatsApp {msg.MessageType} for tenant {msg.TenantId}");

        _logger.LogInformation("Outgoing WhatsApp {MessageType} delivered. MessageId: {MessageId}", msg.MessageType, messageId);
    }
}

public class FollowUpJobConsumer : IConsumer<FollowUpJob>
{
    private readonly IConversationRepository _conversations;
    private readonly ITenantRepository _tenants;
    private readonly ICampaignRepository _campaigns;
    private readonly Application.Common.Interfaces.IWhatsAppService _whatsApp;

    public FollowUpJobConsumer(
        IConversationRepository conversations, ITenantRepository tenants,
        ICampaignRepository campaigns, Application.Common.Interfaces.IWhatsAppService whatsApp)
    {
        _conversations = conversations; _tenants = tenants;
        _campaigns = campaigns; _whatsApp = whatsApp;
    }

    public async Task Consume(ConsumeContext<FollowUpJob> context)
    {
        var job = context.Message;
        var tenant = await _tenants.GetByIdAsync(job.TenantId, context.CancellationToken);
        if (tenant is null || !tenant.IsActive()) return;

        var conversation = await _conversations.GetByIdAsync(job.ConversationId, context.CancellationToken);
        if (conversation is null || conversation.Status == Domain.Enums.ConversationStatus.Closed) return;

        await _whatsApp.SendTextMessageAsync(
            tenant.WhatsAppPhoneNumberId, tenant.WhatsAppAccessToken,
            conversation.Contact!.PhoneNumber, job.Message, context.CancellationToken);
    }
}
