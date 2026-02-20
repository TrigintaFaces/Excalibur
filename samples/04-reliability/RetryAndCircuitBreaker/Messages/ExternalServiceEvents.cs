// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace RetryAndCircuitBreaker.Messages;

/// <summary>
/// Command to call an external payment service.
/// Used to demonstrate retry with exponential backoff.
/// </summary>
public sealed class ProcessPaymentCommand : IDispatchAction
{
	/// <summary>
	/// Gets or sets the payment identifier.
	/// </summary>
	public string PaymentId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the amount to charge.
	/// </summary>
	public decimal Amount { get; set; }

	/// <summary>
	/// Gets or sets the customer identifier.
	/// </summary>
	public string CustomerId { get; set; } = string.Empty;
}

/// <summary>
/// Event published when payment succeeds.
/// </summary>
public sealed class PaymentSucceededEvent : IDispatchEvent
{
	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	public Guid EventId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the payment identifier.
	/// </summary>
	public string PaymentId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the transaction ID from the payment provider.
	/// </summary>
	public string TransactionId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Command to call an external inventory service.
/// Used to demonstrate circuit breaker pattern.
/// </summary>
public sealed class CheckInventoryCommand : IDispatchAction
{
	/// <summary>
	/// Gets or sets the product SKU to check.
	/// </summary>
	public string ProductSku { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the requested quantity.
	/// </summary>
	public int RequestedQuantity { get; set; }
}

/// <summary>
/// Event published when inventory check succeeds.
/// </summary>
public sealed class InventoryCheckedEvent : IDispatchEvent
{
	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	public Guid EventId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the product SKU.
	/// </summary>
	public string ProductSku { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets whether inventory is available.
	/// </summary>
	public bool IsAvailable { get; set; }

	/// <summary>
	/// Gets or sets the available quantity.
	/// </summary>
	public int AvailableQuantity { get; set; }

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Command to send a notification.
/// Used to demonstrate bulkhead isolation.
/// </summary>
public sealed class SendNotificationCommand : IDispatchAction
{
	/// <summary>
	/// Gets or sets the notification type.
	/// </summary>
	public string NotificationType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the recipient.
	/// </summary>
	public string Recipient { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the message content.
	/// </summary>
	public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Event published when notification is sent.
/// </summary>
public sealed class NotificationSentEvent : IDispatchEvent
{
	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	public Guid EventId { get; set; } = Guid.NewGuid();

	/// <summary>
	/// Gets or sets the notification type.
	/// </summary>
	public string NotificationType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the recipient.
	/// </summary>
	public string Recipient { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the event occurred.
	/// </summary>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
