// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace KafkaSample.Messages;

/// <summary>
/// Event representing a sensor reading from an IoT device.
/// </summary>
/// <remarks>
/// This event demonstrates Kafka's strength in high-throughput,
/// time-series data streaming. Sensor readings are partitioned
/// by SensorId for ordered processing per device.
/// Uses <see cref="IIntegrationEvent"/> for cross-service routing to transports.
/// </remarks>
/// <param name="SensorId">The unique identifier for the sensor.</param>
/// <param name="Temperature">The temperature reading in Celsius.</param>
/// <param name="Humidity">The humidity percentage.</param>
/// <param name="Timestamp">When the reading was taken.</param>
public sealed record SensorReadingEvent(
	string SensorId,
	double Temperature,
	double Humidity,
	DateTimeOffset Timestamp) : IIntegrationEvent;
