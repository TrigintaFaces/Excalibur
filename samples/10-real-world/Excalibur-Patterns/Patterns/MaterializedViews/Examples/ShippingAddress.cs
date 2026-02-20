namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Shipping address model.
/// </summary>
public class ShippingAddress
{
	public required string Street { get; set; }
	public required string City { get; set; }
	public required string State { get; set; }
	public required string ZipCode { get; set; }
	public required string Country { get; set; }
	public required string Region { get; set; }
}
