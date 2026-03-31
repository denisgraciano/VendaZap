using VendaZap.Domain.Common;
using VendaZap.Domain.Enums;

namespace VendaZap.Domain.Entities;

public class WhatsAppAccount : Entity
{
    public Guid TenantId { get; private set; }
    public string PhoneNumberId { get; private set; } = string.Empty;
    public string AccessTokenEncrypted { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public WhatsAppAccountStatus Status { get; private set; }

    // Navigation
    public Tenant Tenant { get; private set; } = null!;

    private WhatsAppAccount() { }

    public static WhatsAppAccount Create(
        Guid tenantId,
        string phoneNumberId,
        string accessTokenEncrypted,
        string? displayName = null)
    {
        return new WhatsAppAccount
        {
            TenantId = tenantId,
            PhoneNumberId = phoneNumberId,
            AccessTokenEncrypted = accessTokenEncrypted,
            DisplayName = displayName,
            Status = WhatsAppAccountStatus.Active
        };
    }

    public void UpdateToken(string accessTokenEncrypted)
    {
        AccessTokenEncrypted = accessTokenEncrypted;
        SetUpdatedAt();
    }

    public void UpdateDisplayName(string? displayName)
    {
        DisplayName = displayName;
        SetUpdatedAt();
    }

    public void Suspend()
    {
        Status = WhatsAppAccountStatus.Suspended;
        SetUpdatedAt();
    }

    public void Activate()
    {
        Status = WhatsAppAccountStatus.Active;
        SetUpdatedAt();
    }

    public void Deactivate()
    {
        Status = WhatsAppAccountStatus.Inactive;
        SetUpdatedAt();
    }

    public bool IsActive() => Status == WhatsAppAccountStatus.Active;
}
