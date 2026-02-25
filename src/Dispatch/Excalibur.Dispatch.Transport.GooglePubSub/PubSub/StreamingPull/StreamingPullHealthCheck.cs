// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Diagnostics.HealthChecks;

using AspNetHealthCheckResult = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Health check for Google Pub/Sub streaming pull connections.
/// </summary>
/// <remarks>
/// Reports health based on the state of active streaming pull connections:
/// <list type="bullet">
/// <item><description>Healthy — all streams are active and connected.</description></item>
/// <item><description>Degraded — some streams are unhealthy but at least one is active.</description></item>
/// <item><description>Unhealthy — no streams exist or all streams are down.</description></item>
/// </list>
/// </remarks>
public sealed class StreamingPullHealthCheck : IHealthCheck
{
	private readonly StreamHealthMonitor _monitor;

	/// <summary>
	/// Initializes a new instance of the <see cref="StreamingPullHealthCheck" /> class.
	/// </summary>
	/// <param name="monitor"> The stream health monitor providing connection state. </param>
	public StreamingPullHealthCheck(StreamHealthMonitor monitor)
	{
		_monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	}

	/// <inheritdoc />
	public Task<AspNetHealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
	{
		try
		{
			var streams = _monitor.GetAllHealthInfo();
			var totalStreams = streams.Length;

			if (totalStreams == 0)
			{
				return Task.FromResult(AspNetHealthCheckResult.Unhealthy(
					"No streaming pull connections registered.",
					data: new Dictionary<string, object>(StringComparer.Ordinal)
					{
						["StreamCount"] = 0,
					}));
			}

			var healthyCount = 0;
			var streamDetails = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["StreamCount"] = totalStreams,
			};

			foreach (var stream in streams)
			{
				var isHealthy = _monitor.IsHealthy(stream.StreamId);
				if (isHealthy)
				{
					healthyCount++;
				}

				streamDetails[$"Stream:{stream.StreamId}"] = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["IsConnected"] = stream.IsConnected,
					["IsHealthy"] = isHealthy,
					["MessagesReceived"] = stream.MessagesReceived,
					["ErrorCount"] = stream.ErrorCount,
					["ReconnectCount"] = stream.ReconnectCount,
				};
			}

			var unhealthyCount = totalStreams - healthyCount;
			streamDetails["HealthyStreams"] = healthyCount;
			streamDetails["UnhealthyStreams"] = unhealthyCount;

			if (unhealthyCount == 0)
			{
				return Task.FromResult(AspNetHealthCheckResult.Healthy(
					$"All {totalStreams} streaming pull connections are healthy.",
					data: streamDetails));
			}

			if (healthyCount > 0)
			{
				return Task.FromResult(AspNetHealthCheckResult.Degraded(
					$"{unhealthyCount} of {totalStreams} streaming pull connections are unhealthy.",
					data: streamDetails));
			}

			return Task.FromResult(AspNetHealthCheckResult.Unhealthy(
				$"All {totalStreams} streaming pull connections are unhealthy.",
				data: streamDetails));
		}
		catch (Exception ex)
		{
			return Task.FromResult(AspNetHealthCheckResult.Unhealthy(
				"Streaming pull health check failed.",
				exception: ex));
		}
	}
}
