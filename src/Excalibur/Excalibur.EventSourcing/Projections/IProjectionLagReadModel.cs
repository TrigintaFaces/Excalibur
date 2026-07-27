// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// Intended for operational monitoring (dashboards, health signals). Implementations read from
/// the configured checkpoint store and global-stream head source; when neither is configured the
/// read model reports an empty result rather than failing.
/// </para>
/// </remarks>
public interface IProjectionLagReadModel
{
	/// <summary>
	/// Computes the current lag for every known subscription.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// One <see cref="ProjectionLag"/> per enumerated subscription checkpoint. An empty list when no
	/// checkpoints exist or the checkpoint store is not configured.
	/// </returns>
	ValueTask<IReadOnlyList<ProjectionLag>> GetLagAsync(CancellationToken cancellationToken);
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
