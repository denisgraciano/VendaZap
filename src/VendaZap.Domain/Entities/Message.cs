using VendaZap.Domain.Common;
using VendaZap.Domain.Enums;

namespace VendaZap.Domain.Entities;

public class Message : Entity
{
    public Guid ConversationId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Content { get; private set; } = default!;
    public MessageDirection Direction { get; private set; }
    public MessageType Type { get; private set; }
    public MessageStatus Status { get; private set; }
    public MessageSource Source { get; private set; }
    public string? WhatsAppMessageId { get; private set; }
    public string? MediaUrl { get; private set; }
    public string? MediaMimeType { get; private set; }
    public string? TemplateId { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    // Navigation
    public Conversation Conversation { get; private set; } = default!;

    private Message() { }

    public static Message CreateOutbound(
        Guid conversationId,
        Guid tenantId,
        string content,
        MessageSource source = MessageSource.Bot,
        MessageType type = MessageType.Text)
    {
        return new Message
        {
            ConversationId = conversationId,
            TenantId = tenantId,
            Content = content,
            Direction = MessageDirection.Outbound,
            Type = type,
            Status = MessageStatus.Pending,
            Source = source
        };
    }

    public static Message CreateInbound(
        Guid conversationId,
        Guid tenantId,
        string content,
        string? whatsAppMessageId = null,
        MessageType type = MessageType.Text,
        string? mediaUrl = null,
        string? mediaMimeType = null)
    {
        return new Message
        {
            ConversationId = conversationId,
            TenantId = tenantId,
            Content = content,
            Direction = MessageDirection.Inbound,
            Type = type,
            Status = MessageStatus.Received,
            Source = MessageSource.Customer,
            WhatsAppMessageId = whatsAppMessageId,
            MediaUrl = mediaUrl,
            MediaMimeType = mediaMimeType
        };
    }

    public void SetWhatsAppMessageId(string messageId)
    {
        WhatsAppMessageId = messageId;
        Status = MessageStatus.Sent;
        SetUpdatedAt();
    }

    public void MarkDelivered()
    {
        Status = MessageStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        SetUpdatedAt();
    }

    public void MarkRead()
    {
        Status = MessageStatus.Read;
        ReadAt = DateTime.UtcNow;
        SetUpdatedAt();
    }

    public void MarkFailed(string errorMessage)
    {
        Status = MessageStatus.Failed;
        ErrorMessage = errorMessage;
        SetUpdatedAt();
    }

    // Used to rebuild a Message from cache without EF tracking
    public static Message Reconstitute(
        Guid id, Guid conversationId, Guid tenantId,
        string content, MessageDirection direction, MessageType type,
        MessageStatus status, MessageSource source,
        string? whatsAppMessageId, string? mediaUrl, string? mediaMimeType,
        DateTime createdAt, DateTime? deliveredAt, DateTime? readAt)
    {
        return new Message
        {
            Id = id,
            CreatedAt = createdAt,
            ConversationId = conversationId,
            TenantId = tenantId,
            Content = content,
            Direction = direction,
            Type = type,
            Status = status,
            Source = source,
            WhatsAppMessageId = whatsAppMessageId,
            MediaUrl = mediaUrl,
            MediaMimeType = mediaMimeType,
            DeliveredAt = deliveredAt,
            ReadAt = readAt
        };
    }
}
