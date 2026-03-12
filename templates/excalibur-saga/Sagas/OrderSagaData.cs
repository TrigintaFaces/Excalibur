using Excalibur.Saga.Abstractions;

namespace Company.ExcaliburSaga.Sagas;

/// <summary>
/// State data for the order processing saga.
/// Tracks the progress of an order through payment, inventory, and shipping steps.
/// </summary>
public sealed class OrderSagaData : ISagaData
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets the order identifier being processed.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets the product being ordered.
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity ordered.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the total amount for the order.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether payment has been collected.
    /// </summary>
    public bool PaymentCollected { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether inventory has been reserved.
    /// </summary>
    public bool InventoryReserved { get; set; }

    /// <summary>
    /// Gets or sets the shipping tracking number.
    /// </summary>
    public string? TrackingNumber { get; set; }
}
