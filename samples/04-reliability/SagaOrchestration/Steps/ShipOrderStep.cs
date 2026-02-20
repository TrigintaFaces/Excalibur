// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;

using SagaOrchestration.Sagas;

namespace SagaOrchestration.Steps;

/// <summary>
/// Saga step that ships the order.
/// </summary>
/// <remarks>
/// <para>
/// This step contacts the shipping service to create a shipment.
/// If successful, it stores the tracking number in the saga data.
/// </para>
/// <para>
/// Compensation cancels the shipment if it hasn't been dispatched yet.
/// Note: If the shipment is already in transit, compensation may need
/// to trigger a return process instead.
/// </para>
/// </remarks>
public sealed partial class ShipOrderStep : ISagaStep
{
	private readonly ILogger<ShipOrderStep> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ShipOrderStep"/> class.
	/// </summary>
	public ShipOrderStep(ILogger<ShipOrderStep> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc/>
	public string Name => "ShipOrder";

	/// <inheritdoc/>
	public async Task<bool> ExecuteAsync(OrderSagaData data, CancellationToken cancellationToken)
	{
		LogCreatingShipment(_logger, data.OrderId, data.CustomerId);

		// Simulate shipping service call
		await Task.Delay(75, cancellationToken).ConfigureAwait(false);

		// Generate tracking number (in real implementation, this comes from shipping provider)
		data.ShipmentTrackingNumber = $"TRK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

		LogShipmentCreated(_logger, data.OrderId, data.ShipmentTrackingNumber);

		return true;
	}

	/// <inheritdoc/>
	public async Task<bool> CompensateAsync(OrderSagaData data, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(data.ShipmentTrackingNumber))
		{
			LogNoShipmentToCancel(_logger, data.OrderId);
			return true; // Nothing to compensate
		}

		LogCancellingShipment(_logger, data.OrderId, data.ShipmentTrackingNumber);

		// Simulate shipping service cancellation call
		await Task.Delay(50, cancellationToken).ConfigureAwait(false);

		LogShipmentCancelled(_logger, data.OrderId, data.ShipmentTrackingNumber);

		return true;
	}

	[LoggerMessage(Level = LogLevel.Information, Message = "Creating shipment for order {OrderId}, Customer: {CustomerId}")]
	private static partial void LogCreatingShipment(ILogger logger, string orderId, string customerId);

	[LoggerMessage(Level = LogLevel.Information, Message = "Shipment created for order {OrderId}, TrackingNumber: {TrackingNumber}")]
	private static partial void LogShipmentCreated(ILogger logger, string orderId, string trackingNumber);

	[LoggerMessage(Level = LogLevel.Debug, Message = "No shipment to cancel for order {OrderId}")]
	private static partial void LogNoShipmentToCancel(ILogger logger, string orderId);

	[LoggerMessage(Level = LogLevel.Information, Message = "Cancelling shipment for order {OrderId}, TrackingNumber: {TrackingNumber}")]
	private static partial void LogCancellingShipment(ILogger logger, string orderId, string trackingNumber);

	[LoggerMessage(Level = LogLevel.Information, Message = "Shipment cancelled for order {OrderId}, TrackingNumber: {TrackingNumber}")]
	private static partial void LogShipmentCancelled(ILogger logger, string orderId, string trackingNumber);
}
