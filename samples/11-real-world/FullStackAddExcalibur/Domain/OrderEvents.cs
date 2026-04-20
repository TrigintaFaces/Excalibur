// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace FullStackAddExcalibur.Domain;

/// <summary>
/// Domain event raised when a new order is created.
/// </summary>
public sealed record OrderCreated(
	Guid OrderId,
	string ExternalOrderId,
	Guid CustomerId,
	string CustomerExternalId,
	DateTime OrderDate) : DomainEvent;

/// <summary>
/// Domain event raised when a line item is added to an order.
/// </summary>
public sealed record OrderLineItemAdded(
	Guid OrderId,
	Guid ItemId,
	string ProductName,
	int Quantity,
	decimal UnitPrice) : DomainEvent;

/// <summary>
/// Domain event raised when an order is shipped.
/// </summary>
public sealed record OrderShipped(
	Guid OrderId,
	DateTime ShippedDate) : DomainEvent;

/// <summary>
/// Domain event raised when an order is cancelled.
/// </summary>
public sealed record OrderCancelled(
	Guid OrderId,
	string Reason) : DomainEvent;

/// <summary>
/// Order lifecycle status.
/// </summary>
public enum OrderStatus
{
	/// <summary>Order is pending confirmation.</summary>
	Pending = 0,

	/// <summary>Order has been shipped.</summary>
	Shipped = 1,

	/// <summary>Order has been cancelled.</summary>
	Cancelled = 2
}
