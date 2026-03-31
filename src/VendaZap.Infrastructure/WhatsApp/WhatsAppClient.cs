using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Infrastructure.WhatsApp;

public class WhatsAppClient : IWhatsAppClient
{
    private readonly HttpClient _httpClient;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<WhatsAppClient> _logger;
    private const string BaseUrl = "https://graph.facebook.com/v18.0";
    private const int RateLimitPerSecond = 80;

    public WhatsAppClient(HttpClient httpClient, IConnectionMultiplexer redis, ILogger<WhatsAppClient> logger)
    {
        _httpClient = httpClient;
        _redis = redis;
        _logger = logger;
    }

    public async Task<string?> SendTextAsync(
        string phoneNumberId, string accessToken, string toPhone,
        string message, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhone,
            type = "text",
            text = new { preview_url = false, body = message }
        };

        return await SendAsync(phoneNumberId, accessToken, toPhone, "text", payload, ct);
    }

    public async Task<string?> SendImageAsync(
        string phoneNumberId, string accessToken, string toPhone,
        string imageUrl, string? caption = null, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhone,
            type = "image",
            image = new { link = imageUrl, caption }
        };

        return await SendAsync(phoneNumberId, accessToken, toPhone, "image", payload, ct);
    }

    public async Task<string?> SendInteractiveButtonsAsync(
        string phoneNumberId, string accessToken, string toPhone,
        string bodyText, IEnumerable<WhatsAppButton> buttons, CancellationToken ct = default)
    {
        var buttonList = buttons.Select(b => new
        {
            type = "reply",
            reply = new { id = b.Id, title = b.Title }
        }).ToArray();

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhone,
            type = "interactive",
            interactive = new
            {
                type = "button",
                body = new { text = bodyText },
                action = new { buttons = buttonList }
            }
        };

        return await SendAsync(phoneNumberId, accessToken, toPhone, "interactive_buttons", payload, ct);
    }

    public async Task<string?> SendInteractiveListAsync(
        string phoneNumberId, string accessToken, string toPhone,
        string bodyText, string buttonText, IEnumerable<WhatsAppListSection> sections,
        CancellationToken ct = default)
    {
        var sectionList = sections.Select(s => new
        {
            title = s.Title,
            rows = s.Rows.Select(r => new { id = r.Id, title = r.Title, description = r.Description }).ToArray()
        }).ToArray();

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhone,
            type = "interactive",
            interactive = new
            {
                type = "list",
                body = new { text = bodyText },
                action = new { button = buttonText, sections = sectionList }
            }
        };

        return await SendAsync(phoneNumberId, accessToken, toPhone, "interactive_list", payload, ct);
    }

    private async Task<string?> SendAsync(
        string phoneNumberId, string accessToken, string toPhone,
        string messageType, object payload, CancellationToken ct)
    {
        if (!await CheckRateLimitAsync(phoneNumberId, ct))
        {
            _logger.LogWarning("Rate limit reached for phone number {PhoneNumberId}. Message to {Phone} throttled.",
                phoneNumberId, MaskPhone(toPhone));
            await Task.Delay(1000, ct);
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{phoneNumberId}/messages");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("WhatsApp API error sending {MessageType} to {Phone}: {StatusCode} - {Content}",
                    messageType, MaskPhone(toPhone), response.StatusCode, content);
                return null;
            }

            using var doc = JsonDocument.Parse(content);
            var messageId = doc.RootElement.GetProperty("messages")[0].GetProperty("id").GetString();

            _logger.LogInformation("WhatsApp {MessageType} sent to {Phone} via {PhoneNumberId}. MessageId: {MessageId}",
                messageType, MaskPhone(toPhone), phoneNumberId, messageId);

            return messageId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending WhatsApp {MessageType} to {Phone}", messageType, MaskPhone(toPhone));
            return null;
        }
    }

    private async Task<bool> CheckRateLimitAsync(string phoneNumberId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var key = $"ratelimit:whatsapp:{phoneNumberId}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var count = await db.StringIncrementAsync(key);
        if (count == 1)
            await db.KeyExpireAsync(key, TimeSpan.FromSeconds(2));
        return count <= RateLimitPerSecond;
    }

    private static string MaskPhone(string phone) =>
        phone.Length > 4 ? phone[..4] + "****" : "****";
}
