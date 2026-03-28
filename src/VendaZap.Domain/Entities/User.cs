using VendaZap.Domain.Common;
using VendaZap.Domain.Enums;

namespace VendaZap.Domain.Entities;

public class User : Entity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public string? AvatarUrl { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? RefreshTokenExpiresAt { get; private set; }

    // Navigation
    public Tenant Tenant { get; private set; } = default!;

    private User() { }

    public static User Create(Guid tenantId, string name, string email, string passwordHash, UserRole role = UserRole.Agent)
    {
        var user = new User
        {
            TenantId = tenantId,
            Name = name,
            Email = email.ToLower().Trim(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true
        };
        return user;
    }

    public static User CreateOwner(Guid tenantId, string name, string email, string passwordHash)
        => Create(tenantId, name, email, passwordHash, UserRole.Owner);

    public Result UpdateProfile(string name, string? avatarUrl = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Nome não pode ser vazio."));
        Name = name;
        AvatarUrl = avatarUrl;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        SetUpdatedAt();
        return Result.Success();
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        SetUpdatedAt();
    }

    public void SetRefreshToken(string token, DateTime expiresAt)
    {
        RefreshToken = token;
        RefreshTokenExpiresAt = expiresAt;
        SetUpdatedAt();
    }

    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiresAt = null;
        SetUpdatedAt();
    }

    public Result Deactivate()
    {
        IsActive = false;
        SetUpdatedAt();
        return Result.Success();
    }

    public bool HasValidRefreshToken(string token)
        => RefreshToken == token && RefreshTokenExpiresAt > DateTime.UtcNow;
}
