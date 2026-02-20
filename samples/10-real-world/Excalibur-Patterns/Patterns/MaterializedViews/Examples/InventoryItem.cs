namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Inventory item model.
/// </summary>
public class InventoryItem
{
	public required string SKU { get; set; }
	public required string ProductName { get; set; }
	public required string Category { get; set; }
	public required string WarehouseId { get; set; }
	public required string WarehouseName { get; set; }
	public int QuantityOnHand { get; set; }
	public int ReorderPoint { get; set; }
	public int ReorderQuantity { get; set; }
	public decimal UnitCost { get; set; }
	public decimal UnitPrice { get; set; }
	public DateTime? LastRestockDate { get; set; }
}
