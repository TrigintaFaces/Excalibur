// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using KafkaSample.Messages;

using Microsoft.Extensions.Logging;

namespace KafkaSample.Handlers;

/// <summary>
/// Handles <see cref="SensorReadingEvent"/> messages received from Kafka.
/// </summary>
/// <remarks>
/// This handler processes sensor readings streamed via Kafka.
/// In a real application, this might:
/// - Store readings in a time-series database
/// - Trigger alerts for anomalous values
/// - Update real-time dashboards
/// - Feed machine learning models
/// </remarks>
public sealed class SensorReadingEventHandler : IEventHandler<SensorReadingEvent>
{
	private readonly ILogger<SensorReadingEventHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SensorReadingEventHandler"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public SensorReadingEventHandler(ILogger<SensorReadingEventHandler> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task HandleAsync(SensorReadingEvent eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		_logger.LogInformation(
			"Received SensorReading: Sensor={SensorId}, Temp={Temperature:F1}°C, Humidity={Humidity:F1}%, Time={Timestamp:HH:mm:ss}",
			eventMessage.SensorId,
			eventMessage.Temperature,
			eventMessage.Humidity,
			eventMessage.Timestamp);

		// In a real application, you might:
		// - Insert into InfluxDB or TimescaleDB
		// - Check thresholds and send alerts
		// - Update Grafana/Prometheus metrics
		// - Trigger downstream processing pipelines

		return Task.CompletedTask;
	}
}
