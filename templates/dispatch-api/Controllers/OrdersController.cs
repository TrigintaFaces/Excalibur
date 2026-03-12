using Company.DispatchApi.Actions;
using Excalibur.Dispatch.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Company.DispatchApi.Controllers;

/// <summary>
/// API controller for order operations using Excalibur messaging.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public OrdersController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Creates a new order.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderAction action,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.DispatchAsync<CreateOrderAction, Guid>(action, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetOrder), new { id = result.ReturnValue }, result.ReturnValue);
    }

    /// <summary>
    /// Retrieves an order by identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.DispatchAsync<GetOrderAction, OrderResult?>(new GetOrderAction(id), cancellationToken).ConfigureAwait(false);
        return result.ReturnValue is not null ? Ok(result.ReturnValue) : NotFound();
    }
}
