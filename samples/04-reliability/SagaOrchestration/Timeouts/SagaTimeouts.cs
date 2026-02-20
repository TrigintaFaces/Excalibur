// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace SagaOrchestration.Timeouts;

/// <summary>
/// Timeout marker for inventory reservation expiration.
/// </summary>
/// <remarks>
/// <para>
/// This timeout is scheduled when inventory is reserved to ensure we don't hold
/// inventory indefinitely. If payment is not processed within the timeout period,
/// the saga will be triggered to release the reservation.
/// </para>
/// <para>
/// Usage pattern:
/// <code>
/// await saga.RequestTimeoutAsync&lt;InventoryReservationTimeout&gt;(
///     sagaId,
///     TimeSpan.FromMinutes(5),
///     cancellationToken);
/// </code>
/// </para>
/// </remarks>
public sealed class InventoryReservationTimeout
{
	/// <summary>
	/// Gets or sets the saga ID this timeout belongs to.
	/// </summary>
	public string SagaId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the reservation ID to release.
	/// </summary>
	public string? ReservationId { get; set; }
}

/// <summary>
/// Timeout marker for payment confirmation.
/// </summary>
/// <remarks>
/// <para>
/// This timeout is scheduled after initiating payment to ensure we receive
/// confirmation within a reasonable time. Useful for async payment gateways.
/// </para>
/// <para>
/// If payment confirmation is not received within the timeout period,
/// the saga can decide to retry, escalate, or compensate.
/// </para>
/// </remarks>
public sealed class PaymentConfirmationTimeout
{
	/// <summary>
	/// Gets or sets the saga ID this timeout belongs to.
	/// </summary>
	public string SagaId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the payment transaction ID awaiting confirmation.
	/// </summary>
	public string? TransactionId { get; set; }
}

/// <summary>
/// Timeout marker for shipment confirmation.
/// </summary>
/// <remarks>
/// <para>
/// This timeout is scheduled after shipment to detect potential shipping issues.
/// If shipment tracking doesn't update within the expected timeframe,
/// the saga can trigger investigation or customer notification.
/// </para>
/// </remarks>
public sealed class ShipmentConfirmationTimeout
{
	/// <summary>
	/// Gets or sets the saga ID this timeout belongs to.
	/// </summary>
	public string SagaId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the shipment tracking number.
	/// </summary>
	public string? TrackingNumber { get; set; }
}

/// <summary>
/// Timeout entry stored in the timeout store.
/// </summary>
public sealed class TimeoutEntry
{
	/// <summary>
	/// Gets or sets the unique timeout identifier.
	/// </summary>
	public string Id { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the saga ID this timeout belongs to.
	/// </summary>
	public string SagaId { get; init; } = string.Empty;

	/// <summary>
	/// Gets or sets the timeout marker type.
	/// </summary>
	public Type TimeoutType { get; init; } = typeof(object);

	/// <summary>
	/// Gets or sets when the timeout is due.
	/// </summary>
	public DateTimeOffset DueAt { get; init; }

	/// <summary>
	/// Gets or sets the current timeout status.
	/// </summary>
	public TimeoutStatus Status { get; set; } = TimeoutStatus.Pending;

	/// <summary>
	/// Gets or sets optional data associated with the timeout.
	/// </summary>
	public object? Data { get; init; }
}

/// <summary>
/// Timeout status enumeration.
/// </summary>
public enum TimeoutStatus
{
	/// <summary>
	/// Timeout is pending delivery.
	/// </summary>
	Pending,

	/// <summary>
	/// Timeout was delivered.
	/// </summary>
	Delivered,

	/// <summary>
	/// Timeout was cancelled.
	/// </summary>
	Cancelled,
}
