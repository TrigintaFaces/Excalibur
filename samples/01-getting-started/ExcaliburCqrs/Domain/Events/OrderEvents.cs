// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace ExcaliburCqrs.Domain.Events;

/// <summary>
/// Event raised when a new order is created.
/// </summary>
public sealed record OrderCreated : DomainEvent
{
	public OrderCreated(Guid orderId, string productId, int quantity, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		ProductId = productId;
		Quantity = quantity;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the product identifier.</summary>
	public string ProductId { get; init; }

	/// <summary>Gets the quantity ordered.</summary>
	public int Quantity { get; init; }
}

/// <summary>
/// Event raised when an item is added to an existing order.
/// </summary>
public sealed record OrderItemAdded : DomainEvent
{
	public OrderItemAdded(Guid orderId, string productId, int quantity, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		ProductId = productId;
		Quantity = quantity;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the product identifier added.</summary>
	public string ProductId { get; init; }

	/// <summary>Gets the quantity added.</summary>
	public int Quantity { get; init; }
}

/// <summary>
/// Event raised when an order is confirmed.
/// </summary>
public sealed record OrderConfirmed : DomainEvent
{
	public OrderConfirmed(Guid orderId, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		ConfirmedAt = DateTime.UtcNow;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets when the order was confirmed.</summary>
	public DateTime ConfirmedAt { get; init; }
}

/// <summary>
/// Event raised when an order is shipped.
/// </summary>
public sealed record OrderShipped : DomainEvent
{
	public OrderShipped(Guid orderId, string trackingNumber, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		TrackingNumber = trackingNumber;
		ShippedAt = DateTime.UtcNow;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the shipping tracking number.</summary>
	public string TrackingNumber { get; init; }

	/// <summary>Gets when the order was shipped.</summary>
	public DateTime ShippedAt { get; init; }
}
