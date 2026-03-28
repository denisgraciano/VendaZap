using VendaZap.Domain.Common;
using VendaZap.Domain.Enums;
using VendaZap.Domain.ValueObjects;

namespace VendaZap.Domain.Entities;

public class Product : Entity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public Money Price { get; private set; } = default!;
    public string? ImageUrl { get; private set; }
    public string? ExternalLink { get; private set; }
    public int StockQuantity { get; private set; }
    public bool TrackStock { get; private set; }
    public ProductStatus Status { get; private set; }
    public string? Category { get; private set; }
    public string? Sku { get; private set; }
    public int SortOrder { get; private set; }

    // Navigation
    public Tenant Tenant { get; private set; } = default!;
    private readonly List<OrderItem> _orderItems = [];
    public IReadOnlyCollection<OrderItem> OrderItems => _orderItems.AsReadOnly();

    private Product() { }

    public static Result<Product> Create(
        Guid tenantId,
        string name,
        string description,
        decimal price,
        string? imageUrl = null,
        string? externalLink = null,
        int stockQuantity = 0,
        bool trackStock = false,
        string? category = null,
        string? sku = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Product>(Error.Validation("Name", "Nome do produto é obrigatório."));
        if (price < 0)
            return Result.Failure<Product>(Error.Validation("Price", "Preço não pode ser negativo."));

        var product = new Product
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Price = new Money(price, "BRL"),
            ImageUrl = imageUrl,
            ExternalLink = externalLink,
            StockQuantity = stockQuantity,
            TrackStock = trackStock,
            Status = ProductStatus.Active,
            Category = category,
            Sku = sku
        };
        return Result.Success(product);
    }

    public Result Update(string name, string description, decimal price, string? imageUrl, string? externalLink, string? category)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Nome do produto é obrigatório."));
        if (price < 0)
            return Result.Failure(Error.Validation("Price", "Preço não pode ser negativo."));

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Price = new Money(price, "BRL");
        ImageUrl = imageUrl;
        ExternalLink = externalLink;
        Category = category;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result UpdateStock(int quantity)
    {
        if (quantity < 0)
            return Result.Failure(Error.Validation("Stock", "Estoque não pode ser negativo."));
        StockQuantity = quantity;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result DeductStock(int quantity)
    {
        if (TrackStock && StockQuantity < quantity)
            return Result.Failure(Error.BusinessRule("InsufficientStock", "Estoque insuficiente."));
        if (TrackStock)
            StockQuantity -= quantity;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result Deactivate()
    {
        Status = ProductStatus.Inactive;
        SetUpdatedAt();
        return Result.Success();
    }

    public Result Activate()
    {
        Status = ProductStatus.Active;
        SetUpdatedAt();
        return Result.Success();
    }

    public bool IsAvailable() => Status == ProductStatus.Active && (!TrackStock || StockQuantity > 0);

    public string FormatWhatsAppMessage()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"*{Name}*");
        if (!string.IsNullOrEmpty(Category)) sb.AppendLine($"📁 {Category}");
        sb.AppendLine($"💰 {Price.FormatBRL()}");
        if (!string.IsNullOrWhiteSpace(Description)) sb.AppendLine($"\n{Description}");
        if (TrackStock) sb.AppendLine($"\n📦 Disponível: {StockQuantity} un.");
        if (!string.IsNullOrEmpty(ExternalLink)) sb.AppendLine($"\n🔗 {ExternalLink}");
        return sb.ToString();
    }
}
