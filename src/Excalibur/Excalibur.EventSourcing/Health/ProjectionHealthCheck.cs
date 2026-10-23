// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Health check for projections: how far behind the read models are, and whether applying to them has
/// been failing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lag is measured, not reported.</b> It is computed as the distance between the event store's head
/// and each subscription's confirmed applied position, so a host that has stopped applying — crashed,
/// shut down with its process, never started, or standing by for leadership — shows up as lag
/// that grows and keeps growing. That is the whole point of this check: the failure it exists to catch
/// produces no exception and no log line, so the only way to see it is to measure the distance.
/// </para>
/// <para>
/// <b>Applied and checkpointed progress are distinct.</b> The local applied mark covers clean batches
/// below the checkpoint interval; the durable checkpoint covers other processes. Both are compared
/// against the current stream head, so a stopped host's stale mark cannot hide a growing backlog.
/// </para>
/// <para>
/// <b>Worst state wins.</b> Lag past the unhealthy threshold outranks a recent apply error: an error is
/// a visible, usually transient fault the host retries, while unbounded lag means nothing is advancing
/// at all. Reporting degraded because an error happened recently, while the read model sits a hundred
/// thousand events behind, states the smaller problem.
/// </para>
/// </remarks>
internal sealed class ProjectionHealthCheck : IHealthCheck
{
	private readonly ProjectionHealthState _state;
	private readonly IOptions<ProjectionHealthCheckOptions> _options;
	private readonly IProjectionLagReadModel? _lag;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionHealthCheck"/> class.
	/// </summary>
	/// <param name="state">Records apply errors as the host encounters them.</param>
	/// <param name="options">The thresholds.</param>
	/// <param name="lag">
	/// The lag read model, when one is registered. It is supplied by the projection-processing
	/// registration, so its absence is itself a finding — see the degraded result below.
	/// </param>
	public ProjectionHealthCheck(
		ProjectionHealthState state,
		IOptions<ProjectionHealthCheckOptions> options,
		IProjectionLagReadModel? lag = null)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(options);

