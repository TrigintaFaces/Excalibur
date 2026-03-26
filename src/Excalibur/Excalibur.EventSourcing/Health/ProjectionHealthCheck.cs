// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Health check for inline and async projections (R27.48/R27.50).
/// </summary>
/// <remarks>
/// <para>
/// Reports healthy if:
/// <list type="bullet">
/// <item>No inline projection errors in the configured window</item>
/// <item>Async projection lag is below the configured threshold</item>
/// </list>
/// </para>
/// <para>
/// Reports degraded if inline projection errors occurred recently.
/// Reports unhealthy if async projection lag exceeds the threshold.
/// </para>
/// </remarks>
internal sealed class ProjectionHealthCheck : IHealthCheck
{
	private readonly ProjectionHealthState _state;
	private readonly IOptions<ProjectionHealthCheckOptions> _options;

	public ProjectionHealthCheck(
		ProjectionHealthState state,
		IOptions<ProjectionHealthCheckOptions> options)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(options);

		_state = state;
		_options = options;
	}

	/// <inheritdoc />
	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var opts = _options.Value;

		// Check for recent inline projection errors
		if (_state.LastInlineError.HasValue &&
			DateTimeOffset.UtcNow - _state.LastInlineError.Value < opts.InlineErrorWindow)
		{
			return Task.FromResult(HealthCheckResult.Degraded(
				$"Inline projection error occurred at {_state.LastInlineError.Value:O}. " +
				$"Projection: {_state.LastErrorProjectionType ?? "unknown"}."));
		}

		// Check async projection lag
		if (_state.AsyncLag > opts.UnhealthyLagThreshold)
		{
			return Task.FromResult(HealthCheckResult.Unhealthy(
				$"Async projection lag is {_state.AsyncLag} events (threshold: {opts.UnhealthyLagThreshold})."));
		}

		if (_state.AsyncLag > opts.DegradedLagThreshold)
		{
			return Task.FromResult(HealthCheckResult.Degraded(
				$"Async projection lag is {_state.AsyncLag} events (threshold: {opts.DegradedLagThreshold})."));
		}

		return Task.FromResult(HealthCheckResult.Healthy("Projections operating normally."));
	}
}
