// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Default <see cref="IProjectionLagReadModel"/> implementation that computes per-subscription lag
/// from the subscription checkpoint store and the global-stream head position.
/// </summary>
/// <remarks>
/// For each stored checkpoint, lag is <c>max(0, head - checkpoint)</c>. The head position is read
/// once per call so every subscription is measured against a consistent head.
/// </remarks>
internal sealed class ProjectionLagReadModel : IProjectionLagReadModel
{
	private readonly ISubscriptionCheckpointStore _checkpointStore;
	private readonly IGlobalStreamQuery? _globalStream;
	private readonly string? _expectedSubscription;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionLagReadModel"/> class.
	/// </summary>
	/// <param name="checkpointStore">The subscription checkpoint store to enumerate.</param>
	/// <param name="globalStream">
	/// The global-stream query providing the head position, or <see langword="null"/> when no
	/// event-store provider is configured. When absent the read model reports no lag (fail-open).
	/// </param>
	/// <param name="expectedSubscription">
	/// The subscription this host is configured to run, or <see langword="null"/> when nothing in this
	/// process processes projections.
	/// </param>
	/// <remarks>
	/// <b>The expected subscription is required to report the never-started case at all.</b>
	/// Enumerating the checkpoint store finds only subscriptions that have ALREADY recorded progress,
	/// so a configured projection with a large backlog and no first run contributed no entry — and an
	/// empty entry list is what a health check reads as "nothing is behind". Knowing the name that is
	/// supposed to be there turns a missing row from an absence into a finding.
	/// </remarks>
	public ProjectionLagReadModel(
		ISubscriptionCheckpointStore checkpointStore,
		IGlobalStreamQuery? globalStream,
		string? expectedSubscription = null)
	{
		ArgumentNullException.ThrowIfNull(checkpointStore);

		_checkpointStore = checkpointStore;
		_globalStream = globalStream;
		_expectedSubscription = string.IsNullOrWhiteSpace(expectedSubscription)
			? null
			: expectedSubscription;
	}

	/// <inheritdoc />
	public async ValueTask<ProjectionLagReport> GetLagAsync(CancellationToken cancellationToken)
	{
		// Without an event-store head source there is nothing to subtract a checkpoint from, so lag is
		// UNDEFINED. This still does not throw -- a missing head source is a host configuration gap, not a
		// runtime fault, and a monitoring read should degrade rather than fail. What changed is that it no
		// longer degrades SILENTLY: returning an empty list here was indistinguishable from "no
		// subscription is behind", which a dashboard renders as healthy.
		if (_globalStream is null)
		{
			return new ProjectionLagReport(ProjectionLagAvailability.NoHeadSource, []);
		}

		var head = await _globalStream.GetHeadPositionAsync(cancellationToken).ConfigureAwait(false);
		var checkpoints = await _checkpointStore
			.EnumerateCheckpointsAsync(cancellationToken)
			.ConfigureAwait(false);

		var result = new List<ProjectionLag>(checkpoints.Count + 1);

		// THE CONFIGURED SUBSCRIPTION THAT HAS NEVER RUN. Enumeration finds only subscriptions that
		// have already recorded progress, so this one contributed nothing -- and "no entries" is what a
		// health check reads as "nothing is behind". It is the opposite: it has applied nothing at all,
		// and its true lag is the whole stream, because it must process from the beginning.
		//
		// Reported with a null checkpoint rather than zero, because zero is a legitimate recorded
		// position and would make this indistinguishable from a subscription that has genuinely applied
		// through the first event.
		if (_expectedSubscription is { } expected
			&& !checkpoints.Any(c => string.Equals(c.SubscriptionName, expected, StringComparison.Ordinal)))
		{
			result.Add(new ProjectionLag(expected, CheckpointPosition: null, head, head));
		}

		// Nothing recorded and nothing expected: the head WAS read and there is genuinely nothing to
		// report. This is the good-news empty, and it is distinguishable both from the no-head-source
		// case above and from the never-started case immediately above.
		if (checkpoints.Count == 0)
		{
			return new ProjectionLagReport(ProjectionLagAvailability.Measured, result);
		}

		// A checkpoint ABOVE the head is impossible while the head source and the event store describe the
		// same stream, because the head is at least every delivered position. The clamp below keeps lag
		// non-negative, which is correct arithmetic; on its own it also throws away the only evidence that
		// the wiring is wrong, and then reports the result as a measurement that found nothing behind.
		// Record the contradiction instead of absorbing it.
		var contradicted = false;

		foreach (var checkpoint in checkpoints)
		{
			if (checkpoint.Position > head)
			{
				contradicted = true;
			}

			// Structural safe-op: lag can never go negative, whether the disagreement is transient or the
			// permanent kind the flag above now reports.
			var lag = Math.Max(0, head - checkpoint.Position);
			result.Add(new ProjectionLag(checkpoint.SubscriptionName, checkpoint.Position, head, lag));
		}

		return new ProjectionLagReport(
			contradicted ? ProjectionLagAvailability.CheckpointAheadOfHead : ProjectionLagAvailability.Measured,
			result);
	}
}
