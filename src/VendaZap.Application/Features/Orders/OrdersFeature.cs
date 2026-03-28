using FluentValidation;
using MediatR;
using VendaZap.Application.Common.Interfaces;
using VendaZap.Domain.Common;
using VendaZap.Domain.Entities;
using VendaZap.Domain.Enums;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Application.Features.Orders;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid ContactId,
    string ContactName,
    string Status,
    string PaymentMethod,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    string? DeliveryAddress,
    string? PaymentLink,
    string? PixKey,
    IEnumerable<OrderItemDto> Items,
    DateTime CreatedAt);

public record OrderItemDto(Guid Id, string ProductName, int Quantity, decimal UnitPrice, decimal Total);

// ─── Queries ─────────────────────────────────────────────────────────────────

public record GetOrdersQuery(OrderStatus? Status = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<IEnumerable<OrderDto>>>;

public class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, Result<IEnumerable<OrderDto>>>
{
    private readonly IOrderRepository _orders;
    private readonly ICurrentTenantService _tenant;

    public GetOrdersQueryHandler(IOrderRepository orders, ICurrentTenantService tenant)
    {
        _orders = orders; _tenant = tenant;
    }

    public async Task<Result<IEnumerable<OrderDto>>> Handle(GetOrdersQuery request, CancellationToken ct)
    {
        var orders = await _orders.GetByTenantAsync(_tenant.TenantId, request.Status, request.Page, request.PageSize, ct);
        return Result.Success(orders.Select(MapToDto));
    }

    private static OrderDto MapToDto(Order o) => new(
        o.Id, o.OrderNumber, o.ContactId,
        o.Contact?.GetDisplayName() ?? "Desconhecido",
        o.Status.ToString(), o.PaymentMethod.ToString(),
        o.Subtotal.Amount, o.ShippingCost.Amount, o.Total.Amount,
        o.DeliveryAddress != null
            ? $"{o.DeliveryAddress}, {o.DeliveryCity}/{o.DeliveryState}"
            : null,
        o.PaymentLink, o.PixKey,
        o.Items.Select(i => new OrderItemDto(i.Id, i.ProductName, i.Quantity, i.UnitPrice.Amount, i.Total.Amount)),
        o.CreatedAt);
}

// ─── Commands ─────────────────────────────────────────────────────────────────

public record CreateOrderFromConversationCommand(
    Guid ConversationId,
    PaymentMethod PaymentMethod,
    string? PaymentLink = null,
    string? PixKey = null) : IRequest<Result<OrderDto>>;

public class CreateOrderFromConversationCommandHandler : IRequestHandler<CreateOrderFromConversationCommand, Result<OrderDto>>
{
    private readonly IConversationRepository _conversations;
    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantService _tenant;
    private readonly IWhatsAppService _whatsApp;
    private readonly ITenantRepository _tenants;

    public CreateOrderFromConversationCommandHandler(
        IConversationRepository conversations, IOrderRepository orders,
        IProductRepository products, IUnitOfWork uow,
        ICurrentTenantService tenant, IWhatsAppService whatsApp,
        ITenantRepository tenants)
    {
        _conversations = conversations; _orders = orders; _products = products;
        _uow = uow; _tenant = tenant; _whatsApp = whatsApp; _tenants = tenants;
    }

    public async Task<Result<OrderDto>> Handle(CreateOrderFromConversationCommand request, CancellationToken ct)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId, ct);
        if (conversation is null || conversation.TenantId != _tenant.TenantId)
            return Result.Failure<OrderDto>(Error.NotFound("Conversa"));

        var orderNumber = await _orders.GenerateOrderNumberAsync(_tenant.TenantId, ct);
        var order = Order.Create(
            _tenant.TenantId, conversation.ContactId, conversation.Id,
            orderNumber, request.PaymentMethod);

        // Parse cart items from conversation
        var cart = System.Text.Json.JsonSerializer.Deserialize<CartDto>(conversation.CartJson);
        if (cart?.Items?.Any() == true)
        {
            foreach (var cartItem in cart.Items)
            {
                var product = await _products.GetByIdAsync(cartItem.ProductId, ct);
                if (product is null) continue;

                var deductResult = product.DeductStock(cartItem.Quantity);
                if (deductResult.IsFailure) return Result.Failure<OrderDto>(deductResult.Error);

                var item = OrderItem.Create(order.Id, product.Id, product.Name, cartItem.Quantity, product.Price.Amount);
                order.AddItem(item);
                _products.Update(product);
            }
        }

        order.SetPaymentInfo(request.PaymentMethod, request.PaymentLink, request.PixKey);
        await _orders.AddAsync(order, ct);

        conversation.SetActiveOrder(order.Id);
        conversation.AdvanceStage(ConversationStage.AwaitingPayment);
        _conversations.Update(conversation);

        await _uow.SaveChangesAsync(ct);

        // Notify customer on WhatsApp
        var tenantEntity = await _tenants.GetByIdAsync(_tenant.TenantId, ct);
        if (tenantEntity is not null)
        {
            var summary = order.GenerateWhatsAppSummary();
            await _whatsApp.SendTextMessageAsync(
                tenantEntity.WhatsAppPhoneNumberId, tenantEntity.WhatsAppAccessToken,
                conversation.Contact.PhoneNumber, summary, ct);
        }

        var dto = new OrderDto(
            order.Id, order.OrderNumber, order.ContactId,
            conversation.Contact?.GetDisplayName() ?? "",
            order.Status.ToString(), order.PaymentMethod.ToString(),
            order.Subtotal.Amount, order.ShippingCost.Amount, order.Total.Amount,
            null, order.PaymentLink, order.PixKey,
            order.Items.Select(i => new OrderItemDto(i.Id, i.ProductName, i.Quantity, i.UnitPrice.Amount, i.Total.Amount)),
            order.CreatedAt);

        return Result.Success(dto);
    }
}

public record UpdateOrderStatusCommand(Guid OrderId, OrderStatus NewStatus, string? TrackingCode = null, string? CancellationReason = null)
    : IRequest<r>;

public class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand, Result>
{
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantService _tenant;

    public UpdateOrderStatusCommandHandler(IOrderRepository orders, IUnitOfWork uow, ICurrentTenantService tenant)
    {
        _orders = orders; _uow = uow; _tenant = tenant;
    }

    public async Task<r> Handle(UpdateOrderStatusCommand request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(request.OrderId, ct);
        if (order is null || order.TenantId != _tenant.TenantId)
            return Result.Failure(Error.NotFound("Pedido"));

        Result result = request.NewStatus switch
        {
            OrderStatus.Confirmed => order.Confirm(),
            OrderStatus.Paid => order.MarkAsPaid(),
            OrderStatus.Shipped => order.Ship(request.TrackingCode),
            OrderStatus.Delivered => order.MarkDelivered(),
            OrderStatus.Cancelled => order.Cancel(request.CancellationReason ?? "Cancelado pelo lojista"),
            _ => Result.Failure(Error.Validation("Status", "Status inválido."))
        };

        if (result.IsFailure) return result;
        _orders.Update(order);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// Cart DTO for deserialization
public record CartDto(List<CartItemDto>? Items);
public record CartItemDto(Guid ProductId, int Quantity);
