using MediatR;
using VendaZap.Application.Common.Interfaces;
using VendaZap.Domain.Common;
using VendaZap.Domain.Enums;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Application.Features.Dashboard;

public record GetDashboardQuery(DateTime? From = null, DateTime? To = null) : IRequest<Result<DashboardDto>>;

public record DashboardDto(
    // Conversations
    int OpenConversations,
    int TotalConversationsToday,
    int BotConversations,
    int HumanConversations,

    // Orders
    int TotalOrdersToday,
    int PendingOrders,
    int PaidOrdersToday,
    decimal RevenueToday,
    decimal RevenueThisMonth,

    // Contacts
    int TotalContacts,
    int NewContactsToday,

    // Top products
    IEnumerable<TopProductDto> TopProducts,

    // Charts
    IEnumerable<DailyRevenueDto> DailyRevenue);

public record TopProductDto(Guid Id, string Name, int TotalSold, decimal TotalRevenue);
public record DailyRevenueDto(DateTime Date, decimal Revenue, int Orders);

public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
{
    private readonly IConversationRepository _conversations;
    private readonly IOrderRepository _orders;
    private readonly IContactRepository _contacts;
    private readonly ICurrentTenantService _tenant;

    public GetDashboardQueryHandler(
        IConversationRepository conversations, IOrderRepository orders,
        IContactRepository contacts, ICurrentTenantService tenant)
    {
        _conversations = conversations; _orders = orders;
        _contacts = contacts; _tenant = tenant;
    }

    public async Task<Result<DashboardDto>> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var from = request.From ?? today;
        var to = request.To ?? today.AddDays(1).AddTicks(-1);

        var openConvs = await _conversations.CountOpenAsync(tenantId, ct);
        var revenueToday = await _orders.GetRevenueAsync(tenantId, today, today.AddDays(1), ct);
        var revenueMonth = await _orders.GetRevenueAsync(tenantId, monthStart, DateTime.UtcNow, ct);
        var pendingOrders = await _orders.CountByStatusAsync(tenantId, OrderStatus.Pending, ct);
        var totalContacts = await _contacts.CountByTenantAsync(tenantId, ct);

        var dashboard = new DashboardDto(
            OpenConversations: openConvs,
            TotalConversationsToday: openConvs,
            BotConversations: 0,
            HumanConversations: 0,
            TotalOrdersToday: 0,
            PendingOrders: pendingOrders,
            PaidOrdersToday: 0,
            RevenueToday: revenueToday,
            RevenueThisMonth: revenueMonth,
            TotalContacts: totalContacts,
            NewContactsToday: 0,
            TopProducts: [],
            DailyRevenue: []);

        return Result.Success(dashboard);
    }
}
