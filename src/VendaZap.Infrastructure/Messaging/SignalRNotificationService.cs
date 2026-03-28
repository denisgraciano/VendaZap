using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using VendaZap.Application.Common.Interfaces;

namespace VendaZap.Infrastructure.Messaging;

public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(IHubContext<NotificationHub> hub, ILogger<SignalRNotificationService> logger)
    {
        _hub = hub; _logger = logger;
    }

    public async Task NotifyNewMessageAsync(Guid tenantId, Guid conversationId, string preview, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.Group(tenantId.ToString())
                .SendAsync("NewMessage", new { conversationId, preview, timestamp = DateTime.UtcNow }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SignalR notification for tenant {TenantId}", tenantId);
        }
    }

    public async Task NotifyNewOrderAsync(Guid tenantId, string orderNumber, decimal total, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.Group(tenantId.ToString())
                .SendAsync("NewOrder", new { orderNumber, total, timestamp = DateTime.UtcNow }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send order notification for tenant {TenantId}", tenantId);
        }
    }

    public async Task NotifyHumanTakeoverRequestAsync(Guid tenantId, Guid conversationId, string contactName, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.Group(tenantId.ToString())
                .SendAsync("HumanTakeover", new { conversationId, contactName, timestamp = DateTime.UtcNow }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send takeover notification for tenant {TenantId}", tenantId);
        }
    }
}

public class NotificationHub : Hub
{
    public async Task JoinTenantGroup(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, tenantId);
    }

    public async Task LeaveTenantGroup(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tenantId);
    }
}
