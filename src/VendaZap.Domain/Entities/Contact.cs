using VendaZap.Domain.Common;

namespace VendaZap.Domain.Entities;

public class Contact : Entity
{
    public Guid TenantId { get; private set; }
    public string PhoneNumber { get; private set; } = default!;
    public string? Name { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? ZipCode { get; private set; }
    public string? Notes { get; private set; }
    public bool IsBlocked { get; private set; }
    public DateTime? LastInteractionAt { get; private set; }
    public int TotalOrders { get; private set; }
    public decimal TotalSpent { get; private set; }

    // Tags/Labels JSON
    public string TagsJson { get; private set; } = "[]";

    // Navigation
    public Tenant Tenant { get; private set; } = default!;
    private readonly List<Conversation> _conversations = [];
    public IReadOnlyCollection<Conversation> Conversations => _conversations.AsReadOnly();
    private readonly List<Order> _orders = [];
    public IReadOnlyCollection<Order> Orders => _orders.AsReadOnly();

    private Contact() { }

    public static Contact Create(Guid tenantId, string phoneNumber, string? name = null)
    {
        return new Contact
        {
            TenantId = tenantId,
            PhoneNumber = phoneNumber.Trim(),
            Name = name?.Trim()
        };
    }

    public Result UpdateProfile(string? name, string? email, string? address, string? city, string? state, string? zipCode)
    {
        Name = name?.Trim();
        Email = email?.Trim().ToLower();
        Address = address?.Trim();
        City = city?.Trim();
        State = state?.Trim();
        ZipCode = zipCode?.Trim();
        SetUpdatedAt();
        return Result.Success();
    }

    public void RecordInteraction()
    {
        LastInteractionAt = DateTime.UtcNow;
        SetUpdatedAt();
    }

    public void RecordOrder(decimal amount)
    {
        TotalOrders++;
        TotalSpent += amount;
        LastInteractionAt = DateTime.UtcNow;
        SetUpdatedAt();
    }

    public Result Block(string? reason = null)
    {
        IsBlocked = true;
        Notes = reason;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result Unblock()
    {
        IsBlocked = false;
        SetUpdatedAt();
        return Result.Success();
    }

    public string GetDisplayName() => Name ?? PhoneNumber;
}
