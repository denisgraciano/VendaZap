using VendaZap.Domain.Common;
using VendaZap.Domain.Enums;

namespace VendaZap.Domain.Entities;

public class Campaign : Entity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public CampaignType Type { get; private set; }
    public CampaignStatus Status { get; private set; }
    public string MessageTemplate { get; private set; } = default!;
    public string? MediaUrl { get; private set; }
    public CampaignTrigger Trigger { get; private set; }
    public int TriggerDelayMinutes { get; private set; }

    // Schedule
    public DateTime? ScheduledAt { get; private set; }
    public DayOfWeek[]? ActiveDays { get; private set; }
    public TimeOnly? ActiveFrom { get; private set; }
    public TimeOnly? ActiveTo { get; private set; }

    // Stats
    public int TotalSent { get; private set; }
    public int TotalDelivered { get; private set; }
    public int TotalRead { get; private set; }
    public int TotalReplied { get; private set; }

    // Filter JSON (tags, segments)
    public string FiltersJson { get; private set; } = "{}";

    public Tenant Tenant { get; private set; } = default!;

    private Campaign() { }

    public static Campaign Create(
        Guid tenantId,
        string name,
        CampaignType type,
        string messageTemplate,
        CampaignTrigger trigger,
        int triggerDelayMinutes = 0)
    {
        return new Campaign
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Type = type,
            MessageTemplate = messageTemplate,
            Trigger = trigger,
            TriggerDelayMinutes = triggerDelayMinutes,
            Status = CampaignStatus.Draft
        };
    }

    public Result Activate()
    {
        if (string.IsNullOrWhiteSpace(MessageTemplate))
            return Result.Failure(Error.Validation("MessageTemplate", "Template de mensagem é obrigatório."));
        Status = CampaignStatus.Active;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result Pause()
    {
        Status = CampaignStatus.Paused;
        SetUpdatedAt();
        return Result.Success();
    }

    public void RecordSent() { TotalSent++; SetUpdatedAt(); }
    public void RecordDelivered() { TotalDelivered++; SetUpdatedAt(); }
    public void RecordRead() { TotalRead++; SetUpdatedAt(); }
    public void RecordReplied() { TotalReplied++; SetUpdatedAt(); }

    public string InterpolateMessage(string contactName, string? productName = null, string? orderNumber = null)
    {
        return MessageTemplate
            .Replace("{{nome}}", contactName)
            .Replace("{{produto}}", productName ?? "")
            .Replace("{{pedido}}", orderNumber ?? "")
            .Replace("{{data}}", DateTime.Now.ToString("dd/MM/yyyy"));
    }
}

// AutoReply template
public class AutoReplyTemplate : Entity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string[] Triggers { get; private set; } = [];
    public string Response { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public int Priority { get; private set; }

    private AutoReplyTemplate() { }

    public static AutoReplyTemplate Create(Guid tenantId, string name, string[] triggers, string response, int priority = 0)
    {
        return new AutoReplyTemplate
        {
            TenantId = tenantId,
            Name = name,
            Triggers = triggers,
            Response = response,
            IsActive = true,
            Priority = priority
        };
    }

    public bool Matches(string message)
    {
        var lowerMsg = message.ToLower();
        return Triggers.Any(t => lowerMsg.Contains(t.ToLower()));
    }
}
