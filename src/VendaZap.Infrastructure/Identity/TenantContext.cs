using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Infrastructure.Identity;

/// <summary>
/// Resolve o TenantId do contexto atual via claim JWT.
/// Retorna null quando não há usuário autenticado (ex: endpoints de registro).
/// </summary>
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid? TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
