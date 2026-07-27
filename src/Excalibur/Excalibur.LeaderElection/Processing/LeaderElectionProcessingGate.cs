// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Outbox.Processing;

namespace Excalibur.LeaderElection.Processing;

/// <summary>
/// Processing gate backed directly by <see cref="ILeaderElection.CurrentLeadership"/> — the atomic,
/// fencing-token-carrying leadership snapshot — rather than the advisory <see cref="ILeaderElection.IsLeader"/>
/// bool.
/// </summary>
/// <remarks>
/// Gating on <see cref="ILeaderElection.CurrentLeadership"/> instead of <see cref="ILeaderElection.IsLeader"/>
/// is deliberate: <c>CurrentLeadership</c> is <see langword="null"/> IFF this instance is not the leader, and a
/// <see langword="null"/> snapshot fails the gate closed. Gating on the advisory bool instead would be
/// split-brain-vulnerable — the whole point of this type is to make that mistake unreachable on the default path.
/// </remarks>
internal sealed class LeaderElectionProcessingGate : ILeaderProcessingGate
{
	private readonly ILeaderElection _leaderElection;

	/// <summary>
	/// Initializes a new instance of the <see cref="LeaderElectionProcessingGate"/> class.
	/// </summary>
	/// <param name="leaderElection">The leader election instance backing this gate.</param>
	public LeaderElectionProcessingGate(ILeaderElection leaderElection)
	{
		_leaderElection = leaderElection ?? throw new ArgumentNullException(nameof(leaderElection));
	}

	/// <inheritdoc/>
	public bool ShouldProcess => _leaderElection.CurrentLeadership is not null;

	/// <inheritdoc/>
	public long? FencingToken => _leaderElection.CurrentLeadership?.FencingToken;
}
