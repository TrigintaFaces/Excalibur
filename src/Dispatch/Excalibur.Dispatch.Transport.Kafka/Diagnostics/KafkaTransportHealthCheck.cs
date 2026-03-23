// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Transport.Kafka.Diagnostics;

/// <summary>
/// Health check for Kafka transport connectivity.
/// </summary>
/// <remarks>
/// Reports <see cref="HealthStatus.Healthy"/> when the Kafka producer is registered and its
/// underlying handle is valid, <see cref="HealthStatus.Unhealthy"/> when unavailable.
/// Register via <c>AddHealthChecks().AddCheck&lt;KafkaTransportHealthCheck&gt;("kafka-transport")</c>.
/// </remarks>
internal sealed class KafkaTransportHealthCheck : IHealthCheck
{
	private readonly IProducer<string, byte[]>? _producer;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaTransportHealthCheck"/> class.
	/// </summary>
	/// <param name="producer">The Kafka producer, or null if not yet established.</param>
	public KafkaTransportHealthCheck(IProducer<string, byte[]>? producer = null)
	{
		_producer = producer;
	}

	/// <inheritdoc/>
	public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			if (_producer is null)
			{
				return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
					"Kafka producer is not registered."));
			}

			// Verify the producer handle is valid by checking the Name property.
			// A disposed or failed producer will throw on property access.
			var name = _producer.Name;

			return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
				$"Kafka producer '{name}' is available."));
		}
		catch (ObjectDisposedException)
		{
			return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
				"Kafka producer has been disposed."));
		}
		catch (KafkaException ex)
		{
			return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
				$"Kafka health check failed: {ex.Error.Reason}", ex));
		}
		catch (Exception ex)
		{
			return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
				"Kafka health check failed.", ex));
		}
	}
}
