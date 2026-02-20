// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace SagaOrchestration.Sagas;

/// <summary>
/// Data carried through the order fulfillment saga.
/// </summary>
/// <remarks>
/// This represents the state that persists across all saga steps.
/// Each step can read and modify this data, and it's persisted after each step.
/// </remarks>
public sealed class OrderSagaData
{
	/// <summary>
	/// Gets or sets the unique saga identifier.
	/// </summary>
	public string SagaId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the order identifier.
	/// </summary>
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the customer identifier.
	/// </summary>
	public string CustomerId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the order total amount.
	/// </summary>
	public decimal TotalAmount { get; set; }

	/// <summary>
	/// Gets or sets the reserved inventory SKU.
	/// </summary>
	public string? InventorySku { get; set; }

	/// <summary>
	/// Gets or sets the inventory reservation ID (for compensation).
	/// </summary>
	public string? ReservationId { get; set; }

	/// <summary>
	/// Gets or sets the payment transaction ID.
	/// </summary>
	public string? PaymentTransactionId { get; set; }

	/// <summary>
	/// Gets or sets the shipment tracking number.
	/// </summary>
	public string? ShipmentTrackingNumber { get; set; }

	/// <summary>
	/// Gets or sets the current saga status.
	/// </summary>
	public SagaStatus Status { get; set; } = SagaStatus.Created;

	/// <summary>
	/// Gets or sets the list of completed steps (for LIFO compensation).
	/// </summary>
	public List<string> CompletedSteps { get; init; } = new();

	/// <summary>
	/// Gets or sets the saga version for optimistic concurrency.
	/// </summary>
	public int Version { get; set; } = 1;

	/// <summary>
	/// Gets or sets the timestamp when the saga was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the timestamp when the saga was last updated.
	/// </summary>
	public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the failure reason if the saga failed.
	/// </summary>
	public string? FailureReason { get; set; }
}

/// <summary>
/// Saga status enumeration.
/// </summary>
public enum SagaStatus
{
	/// <summary>
	/// Saga has been created but not started.
	/// </summary>
	Created,

	/// <summary>
	/// Saga is actively running.
	/// </summary>
	Running,

	/// <summary>
	/// Saga completed successfully.
	/// </summary>
	Completed,

	/// <summary>
	/// Saga is compensating (rolling back).
	/// </summary>
	Compensating,

	/// <summary>
	/// Saga compensation completed successfully.
	/// </summary>
	Compensated,

	/// <summary>
	/// Saga compensation partially completed (some compensations failed).
	/// </summary>
	PartiallyCompensated,

	/// <summary>
	/// Saga failed and cannot be recovered.
	/// </summary>
	Failed,
}
