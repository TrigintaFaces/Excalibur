namespace Company.ExcaliburCqrs.ReadModel;

/// <summary>
/// Read model for order queries (CQRS read side).
/// </summary>
public sealed class OrderReadModel
{
    /// <summary>
    /// Gets or sets the order identifier.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets the current order status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of items in the order.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Gets or sets when the order was last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }
}
