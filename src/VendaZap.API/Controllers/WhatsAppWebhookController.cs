using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
    public async Task<IActionResult> Receive(
        string tenantSlug,
        [FromBody] WhatsAppWebhookPayload payload,
        CancellationToken ct)
    {
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

                    await _bus.Publish(new InboundWhatsAppMessage(
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
}
