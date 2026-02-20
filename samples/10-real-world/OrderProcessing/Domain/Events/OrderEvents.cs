// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace OrderProcessingSample.Domain.Events;

// ============================================================================
// Order Domain Events
// ============================================================================
// These events capture the state changes in the order lifecycle:
// Created → Validated → PaymentProcessed → Shipped → Completed
// Any step can fail, leading to: PaymentFailed or Cancelled states

/// <summary>
/// Raised when a new order is created.
/// </summary>
public sealed record OrderCreated : DomainEvent
{
	public OrderCreated(
		Guid orderId,
		Guid customerId,
		IReadOnlyList<OrderLineItem> items,
		string shippingAddress,
		long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		CustomerId = customerId;
		Items = items;
		ShippingAddress = shippingAddress;
		TotalAmount = items.Sum(i => i.UnitPrice * i.Quantity);
	}

	public Guid OrderId { get; init; }
	public Guid CustomerId { get; init; }
	public IReadOnlyList<OrderLineItem> Items { get; init; }
	public string ShippingAddress { get; init; }
	public decimal TotalAmount { get; init; }
}

/// <summary>
/// Raised when order validation succeeds.
/// </summary>
public sealed record OrderValidated : DomainEvent
{
	public OrderValidated(Guid orderId, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
	}

	public Guid OrderId { get; init; }
}

/// <summary>
/// Raised when order validation fails.
/// </summary>
public sealed record OrderValidationFailed : DomainEvent
{
	public OrderValidationFailed(Guid orderId, string reason, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		Reason = reason;
	}

	public Guid OrderId { get; init; }
	public string Reason { get; init; }
}

/// <summary>
/// Raised when payment is successfully processed.
/// </summary>
public sealed record PaymentProcessed : DomainEvent
{
	public PaymentProcessed(
		Guid orderId,
		string transactionId,
		decimal amount,
		long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		TransactionId = transactionId;
		Amount = amount;
	}

	public Guid OrderId { get; init; }
	public string TransactionId { get; init; }
	public decimal Amount { get; init; }
}

/// <summary>
/// Raised when payment fails.
/// </summary>
public sealed record PaymentFailed : DomainEvent
{
	public PaymentFailed(Guid orderId, string reason, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		Reason = reason;
	}

	public Guid OrderId { get; init; }
	public string Reason { get; init; }
}

/// <summary>
/// Raised when the order is shipped.
/// </summary>
public sealed record OrderShipped : DomainEvent
{
	public OrderShipped(
		Guid orderId,
		string trackingNumber,
		string carrier,
		long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		TrackingNumber = trackingNumber;
		Carrier = carrier;
	}

	public Guid OrderId { get; init; }
	public string TrackingNumber { get; init; }
	public string Carrier { get; init; }
}

/// <summary>
/// Raised when the order is delivered and completed.
/// </summary>
public sealed record OrderCompleted : DomainEvent
{
	public OrderCompleted(Guid orderId, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
	}

	public Guid OrderId { get; init; }
}

/// <summary>
/// Raised when an order is cancelled.
/// </summary>
public sealed record OrderCancelled : DomainEvent
{
	public OrderCancelled(Guid orderId, string reason, long version)
		: base(orderId.ToString(), version)
	{
		OrderId = orderId;
		Reason = reason;
	}

	public Guid OrderId { get; init; }
	public string Reason { get; init; }
}

/// <summary>
/// Represents an item in an order.
/// </summary>
public sealed record OrderLineItem(
	Guid ProductId,
	string ProductName,
	int Quantity,
	decimal UnitPrice);
