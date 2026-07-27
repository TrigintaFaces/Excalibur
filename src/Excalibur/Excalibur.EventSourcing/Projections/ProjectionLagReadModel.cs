// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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

	/// <summary>
	/// Initializes a new instance of the <see cref="ProjectionLagReadModel"/> class.
	/// </summary>
	/// <param name="checkpointStore">The subscription checkpoint store to enumerate.</param>
	/// <param name="globalStream">
	/// The global-stream query providing the head position, or <see langword="null"/> when no
	/// event-store provider is configured. When absent the read model reports no lag (fail-open).
	/// </param>
	public ProjectionLagReadModel(
		ISubscriptionCheckpointStore checkpointStore,
		IGlobalStreamQuery? globalStream)
	{
		ArgumentNullException.ThrowIfNull(checkpointStore);

		_checkpointStore = checkpointStore;
		_globalStream = globalStream;
	}

	/// <inheritdoc />
	public async ValueTask<IReadOnlyList<ProjectionLag>> GetLagAsync(CancellationToken cancellationToken)
	{
		// Fail open: without an event-store head source, lag is undefined — report none rather than throw.
		if (_globalStream is null)
		{
			return [];
		}

		var head = await _globalStream.GetHeadPositionAsync(cancellationToken).ConfigureAwait(false);
		var checkpoints = await _checkpointStore
			.EnumerateCheckpointsAsync(cancellationToken)
			.ConfigureAwait(false);

		if (checkpoints.Count == 0)
		{
			return [];
		}

		var result = new List<ProjectionLag>(checkpoints.Count);
		foreach (var checkpoint in checkpoints)
		{
			// Structural safe-op: lag can never go negative even if a checkpoint transiently
			// reports ahead of the observed head.
			var lag = Math.Max(0, head - checkpoint.Position);
			result.Add(new ProjectionLag(checkpoint.SubscriptionName, checkpoint.Position, head, lag));
		}

		return result;
	}
}
