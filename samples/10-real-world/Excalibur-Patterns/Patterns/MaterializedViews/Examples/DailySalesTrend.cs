namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Daily sales trend data.
/// </summary>
public class DailySalesTrend
{
	public DateTime Date { get; set; }
	public decimal Revenue { get; set; }
	public int OrderCount { get; set; }
	public decimal AverageOrderValue => OrderCount > 0 ? Revenue / OrderCount : 0;
}
