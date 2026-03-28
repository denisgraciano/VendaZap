using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using VendaZap.Application.Common.Interfaces;

namespace VendaZap.Infrastructure.WhatsApp;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppService> _logger;
    private const string BaseUrl = "https://graph.facebook.com/v18.0";

    public WhatsAppService(HttpClient httpClient, ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> SendTextMessageAsync(
        string phoneNumberId, string accessToken, string toPhone, string message, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toPhone,
            type = "text",
            text = new { preview_url = false, body = message }
        };
        return await SendMessageAsync(phoneNumberId, accessToken, payload, ct);
    }

    public async Task<string?> SendImageMessageAsync(
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
        return await SendMessageAsync(phoneNumberId, accessToken, payload, ct);
    }

    public async Task<string?> SendTemplateMessageAsync(
        string phoneNumberId, string accessToken, string toPhone,
        string templateName, string language, object[]? components = null, CancellationToken ct = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = toPhone,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = language },
                components = components ?? []
            }
        };
        return await SendMessageAsync(phoneNumberId, accessToken, payload, ct);
    }

    public async Task<bool> MarkMessageAsReadAsync(
        string phoneNumberId, string accessToken, string whatsAppMessageId, CancellationToken ct = default)
    {
        try
        {
            var payload = new { messaging_product = "whatsapp", status = "read", message_id = whatsAppMessageId };
            var request = CreateRequest(phoneNumberId, accessToken, payload);
            var response = await _httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark message as read: {MessageId}", whatsAppMessageId);
            return false;
        }
    }

    public async Task<bool> SendTypingIndicatorAsync(
        string phoneNumberId, string accessToken, string toPhone, CancellationToken ct = default)
    {
        // WhatsApp Business API doesn't have a typing indicator endpoint directly.
        // Simulated via read receipt timing. Return true.
        await Task.Delay(500, ct); // Brief delay before responding
        return true;
    }

    private async Task<string?> SendMessageAsync(string phoneNumberId, string accessToken, object payload, CancellationToken ct)
    {
        try
        {
            var request = CreateRequest(phoneNumberId, accessToken, payload);
            var response = await _httpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("WhatsApp API error: {StatusCode} - {Content}", response.StatusCode, content);
                return null;
            }

            using var doc = JsonDocument.Parse(content);
            return doc.RootElement
                .GetProperty("messages")[0]
                .GetProperty("id")
                .GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending WhatsApp message to {Phone}", "REDACTED");
            return null;
        }
    }

    private static HttpRequestMessage CreateRequest(string phoneNumberId, string accessToken, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{phoneNumberId}/messages");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Content = JsonContent.Create(payload);
        return request;
    }
}

// ─── Webhook Models ───────────────────────────────────────────────────────────

public record WhatsAppWebhookPayload(string @object, WhatsAppEntry[] entry);
public record WhatsAppEntry(string id, WhatsAppChange[] changes);
public record WhatsAppChange(WhatsAppValue value, string field);
public record WhatsAppValue(
    string messaging_product,
    WhatsAppMetadata? metadata,
    WhatsAppContact[]? contacts,
    WhatsAppMessage[]? messages,
    WhatsAppStatus[]? statuses);
public record WhatsAppMetadata(string display_phone_number, string phone_number_id);
public record WhatsAppContact(WhatsAppContactProfile profile, string wa_id);
public record WhatsAppContactProfile(string name);
public record WhatsAppMessage(
    string from, string id, string timestamp, string type,
    WhatsAppTextContent? text,
    WhatsAppMediaContent? image,
    WhatsAppMediaContent? audio,
    WhatsAppMediaContent? video,
    WhatsAppMediaContent? document,
    WhatsAppContext? context);
public record WhatsAppTextContent(string body);
public record WhatsAppMediaContent(string id, string? mime_type, string? sha256, string? caption, string? filename);
public record WhatsAppContext(string from, string id);
public record WhatsAppStatus(string id, string status, string timestamp, string recipient_id);
