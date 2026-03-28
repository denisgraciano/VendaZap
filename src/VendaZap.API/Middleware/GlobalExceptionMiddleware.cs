using System.Net;
using System.Text.Json;
using FluentValidation;

namespace VendaZap.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message, errors) = exception switch
        {
            ValidationException ve => (
                HttpStatusCode.BadRequest,
                "Dados inválidos.",
                ve.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }).Cast<object>().ToArray()
            ),
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Não autorizado.",
                Array.Empty<object>()
            ),
            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                "Recurso não encontrado.",
                Array.Empty<object>()
            ),
            InvalidOperationException ioe => (
                HttpStatusCode.BadRequest,
                ioe.Message,
                Array.Empty<object>()
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "Ocorreu um erro interno. Tente novamente.",
                Array.Empty<object>()
            )
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            status = (int)statusCode,
            message,
            errors,
            traceId = context.TraceIdentifier,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
