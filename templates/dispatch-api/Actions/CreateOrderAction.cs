using System.ComponentModel.DataAnnotations;
using Excalibur.Dispatch;

namespace Company.DispatchApi.Actions;

/// <summary>
/// Represents a request to create a new order. Returns the new order ID.
/// </summary>
public sealed class CreateOrderAction : IDispatchAction<Guid>
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
