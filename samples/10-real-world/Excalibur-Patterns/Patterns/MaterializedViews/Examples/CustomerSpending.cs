namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Customer spending information.
/// </summary>
public class CustomerSpending
{
	public string CustomerId { get; set; } = string.Empty;
	public string CustomerName { get; set; } = string.Empty;
	public decimal TotalSpent { get; set; }
	public int OrderCount { get; set; }
	public DateTime FirstPurchase { get; set; }
	public DateTime LastPurchase { get; set; }
}
