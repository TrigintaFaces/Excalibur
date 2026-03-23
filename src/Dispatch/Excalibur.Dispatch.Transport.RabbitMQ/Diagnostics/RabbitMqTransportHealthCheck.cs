// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ.Diagnostics;

/// <summary>
/// Health check for RabbitMQ transport connectivity.
/// </summary>
/// <remarks>
/// Reports <see cref="HealthStatus.Healthy"/> when the AMQP connection is open,
/// <see cref="HealthStatus.Unhealthy"/> when disconnected or unavailable.
/// Register via <c>AddHealthChecks().AddCheck&lt;RabbitMqTransportHealthCheck&gt;("rabbitmq-transport")</c>.
/// </remarks>
internal sealed class RabbitMqTransportHealthCheck : IHealthCheck
{
	private readonly IConnection? _connection;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqTransportHealthCheck"/> class.
	/// </summary>
	/// <param name="connection">The RabbitMQ connection, or null if not yet established.</param>
	public RabbitMqTransportHealthCheck(IConnection? connection = null)
	{
		_connection = connection;
	}

	/// <inheritdoc/>
	public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			if (_connection is null)
			{
				return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
					"RabbitMQ connection is not registered."));
			}

			if (_connection.IsOpen)
			{
				return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
					$"RabbitMQ connection is open to {_connection.Endpoint}."));
			}

			return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
				"RabbitMQ connection is closed."));
		}
		catch (Exception ex)
		{
			return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
				"RabbitMQ health check failed.", ex));
		}
	}
}
