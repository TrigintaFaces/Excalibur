// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace CdcEventStoreElasticsearch.Domain;

/// <summary>
/// Event raised when an order is created.
/// </summary>
public sealed record OrderCreated(
	Guid OrderId,
	string ExternalOrderId,
	Guid CustomerId,
	string CustomerExternalId,
	DateTime OrderDate) : DomainEvent;

/// <summary>
/// Event raised when a line item is added to an order.
/// </summary>
public sealed record OrderLineItemAdded(
	Guid OrderId,
	Guid ItemId,
	string ExternalItemId,
	string ProductName,
	int Quantity,
	decimal UnitPrice) : DomainEvent
{
	/// <summary>Gets the line total.</summary>
	public decimal LineTotal => Quantity * UnitPrice;
}

/// <summary>
/// Event raised when a line item quantity is updated.
/// </summary>
public sealed record OrderLineItemUpdated(
	Guid OrderId,
	Guid ItemId,
	int OldQuantity,
	int NewQuantity) : DomainEvent;

/// <summary>
/// Event raised when a line item is removed from an order.
/// </summary>
public sealed record OrderLineItemRemoved(
	Guid OrderId,
	Guid ItemId,
	string ProductName,
	int Quantity,
	decimal UnitPrice) : DomainEvent;

/// <summary>
/// Event raised when an order status is updated.
/// </summary>
public sealed record OrderStatusUpdated(
	Guid OrderId,
	OrderStatus OldStatus,
	OrderStatus NewStatus) : DomainEvent;

/// <summary>
/// Event raised when an order is shipped.
/// </summary>
public sealed record OrderShipped(Guid OrderId, DateTime ShippedDate) : DomainEvent;

/// <summary>
/// Event raised when an order is delivered.
/// </summary>
public sealed record OrderDelivered(Guid OrderId, DateTime DeliveredDate) : DomainEvent;

/// <summary>
/// Event raised when an order is cancelled.
/// </summary>
public sealed record OrderCancelled(Guid OrderId, string Reason) : DomainEvent;
