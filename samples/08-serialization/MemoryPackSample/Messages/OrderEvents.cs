// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

using MemoryPack;

namespace MemoryPackSample.Messages;

/// <summary>
/// Event published when an order is placed.
/// Demonstrates MemoryPack serialization with zero-allocation performance.
/// </summary>
/// <remarks>
/// <para>
/// The [MemoryPackable] attribute marks this type for MemoryPack source generation.
/// The class MUST be partial for the source generator to emit serialization code.
/// </para>
/// <para>
/// MemoryPack features:
/// - Zero-allocation deserialization via ReadOnlySpan
/// - Automatic property ordering (or explicit via [MemoryPackOrder])
/// - NativeAOT/trimming compatible
/// - Fastest .NET binary serializer
/// </para>
/// </remarks>
[MemoryPackable]
public partial class OrderPlacedEvent : IDispatchEvent
{
	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	public Guid EventId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the unique order identifier.
	/// </summary>
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the customer identifier.
	/// </summary>
	public string CustomerId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the order items.
	/// </summary>
	public List<OrderItem> Items { get; set; } = [];

	/// <summary>
	/// Gets or sets the total order amount.
	/// </summary>
	public decimal TotalAmount { get; set; }

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents an item in an order.
/// </summary>
[MemoryPackable]
public partial class OrderItem
{
	/// <summary>
	/// Gets or sets the product SKU.
	/// </summary>
	public string ProductSku { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the product name.
	/// </summary>
	public string ProductName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the quantity ordered.
	/// </summary>
	public int Quantity { get; set; }

	/// <summary>
	/// Gets or sets the unit price.
	/// </summary>
	public decimal UnitPrice { get; set; }
}

/// <summary>
/// Event published when an order is cancelled.
/// </summary>
[MemoryPackable]
public partial class OrderCancelledEvent : IDispatchEvent
{
	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	public Guid EventId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the order identifier.
	/// </summary>
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the cancellation reason.
	/// </summary>
	public string Reason { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets who cancelled the order.
	/// </summary>
	public string CancelledBy { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event published when an order is shipped.
/// </summary>
[MemoryPackable]
public partial class OrderShippedEvent : IDispatchEvent
{
	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	public Guid EventId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the order identifier.
	/// </summary>
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the tracking number.
	/// </summary>
	public string TrackingNumber { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the carrier name.
	/// </summary>
	public string Carrier { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the estimated delivery date.
	/// </summary>
	public DateTimeOffset EstimatedDelivery { get; set; }

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Immutable event demonstrating constructor-based deserialization.
/// </summary>
/// <remarks>
/// Use [MemoryPackConstructor] to specify which constructor MemoryPack
/// should use for deserialization of immutable types.
/// </remarks>
[MemoryPackable]
public partial class OrderCompletedEvent : IDispatchEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OrderCompletedEvent"/> class.
	/// </summary>
	[MemoryPackConstructor]
	public OrderCompletedEvent(Guid eventId, string orderId, DateTimeOffset completedAt, decimal finalAmount)
	{
		EventId = eventId;
		OrderId = orderId;
		CompletedAt = completedAt;
		FinalAmount = finalAmount;
	}

	/// <summary>
	/// Gets the unique event identifier.
	/// </summary>
	public Guid EventId { get; }

	/// <summary>
	/// Gets the order identifier.
	/// </summary>
	public string OrderId { get; }

	/// <summary>
	/// Gets when the order was completed.
	/// </summary>
	public DateTimeOffset CompletedAt { get; }

	/// <summary>
	/// Gets the final order amount.
	/// </summary>
	public decimal FinalAmount { get; }
}
