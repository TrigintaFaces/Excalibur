// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;

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
[MessageName("Contoso.Orders.OrderCreated")]
public sealed record OrderCreated(
	Guid OrderId,
	Guid CustomerId,
	IReadOnlyList<OrderLineItem> Items,
	string ShippingAddress) : DomainEvent
{
	/// <summary>Gets the total order amount.</summary>
	public decimal TotalAmount { get; init; } = Items.Sum(i => i.UnitPrice * i.Quantity);
}

/// <summary>
/// Raised when order validation succeeds.
/// </summary>
[MessageName("Contoso.Orders.OrderValidated")]
public sealed record OrderValidated(Guid OrderId) : DomainEvent;

/// <summary>
/// Raised when order validation fails.
/// </summary>
[MessageName("Contoso.Orders.OrderValidationFailed")]
public sealed record OrderValidationFailed(Guid OrderId, string Reason) : DomainEvent;

/// <summary>
/// Raised when payment is successfully processed.
/// </summary>
[MessageName("Contoso.Payments.PaymentProcessed")]
public sealed record PaymentProcessed(
	Guid OrderId,
	string TransactionId,
	decimal Amount) : DomainEvent;

/// <summary>
/// Raised when payment fails.
/// </summary>
[MessageName("Contoso.Payments.PaymentFailed")]
public sealed record PaymentFailed(Guid OrderId, string Reason) : DomainEvent;

/// <summary>
/// Raised when the order is shipped.
/// </summary>
[MessageName("Contoso.Orders.OrderShipped")]
public sealed record OrderShipped(
	Guid OrderId,
	string TrackingNumber,
	string Carrier) : DomainEvent;

/// <summary>
/// Raised when the order is delivered and completed.
/// </summary>
[MessageName("Contoso.Orders.OrderCompleted")]
public sealed record OrderCompleted(Guid OrderId) : DomainEvent;

/// <summary>
/// Raised when an order is cancelled.
/// </summary>
[MessageName("Contoso.Orders.OrderCancelled")]
public sealed record OrderCancelled(Guid OrderId, string Reason) : DomainEvent;

/// <summary>
/// Represents an item in an order.
/// </summary>
public sealed record OrderLineItem(
	Guid ProductId,
	string ProductName,
	int Quantity,
	decimal UnitPrice);
