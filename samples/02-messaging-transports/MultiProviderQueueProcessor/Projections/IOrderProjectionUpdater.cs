// Order Projection Updater Interface

namespace MultiProviderQueueProcessor.Projections;

/// <summary>
/// Interface for updating order projections (read models) in ElasticSearch.
/// </summary>
public interface IOrderProjectionUpdater
{
	/// <summary>
	/// Creates a new order projection.
	/// </summary>
	Task CreateOrderProjectionAsync(
		string orderId,
		string customerId,
		decimal totalAmount,
		string currency,
		CancellationToken cancellationToken);

	/// <summary>
	/// Adds an item to the order projection.
	/// </summary>
	Task AddOrderItemAsync(
		string orderId,
		string productId,
		string productName,
		int quantity,
		decimal unitPrice,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates the order status.
	/// </summary>
	Task UpdateOrderStatusAsync(
		string orderId,
		string status,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks the order as shipped.
	/// </summary>
	Task MarkOrderShippedAsync(
		string orderId,
		string trackingNumber,
		string carrier,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks the order as cancelled.
	/// </summary>
	Task MarkOrderCancelledAsync(
		string orderId,
		string reason,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets an order projection by ID.
	/// </summary>
	Task<OrderProjection?> GetOrderAsync(string orderId, CancellationToken cancellationToken);

	/// <summary>
	/// Searches orders by customer ID.
	/// </summary>
	Task<IReadOnlyList<OrderProjection>> SearchByCustomerAsync(
		string customerId,
		int skip,
		int take,
		CancellationToken cancellationToken);
}

/// <summary>
/// Order projection (read model) stored in ElasticSearch.
/// </summary>
public sealed class OrderProjection
{
	public required string Id { get; init; }
	public required string CustomerId { get; init; }
	public decimal TotalAmount { get; set; }
	public required string Currency { get; init; }
	public string Status { get; set; } = "Created";
	public List<OrderItemProjection> Items { get; init; } = [];
	public string? TrackingNumber { get; set; }
	public string? Carrier { get; set; }
	public string? CancellationReason { get; set; }
	public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
	public DateTimeOffset? ShippedAt { get; set; }
	public DateTimeOffset? CancelledAt { get; set; }
	public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Order item projection.
/// </summary>
public sealed class OrderItemProjection
{
	public required string ProductId { get; init; }
	public required string ProductName { get; init; }
	public required int Quantity { get; init; }
	public required decimal UnitPrice { get; init; }
	public decimal Total => Quantity * UnitPrice;
}
