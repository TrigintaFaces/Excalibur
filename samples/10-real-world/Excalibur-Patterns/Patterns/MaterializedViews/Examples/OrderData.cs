namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Order source data.
/// </summary>
public class OrderData
{
	public string OrderId { get; set; } = string.Empty;
	public string CustomerId { get; set; } = string.Empty;
	public DateTime OrderDate { get; set; }
	public decimal TotalAmount { get; set; }
	public string Status { get; set; } = string.Empty;
}
