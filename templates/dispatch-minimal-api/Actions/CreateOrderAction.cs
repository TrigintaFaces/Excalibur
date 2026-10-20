using System.ComponentModel.DataAnnotations;
using Excalibur.Dispatch;

namespace Company.DispatchMinimalApi.Actions;

/// <summary>
/// Represents a request to create a new order. Returns the created order result.
/// </summary>
public sealed class CreateOrderAction : IDispatchAction<CreateOrderResult>
{
    public CreateOrderAction() { }

    public CreateOrderAction(string productId, int quantity)
    {
        ProductId = productId;
        Quantity = quantity;
    }

    [Required]
    public string ProductId { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}

/// <summary>
/// The result of creating an order, passed through the
/// <c>DispatchPostBodyAction</c> + <c>ToHttpResult</c> railway-to-HTTP bridge.
/// </summary>
public sealed record CreateOrderResult(Guid OrderId);
