using VendaZap.Domain.Common;
using VendaZap.Domain.Enums;
using VendaZap.Domain.ValueObjects;

namespace VendaZap.Domain.Entities;

public class Order : Entity
{
    public Guid TenantId { get; private set; }
    public Guid ContactId { get; private set; }
    public Guid ConversationId { get; private set; }
    public string OrderNumber { get; private set; } = default!;
    public OrderStatus Status { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string? PaymentLink { get; private set; }
    public string? PaymentInstructions { get; private set; }
    public string? PixKey { get; private set; }
    public Money Subtotal { get; private set; } = default!;
    public Money ShippingCost { get; private set; } = default!;
    public Money Total { get; private set; } = default!;
    public string? DeliveryAddress { get; private set; }
    public string? DeliveryCity { get; private set; }
    public string? DeliveryState { get; private set; }
    public string? DeliveryZipCode { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? TrackingCode { get; private set; }

    // Navigation
    public Tenant Tenant { get; private set; } = default!;
    public Contact Contact { get; private set; } = default!;
    public Conversation Conversation { get; private set; } = default!;

    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public static Order Create(
        Guid tenantId,
        Guid contactId,
        Guid conversationId,
        string orderNumber,
        PaymentMethod paymentMethod)
    {
        var order = new Order
        {
            TenantId = tenantId,
            ContactId = contactId,
            ConversationId = conversationId,
            OrderNumber = orderNumber,
            Status = OrderStatus.Pending,
            PaymentMethod = paymentMethod,
            Subtotal = new Money(0, "BRL"),
            ShippingCost = new Money(0, "BRL"),
            Total = new Money(0, "BRL")
        };
        order.AddDomainEvent(new OrderCreatedEvent(order.Id, tenantId, contactId, orderNumber));
        return order;
    }

    public Result AddItem(OrderItem item)
    {
        _items.Add(item);
        RecalculateTotals();
        SetUpdatedAt();
        return Result.Success();
    }

    public Result RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result.Failure(Error.NotFound("OrderItem"));
        _items.Remove(item);
        RecalculateTotals();
        SetUpdatedAt();
        return Result.Success();
    }

    public Result SetDeliveryInfo(string address, string city, string state, string zipCode, decimal shippingCost)
    {
        DeliveryAddress = address;
        DeliveryCity = city;
        DeliveryState = state;
        DeliveryZipCode = zipCode;
        ShippingCost = new Money(shippingCost, "BRL");
        RecalculateTotals();
        SetUpdatedAt();
        return Result.Success();
    }

    public Result SetPaymentInfo(PaymentMethod method, string? link = null, string? pixKey = null, string? instructions = null)
    {
        PaymentMethod = method;
        PaymentLink = link;
        PixKey = pixKey;
        PaymentInstructions = instructions;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result Confirm()
    {
        if (!_items.Any())
            return Result.Failure(Error.BusinessRule("EmptyOrder", "Pedido não pode ser confirmado sem itens."));
        Status = OrderStatus.Confirmed;
        SetUpdatedAt();
        AddDomainEvent(new OrderConfirmedEvent(Id, TenantId, ContactId, Total.Amount));
        return Result.Success();
    }

    public Result MarkAsPaid()
    {
        Status = OrderStatus.Paid;
        PaidAt = DateTime.UtcNow;
        SetUpdatedAt();
        AddDomainEvent(new OrderPaidEvent(Id, TenantId, Total.Amount));
        return Result.Success();
    }

    public Result Ship(string? trackingCode = null)
    {
        if (Status != OrderStatus.Paid && Status != OrderStatus.Confirmed)
            return Result.Failure(Error.BusinessRule("InvalidStatus", "Pedido precisa estar pago para ser enviado."));
        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
        TrackingCode = trackingCode;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result MarkDelivered()
    {
        Status = OrderStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        Status = OrderStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancellationReason = reason;
        SetUpdatedAt();
        AddDomainEvent(new OrderCancelledEvent(Id, TenantId, reason));
        return Result.Success();
    }

    private void RecalculateTotals()
    {
        var subtotal = _items.Sum(i => i.UnitPrice.Amount * i.Quantity);
        Subtotal = new Money(subtotal, "BRL");
        Total = new Money(subtotal + ShippingCost.Amount, "BRL");
    }

    public string GenerateWhatsAppSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"🛒 *Resumo do Pedido #{OrderNumber}*");
        sb.AppendLine("---");
        foreach (var item in _items)
            sb.AppendLine($"• {item.ProductName} x{item.Quantity} = {item.Total.FormatBRL()}");
        sb.AppendLine("---");
        sb.AppendLine($"Subtotal: {Subtotal.FormatBRL()}");
        if (ShippingCost.Amount > 0) sb.AppendLine($"Frete: {ShippingCost.FormatBRL()}");
        sb.AppendLine($"*Total: {Total.FormatBRL()}*");

        if (PaymentMethod == PaymentMethod.Pix && PixKey != null)
        {
            sb.AppendLine();
            sb.AppendLine("💸 *Pagamento via PIX*");
            sb.AppendLine($"Chave: `{PixKey}`");
        }
        else if (PaymentLink != null)
        {
            sb.AppendLine();
            sb.AppendLine($"💳 *Link de pagamento:*");
            sb.AppendLine(PaymentLink);
        }
        return sb.ToString();
    }
}

public class OrderItem : Entity
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = default!;
    public Money Total { get; private set; } = default!;

    public Order Order { get; private set; } = default!;
    public Product Product { get; private set; } = default!;

    private OrderItem() { }

    public static OrderItem Create(Guid orderId, Guid productId, string productName, int quantity, decimal unitPrice)
    {
        return new OrderItem
        {
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = new Money(unitPrice, "BRL"),
            Total = new Money(unitPrice * quantity, "BRL")
        };
    }
}

// Domain Events
public record OrderCreatedEvent(Guid OrderId, Guid TenantId, Guid ContactId, string OrderNumber) : DomainEvent;
public record OrderConfirmedEvent(Guid OrderId, Guid TenantId, Guid ContactId, decimal Total) : DomainEvent;
public record OrderPaidEvent(Guid OrderId, Guid TenantId, decimal Total) : DomainEvent;
public record OrderCancelledEvent(Guid OrderId, Guid TenantId, string Reason) : DomainEvent;
