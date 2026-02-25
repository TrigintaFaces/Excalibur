// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace OutboxPattern.Messages;

/// <summary>
/// Event published when an order is placed.
/// This event will be stored in the outbox for reliable delivery.
/// </summary>
public sealed class OrderPlacedEvent : IDispatchEvent
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
	/// Gets or sets the total order amount.
	/// </summary>
	public decimal TotalAmount { get; set; }

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event published when payment is processed.
/// Demonstrates chained events in outbox.
/// </summary>
public sealed class PaymentProcessedEvent : IDispatchEvent
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
	/// Gets or sets the payment transaction ID.
	/// </summary>
	public string TransactionId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the payment amount.
	/// </summary>
	public decimal Amount { get; set; }

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event published when inventory is reserved.
/// Demonstrates outbox deduplication.
/// </summary>
public sealed class InventoryReservedEvent : IDispatchEvent
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
	/// Gets or sets the SKU of the reserved product.
	/// </summary>
	public string ProductSku { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the quantity reserved.
	/// </summary>
	public int Quantity { get; set; }

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event published when an order fails.
/// Demonstrates retry handling in outbox.
/// </summary>
public sealed class OrderFailedEvent : IDispatchEvent
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
	/// Gets or sets the failure reason.
	/// </summary>
	public string Reason { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
