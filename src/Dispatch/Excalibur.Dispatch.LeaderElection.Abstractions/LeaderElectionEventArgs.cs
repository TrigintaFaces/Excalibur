// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Event arguments for leader election events.
/// </summary>
/// <param name="candidateId">The identifier of the candidate involved in the election event.</param>
/// <param name="resourceName">The name of the resource for which the election is being conducted.</param>
/// <param name="timestamp">
/// When the event occurred, supplied by the caller from its configured time source. Omit it to read the
/// system clock. An election that was given a <see cref="TimeProvider"/> passes that provider's time, so
/// a caller controlling the clock sees the same instant here as on the election's own properties.
/// </param>
public sealed class LeaderElectionEventArgs(string candidateId, string resourceName, DateTimeOffset? timestamp = null) : EventArgs
{
	/// <summary>
	/// Gets the candidate ID involved in the event.
	/// </summary>
	/// <value>the candidate ID involved in the event.</value>
	public string CandidateId { get; } = candidateId;

	/// <summary>
	/// Gets the resource name.
	/// </summary>
	/// <value>the resource name.</value>
	public string ResourceName { get; } = resourceName;

	/// <summary>
	/// Gets when the event occurred.
	/// </summary>
	/// <value>when the event occurred.</value>
	public DateTimeOffset Timestamp { get; } = timestamp ?? DateTimeOffset.UtcNow;
}
