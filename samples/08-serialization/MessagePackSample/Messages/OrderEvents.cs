// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

using MessagePack;

namespace MessagePackSample.Messages;

/// <summary>
/// Base interface for order events. Demonstrates MessagePack Union types for polymorphism.
/// </summary>
/// <remarks>
/// The [Union] attributes enable polymorphic serialization. Each derived type
/// gets a unique key (0, 1, 2, etc.) that identifies it in the wire format.
/// </remarks>
[Union(0, typeof(OrderPlacedEvent))]
[Union(1, typeof(OrderCancelledEvent))]
[Union(2, typeof(OrderShippedEvent))]
public interface IOrderEvent : IDispatchEvent
{
	/// <summary>
	/// Gets the order identifier.
	/// </summary>
	string OrderId { get; }
}

/// <summary>
/// Event published when an order is placed.
/// Demonstrates MessagePack object serialization with LZ4 compression support.
/// </summary>
/// <remarks>
/// <para>
/// The [MessagePackObject] attribute marks this type for MessagePack serialization.
/// Each property requires a [Key(n)] attribute with a unique index.
/// </para>
/// <para>
/// Key indices:
/// - Must be unique within the type (0 through int.MaxValue)
/// - Lower indices are more efficient
/// - Once assigned, indices should never change (for backwards compatibility)
/// </para>
/// </remarks>
[MessagePackObject]
public sealed class OrderPlacedEvent : IOrderEvent
{
	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	[Key(0)]
	public Guid EventId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the unique order identifier.
	/// </summary>
	[Key(1)]
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the customer identifier.
	/// </summary>
	[Key(2)]
	public string CustomerId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the order items.
	/// </summary>
	[Key(3)]
	public List<OrderItem> Items { get; set; } = [];

	/// <summary>
	/// Gets or sets the total order amount.
	/// </summary>
	[Key(4)]
	public decimal TotalAmount { get; set; }

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	[Key(5)]
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents an item in an order.
/// </summary>
[MessagePackObject]
public sealed class OrderItem
{
	/// <summary>
	/// Gets or sets the product SKU.
	/// </summary>
	[Key(0)]
	public string ProductSku { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the product name.
	/// </summary>
	[Key(1)]
	public string ProductName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the quantity ordered.
	/// </summary>
	[Key(2)]
	public int Quantity { get; set; }

	/// <summary>
	/// Gets or sets the unit price.
	/// </summary>
	[Key(3)]
	public decimal UnitPrice { get; set; }
}

/// <summary>
/// Event published when an order is cancelled.
/// </summary>
[MessagePackObject]
public sealed class OrderCancelledEvent : IOrderEvent
{
	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	[Key(0)]
	public Guid EventId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the order identifier.
	/// </summary>
	[Key(1)]
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the cancellation reason.
	/// </summary>
	[Key(2)]
	public string Reason { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets who cancelled the order.
	/// </summary>
	[Key(3)]
	public string CancelledBy { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	[Key(4)]
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event published when an order is shipped.
/// </summary>
[MessagePackObject]
public sealed class OrderShippedEvent : IOrderEvent
{
	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	[Key(0)]
	public Guid EventId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the order identifier.
	/// </summary>
	[Key(1)]
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the tracking number.
	/// </summary>
	[Key(2)]
	public string TrackingNumber { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the carrier name.
	/// </summary>
	[Key(3)]
	public string Carrier { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the estimated delivery date.
	/// </summary>
	[Key(4)]
	public DateTimeOffset EstimatedDelivery { get; set; }

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	[Key(5)]
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
