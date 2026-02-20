// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace CdcEventStoreElasticsearch.Domain;

/// <summary>
/// Event raised when an order is created.
/// </summary>
public sealed record OrderCreated : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OrderCreated"/> class.
	/// </summary>
	public OrderCreated(
		Guid orderId,
		string externalOrderId,
		Guid customerId,
		string customerExternalId,
		DateTime orderDate,
		long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		ExternalOrderId = externalOrderId;
		CustomerId = customerId;
		CustomerExternalId = customerExternalId;
		OrderDate = orderDate;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the external order ID from the legacy system.</summary>
	public string ExternalOrderId { get; init; }

	/// <summary>Gets the customer identifier.</summary>
	public Guid CustomerId { get; init; }

	/// <summary>Gets the customer's external ID.</summary>
	public string CustomerExternalId { get; init; }

	/// <summary>Gets the order date.</summary>
	public DateTime OrderDate { get; init; }
}

/// <summary>
/// Event raised when a line item is added to an order.
/// </summary>
public sealed record OrderLineItemAdded : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OrderLineItemAdded"/> class.
	/// </summary>
	public OrderLineItemAdded(
		Guid orderId,
		Guid itemId,
		string externalItemId,
		string productName,
		int quantity,
		decimal unitPrice,
		long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		ItemId = itemId;
		ExternalItemId = externalItemId;
		ProductName = productName;
		Quantity = quantity;
		UnitPrice = unitPrice;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the line item identifier.</summary>
	public Guid ItemId { get; init; }

	/// <summary>Gets the external item ID from the legacy system.</summary>
	public string ExternalItemId { get; init; }

	/// <summary>Gets the product name.</summary>
	public string ProductName { get; init; }

	/// <summary>Gets the quantity ordered.</summary>
	public int Quantity { get; init; }

	/// <summary>Gets the unit price.</summary>
	public decimal UnitPrice { get; init; }

	/// <summary>Gets the line total.</summary>
	public decimal LineTotal => Quantity * UnitPrice;
}

/// <summary>
/// Event raised when a line item quantity is updated.
/// </summary>
public sealed record OrderLineItemUpdated : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OrderLineItemUpdated"/> class.
	/// </summary>
	public OrderLineItemUpdated(
		Guid orderId,
		Guid itemId,
		int oldQuantity,
		int newQuantity,
		long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		ItemId = itemId;
		OldQuantity = oldQuantity;
		NewQuantity = newQuantity;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the line item identifier.</summary>
	public Guid ItemId { get; init; }

	/// <summary>Gets the old quantity.</summary>
	public int OldQuantity { get; init; }

	/// <summary>Gets the new quantity.</summary>
	public int NewQuantity { get; init; }
}

/// <summary>
/// Event raised when a line item is removed from an order.
/// </summary>
public sealed record OrderLineItemRemoved : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OrderLineItemRemoved"/> class.
	/// </summary>
	public OrderLineItemRemoved(
		Guid orderId,
		Guid itemId,
		string productName,
		int quantity,
		decimal unitPrice,
		long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		ItemId = itemId;
		ProductName = productName;
		Quantity = quantity;
		UnitPrice = unitPrice;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the line item identifier.</summary>
	public Guid ItemId { get; init; }

	/// <summary>Gets the removed product name.</summary>
	public string ProductName { get; init; }

	/// <summary>Gets the removed quantity.</summary>
	public int Quantity { get; init; }

	/// <summary>Gets the unit price of the removed item.</summary>
	public decimal UnitPrice { get; init; }
}

/// <summary>
/// Event raised when an order status is updated.
/// </summary>
public sealed record OrderStatusUpdated : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OrderStatusUpdated"/> class.
	/// </summary>
	public OrderStatusUpdated(
		Guid orderId,
		OrderStatus oldStatus,
		OrderStatus newStatus,
		long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		OldStatus = oldStatus;
		NewStatus = newStatus;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the old status.</summary>
	public OrderStatus OldStatus { get; init; }

	/// <summary>Gets the new status.</summary>
	public OrderStatus NewStatus { get; init; }
}

/// <summary>
/// Event raised when an order is shipped.
/// </summary>
public sealed record OrderShipped : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OrderShipped"/> class.
	/// </summary>
	public OrderShipped(Guid orderId, DateTime shippedDate, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		ShippedDate = shippedDate;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the shipped date.</summary>
	public DateTime ShippedDate { get; init; }
}

/// <summary>
/// Event raised when an order is delivered.
/// </summary>
public sealed record OrderDelivered : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OrderDelivered"/> class.
	/// </summary>
	public OrderDelivered(Guid orderId, DateTime deliveredDate, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		DeliveredDate = deliveredDate;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the delivered date.</summary>
	public DateTime DeliveredDate { get; init; }
}

/// <summary>
/// Event raised when an order is cancelled.
/// </summary>
public sealed record OrderCancelled : DomainEvent
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OrderCancelled"/> class.
	/// </summary>
	public OrderCancelled(Guid orderId, string reason, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		Reason = reason;
	}

	/// <summary>Gets the order identifier.</summary>
	public Guid OrderId { get; init; }

	/// <summary>Gets the cancellation reason.</summary>
	public string Reason { get; init; }
}
