using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using VendaZap.Application.Common.Interfaces;

namespace VendaZap.Infrastructure.Identity;

public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid TenantId =>
        Guid.TryParse(User?.FindFirstValue("tenant_id"), out var id)
            ? id
            : throw new UnauthorizedAccessException("Tenant ID not found in token.");

    public Guid UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      User?.FindFirstValue("sub"), out var id)
            ? id
            : throw new UnauthorizedAccessException("User ID not found in token.");

    public string UserEmail =>
        User?.FindFirstValue(ClaimTypes.Email) ??
        User?.FindFirstValue("email") ?? string.Empty;

    public string UserRole =>
        User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;
}
