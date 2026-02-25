// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Diagnostics;

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance.HealthChecks;

/// <summary>
/// Health check for the erasure subsystem.
/// </summary>
/// <remarks>
/// <para>
/// Verifies that the <see cref="IErasureStore"/> is accessible and functioning
/// by executing a lightweight status query for a non-existent request ID. Reports:
/// </para>
/// <list type="bullet">
///   <item><b>Healthy:</b> Store responded within the degraded threshold</item>
///   <item><b>Degraded:</b> Store responded but slower than the threshold</item>
///   <item><b>Unhealthy:</b> Store query failed with an exception</item>
/// </list>
/// </remarks>
public sealed partial class ErasureHealthCheck : IHealthCheck
{
	private readonly IErasureStore _erasureStore;
	private readonly ILogger<ErasureHealthCheck> _logger;
	private readonly TimeSpan _degradedThreshold;

	/// <summary>
	/// Initializes a new instance of the <see cref="ErasureHealthCheck"/> class.
	/// </summary>
	/// <param name="erasureStore">The erasure store to check.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="degradedThreshold">Optional threshold for degraded status. Default is 500ms.</param>
	public ErasureHealthCheck(
		IErasureStore erasureStore,
		ILogger<ErasureHealthCheck> logger,
		TimeSpan? degradedThreshold = null)
	{
		_erasureStore = erasureStore ?? throw new ArgumentNullException(nameof(erasureStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_degradedThreshold = degradedThreshold ?? TimeSpan.FromMilliseconds(500);
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var data = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["store_type"] = _erasureStore.GetType().Name,
		};

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			// Lightweight probe: query status for a well-known non-existent ID
			var status = await _erasureStore.GetStatusAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);

			data["duration_ms"] = stopwatch.Elapsed.TotalMilliseconds;
			data["probe_result"] = status is null ? "no_record" : status.ToString();

			if (stopwatch.Elapsed > _degradedThreshold)
			{
				LogErasureHealthCheckDegraded(stopwatch.Elapsed.TotalMilliseconds);
				return HealthCheckResult.Degraded(
					$"Erasure store responded slowly ({stopwatch.Elapsed.TotalMilliseconds:F1}ms > {_degradedThreshold.TotalMilliseconds}ms).",
					data: data);
			}

			LogErasureHealthCheckPassed();
			return HealthCheckResult.Healthy(
				"Erasure store is healthy.",
				data: data);
		}
		catch (Exception ex)
		{
			data["duration_ms"] = stopwatch.Elapsed.TotalMilliseconds;

			LogErasureHealthCheckFailed(ex);
			return HealthCheckResult.Unhealthy(
				$"Erasure store health check failed: {ex.Message}",
				exception: ex,
				data: data);
		}
	}

	[LoggerMessage(
		ComplianceEventId.ErasureHealthCheckPassed,
		LogLevel.Debug,
		"Erasure store health check passed")]
	private partial void LogErasureHealthCheckPassed();

	[LoggerMessage(
		ComplianceEventId.ErasureHealthCheckDegraded,
		LogLevel.Warning,
		"Erasure store health check degraded: response time {DurationMs:F1}ms")]
	private partial void LogErasureHealthCheckDegraded(double durationMs);

	[LoggerMessage(
		ComplianceEventId.ErasureHealthCheckFailed,
		LogLevel.Error,
		"Erasure store health check failed")]
	private partial void LogErasureHealthCheckFailed(Exception exception);
}
