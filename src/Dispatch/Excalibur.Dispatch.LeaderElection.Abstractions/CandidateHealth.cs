// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Represents the health status of a candidate.
/// </summary>
public sealed class CandidateHealth
{
	/// <summary>
	/// Gets the candidate ID.
	/// </summary>
	/// <value>the candidate ID.</value>
	public string CandidateId { get; init; } = string.Empty;

	/// <summary>
	/// Gets a value indicating whether the candidate is healthy.
	/// </summary>
	/// <value><see langword="true"/> if the candidate is healthy.; otherwise, <see langword="false"/>.</value>
	public bool IsHealthy { get; init; }

	/// <summary>
	/// Gets the health score (0.0 to 1.0).
	/// </summary>
	/// <value>the health score (0.0 to 1.0).</value>
	public double HealthScore { get; init; }

	/// <summary>
	/// Gets when the health was last updated.
	/// </summary>
	/// <value>when the health was last updated.</value>
	public DateTimeOffset LastUpdated { get; init; }

	/// <summary>
	/// Gets a value indicating whether this candidate is the current leader.
	/// </summary>
	/// <value><see langword="true"/> if this candidate is the current leader.; otherwise, <see langword="false"/>.</value>
	public bool IsLeader { get; init; }

	/// <summary>
	/// Gets health metadata.
	/// </summary>
	/// <value>health metadata.</value>
	public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
