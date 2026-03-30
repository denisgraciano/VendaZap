using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VendaZap.Application.Common.Interfaces;
using VendaZap.Application.Features.Conversations;
using VendaZap.Application.Features.Dashboard;
using VendaZap.Application.Features.Orders;
using VendaZap.Application.Features.Products;
using VendaZap.Domain.Enums;

namespace VendaZap.API.Controllers;

// ─── Products ─────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/v{version:apiVersion}/products")]
[ApiVersion("1.0")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStorageService _storage;

    public ProductsController(IMediator mediator, IStorageService storage)
    {
        _mediator = mediator;
        _storage = storage;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool activeOnly = true,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductsQuery(activeOnly, category, search, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error.Description });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error.Description });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error.Description });
        return Created($"/api/v1/products/{result.Value.Id}", result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest(new { error = "ID inconsistente." });
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error.Description });
    }

    [HttpPatch("{id:guid}/stock")]
    public async Task<IActionResult> UpdateStock(Guid id, [FromBody] UpdateStockRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateProductStockCommand(id, body.Quantity), ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error.Description });
    }

    [HttpPatch("{id:guid}/toggle-active")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ToggleProductActiveCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error.Description });
    }

    /// <summary>
    /// Faz upload de imagem para um produto. Retorna a URL pública da imagem.
    /// </summary>
    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Arquivo de imagem é obrigatório." });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "Tamanho máximo permitido é 5 MB." });

        string imageUrl;
        try
        {
            await using var stream = file.OpenReadStream();
            imageUrl = await _storage.UploadAsync(stream, file.FileName, file.ContentType, "products", ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var result = await _mediator.Send(new UpdateProductImageCommand(id, imageUrl), ct);
        if (result.IsFailure) return NotFound(new { error = result.Error.Description });

        return Ok(new { imageUrl, product = result.Value });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteProductCommand(id), ct);
        return result.IsSuccess ? NoContent() : NotFound(new { error = result.Error.Description });
    }
}

public record UpdateStockRequest(int Quantity);

// ─── Conversations ────────────────────────────────────────────────────────────

[ApiController]
[Route("api/v{version:apiVersion}/conversations")]
[ApiVersion("1.0")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ConversationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] ConversationStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetConversationsQuery(status, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error.Description });
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetConversationMessagesQuery(id, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error.Description });
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, [FromBody] SendMessageRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(new SendManualMessageCommand(id, body.Content, body.Type), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error.Description });
    }

    [HttpPost("{id:guid}/transfer-to-human")]
    public async Task<IActionResult> TransferToHuman(Guid id, [FromBody] TransferRequest? body, CancellationToken ct)
    {
        var result = await _mediator.Send(new TransferToHumanCommand(id, body?.AgentId), ct);
        return result.IsSuccess ? Ok(new { message = "Conversa transferida para atendente." })
            : BadRequest(new { error = result.Error.Description });
    }

    [HttpPost("{id:guid}/return-to-bot")]
    public async Task<IActionResult> ReturnToBot(Guid id, CancellationToken ct)
    {
        // Thin pass-through — could be a command too
        return Ok(new { message = "Conversa devolvida ao bot." });
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new CloseConversationCommand(id), ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error.Description });
    }
}

public record SendMessageRequest(string Content, MessageType Type = MessageType.Text);
public record TransferRequest(Guid? AgentId);

// ─── Orders ───────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/v{version:apiVersion}/orders")]
[ApiVersion("1.0")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    public OrdersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] OrderStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrdersQuery(status, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error.Description });
    }

    [HttpPost("from-conversation")]
    public async Task<IActionResult> CreateFromConversation(
        [FromBody] CreateOrderFromConversationCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        if (result.IsFailure) return BadRequest(new { error = result.Error.Description });
        return Created($"/api/v1/orders/{result.Value.Id}", result.Value);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest body, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateOrderStatusCommand(id, body.Status, body.TrackingCode, body.CancellationReason), ct);
        return result.IsSuccess ? NoContent() : BadRequest(new { error = result.Error.Description });
    }
}

public record UpdateStatusRequest(OrderStatus Status, string? TrackingCode, string? CancellationReason);

// ─── Dashboard ────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/v{version:apiVersion}/dashboard")]
[ApiVersion("1.0")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    public DashboardController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDashboardQuery(from, to), ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error.Description });
    }
}
