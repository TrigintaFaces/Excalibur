namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Order item model.
/// </summary>
public class OrderItem
{
	public required string ProductId { get; set; }
	public required string ProductName { get; set; }
	public required string Category { get; set; }
	public int Quantity { get; set; }
	public decimal Amount { get; set; }
	public bool IsBackordered { get; set; }
}
