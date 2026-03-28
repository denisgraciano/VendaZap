using MediatR;
using VendaZap.Application.Common.Interfaces;
using VendaZap.Domain.Common;
using VendaZap.Domain.Entities;
using VendaZap.Domain.Enums;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Application.Features.Conversations;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record ConversationDto(
    Guid Id,
    Guid ContactId,
    string ContactName,
    string ContactPhone,
    string Status,
    string Mode,
    string Stage,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    int UnreadCount,
    Guid? AssignedToUserId,
    string? AssignedToUserName,
    DateTime CreatedAt);

public record MessageDto(
    Guid Id,
    string Content,
    string Direction,
    string Type,
    string Status,
    string Source,
    string? MediaUrl,
    DateTime CreatedAt);

// ─── Queries ─────────────────────────────────────────────────────────────────

public record GetConversationsQuery(
    ConversationStatus? Status = null,
    int Page = 1,
    int PageSize = 30) : IRequest<Result<PagedResult<ConversationDto>>>;

public record PagedResult<T>(IEnumerable<T> Items, int Total, int Page, int PageSize, int TotalPages);

public class GetConversationsQueryHandler : IRequestHandler<GetConversationsQuery, Result<PagedResult<ConversationDto>>>
{
    private readonly IConversationRepository _conversations;
    private readonly ICurrentTenantService _tenant;

    public GetConversationsQueryHandler(IConversationRepository conversations, ICurrentTenantService tenant)
    {
        _conversations = conversations; _tenant = tenant;
    }

    public async Task<Result<PagedResult<ConversationDto>>> Handle(GetConversationsQuery request, CancellationToken ct)
    {
        var items = await _conversations.GetByTenantAsync(
            _tenant.TenantId, request.Status, request.Page, request.PageSize, ct);

        var total = await _conversations.CountOpenAsync(_tenant.TenantId, ct);
        var dtos = items.Select(c => new ConversationDto(
            c.Id, c.ContactId,
            c.Contact?.GetDisplayName() ?? "Desconhecido",
            c.Contact?.PhoneNumber ?? "",
            c.Status.ToString(), c.Mode.ToString(), c.Stage.ToString(),
            c.LastMessagePreview, c.LastMessageAt, c.UnreadCount,
            c.AssignedToUserId, c.AssignedToUser?.Name, c.CreatedAt));

        return Result.Success(new PagedResult<ConversationDto>(
            dtos, total, request.Page, request.PageSize,
            (int)Math.Ceiling((double)total / request.PageSize)));
    }
}

public record GetConversationMessagesQuery(Guid ConversationId, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<MessageDto>>>;

public class GetConversationMessagesQueryHandler : IRequestHandler<GetConversationMessagesQuery, Result<PagedResult<MessageDto>>>
{
    private readonly IMessageRepository _messages;
    private readonly IConversationRepository _conversations;
    private readonly ICurrentTenantService _tenant;

    public GetConversationMessagesQueryHandler(IMessageRepository messages, IConversationRepository conversations, ICurrentTenantService tenant)
    {
        _messages = messages; _conversations = conversations; _tenant = tenant;
    }

    public async Task<Result<PagedResult<MessageDto>>> Handle(GetConversationMessagesQuery request, CancellationToken ct)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId, ct);
        if (conversation is null || conversation.TenantId != _tenant.TenantId)
            return Result.Failure<PagedResult<MessageDto>>(Error.NotFound("Conversa"));

        var messages = await _messages.GetByConversationAsync(request.ConversationId, request.Page, request.PageSize, ct);
        var dtos = messages.Select(m => new MessageDto(
            m.Id, m.Content, m.Direction.ToString(), m.Type.ToString(),
            m.Status.ToString(), m.Source.ToString(), m.MediaUrl, m.CreatedAt));

        return Result.Success(new PagedResult<MessageDto>(dtos, messages.Count(), request.Page, request.PageSize, 1));
    }
}

// ─── Commands ─────────────────────────────────────────────────────────────────

public record SendManualMessageCommand(Guid ConversationId, string Content, MessageType Type = MessageType.Text)
    : IRequest<Result<MessageDto>>;

