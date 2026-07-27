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
	/// Event raised when this instance fails to acquire leadership, either by losing the
	/// acquisition race to another candidate or because an error occurred during the attempt.
	/// </summary>
	event EventHandler<LeaderElectionAcquisitionFailedEventArgs>? AcquisitionFailed;

	/// <summary>
	/// Gets the unique identifier for this election participant.
	/// </summary>
	/// <value>the unique identifier for this election participant.</value>
	string CandidateId { get; }

	/// <summary>
	/// Gets a value indicating whether this instance is currently the leader.
	/// </summary>
	/// <remarks>
	/// This is an advisory convenience derived from the same underlying state as
	/// <see cref="CurrentLeadership"/>. For a single atomic read of leadership state that also carries the
	/// fencing token, prefer <c>CurrentLeadership is not null</c> — it is the authoritative check.
	/// </remarks>
	/// <value><see langword="true"/> if whether this instance is currently the leader.; otherwise, <see langword="false"/>.</value>
	bool IsLeader { get; }

	/// <summary>
	/// Gets the current leadership tenure, or <see langword="null"/> if this instance is not currently
	/// the leader.
	/// </summary>
	/// <remarks>
	/// This is the authoritative, atomic check for leadership: it is non-null if and only if this instance
	/// currently considers itself the leader, and the returned <see cref="Leadership"/> carries the
	/// fencing token minted for the current tenure alongside the instant it began. Read once per tenure
	/// and pin the returned value — see the <see cref="Leadership"/> remarks.
	/// </remarks>
	/// <value>The current <see cref="Leadership"/> snapshot, or <see langword="null"/> if not leading.</value>
	Leadership? CurrentLeadership { get; }

	/// <summary>
	/// Gets the identifier of the instance currently believed to hold leadership, on a <strong>best-effort</strong>
	/// basis.
	/// </summary>
	/// <value>
	/// The current leader's identifier, or <see langword="null"/> when this instance cannot determine it.
	/// </value>
	/// <remarks>
	/// <para>
	/// <strong>Guaranteed non-null only while this instance is itself the leader.</strong> When another instance
	/// holds leadership, whether this property can name it depends on the provider's arbitration primitive, and
	/// callers must treat <see langword="null"/> as "unknown", never as "there is no leader".
	/// </para>
	/// <para>
	/// A provider that arbitrates through a primitive carrying no owner identity — a Postgres session-level
	/// advisory lock, for example, which answers only true or false to the instance attempting it — has nothing
	/// to report to a follower and returns <see langword="null"/> there. A provider that arbitrates through a
	/// claims row or lease record can name the holder to every instance. Both are conforming: leader
	/// <em>discovery</em> is an optional capability, and its absence does not weaken the mutual-exclusion
	/// guarantee, which every provider must uphold.
	/// </para>
	/// <para>
	/// <strong>Never use this for a leadership decision.</strong> It is a diagnostic and a convenience for
	/// logging or surfacing topology. The authoritative, atomic check is <see cref="CurrentLeadership"/>, which
	/// carries the fencing token for the tenure; a non-null value here says nothing about whether the named
	/// instance still holds leadership by the time the caller acts on it.
	/// </para>
	/// </remarks>
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
