using FluentValidation;
using MediatR;
using VendaZap.Application.Common.Interfaces;
using VendaZap.Domain.Common;
using VendaZap.Domain.Entities;
using VendaZap.Domain.Enums;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Application.Features.Products;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string PriceFormatted,
    string? ImageUrl,
    string? ExternalLink,
    int StockQuantity,
    bool TrackStock,
    string Status,
    string? Category,
    string? Sku,
    bool IsAvailable,
    DateTime CreatedAt);

// ─── Paginação ────────────────────────────────────────────────────────────────

public record ProductsPagedDto(
    IEnumerable<ProductDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

// ─── Queries ─────────────────────────────────────────────────────────────────

public record GetProductsQuery(
    bool ActiveOnly = true,
    string? Category = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20)
    : IRequest<Result<ProductsPagedDto>>;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, Result<ProductsPagedDto>>
{
    private readonly IProductRepository _products;
    private readonly ICurrentTenantService _tenant;

    public GetProductsQueryHandler(IProductRepository products, ICurrentTenantService tenant)
    {
        _products = products; _tenant = tenant;
    }

    public async Task<Result<ProductsPagedDto>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        IEnumerable<Product> products;

        if (!string.IsNullOrWhiteSpace(request.Search))
            products = await _products.SearchAsync(_tenant.TenantId, request.Search, ct);
        else if (!string.IsNullOrWhiteSpace(request.Category))
            products = await _products.GetByCategoryAsync(_tenant.TenantId, request.Category, ct);
        else
            products = await _products.GetByTenantAsync(_tenant.TenantId, request.ActiveOnly, ct);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var allItems = products.ToList();
        var totalCount = allItems.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var items = allItems.Skip((page - 1) * pageSize).Take(pageSize).Select(MapToDto);

        return Result.Success(new ProductsPagedDto(items, totalCount, page, pageSize, totalPages));
    }

    private static ProductDto MapToDto(Product p) => new(
        p.Id, p.Name, p.Description, p.Price.Amount, p.Price.FormatBRL(),
        p.ImageUrl, p.ExternalLink, p.StockQuantity, p.TrackStock,
        p.Status.ToString(), p.Category, p.Sku, p.IsAvailable(), p.CreatedAt);
}

public record GetProductByIdQuery(Guid Id) : IRequest<Result<ProductDto>>;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly ICurrentTenantService _tenant;

    public GetProductByIdQueryHandler(IProductRepository products, ICurrentTenantService tenant)
    {
        _products = products; _tenant = tenant;
    }

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(request.Id, ct);
        if (product is null || product.TenantId != _tenant.TenantId)
            return Result.Failure<ProductDto>(Error.NotFound("Produto"));
        return Result.Success(new ProductDto(
            product.Id, product.Name, product.Description, product.Price.Amount, product.Price.FormatBRL(),
            product.ImageUrl, product.ExternalLink, product.StockQuantity, product.TrackStock,
            product.Status.ToString(), product.Category, product.Sku, product.IsAvailable(), product.CreatedAt));
    }
}

// ─── Commands ─────────────────────────────────────────────────────────────────

public record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    string? ImageUrl,
    string? ExternalLink,
    int StockQuantity,
    bool TrackStock,
    string? Category,
    string? Sku) : IRequest<Result<ProductDto>>;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Nome é obrigatório (máx. 200 caracteres).");
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("Preço não pode ser negativo.");
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0).When(x => x.TrackStock);
        RuleFor(x => x.Category).MaximumLength(100).When(x => x.Category is not null);
        RuleFor(x => x.Sku).MaximumLength(100).When(x => x.Sku is not null);
        RuleFor(x => x.ExternalLink).MaximumLength(500).When(x => x.ExternalLink is not null);
    }
}

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantService _tenant;

    public CreateProductCommandHandler(IProductRepository products, IUnitOfWork uow, ICurrentTenantService tenant)
    {
        _products = products; _uow = uow; _tenant = tenant;
    }

    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var result = Product.Create(
            _tenant.TenantId, request.Name, request.Description, request.Price,
            request.ImageUrl, request.ExternalLink, request.StockQuantity,
            request.TrackStock, request.Category, request.Sku);

        if (result.IsFailure) return Result.Failure<ProductDto>(result.Error);

        await _products.AddAsync(result.Value, ct);
        await _uow.SaveChangesAsync(ct);

        var p = result.Value;
        return Result.Success(new ProductDto(
            p.Id, p.Name, p.Description, p.Price.Amount, p.Price.FormatBRL(),
            p.ImageUrl, p.ExternalLink, p.StockQuantity, p.TrackStock,
            p.Status.ToString(), p.Category, p.Sku, p.IsAvailable(), p.CreatedAt));
    }
}

