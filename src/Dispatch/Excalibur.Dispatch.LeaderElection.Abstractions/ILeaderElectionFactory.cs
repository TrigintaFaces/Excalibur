// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Factory for creating leader election instances.
/// </summary>
public interface ILeaderElectionFactory
{
	/// <summary>
	/// Creates a leader election instance for the specified resource.
	/// </summary>
	/// <param name="resourceName"> The resource to elect a leader for. </param>
	/// <param name="candidateId"> Optional candidate ID (defaults to instance ID). </param>
	/// <returns> A leader election instance. </returns>
	ILeaderElection CreateElection(string resourceName, string? candidateId);

	/// <summary>
	/// Creates a health-based leader election instance.
	/// </summary>
	/// <param name="resourceName"> The resource to elect a leader for. </param>
	/// <param name="candidateId"> Optional candidate ID (defaults to instance ID). </param>
	/// <returns> A health-based leader election instance. </returns>
	IHealthBasedLeaderElection CreateHealthBasedElection(string resourceName, string? candidateId);
}
