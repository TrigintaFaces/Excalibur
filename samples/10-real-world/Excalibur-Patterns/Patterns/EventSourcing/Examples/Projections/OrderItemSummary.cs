namespace examples.Excalibur.Patterns.EventSourcing.Examples.Projections;

/// <summary>
///     Summary of an order item.
/// </summary>
public class OrderItemSummary
{
	/// <summary>
	///     Gets or sets the product ID.
	/// </summary>
	public string ProductId { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the product name.
	/// </summary>
	public string ProductName { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the quantity.
	/// </summary>
	public int Quantity { get; set; }

	/// <summary>
	///     Gets or sets the unit price.
	/// </summary>
	public decimal UnitPrice { get; set; }

	/// <summary>
	///     Gets the line total.
	/// </summary>
	public decimal LineTotal => Quantity * UnitPrice;
}
