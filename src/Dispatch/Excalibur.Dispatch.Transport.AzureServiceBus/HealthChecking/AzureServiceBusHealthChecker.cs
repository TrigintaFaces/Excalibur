// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using AspNetHealthCheckResult = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Health checker for Azure Service Bus connections.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AzureServiceBusHealthChecker" /> class. </remarks>
/// <param name="logger"> The logger. </param>
/// <param name="serviceBusClient"> The Service Bus client. </param>
/// <param name="connectionString"> The connection string for admin operations. </param>
/// <param name="testQueueName"> Optional test queue name for health checks. </param>
public sealed class AzureServiceBusHealthChecker(
	ILogger<AzureServiceBusHealthChecker> logger,
	ServiceBusClient serviceBusClient,
	string? connectionString = null,
	string? testQueueName = null) : IHealthCheck
{
	private readonly ILogger<AzureServiceBusHealthChecker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ServiceBusClient _serviceBusClient = serviceBusClient ?? throw new ArgumentNullException(nameof(serviceBusClient));

	/// <summary>
	/// Checks the health of a Service Bus connection.
	/// </summary>
	/// <param name="context"> The health check context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The health check result. </returns>
	public async Task<AspNetHealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var data = new Dictionary<string, object>(StringComparer.Ordinal);

		var stopwatch = Stopwatch.StartNew();

		try
		{
			// Check if the client is closed
			if (_serviceBusClient.IsClosed)
			{
				data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
				return AspNetHealthCheckResult.Unhealthy("Service Bus client is closed", data: data);
			}

			// If we have a test queue, try to peek a message (non-destructive)
			if (!string.IsNullOrEmpty(testQueueName))
			{
				await using var receiver = _serviceBusClient.CreateReceiver(testQueueName);

				// Peek a single message with short timeout
				var message = await receiver.PeekMessageAsync(cancellationToken: cancellationToken)
					.ConfigureAwait(false);

				stopwatch.Stop();

				_logger.LogDebug(
					"Service Bus health check succeeded for queue {QueueName} in {ElapsedMs}ms",
					testQueueName,
					stopwatch.ElapsedMilliseconds);

				data["QueueName"] = testQueueName;
				data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
				return AspNetHealthCheckResult.Healthy(
					$"Service Bus connection is healthy. Queue accessible: {testQueueName}",
					data);
			}

			if (!string.IsNullOrEmpty(connectionString))
			{
				// Use admin client to check namespace connectivity
				var adminClient = new ServiceBusAdministrationClient(connectionString);

				// Get namespace properties (lightweight operation)
				var properties = await adminClient.GetNamespacePropertiesAsync(cancellationToken)
					.ConfigureAwait(false);

				stopwatch.Stop();

				_logger.LogDebug(
					"Service Bus health check succeeded for namespace {Namespace} in {ElapsedMs}ms",
					properties.Value.Name,
					stopwatch.ElapsedMilliseconds);

				data["Namespace"] = properties.Value.Name;
				data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
				return AspNetHealthCheckResult.Healthy(
					$"Service Bus connection is healthy. Namespace: {properties.Value.Name}",
					data);
			}

			// Basic check - if client is not closed, consider it healthy
			stopwatch.Stop();

			data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
			return AspNetHealthCheckResult.Healthy("Service Bus client is not closed", data);
		}
		catch (Exception ex)
		{
			stopwatch.Stop();

			_logger.LogWarning(ex, "Service Bus health check failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

			data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
			data["Exception"] = ex.GetType().Name;
			return AspNetHealthCheckResult.Unhealthy(
				$"Service Bus health check failed: {ex.Message}",
				ex,
				data);
		}
	}
}
