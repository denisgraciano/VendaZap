using VendaZap.Domain.Common;
using VendaZap.Domain.Enums;

namespace VendaZap.Domain.Entities;

public class Tenant : Entity
{
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string WhatsAppPhoneNumberId { get; private set; } = default!;
    public string WhatsAppAccessToken { get; private set; } = default!;
    public string? WhatsAppBusinessAccountId { get; private set; }
    public string? OpenAiAssistantId { get; private set; }
    public TenantPlan Plan { get; private set; }
    public TenantStatus Status { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? WelcomeMessage { get; private set; }
    public string? AwayMessage { get; private set; }
    public bool IsHumanTakeoverEnabled { get; private set; }
    public int MaxConcurrentConversations { get; private set; }
    public DateTime? TrialEndsAt { get; private set; }
    public DateTime? SubscriptionEndsAt { get; private set; }

    // Navigation
    private readonly List<User> _users = [];
    public IReadOnlyCollection<User> Users => _users.AsReadOnly();

    private readonly List<Product> _products = [];
    public IReadOnlyCollection<Product> Products => _products.AsReadOnly();

    private Tenant() { }

    public static Tenant Create(
        string name,
        string slug,
        string phoneNumberId,
        string accessToken,
        TenantPlan plan = TenantPlan.Starter)
    {
        var tenant = new Tenant
        {
            Name = name,
            Slug = slug.ToLower().Trim(),
            WhatsAppPhoneNumberId = phoneNumberId,
            WhatsAppAccessToken = accessToken,
            Plan = plan,
            Status = TenantStatus.Trial,
            IsHumanTakeoverEnabled = true,
            MaxConcurrentConversations = plan == TenantPlan.Starter ? 50 : plan == TenantPlan.Pro ? 200 : 1000,
            WelcomeMessage = "Olá! 👋 Seja bem-vindo(a)! Como posso te ajudar hoje?",
            AwayMessage = "Olá! No momento estamos fora do horário de atendimento. Retornaremos em breve! 🕐",
            TrialEndsAt = DateTime.UtcNow.AddDays(14)
        };
        tenant.AddDomainEvent(new TenantCreatedEvent(tenant.Id, tenant.Name, tenant.Plan));
        return tenant;
    }

    public Result UpdateWhatsAppCredentials(string phoneNumberId, string accessToken, string? businessAccountId = null)
    {
        WhatsAppPhoneNumberId = phoneNumberId;
        WhatsAppAccessToken = accessToken;
        WhatsAppBusinessAccountId = businessAccountId;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result ConfigureAI(string assistantId)
    {
        OpenAiAssistantId = assistantId;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result UpdateWelcomeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Result.Failure(Error.Validation("WelcomeMessage", "Mensagem não pode ser vazia."));
        WelcomeMessage = message;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result Activate()
    {
        Status = TenantStatus.Active;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result Suspend(string reason)
    {
        Status = TenantStatus.Suspended;
        SetUpdatedAt();
        AddDomainEvent(new TenantSuspendedEvent(Id, reason));
        return Result.Success();
    }

    public Result UpgradePlan(TenantPlan newPlan, DateTime subscriptionEndsAt)
    {
        Plan = newPlan;
        Status = TenantStatus.Active;
        SubscriptionEndsAt = subscriptionEndsAt;
        MaxConcurrentConversations = newPlan == TenantPlan.Starter ? 50 : newPlan == TenantPlan.Pro ? 200 : 1000;
        SetUpdatedAt();
        AddDomainEvent(new TenantPlanUpgradedEvent(Id, Plan, newPlan));
        return Result.Success();
    }

    public bool IsActive() => Status == TenantStatus.Active ||
        (Status == TenantStatus.Trial && TrialEndsAt > DateTime.UtcNow);
}

// Domain Events
public record TenantCreatedEvent(Guid TenantId, string Name, TenantPlan Plan) : DomainEvent;
public record TenantSuspendedEvent(Guid TenantId, string Reason) : DomainEvent;
public record TenantPlanUpgradedEvent(Guid TenantId, TenantPlan OldPlan, TenantPlan NewPlan) : DomainEvent;