public record UpdateProductCommand(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string? ImageUrl,
    string? ExternalLink,
    string? Category) : IRequest<Result<ProductDto>>;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Nome é obrigatório (máx. 200 caracteres).");
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("Preço não pode ser negativo.");
        RuleFor(x => x.Category).MaximumLength(100).When(x => x.Category is not null);
        RuleFor(x => x.ExternalLink).MaximumLength(500).When(x => x.ExternalLink is not null);
        RuleFor(x => x.ImageUrl).MaximumLength(500).When(x => x.ImageUrl is not null);
    }
}

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantService _tenant;

    public UpdateProductCommandHandler(IProductRepository products, IUnitOfWork uow, ICurrentTenantService tenant)
    {
        _products = products; _uow = uow; _tenant = tenant;
    }

    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(request.Id, ct);
        if (product is null || product.TenantId != _tenant.TenantId)
            return Result.Failure<ProductDto>(Error.NotFound("Produto"));

        var updateResult = product.Update(request.Name, request.Description, request.Price, request.ImageUrl, request.ExternalLink, request.Category);
        if (updateResult.IsFailure) return Result.Failure<ProductDto>(updateResult.Error);

        _products.Update(product);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new ProductDto(
            product.Id, product.Name, product.Description, product.Price.Amount, product.Price.FormatBRL(),
            product.ImageUrl, product.ExternalLink, product.StockQuantity, product.TrackStock,
            product.Status.ToString(), product.Category, product.Sku, product.IsAvailable(), product.CreatedAt));
    }
}

public record UpdateProductStockCommand(Guid Id, int Quantity) : IRequest<Result> { }

public class UpdateProductStockCommandHandler : IRequestHandler<UpdateProductStockCommand, Result>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantService _tenant;

    public UpdateProductStockCommandHandler(IProductRepository products, IUnitOfWork uow, ICurrentTenantService tenant)
    {
        _products = products; _uow = uow; _tenant = tenant;
    }

    public async Task<Result> Handle(UpdateProductStockCommand request, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(request.Id, ct);
        if (product is null || product.TenantId != _tenant.TenantId)
            return Result.Failure(Error.NotFound("Produto"));
        var r = product.UpdateStock(request.Quantity);
        if (r.IsFailure) return r;
        _products.Update(product);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record DeleteProductCommand(Guid Id) : IRequest<Result> { }

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantService _tenant;

    public DeleteProductCommandHandler(IProductRepository products, IUnitOfWork uow, ICurrentTenantService tenant)
    {
        _products = products; _uow = uow; _tenant = tenant;
    }

    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(request.Id, ct);
        if (product is null || product.TenantId != _tenant.TenantId)
            return Result.Failure(Error.NotFound("Produto"));
        product.Deactivate();
        _products.Update(product);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record ToggleProductActiveCommand(Guid Id) : IRequest<Result<ProductDto>> { }

public class ToggleProductActiveCommandHandler : IRequestHandler<ToggleProductActiveCommand, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantService _tenant;

    public ToggleProductActiveCommandHandler(IProductRepository products, IUnitOfWork uow, ICurrentTenantService tenant)
    {
        _products = products; _uow = uow; _tenant = tenant;
    }

    public async Task<Result<ProductDto>> Handle(ToggleProductActiveCommand request, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(request.Id, ct);
        if (product is null || product.TenantId != _tenant.TenantId)
            return Result.Failure<ProductDto>(Error.NotFound("Produto"));

        var result = product.Status == VendaZap.Domain.Enums.ProductStatus.Active
            ? product.Deactivate()
            : product.Activate();

        if (result.IsFailure) return Result.Failure<ProductDto>(result.Error);

        _products.Update(product);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new ProductDto(
            product.Id, product.Name, product.Description, product.Price.Amount, product.Price.FormatBRL(),
            product.ImageUrl, product.ExternalLink, product.StockQuantity, product.TrackStock,
            product.Status.ToString(), product.Category, product.Sku, product.IsAvailable(), product.CreatedAt));
    }
}

public record UpdateProductImageCommand(Guid Id, string ImageUrl) : IRequest<Result<ProductDto>> { }

public class UpdateProductImageCommandHandler : IRequestHandler<UpdateProductImageCommand, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenantService _tenant;

    public UpdateProductImageCommandHandler(IProductRepository products, IUnitOfWork uow, ICurrentTenantService tenant)
    {
        _products = products; _uow = uow; _tenant = tenant;
    }

    public async Task<Result<ProductDto>> Handle(UpdateProductImageCommand request, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(request.Id, ct);
        if (product is null || product.TenantId != _tenant.TenantId)
            return Result.Failure<ProductDto>(Error.NotFound("Produto"));

        var updateResult = product.Update(
            product.Name, product.Description, product.Price.Amount,
            request.ImageUrl, product.ExternalLink, product.Category);

        if (updateResult.IsFailure) return Result.Failure<ProductDto>(updateResult.Error);

        _products.Update(product);
        await _uow.SaveChangesAsync(ct);

        return Result.Success(new ProductDto(
            product.Id, product.Name, product.Description, product.Price.Amount, product.Price.FormatBRL(),
            product.ImageUrl, product.ExternalLink, product.StockQuantity, product.TrackStock,
            product.Status.ToString(), product.Category, product.Sku, product.IsAvailable(), product.CreatedAt));
    }
}
