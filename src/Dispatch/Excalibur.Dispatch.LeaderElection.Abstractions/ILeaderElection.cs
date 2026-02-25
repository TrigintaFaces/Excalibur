// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Provides leader election capabilities for distributed systems.
/// </summary>
public interface ILeaderElection : IAsyncDisposable
{
	/// <summary>
	/// Event raised when this instance becomes the leader.
	/// </summary>
	event EventHandler<LeaderElectionEventArgs>? BecameLeader;

	/// <summary>
	/// Event raised when this instance loses leadership.
	/// </summary>
	event EventHandler<LeaderElectionEventArgs>? LostLeadership;

	/// <summary>
	/// Event raised when the leader changes (any instance).
	/// </summary>
	event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

	/// <summary>
	/// Gets the unique identifier for this election participant.
	/// </summary>
	/// <value>the unique identifier for this election participant.</value>
	string CandidateId { get; }

	/// <summary>
	/// Gets a value indicating whether this instance is currently the leader.
	/// </summary>
	/// <value><see langword="true"/> if whether this instance is currently the leader.; otherwise, <see langword="false"/>.</value>
	bool IsLeader { get; }

	/// <summary>
	/// Gets the current leader's identifier.
	/// </summary>
	/// <value>the current leader's identifier., or <see langword="null"/> if not specified.</value>
	string? CurrentLeaderId { get; }

	/// <summary>
	/// Starts participating in leader election.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns>A task that represents the asynchronous start operation.</returns>
	Task StartAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stops participating in leader election and relinquishes leadership if held.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns>A task that represents the asynchronous stop operation.</returns>
	Task StopAsync(CancellationToken cancellationToken);
}
