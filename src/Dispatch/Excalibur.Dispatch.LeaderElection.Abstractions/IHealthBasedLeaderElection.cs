// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

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
	/// <returns>A task that represents the asynchronous update operation.</returns>
	[RequiresDynamicCode("JSON serialization of health metadata requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming")]
	Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata);

	/// <summary>
	/// Gets the health status of all candidates.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> Health status of all candidates. </returns>
	[RequiresDynamicCode("JSON serialization of candidate health requires dynamic code generation for type inspection and property access")]
	[RequiresUnreferencedCode("JSON serialization may reference types not preserved during trimming")]
	Task<IEnumerable<CandidateHealth>> GetCandidateHealthAsync(CancellationToken cancellationToken);
}
