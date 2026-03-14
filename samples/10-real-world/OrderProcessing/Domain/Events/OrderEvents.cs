// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace OrderProcessingSample.Domain.Events;

// ============================================================================
// Order Domain Events
// ============================================================================
// These events capture the state changes in the order lifecycle:
// Created -> Validated -> PaymentProcessed -> Shipped -> Completed
// Any step can fail, leading to: PaymentFailed or Cancelled states

/// <summary>
/// Raised when a new order is created.
/// </summary>
public sealed record OrderCreated(
	Guid OrderId,
	Guid CustomerId,
	IReadOnlyList<OrderLineItem> Items,
	string ShippingAddress) : DomainEvent
{
	/// <inheritdoc/>
	public override string AggregateId => OrderId.ToString();

	/// <summary>Gets the total order amount.</summary>
	public decimal TotalAmount { get; init; } = Items.Sum(i => i.UnitPrice * i.Quantity);
}

/// <summary>
/// Raised when order validation succeeds.
/// </summary>
public sealed record OrderValidated(Guid OrderId) : DomainEvent
{
	/// <inheritdoc/>
	public override string AggregateId => OrderId.ToString();
}

/// <summary>
/// Raised when order validation fails.
/// </summary>
public sealed record OrderValidationFailed(Guid OrderId, string Reason) : DomainEvent
{
	/// <inheritdoc/>
	public override string AggregateId => OrderId.ToString();
}

/// <summary>
/// Raised when payment is successfully processed.
/// </summary>
public sealed record PaymentProcessed(
	Guid OrderId,
	string TransactionId,
	decimal Amount) : DomainEvent
{
	/// <inheritdoc/>
	public override string AggregateId => OrderId.ToString();
}

/// <summary>
/// Raised when payment fails.
/// </summary>
public sealed record PaymentFailed(Guid OrderId, string Reason) : DomainEvent
{
	/// <inheritdoc/>
	public override string AggregateId => OrderId.ToString();
}

/// <summary>
/// Raised when the order is shipped.
/// </summary>
public sealed record OrderShipped(
	Guid OrderId,
	string TrackingNumber,
	string Carrier) : DomainEvent
{
	/// <inheritdoc/>
	public override string AggregateId => OrderId.ToString();
}

/// <summary>
/// Raised when the order is delivered and completed.
/// </summary>
public sealed record OrderCompleted(Guid OrderId) : DomainEvent
{
	/// <inheritdoc/>
	public override string AggregateId => OrderId.ToString();
}

/// <summary>
/// Raised when an order is cancelled.
/// </summary>
public sealed record OrderCancelled(Guid OrderId, string Reason) : DomainEvent
{
	/// <inheritdoc/>
	public override string AggregateId => OrderId.ToString();
}

/// <summary>
/// Represents an item in an order.
/// </summary>
public sealed record OrderLineItem(
	Guid ProductId,
	string ProductName,
	int Quantity,
	decimal UnitPrice);
