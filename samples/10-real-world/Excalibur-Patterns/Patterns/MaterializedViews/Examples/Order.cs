namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Order entity model.
/// </summary>
public class Order
{
	public required string OrderId { get; set; }
	public required string CustomerId { get; set; }
	public required string CustomerName { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public DateTime? CancelledAt { get; set; }
	public DateTime? ShippedAt { get; set; }
	public DateTime? DeliveredAt { get; set; }
	public DateTime? EstimatedDeliveryDate { get; set; }
	public OrderStatus Status { get; set; }
	public decimal TotalAmount { get; set; }
	public required string PaymentMethod { get; set; }
	public PaymentStatus PaymentStatus { get; set; }
	public required ShippingAddress ShippingAddress { get; set; }
	public List<OrderItem> Items { get; set; } = new();
}