public class SendManualMessageCommandHandler : IRequestHandler<SendManualMessageCommand, Result<MessageDto>>
{
    private readonly IConversationRepository _conversations;
    private readonly ITenantRepository _tenants;
    private readonly IMessageRepository _messages;
    private readonly IWhatsAppService _whatsApp;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantService _tenant;

    public SendManualMessageCommandHandler(
        IConversationRepository conversations, ITenantRepository tenants,
        IMessageRepository messages, IWhatsAppService whatsApp,
        IUnitOfWork uow, ICurrentTenantService tenant)
    {
        _conversations = conversations; _tenants = tenants; _messages = messages;
        _whatsApp = whatsApp; _uow = uow; _tenant = tenant;
    }

    public async Task<Result<MessageDto>> Handle(SendManualMessageCommand request, CancellationToken ct)
    {
        var conversation = await _conversations.GetWithMessagesAsync(request.ConversationId, ct);
        if (conversation is null || conversation.TenantId != _tenant.TenantId)
            return Result.Failure<MessageDto>(Error.NotFound("Conversa"));

        var tenantEntity = await _tenants.GetByIdAsync(_tenant.TenantId, ct);
        if (tenantEntity is null) return Result.Failure<MessageDto>(Error.NotFound("Tenant"));

        var message = Message.CreateOutbound(
            conversation.Id, _tenant.TenantId, request.Content, MessageSource.Human, request.Type);

        await _messages.AddAsync(message, ct);
        conversation.AddMessage(message);
        _conversations.Update(conversation);
        await _uow.SaveChangesAsync(ct);

        // Send via WhatsApp
        var phone = conversation.Contact.PhoneNumber;
        var waMessageId = await _whatsApp.SendTextMessageAsync(
            tenantEntity.WhatsAppPhoneNumberId, tenantEntity.WhatsAppAccessToken, phone, request.Content, ct);

        if (waMessageId is not null)
        {
            message.SetWhatsAppMessageId(waMessageId);
            await _uow.SaveChangesAsync(ct);
        }

        return Result.Success(new MessageDto(message.Id, message.Content, message.Direction.ToString(),
            message.Type.ToString(), message.Status.ToString(), message.Source.ToString(), null, message.CreatedAt));
    }
}

public record TransferToHumanCommand(Guid ConversationId, Guid? AgentId = null) : IRequest<Result>;

public class TransferToHumanCommandHandler : IRequestHandler<TransferToHumanCommand, Result>
{
    private readonly IConversationRepository _conversations;
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantService _tenant;

    public TransferToHumanCommandHandler(IConversationRepository conversations, INotificationService notifications, IUnitOfWork uow, ICurrentTenantService tenant)
    {
        _conversations = conversations; _notifications = notifications; _uow = uow; _tenant = tenant;
    }

    public async Task<Result> Handle(TransferToHumanCommand request, CancellationToken ct)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId, ct);
        if (conversation is null || conversation.TenantId != _tenant.TenantId)
            return Result.Failure(Error.NotFound("Conversa"));

        conversation.TransferToHuman(request.AgentId);
        _conversations.Update(conversation);
        await _uow.SaveChangesAsync(ct);

        await _notifications.NotifyHumanTakeoverRequestAsync(
            _tenant.TenantId, conversation.Id,
            conversation.Contact?.GetDisplayName() ?? "Cliente", ct);

        return Result.Success();
    }
}

public record CloseConversationCommand(Guid ConversationId) : IRequest<Result>;

public class CloseConversationCommandHandler : IRequestHandler<CloseConversationCommand, Result>
{
    private readonly IConversationRepository _conversations;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantService _tenant;

    public CloseConversationCommandHandler(IConversationRepository conversations, IUnitOfWork uow, ICurrentTenantService tenant)
    {
        _conversations = conversations; _uow = uow; _tenant = tenant;
    }

    public async Task<Result> Handle(CloseConversationCommand request, CancellationToken ct)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId, ct);
        if (conversation is null || conversation.TenantId != _tenant.TenantId)
            return Result.Failure(Error.NotFound("Conversa"));

        conversation.Close();
        _conversations.Update(conversation);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