		_state = state;
		_options = options;
		_lag = lag;
	}

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var opts = _options.Value;

		// FIRST, and ahead of lag: a halted host does not advance, so on a quiet stream it sits about one
		// event from the head and every lag threshold reads as healthy. The latch is cleared by a clean
		// apply rather than by an elapsed window, so this cannot report recovery that did not happen.
		if (_state.HaltedSubscription is { } halted)
		{
			return HealthCheckResult.Unhealthy(
				$"Projection subscription '{halted}' has stopped advancing: a poison event or an apply "
				+ "failure left its position unchanged, and it will re-read the same batch until that "
				+ "batch succeeds. Projections on this subscription are frozen, not merely behind.");
		}

		// NOT HEALTHY, deliberately. Nothing registered a lag read model, which means nothing registered
		// projection processing either -- so no projection is advancing and this check cannot see how far
		// behind they are. Reporting healthy here is how a host with no projection processing at all looks
		// identical to one that is fully caught up.
		if (_lag is null)
		{
			return HealthCheckResult.Degraded(
				"Projection lag cannot be measured: no projection lag read model is registered, which "
				+ "means projection processing is not enabled in this process. Call "
				+ "EnableProjectionProcessing() on the host that owns the projections, or drive "
				+ "IProjectionProcessor.ProcessPendingAsync() from a trigger.");
		}

		var report = await _lag.GetLagAsync(cancellationToken).ConfigureAwait(false);

		if (report.Availability == ProjectionLagAvailability.NoHeadSource)
		{
			return HealthCheckResult.Degraded(
				"Projection lag cannot be measured: the event store provider supplies no global-stream "
				+ "head to compare checkpoints against, so how far behind the read models are is unknown.");
		}

		// Neither durable nor local evidence of progress: distinguish this from a clean first batch
		// that has not yet accumulated enough events to write its checkpoint.
		//
		// Ahead of the thresholds for the message, not the status. A large backlog would be caught by
		// the unhealthy threshold anyway, but it would be described as "behind by 100000" -- true, and
		// it omits the fact an operator needs, which is that nothing has ever applied a batch. A SMALL
		// stream is the case the thresholds cannot catch at all: ten pending events sit under every
		// threshold and report healthy, which is the same hole this check was rewritten to close.
		//
		// Severity still escalates with the backlog, because a never-started subscription with a large
		// one is both never-started and badly behind.
		var worst = Worst(report);
		var neverStarted = report.Streams.FirstOrDefault(stream =>
			stream.CheckpointPosition is null && _state.AppliedPosition(stream.SubscriptionName) is null);

		// A short never-started subscription must not hide a more severely lagging one.
		if (worst is { } stalled && stalled.Lag > opts.UnhealthyLagThreshold
			&& (neverStarted.SubscriptionName is null || stalled.Lag > neverStarted.Lag))
		{
			return HealthCheckResult.Unhealthy(Describe(stalled, opts.UnhealthyLagThreshold));
		}

		if (neverStarted.SubscriptionName is not null)
		{
			var message =
				$"Projection subscription '{neverStarted.SubscriptionName}' has recorded no progress at "
				+ $"all: it holds no checkpoint, so it has applied none of the {neverStarted.HeadPosition} "
				+ "event(s) in the stream. Either nothing is driving it -- no host started and no call to "
				+ "IProjectionProcessor.ProcessPendingAsync() -- or its first run has not yet completed a "
				+ "batch.";

			return neverStarted.Lag > opts.UnhealthyLagThreshold
				? HealthCheckResult.Unhealthy(message)
				: HealthCheckResult.Degraded(message);
		}
		// A recent error that did NOT halt the subscription -- a contested checkpoint, a fault the host has
		// since applied past. Degraded is right for that, and the window is right for it too, because the
		// host did make progress afterwards. It is only the wrong instrument for a halt, which is why the
		// latch above is checked first rather than instead.
		if (_state.LastInlineError.HasValue &&
			DateTimeOffset.UtcNow - _state.LastInlineError.Value < opts.InlineErrorWindow)
		{
			return HealthCheckResult.Degraded(
				$"Projection apply error occurred at {_state.LastInlineError.Value:O}. " +
				$"Projection: {_state.LastErrorProjectionType ?? "unknown"}.");
		}

		if (worst is { } lagging && lagging.Lag > opts.DegradedLagThreshold)
		{
			return HealthCheckResult.Degraded(Describe(lagging, opts.DegradedLagThreshold));
		}

		return HealthCheckResult.Healthy("Projections operating normally.");
	}

	// The furthest-behind subscription decides the verdict: one stalled projection is a fault even when
	// every other subscription is current, and averaging would hide it.
	private ProjectionLag? Worst(ProjectionLagReport report)
	{
		ProjectionLag? worst = null;

		foreach (var reported in report.Streams)
		{
			var applied = _state.AppliedPosition(reported.SubscriptionName);
			var position = Math.Max(reported.CheckpointPosition ?? 0, applied ?? 0);
			var stream = applied is null
				? reported
				: reported with { Lag = Math.Max(0, reported.HeadPosition - position) };

			if (worst is null || stream.Lag > worst.Value.Lag)
			{
				worst = stream;
			}
		}

		return worst;
	}

	// Names the subscription and both positions, because "lag is 40000" does not say which projection
	// stopped or whether the head moved or the checkpoint stuck.
	private static string Describe(ProjectionLag lag, long threshold) =>
		string.Format(
			CultureInfo.InvariantCulture,
			"Projection subscription '{0}' is {1} event(s) behind (threshold: {2}); "
			+ "checkpoint at {3}, stream head at {4}.",
			lag.SubscriptionName,
			lag.Lag,
			threshold,
			lag.CheckpointPosition?.ToString(CultureInfo.InvariantCulture) ?? "none",
			lag.HeadPosition);
}
