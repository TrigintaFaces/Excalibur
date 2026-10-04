// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Reports per-subscription projection lag: how far each durable subscription's checkpoint
/// trails the global stream head position.
/// </summary>
/// <remarks>
/// <para>
/// Lag is computed as <c>max(0, head - checkpoint)</c> per subscription, so it can never be
/// negative even if a checkpoint transiently reports ahead of an observed head.
/// </para>
/// <para>
/// <b>That subtraction is an event COUNT only because committed global positions are dense.</b> The
/// event store's Global Stream Ordering contract guarantees that the set of committed positions is a
/// contiguous prefix, so the number of positions between two points equals the number of events between
/// them. The dependency is named here because it is not local: if positions were ever re-scoped, this
/// figure would silently become an upper bound rather than a count, and nothing in this type would say so.
/// </para>
/// <para>
/// Intended for operational monitoring (dashboards, health signals).
/// </para>
/// <para>
/// <b>An empty result and an unmeasurable one are different answers, and this contract keeps them
/// apart.</b> Without a global-stream head source there is no head to subtract from, so lag is
/// UNDEFINED — not zero, and not "nothing is behind". Reporting an empty list for that case is the
/// failure mode a monitoring surface can least afford: a dashboard renders it as healthy. See
/// <see cref="ProjectionLagAvailability"/>.
/// </para>
/// </remarks>
public interface IProjectionLagReadModel
{
	/// <summary>
	/// Computes the current lag for every known subscription, and reports whether it could be measured
	/// at all.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A report whose <see cref="ProjectionLagReport.Availability"/> states whether lag was measurable,
	/// and whose <see cref="ProjectionLagReport.Streams"/> carries one entry per enumerated subscription
	/// checkpoint. An empty <c>Streams</c> with <see cref="ProjectionLagAvailability.Measured"/> means the
	/// head was read and no subscription is behind; an empty <c>Streams</c> with
	/// <see cref="ProjectionLagAvailability.NoHeadSource"/> means nothing was measured.
	/// </returns>
	/// <remarks>
	/// This returned a bare list of entries, where an empty list conflated "no subscription is behind"
	/// with "lag cannot be computed here". The two have opposite operational meanings and only one of them
	/// is good news.
	/// </remarks>
	ValueTask<ProjectionLagReport> GetLagAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The projection lag for a single subscription at a point in time.
/// </summary>
/// <param name="SubscriptionName">The unique subscription identifier.</param>
/// <param name="CheckpointPosition">The subscription's last checkpointed global-stream position.</param>
/// <param name="HeadPosition">The current global-stream head position.</param>
/// <param name="Lag">
/// The number of events the subscription trails the head, computed as
/// <c>max(0, <paramref name="HeadPosition"/> - <paramref name="CheckpointPosition"/>)</c>.
/// </param>
public readonly record struct ProjectionLag(
	string SubscriptionName,
	long CheckpointPosition,
	long HeadPosition,
	long Lag);

/// <summary>
/// Whether projection lag could be measured.
/// </summary>
public enum ProjectionLagAvailability
{
	/// <summary>
	/// The global-stream head was read, so the reported entries are a measurement. An empty set of
	/// entries means no subscription is behind.
	/// </summary>
	Measured,

	/// <summary>
	/// The head was read, but at least one checkpoint reports a position ABOVE it. That is impossible
	/// under correct wiring, so the numbers are reported but must not be read as lag.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why this is a distinct state rather than a clamped zero.</b> The head is, by construction, at
	/// least every position that has been delivered to any subscription, so a checkpoint cannot exceed it
	/// while the head source and the event store describe the same stream. A non-negative clamp is still
	/// correct arithmetic — lag is never negative — but clamping ALONE discards the one observation that
	/// proves the configuration is wrong, and reports the result as a measurement that found nothing
	/// behind. The clamp absorbs a transient disagreement, which is what it was written for, and it also
	/// absorbed the permanent one.
	/// </para>
	/// <para>
	/// <b>The likeliest cause is a head source that does not describe the store being written.</b> Every
	/// global-stream query registration in this framework is a <c>TryAdd</c>, and the global-stream query
	/// is not registered under a key, so in a host that configures two event-store providers the first
	/// registration wins and cannot be selected against. The event store itself IS selectable by key, so
	/// a host can end up writing to one store and reading its stream head from another — durable writes,
	/// an empty head, and every subscription appearing caught up.
	/// </para>
	/// <para>
	/// Treat this as a configuration fault, not as lag. The entries are still reported, with their
	/// non-negative clamped values, so an operator can see WHICH subscriptions are implicated.
	/// </para>
	/// </remarks>
	CheckpointAheadOfHead,

	/// <summary>
	/// No global-stream head source is registered, so there is nothing to subtract a checkpoint from and
	/// lag is undefined. The reported entries are empty because nothing was measured — NOT because
	/// nothing is behind.
	/// </summary>
	/// <remarks>
	/// A host reaches this state by registering the lag read model without an event-store head source. It
	/// is a configuration gap rather than a runtime fault, which is exactly why it must be reported rather
	/// than thrown: a monitoring surface should degrade to "not reporting", never to "all clear".
	/// </remarks>
	NoHeadSource,
}

/// <summary>
/// The result of a projection-lag read: whether it was measurable, and the per-subscription entries.
/// </summary>
/// <param name="Availability">Whether lag could be measured at all.</param>
/// <param name="Streams">
/// One entry per enumerated subscription checkpoint. Always empty when
/// <paramref name="Availability"/> is <see cref="ProjectionLagAvailability.NoHeadSource"/>.
/// </param>
public readonly record struct ProjectionLagReport(
	ProjectionLagAvailability Availability,
	IReadOnlyList<ProjectionLag> Streams);
