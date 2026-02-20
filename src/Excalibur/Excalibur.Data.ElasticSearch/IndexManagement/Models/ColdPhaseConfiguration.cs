// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Configuration for the cold phase of index lifecycle management.
/// </summary>
public sealed class ColdPhaseConfiguration : PhaseConfiguration
{
	/// <summary>
	/// Gets the number of replicas for cold indices.
	/// </summary>
	/// <value> The number of replicas for indices in cold phase. </value>
	public int? NumberOfReplicas { get; init; }

	/// <summary>
	/// Gets the priority for cold indices.
	/// </summary>
	/// <value> The priority value for cold indices. </value>
	public int? Priority { get; init; }
}
