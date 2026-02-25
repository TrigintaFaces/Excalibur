namespace examples.Excalibur.Patterns.EventSourcing.Examples.Projections;

/// <summary>
///     Information about a recent order.
/// </summary>
public class RecentOrderInfo
{
	/// <summary>
	///     Gets or sets the order ID.
	/// </summary>
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the order date.
	/// </summary>
	public DateTime OrderDate { get; set; }

	/// <summary>
	///     Gets or sets the total amount.
	/// </summary>
	public decimal TotalAmount { get; set; }

	/// <summary>
	///     Gets or sets the status.
	/// </summary>
	public string Status { get; set; } = string.Empty;
}
