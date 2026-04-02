using VendaZap.Domain.Common;
using VendaZap.Domain.Enums;

namespace VendaZap.Domain.Entities;

public class Conversation : Entity
{
    public Guid TenantId { get; private set; }
    public Guid ContactId { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public ConversationStatus Status { get; private set; }
    public ConversationMode Mode { get; private set; }
    public ConversationStage Stage { get; private set; }
    public string? LastMessagePreview { get; private set; }
    public DateTime? LastMessageAt { get; private set; }
    public int UnreadCount { get; private set; }
    public string? WhatsAppConversationId { get; private set; }
    public string? AiThreadId { get; private set; }

    // Cart state (JSON)
    public string CartJson { get; private set; } = "{}";
    public Guid? ActiveOrderId { get; private set; }

    // Navigation
    public Tenant Tenant { get; private set; } = default!;
    public Contact Contact { get; private set; } = default!;
    public User? AssignedToUser { get; private set; }

    private readonly List<Message> _messages = [];
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private Conversation() { }

    public static Conversation Create(Guid tenantId, Guid contactId, string? whatsAppConversationId = null)
    {
        var conversation = new Conversation
        {
            TenantId = tenantId,
            ContactId = contactId,
            Status = ConversationStatus.Open,
            Mode = ConversationMode.Bot,
            Stage = ConversationStage.Initial,
            WhatsAppConversationId = whatsAppConversationId,
            UnreadCount = 0
        };
        conversation.AddDomainEvent(new ConversationStartedEvent(conversation.Id, tenantId, contactId));
        return conversation;
    }

    public Result AddMessage(Message message)
    {
        _messages.Add(message);
        LastMessagePreview = message.Content.Length > 100
            ? message.Content[..97] + "..."
            : message.Content;
        LastMessageAt = message.CreatedAt;

        if (message.Direction == MessageDirection.Inbound)
        {
            UnreadCount++;
            AddDomainEvent(new MessageReceivedEvent(Id, TenantId, ContactId, message.Id, message.Content));
        }

        SetUpdatedAt();
        return Result.Success();
    }

    public Result TransferToHuman(Guid? userId = null)
    {
        Mode = ConversationMode.Human;
        Status = userId.HasValue ? Status : ConversationStatus.WaitingHuman;
        AssignedToUserId = userId;
        AddDomainEvent(new ConversationTransferredToHumanEvent(Id, TenantId, userId));
        SetUpdatedAt();
        return Result.Success();
    }

    public Result AssignHumanAgent(Guid userId)
    {
        AssignedToUserId = userId;
        Status = ConversationStatus.Open;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result ReturnToBot()
    {
        Mode = ConversationMode.Bot;
        AssignedToUserId = null;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result AssignTo(Guid userId)
    {
        AssignedToUserId = userId;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result AdvanceStage(ConversationStage newStage)
    {
        Stage = newStage;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result Close()
    {
        Status = ConversationStatus.Closed;
        SetUpdatedAt();
        AddDomainEvent(new ConversationClosedEvent(Id, TenantId));
        return Result.Success();
    }

    public Result Reopen()
    {
        Status = ConversationStatus.Open;
        UnreadCount = 0;
        SetUpdatedAt();
        return Result.Success();
    }

    public void MarkAsRead()
    {
        UnreadCount = 0;
        SetUpdatedAt();
    }

    public void SetAiThreadId(string threadId)
    {
        AiThreadId = threadId;
        SetUpdatedAt();
    }

    public void UpdateCart(string cartJson)
    {
        CartJson = cartJson;
        SetUpdatedAt();
    }

    public void SetActiveOrder(Guid orderId)
    {
        ActiveOrderId = orderId;
        SetUpdatedAt();
    }

    public bool IsBot() => Mode == ConversationMode.Bot;
    public bool IsHuman() => Mode == ConversationMode.Human;
}

// Domain Events
public record ConversationStartedEvent(Guid ConversationId, Guid TenantId, Guid ContactId) : DomainEvent;
public record ConversationClosedEvent(Guid ConversationId, Guid TenantId) : DomainEvent;
public record ConversationTransferredToHumanEvent(Guid ConversationId, Guid TenantId, Guid? UserId) : DomainEvent;
public record MessageReceivedEvent(Guid ConversationId, Guid TenantId, Guid ContactId, Guid MessageId, string Content) : DomainEvent;
