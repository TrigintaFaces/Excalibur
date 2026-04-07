// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Provides leader election capabilities with health checks.
/// </summary>
public interface IHealthBasedLeaderElection : ILeaderElection
{
	/// <summary>
	/// Updates the health status of this candidate.
	/// </summary>
	/// <param name="isHealthy"> Whether the candidate is healthy. </param>
	/// <param name="metadata"> Optional health metadata. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns>A task that represents the asynchronous update operation.</returns>
	Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the health status of all candidates.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Health status of all candidates. </returns>
	Task<IEnumerable<CandidateHealth>> GetCandidateHealthAsync(CancellationToken cancellationToken);
}
