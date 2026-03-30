using VendaZap.Domain.Interfaces;

namespace VendaZap.API.Middleware;

/// <summary>
/// Middleware responsável por injetar e validar o TenantId no pipeline da requisição.
/// Deve ser registrado após UseAuthentication e UseAuthorization.
/// Requisições anônimas (ex: login, registro, health) são permitidas sem tenant.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    // Rotas que não requerem tenant autenticado
    private static readonly string[] _anonymousPaths =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/register",
        "/api/v1/auth/refresh",
        "/health",
        "/docs",
        "/swagger",
        "/hubs"
    ];

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Permite requisições anônimas em rotas específicas
        var isAnonymousPath = _anonymousPaths.Any(p =>
            path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!isAnonymousPath && context.User.Identity?.IsAuthenticated == true)
        {
            if (!tenantContext.HasTenant)
            {
                _logger.LogWarning(
                    "Requisição autenticada sem TenantId no token. Path: {Path}", path);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    status = 401,
                    message = "Token inválido: TenantId não encontrado.",
                    timestamp = DateTime.UtcNow
                });
                return;
            }

            _logger.LogDebug(
                "Tenant {TenantId} resolvido para requisição {Method} {Path}",
                tenantContext.TenantId, context.Request.Method, path);
        }

        await _next(context);
    }
}

public static class TenantMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app)
        => app.UseMiddleware<TenantMiddleware>();
}
