// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Health check for materialized view infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// Evaluates the health of the materialized view system by checking:
/// <list type="bullet">
/// <item>Staleness: Time since last successful refresh</item>
/// <item>Failure rate: Percentage of failed refresh attempts</item>
/// <item>View registration: At least one view builder is registered</item>
/// </list>
/// </para>
/// <para>
/// The health check reports:
/// <list type="bullet">
/// <item><b>Healthy:</b> All views are current and failure rate is acceptable</item>
/// <item><b>Degraded:</b> Views are stale or failure rate exceeds threshold</item>
/// <item><b>Unhealthy:</b> No views registered or store is unavailable</item>
/// </list>
/// </para>
/// </remarks>
public sealed class MaterializedViewHealthCheck : IHealthCheck
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IOptions<MaterializedViewHealthCheckOptions> _options;
	private readonly MaterializedViewMetrics _metrics;
	private readonly TimeProvider _timeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="MaterializedViewHealthCheck"/> class.
	/// </summary>
	/// <param name="scopeFactory">The service scope factory.</param>
	/// <param name="options">The health check options.</param>
	/// <param name="metrics">The metrics instance for accessing health data.</param>
	/// <param name="timeProvider">The time provider.</param>
	public MaterializedViewHealthCheck(
		IServiceScopeFactory scopeFactory,
		IOptions<MaterializedViewHealthCheckOptions> options,
		MaterializedViewMetrics metrics,
		TimeProvider timeProvider)
	{
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var opts = _options.Value;
		var data = new Dictionary<string, object>();

		try
		{
			await using var scope = _scopeFactory.CreateAsyncScope();

			// Get registered views
			var registrations = scope.ServiceProvider.GetServices<MaterializedViewBuilderRegistration>().ToList();
			if (registrations.Count == 0)
			{
				return HealthCheckResult.Unhealthy(
					"No materialized views registered.",
					data: opts.IncludeDetails ? data : null);
			}

			data["registeredViews"] = registrations.Count;
			data["viewNames"] = registrations.Select(r => r.ViewType.Name).ToArray();

			// Check staleness from metrics
			var stalenessSeconds = _metrics.GetMaxStalenessSeconds();
			var staleness = TimeSpan.FromSeconds(stalenessSeconds);
			data["maxStaleness"] = staleness.ToString();

			// Check failure rate from metrics
			var failureRate = _metrics.GetFailureRatePercent();
			data["failureRatePercent"] = failureRate;

			// Evaluate health
			if (staleness > opts.StalenessThreshold)
			{
				return HealthCheckResult.Degraded(
					$"Materialized views are stale. Max staleness: {staleness}, threshold: {opts.StalenessThreshold}.",
					data: opts.IncludeDetails ? data : null);
			}

			if (failureRate > opts.FailureRateThresholdPercent)
			{
				return HealthCheckResult.Degraded(
					$"Materialized view refresh failure rate ({failureRate:F1}%) exceeds threshold ({opts.FailureRateThresholdPercent}%).",
					data: opts.IncludeDetails ? data : null);
			}

			return HealthCheckResult.Healthy(
				$"{registrations.Count} materialized views healthy.",
				data: opts.IncludeDetails ? data : null);
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(
				"Failed to check materialized view health.",
				ex,
				opts.IncludeDetails ? data : null);
		}
	}
}
