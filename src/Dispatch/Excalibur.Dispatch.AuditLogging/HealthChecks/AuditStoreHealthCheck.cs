// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.AuditLogging.Diagnostics;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.AuditLogging.HealthChecks;

/// <summary>
/// Health check for the audit store subsystem.
/// </summary>
/// <remarks>
/// <para>
/// Verifies that the <see cref="IAuditStore"/> is accessible and functioning
/// by executing a lightweight count query. Reports:
/// </para>
/// <list type="bullet">
///   <item><b>Healthy:</b> Store responded within the degraded threshold</item>
///   <item><b>Degraded:</b> Store responded but slower than the threshold</item>
///   <item><b>Unhealthy:</b> Store query failed with an exception</item>
/// </list>
/// </remarks>
public sealed partial class AuditStoreHealthCheck : IHealthCheck
{
	private readonly IAuditStore _auditStore;
	private readonly ILogger<AuditStoreHealthCheck> _logger;
	private readonly TimeSpan _degradedThreshold;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditStoreHealthCheck"/> class.
	/// </summary>
	/// <param name="auditStore">The audit store to check.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="degradedThreshold">Optional threshold for degraded status. Default is 500ms.</param>
	public AuditStoreHealthCheck(
		IAuditStore auditStore,
		ILogger<AuditStoreHealthCheck> logger,
		TimeSpan? degradedThreshold = null)
	{
		_auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
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
			["store_type"] = _auditStore.GetType().Name,
		};

		var startTimestamp = Stopwatch.GetTimestamp();
		try
		{
			var query = new AuditQuery { MaxResults = 1 };
			var count = await _auditStore.CountAsync(query, cancellationToken).ConfigureAwait(false);
			var elapsed = Stopwatch.GetElapsedTime(startTimestamp);

			data["duration_ms"] = elapsed.TotalMilliseconds;
			data["total_events"] = count;

			if (elapsed > _degradedThreshold)
			{
				LogAuditStoreHealthCheckDegraded(elapsed.TotalMilliseconds);
				return HealthCheckResult.Degraded(
					$"Audit store responded slowly ({elapsed.TotalMilliseconds:F1}ms > {_degradedThreshold.TotalMilliseconds}ms).",
					data: data);
			}

			LogAuditStoreHealthCheckPassed();
			return HealthCheckResult.Healthy(
				$"Audit store is healthy. {count} event(s) recorded.",
				data: data);
		}
		catch (Exception ex)
		{
			data["duration_ms"] = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

			LogAuditStoreHealthCheckFailed(ex);
			return HealthCheckResult.Unhealthy(
				$"Audit store health check failed: {ex.Message}",
				exception: ex,
				data: data);
		}
	}

	[LoggerMessage(
		AuditLoggingEventId.AuditStoreHealthCheckPassed,
		LogLevel.Debug,
		"Audit store health check passed")]
	private partial void LogAuditStoreHealthCheckPassed();

	[LoggerMessage(
		AuditLoggingEventId.AuditStoreHealthCheckDegraded,
		LogLevel.Warning,
		"Audit store health check degraded: response time {DurationMs:F1}ms")]
	private partial void LogAuditStoreHealthCheckDegraded(double durationMs);

	[LoggerMessage(
		AuditLoggingEventId.AuditStoreHealthCheckFailed,
		LogLevel.Error,
		"Audit store health check failed")]
	private partial void LogAuditStoreHealthCheckFailed(Exception exception);
}
