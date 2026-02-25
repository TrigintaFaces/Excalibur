// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

using SagaOrchestration.Sagas;

namespace SagaOrchestration.Steps;

/// <summary>
/// Saga step that reserves inventory for the order.
/// </summary>
/// <remarks>
/// <para>
/// This step contacts the inventory service to reserve stock for the order.
/// If successful, it stores the reservation ID in the saga data for later
/// use during compensation if needed.
/// </para>
/// <para>
/// Compensation releases the reservation, making the stock available again.
/// </para>
/// </remarks>
public sealed partial class ReserveInventoryStep : ISagaStep
{
	private readonly ILogger<ReserveInventoryStep> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ReserveInventoryStep"/> class.
	/// </summary>
	public ReserveInventoryStep(ILogger<ReserveInventoryStep> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc/>
	public string Name => "ReserveInventory";

	/// <inheritdoc/>
	public async Task<bool> ExecuteAsync(OrderSagaData data, CancellationToken cancellationToken)
	{
		LogReservingInventory(_logger, data.OrderId, data.InventorySku ?? "UNKNOWN");

		// Simulate inventory service call
		await Task.Delay(50, cancellationToken).ConfigureAwait(false);

		// Generate reservation ID (in real implementation, this comes from inventory service)
		data.ReservationId = $"RES-{Guid.NewGuid():N}";

		LogInventoryReserved(_logger, data.OrderId, data.ReservationId);

		return true;
	}

	/// <inheritdoc/>
	public async Task<bool> CompensateAsync(OrderSagaData data, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(data.ReservationId))
		{
			LogNoReservationToRelease(_logger, data.OrderId);
			return true; // Nothing to compensate
		}

		LogReleasingReservation(_logger, data.OrderId, data.ReservationId);

		// Simulate inventory service call to release reservation
		await Task.Delay(30, cancellationToken).ConfigureAwait(false);

		LogReservationReleased(_logger, data.OrderId, data.ReservationId);

		return true;
	}

	[LoggerMessage(Level = LogLevel.Information, Message = "Reserving inventory for order {OrderId}, SKU: {Sku}")]
	private static partial void LogReservingInventory(ILogger logger, string orderId, string sku);

	[LoggerMessage(Level = LogLevel.Information, Message = "Inventory reserved for order {OrderId}, ReservationId: {ReservationId}")]
	private static partial void LogInventoryReserved(ILogger logger, string orderId, string reservationId);

	[LoggerMessage(Level = LogLevel.Debug, Message = "No reservation to release for order {OrderId}")]
	private static partial void LogNoReservationToRelease(ILogger logger, string orderId);

	[LoggerMessage(Level = LogLevel.Information,
		Message = "Releasing inventory reservation for order {OrderId}, ReservationId: {ReservationId}")]
	private static partial void LogReleasingReservation(ILogger logger, string orderId, string reservationId);

	[LoggerMessage(Level = LogLevel.Information,
		Message = "Inventory reservation released for order {OrderId}, ReservationId: {ReservationId}")]
	private static partial void LogReservationReleased(ILogger logger, string orderId, string reservationId);
}
