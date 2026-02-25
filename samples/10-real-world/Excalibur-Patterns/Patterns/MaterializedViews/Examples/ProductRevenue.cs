namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Product revenue information.
/// </summary>
public class ProductRevenue
{
	public string ProductId { get; set; } = string.Empty;
	public string ProductName { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public decimal Revenue { get; set; }
	public int UnitsSold { get; set; }
}
