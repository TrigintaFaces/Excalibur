// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Transport;

using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.RabbitMQ.HealthChecking;

/// <summary>
/// Transport health checker for RabbitMQ CloudEvent adapter connections.
/// </summary>
internal sealed partial class RabbitMqCloudEventHealthChecker : ITransportHealthChecker
{
	private readonly IConnection? _connection;
	private readonly ILogger<RabbitMqCloudEventHealthChecker> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="RabbitMqCloudEventHealthChecker"/> class.
	/// </summary>
	/// <param name="connection">The RabbitMQ connection, or null if not yet established.</param>
	/// <param name="logger">The logger.</param>
	public RabbitMqCloudEventHealthChecker(
		IConnection? connection,
		ILogger<RabbitMqCloudEventHealthChecker> logger)
	{
		_connection = connection;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Name => "RabbitMQ CloudEvent Transport";

	/// <inheritdoc />
	public string TransportType => "RabbitMQ";

	/// <inheritdoc />
	public TransportHealthCheckCategory Categories => TransportHealthCheckCategory.Connectivity;

	/// <inheritdoc />
	public Task<TransportHealthCheckResult> CheckHealthAsync(
		TransportHealthCheckContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			if (_connection is null)
			{
				LogConnectionNotRegistered();

				return Task.FromResult(TransportHealthCheckResult.Unhealthy(
					"RabbitMQ connection is not registered.",
					TransportHealthCheckCategory.Connectivity,
					stopwatch.Elapsed));
			}

			if (_connection.IsOpen)
			{
				var data = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["Endpoint"] = _connection.Endpoint.ToString()!,
					["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds
				};

				LogHealthCheckSucceeded(_connection.Endpoint.ToString()!, stopwatch.ElapsedMilliseconds);

				return Task.FromResult(TransportHealthCheckResult.Healthy(
					$"RabbitMQ connection is open to {_connection.Endpoint}.",
					TransportHealthCheckCategory.Connectivity,
					stopwatch.Elapsed,
					data));
			}

			LogConnectionClosed();

			return Task.FromResult(TransportHealthCheckResult.Unhealthy(
				"RabbitMQ connection is closed.",
				TransportHealthCheckCategory.Connectivity,
				stopwatch.Elapsed));
		}
		catch (Exception ex)
		{
			LogHealthCheckFailed(stopwatch.ElapsedMilliseconds, ex);

			var data = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds,
				["Exception"] = ex.GetType().Name
			};

			return Task.FromResult(TransportHealthCheckResult.Unhealthy(
				$"RabbitMQ health check failed: {ex.Message}",
				TransportHealthCheckCategory.Connectivity,
				stopwatch.Elapsed,
				data));
		}
	}

	/// <inheritdoc />
	public Task<TransportHealthCheckResult> CheckQuickHealthAsync(CancellationToken cancellationToken)
	{
		var context = new TransportHealthCheckContext(TransportHealthCheckCategory.Connectivity, TimeSpan.FromSeconds(5));
		return CheckHealthAsync(context, cancellationToken);
	}

	[LoggerMessage(600, LogLevel.Debug,
		"RabbitMQ health check succeeded for endpoint {Endpoint} in {ElapsedMs}ms")]
	private partial void LogHealthCheckSucceeded(string endpoint, double elapsedMs);

	[LoggerMessage(601, LogLevel.Warning,
		"RabbitMQ health check failed after {ElapsedMs}ms")]
	private partial void LogHealthCheckFailed(double elapsedMs, Exception ex);

	[LoggerMessage(602, LogLevel.Warning,
		"RabbitMQ connection is not registered for health check")]
	private partial void LogConnectionNotRegistered();

	[LoggerMessage(603, LogLevel.Warning,
		"RabbitMQ connection is closed")]
	private partial void LogConnectionClosed();
}
