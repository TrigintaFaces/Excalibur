// Order Domain Events

using Excalibur.Dispatch.Abstractions;

namespace MultiProviderQueueProcessor.Events;

/// <summary>
/// Event raised when an order is created.
/// </summary>
public sealed record OrderCreatedEvent : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = string.Empty;
	public long Version { get; init; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(OrderCreatedEvent);
	public IDictionary<string, object>? Metadata { get; init; }

	public required string CustomerId { get; init; }
	public required decimal TotalAmount { get; init; }
	public required string Currency { get; init; }
}

/// <summary>
/// Event raised when an item is added to an order.
/// </summary>
public sealed record OrderItemAddedEvent : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = string.Empty;
	public long Version { get; init; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(OrderItemAddedEvent);
	public IDictionary<string, object>? Metadata { get; init; }

	public required string ProductId { get; init; }
	public required string ProductName { get; init; }
	public required int Quantity { get; init; }
	public required decimal UnitPrice { get; init; }
}

/// <summary>
/// Event raised when an order is submitted for processing.
/// </summary>
public sealed record OrderSubmittedEvent : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = string.Empty;
	public long Version { get; init; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(OrderSubmittedEvent);
	public IDictionary<string, object>? Metadata { get; init; }

	public required DateTime SubmittedAt { get; init; }
}

/// <summary>
/// Event raised when an order is shipped.
/// </summary>
public sealed record OrderShippedEvent : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = string.Empty;
	public long Version { get; init; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(OrderShippedEvent);
	public IDictionary<string, object>? Metadata { get; init; }

	public required string TrackingNumber { get; init; }
	public required string Carrier { get; init; }
	public required DateTime ShippedAt { get; init; }
}

/// <summary>
/// Event raised when an order is cancelled.
/// </summary>
public sealed record OrderCancelledEvent : IDomainEvent
{
	public string EventId { get; init; } = Guid.NewGuid().ToString();
	public string AggregateId { get; init; } = string.Empty;
	public long Version { get; init; }
	public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
	public string EventType => nameof(OrderCancelledEvent);
	public IDictionary<string, object>? Metadata { get; init; }

	public required string Reason { get; init; }
	public required DateTime CancelledAt { get; init; }
}
