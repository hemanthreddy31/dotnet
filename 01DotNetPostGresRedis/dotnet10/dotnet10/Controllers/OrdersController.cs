using dotnet10.Models;
using dotnet10.Services;
using Microsoft.AspNetCore.Mvc;

namespace dotnet10.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(ShardedOrderRepository repository) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderResponse>> CreateAsync(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await repository.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByIdAsync), new { tenantId = order.TenantId, orderId = order.Id }, order);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> ListAllAsync(CancellationToken cancellationToken)
    {
        var orders = await repository.ListAllAsync(cancellationToken);
        return Ok(orders);
    }

    [HttpGet("tenant/{tenantId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> ListForTenantAsync(
        int tenantId,
        CancellationToken cancellationToken)
    {
        var orders = await repository.ListForTenantAsync(tenantId, cancellationToken);
        return Ok(orders);
    }

    [HttpGet("tenant/{tenantId:int}/{orderId:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetByIdAsync(
        int tenantId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetAsync(tenantId, orderId, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpDelete("tenant/{tenantId:int}/{orderId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(int tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteAsync(tenantId, orderId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("shards/summary")]
    [ProducesResponseType(typeof(IReadOnlyList<ShardSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ShardSummary>>> GetShardSummaryAsync(CancellationToken cancellationToken)
    {
        var summary = await repository.GetShardSummaryAsync(cancellationToken);
        return Ok(summary);
    }
}
