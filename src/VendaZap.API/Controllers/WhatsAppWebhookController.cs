using Asp.Versioning;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VendaZap.Infrastructure.Messaging.Consumers;
using VendaZap.Infrastructure.WhatsApp;
using VendaZap.Domain.Interfaces;

namespace VendaZap.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/webhooks/whatsapp")]
[ApiVersion("1.0")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IBus _bus;
    private readonly ITenantRepository _tenants;
    private readonly ILogger<WhatsAppWebhookController> _logger;
    private readonly IConfiguration _config;

    public WhatsAppWebhookController(
        IBus bus, ITenantRepository tenants,
        ILogger<WhatsAppWebhookController> logger, IConfiguration config)
    {
        _bus = bus; _tenants = tenants; _logger = logger; _config = config;
    }

    /// <summary>Verificação do webhook pelo Meta (GET).</summary>
    [HttpGet("{tenantSlug}")]
    public IActionResult Verify(
        string tenantSlug,
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken)
    {
        var expectedToken = _config["WhatsApp:VerifyToken"] ?? "vendazap-webhook-token";

        if (mode == "subscribe" && verifyToken == expectedToken)
        {
            _logger.LogInformation("WhatsApp webhook verified for tenant {Slug}", tenantSlug);
            return Ok(challenge);
        }

        _logger.LogWarning("WhatsApp webhook verification failed for tenant {Slug}", tenantSlug);
        return Forbid();
    }

    /// <summary>Recebe eventos do WhatsApp (mensagens, status).</summary>
    [HttpPost("{tenantSlug}")]
    public async Task<IActionResult> Receive(string tenantSlug, CancellationToken ct)
    {
        // Lê o body bruto para validação HMAC antes da deserialização
        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync(ct);
        }
        Request.Body.Position = 0;

        // Valida assinatura HMAC-SHA256
        var appSecret = _config["WhatsApp:AppSecret"];
        if (!string.IsNullOrEmpty(appSecret))
        {
            var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature) || !ValidateHmacSignature(rawBody, signature, appSecret))
            {
                _logger.LogWarning("Invalid HMAC-SHA256 signature on webhook for tenant {Slug}", tenantSlug);
                return StatusCode(403);
            }
        }

        WhatsAppWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize webhook payload for tenant {Slug}", tenantSlug);
            return Ok(); // Always 200 to prevent Meta retries
        }

        if (payload is null) return Ok();

        // Always return 200 immediately — WhatsApp retries if we don't
        _ = ProcessAsync(tenantSlug, payload, ct);
        return Ok();
    }

    private async Task ProcessAsync(string tenantSlug, WhatsAppWebhookPayload payload, CancellationToken ct)
    {
        try
        {
            var tenant = await _tenants.GetBySlugAsync(tenantSlug, ct);
            if (tenant is null)
            {
                _logger.LogWarning("Webhook received for unknown tenant slug: {Slug}", tenantSlug);
                return;
            }

            foreach (var entry in payload.entry ?? [])
            foreach (var change in entry.changes ?? [])
            {
                var value = change.value;

                // Process messages
                foreach (var msg in value.messages ?? [])
                {
                    var contactName = value.contacts?
                        .FirstOrDefault(c => c.wa_id == msg.from)?.profile?.name;

                    // Áudio: publicar IncomingMessageEvent com flag de áudio
                    // O tipo já é passado e tratado no handler com resposta padrão
                    var messageBody = msg.type switch
                    {
                        "text" => msg.text?.body ?? string.Empty,
                        "image" => msg.image?.caption ?? "[Imagem]",
                        "audio" => "[Áudio]",
                        "video" => msg.video?.caption ?? "[Vídeo]",
                        "document" => msg.document?.filename ?? "[Documento]",
                        _ => $"[{msg.type}]"
                    };

                    var mediaUrl = msg.type switch
                    {
                        "image" => $"whatsapp-media:{msg.image?.id}",
                        "audio" => $"whatsapp-media:{msg.audio?.id}",
                        "video" => $"whatsapp-media:{msg.video?.id}",
                        _ => null
                    };

                    await _bus.Publish(new IncomingMessageEvent(
                        tenant.Id,
                        msg.from,
                        contactName,
                        messageBody,
                        msg.id,
                        msg.type,
                        mediaUrl), ct);
                }

                // Process status updates
                foreach (var status in value.statuses ?? [])
                {
                    await _bus.Publish(new WhatsAppStatusUpdate(
                        status.id, status.status,
                        DateTimeOffset.FromUnixTimeSeconds(long.Parse(status.timestamp)).UtcDateTime), ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WhatsApp webhook for tenant {Slug}", tenantSlug);
        }
    }

    private static bool ValidateHmacSignature(string rawBody, string signature, string appSecret)
    {
        // Meta envia: sha256=<hash_hex>
        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        var receivedHash = signature["sha256=".Length..];
        var keyBytes = Encoding.UTF8.GetBytes(appSecret);
        var bodyBytes = Encoding.UTF8.GetBytes(rawBody);

        using var hmac = new HMACSHA256(keyBytes);
        var computedHash = hmac.ComputeHash(bodyBytes);
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        // Comparação em tempo constante para prevenir timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(receivedHash.ToLowerInvariant()));
    }
}
