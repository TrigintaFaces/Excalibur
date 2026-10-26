// Order Domain Events

using Excalibur.Dispatch;

namespace MultiProviderQueueProcessor.Events;

/// <summary>
/// Event raised when an order is created.
/// </summary>
[MessageName("Contoso.Orders.OrderCreatedEvent")]
public sealed record OrderCreatedEvent : DomainEvent
{
	/// <summary>Gets the order this event belongs to — the event's own business data.</summary>
	/// <remarks>
	/// A domain event carries no aggregate id and no stream version. The aggregate id is a parameter
	/// to the event store; the version is assigned by the store at append time and read back on
	/// <c>StoredEvent.Version</c>. <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c> come from
	/// <c>DomainEvent</c>.
	/// </remarks>
	public required string OrderId { get; init; }

	public required string CustomerId { get; init; }
	public required decimal TotalAmount { get; init; }
	public required string Currency { get; init; }
}

/// <summary>
/// Event raised when an item is added to an order.
/// </summary>
[MessageName("Contoso.Orders.OrderItemAddedEvent")]
public sealed record OrderItemAddedEvent : DomainEvent
{
	/// <summary>Gets the order this event belongs to — the event's own business data.</summary>
	/// <remarks>
	/// A domain event carries no aggregate id and no stream version. The aggregate id is a parameter
	/// to the event store; the version is assigned by the store at append time and read back on
	/// <c>StoredEvent.Version</c>. <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c> come from
	/// <c>DomainEvent</c>.
	/// </remarks>
	public required string OrderId { get; init; }

	public required string ProductId { get; init; }
	public required string ProductName { get; init; }
	public required int Quantity { get; init; }
	public required decimal UnitPrice { get; init; }
}

/// <summary>
/// Event raised when an order is submitted for processing.
/// </summary>
[MessageName("Contoso.Orders.OrderSubmittedEvent")]
public sealed record OrderSubmittedEvent : DomainEvent
{
	/// <summary>Gets the order this event belongs to — the event's own business data.</summary>
	/// <remarks>
	/// A domain event carries no aggregate id and no stream version. The aggregate id is a parameter
	/// to the event store; the version is assigned by the store at append time and read back on
	/// <c>StoredEvent.Version</c>. <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c> come from
	/// <c>DomainEvent</c>.
	/// </remarks>
	public required string OrderId { get; init; }

	public required DateTime SubmittedAt { get; init; }
}

/// <summary>
/// Event raised when an order is shipped.
/// </summary>
[MessageName("Contoso.Orders.OrderShippedEvent")]
public sealed record OrderShippedEvent : DomainEvent
{
	/// <summary>Gets the order this event belongs to — the event's own business data.</summary>
	/// <remarks>
	/// A domain event carries no aggregate id and no stream version. The aggregate id is a parameter
	/// to the event store; the version is assigned by the store at append time and read back on
	/// <c>StoredEvent.Version</c>. <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c> come from
	/// <c>DomainEvent</c>.
	/// </remarks>
	public required string OrderId { get; init; }

	public required string TrackingNumber { get; init; }
	public required string Carrier { get; init; }
	public required DateTime ShippedAt { get; init; }
}

/// <summary>
/// Event raised when an order is cancelled.
/// </summary>
[MessageName("Contoso.Orders.OrderCancelledEvent")]
public sealed record OrderCancelledEvent : DomainEvent
{
	/// <summary>Gets the order this event belongs to — the event's own business data.</summary>
	/// <remarks>
	/// A domain event carries no aggregate id and no stream version. The aggregate id is a parameter
	/// to the event store; the version is assigned by the store at append time and read back on
	/// <c>StoredEvent.Version</c>. <c>EventId</c>, <c>OccurredAt</c> and <c>Metadata</c> come from
	/// <c>DomainEvent</c>.
	/// </remarks>
	public required string OrderId { get; init; }

	public required string Reason { get; init; }
	public required DateTime CancelledAt { get; init; }
}
