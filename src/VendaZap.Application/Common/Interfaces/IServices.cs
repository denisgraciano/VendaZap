using VendaZap.Domain.Entities;

namespace VendaZap.Application.Common.Interfaces;

public interface ICurrentTenantService
{
    Guid TenantId { get; }
    Guid UserId { get; }
    string UserEmail { get; }
    string UserRole { get; }
    bool IsAuthenticated { get; }
}

public interface IJwtTokenService
{
    string GenerateAccessToken(User user, Tenant tenant);
    string GenerateRefreshToken();
    (Guid userId, Guid tenantId)? ValidateToken(string token);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IWhatsAppService
{
    Task<string?> SendTextMessageAsync(string tenantPhoneNumberId, string accessToken, string toPhone, string message, CancellationToken ct = default);
    Task<string?> SendImageMessageAsync(string tenantPhoneNumberId, string accessToken, string toPhone, string imageUrl, string? caption = null, CancellationToken ct = default);
    Task<string?> SendTemplateMessageAsync(string tenantPhoneNumberId, string accessToken, string toPhone, string templateName, string language, object[]? components = null, CancellationToken ct = default);
    Task<bool> MarkMessageAsReadAsync(string tenantPhoneNumberId, string accessToken, string whatsAppMessageId, CancellationToken ct = default);
    Task<bool> SendTypingIndicatorAsync(string tenantPhoneNumberId, string accessToken, string toPhone, CancellationToken ct = default);
}

public interface IAiConversationService
{
    Task<string> GetResponseAsync(Guid tenantId, string conversationThreadId, string userMessage, string systemContext, CancellationToken ct = default);
    Task<string> CreateThreadAsync(CancellationToken ct = default);
    Task<bool> DeleteThreadAsync(string threadId, CancellationToken ct = default);
    Task<string> GetSalesResponseAsync(Guid tenantId, string userMessage, SalesContext context, CancellationToken ct = default);
}

public record SalesContext(
    string TenantName,
    string ContactName,
    string? CurrentStage,
    IEnumerable<ProductSummary> AvailableProducts,
    string? CartSummary,
    string? PaymentInfo,
    string? CustomInstructions = null);

public record ProductSummary(Guid Id, string Name, decimal Price, string Description, bool InStock);

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
}

public interface IStorageService
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder = "general", CancellationToken ct = default);
    Task DeleteAsync(string fileUrl, CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendWelcomeEmailAsync(string toEmail, string name, string tenantName, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string name, string resetLink, CancellationToken ct = default);
    Task SendOrderConfirmationAsync(string toEmail, string contactName, string orderNumber, decimal total, CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyNewMessageAsync(Guid tenantId, Guid conversationId, string preview, CancellationToken ct = default);
    Task NotifyNewOrderAsync(Guid tenantId, string orderNumber, decimal total, CancellationToken ct = default);
    Task NotifyHumanTakeoverRequestAsync(Guid tenantId, Guid conversationId, string contactName, CancellationToken ct = default);
}
