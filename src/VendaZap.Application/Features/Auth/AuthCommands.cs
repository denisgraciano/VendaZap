using FluentValidation;
using MediatR;
using VendaZap.Application.Common.Interfaces;
using VendaZap.Domain.Common;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Application.Features.Auth;

// ─── Login ───────────────────────────────────────────────────────────────────

public record LoginCommand(string Email, string Password, string TenantSlug) : IRequest<Result<LoginResponse>>;

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User,
    TenantDto Tenant);

public record UserDto(Guid Id, string Name, string Email, string Role, string? AvatarUrl);
public record TenantDto(Guid Id, string Name, string Plan, string Status, bool IsActive);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("E-mail inválido.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).WithMessage("Senha inválida.");
        RuleFor(x => x.TenantSlug).NotEmpty().WithMessage("Tenant inválido.");
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly ITenantRepository _tenants;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;

    public LoginCommandHandler(ITenantRepository tenants, IUserRepository users, IPasswordHasher hasher, IJwtTokenService jwt)
    {
        _tenants = tenants; _users = users; _hasher = hasher; _jwt = jwt;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var tenant = await _tenants.GetBySlugAsync(request.TenantSlug, ct);
        if (tenant is null)
            return Result.Failure<LoginResponse>(Error.NotFound("Tenant"));

        if (!tenant.IsActive())
            return Result.Failure<LoginResponse>(Error.BusinessRule("TenantInactive", "Conta suspensa ou expirada."));

        var user = await _users.GetByEmailAsync(request.Email.ToLower(), tenant.Id, ct);
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            return Result.Failure<LoginResponse>(Error.Unauthorized());

        if (!user.IsActive)
            return Result.Failure<LoginResponse>(Error.BusinessRule("UserInactive", "Usuário inativo."));

        var accessToken = _jwt.GenerateAccessToken(user, tenant);
        var refreshToken = _jwt.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddHours(8);

        user.RecordLogin();
        user.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(30));

        return Result.Success(new LoginResponse(
            accessToken, refreshToken, expiresAt,
            new UserDto(user.Id, user.Name, user.Email, user.Role.ToString(), user.AvatarUrl),
            new TenantDto(tenant.Id, tenant.Name, tenant.Plan.ToString(), tenant.Status.ToString(), tenant.IsActive())
        ));
    }
}

// ─── Register Tenant ─────────────────────────────────────────────────────────

public record RegisterTenantCommand(
    string TenantName,
    string OwnerName,
    string OwnerEmail,
    string Password,
    string WhatsAppPhoneNumberId,
    string WhatsAppAccessToken) : IRequest<Result<RegisterTenantResponse>>;

public record RegisterTenantResponse(Guid TenantId, string Slug, string AccessToken, string RefreshToken);

public class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(100).WithMessage("Nome da empresa é obrigatório.");
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress().WithMessage("E-mail inválido.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).WithMessage("Senha mínima de 8 caracteres.");
        RuleFor(x => x.WhatsAppPhoneNumberId).NotEmpty().WithMessage("Phone Number ID do WhatsApp é obrigatório.");
        RuleFor(x => x.WhatsAppAccessToken).NotEmpty().WithMessage("Token do WhatsApp é obrigatório.");
    }
}

public class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, Result<RegisterTenantResponse>>
{
    private readonly ITenantRepository _tenants;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;

    public RegisterTenantCommandHandler(ITenantRepository tenants, IUserRepository users, IUnitOfWork uow, IPasswordHasher hasher, IJwtTokenService jwt)
    {
        _tenants = tenants; _users = users; _uow = uow; _hasher = hasher; _jwt = jwt;
    }

    public async Task<Result<RegisterTenantResponse>> Handle(RegisterTenantCommand request, CancellationToken ct)
    {
        var slug = GenerateSlug(request.TenantName);

        if (await _tenants.SlugExistsAsync(slug, ct))
            slug = slug + "-" + Guid.NewGuid().ToString("N")[..6];

        var tenant = Domain.Entities.Tenant.Create(
            request.TenantName, slug,
            request.WhatsAppPhoneNumberId,
            request.WhatsAppAccessToken);

        await _tenants.AddAsync(tenant, ct);

        var passwordHash = _hasher.Hash(request.Password);
        var owner = Domain.Entities.User.CreateOwner(tenant.Id, request.OwnerName, request.OwnerEmail, passwordHash);
        await _users.AddAsync(owner, ct);

        await _uow.SaveChangesAsync(ct);

        var accessToken = _jwt.GenerateAccessToken(owner, tenant);
        var refreshToken = _jwt.GenerateRefreshToken();
        owner.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(30));
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new RegisterTenantResponse(tenant.Id, tenant.Slug, accessToken, refreshToken));
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLower()
            .Replace(" ", "-")
            .Replace("ã", "a").Replace("ç", "c").Replace("é", "e")
            .Replace("ê", "e").Replace("á", "a").Replace("â", "a")
            .Replace("ó", "o").Replace("ô", "o").Replace("ú", "u")
            .Replace("í", "i").Replace("õ", "o");
        return new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
    }
}

// ─── Refresh Token ────────────────────────────────────────────────────────────

public record RefreshTokenCommand(Guid UserId, string RefreshToken) : IRequest<Result<LoginResponse>>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly IJwtTokenService _jwt;
    private readonly IUnitOfWork _uow;

    public RefreshTokenCommandHandler(IUserRepository users, ITenantRepository tenants, IJwtTokenService jwt, IUnitOfWork uow)
    {
        _users = users; _tenants = tenants; _jwt = jwt; _uow = uow;
    }

    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct);
        if (user is null || !user.HasValidRefreshToken(request.RefreshToken))
            return Result.Failure<LoginResponse>(Error.Unauthorized());

        var tenant = await _tenants.GetByIdAsync(user.TenantId, ct);
        if (tenant is null || !tenant.IsActive())
            return Result.Failure<LoginResponse>(Error.BusinessRule("TenantInactive", "Conta inativa."));

        var accessToken = _jwt.GenerateAccessToken(user, tenant);
        var refreshToken = _jwt.GenerateRefreshToken();
        user.SetRefreshToken(refreshToken, DateTime.UtcNow.AddDays(30));
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new LoginResponse(
            accessToken, refreshToken, DateTime.UtcNow.AddHours(8),
            new UserDto(user.Id, user.Name, user.Email, user.Role.ToString(), user.AvatarUrl),
            new TenantDto(tenant.Id, tenant.Name, tenant.Plan.ToString(), tenant.Status.ToString(), tenant.IsActive())
        ));
    }
}
