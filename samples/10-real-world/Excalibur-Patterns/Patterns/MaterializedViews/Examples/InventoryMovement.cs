namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Inventory movement record.
/// </summary>
public class InventoryMovement
{
	public required string Id { get; set; }
	public required string SKU { get; set; }
	public required string WarehouseId { get; set; }
	public MovementType Type { get; set; }
	public int Quantity { get; set; }
	public DateTime Date { get; set; }
	public required string Reference { get; set; }
}
