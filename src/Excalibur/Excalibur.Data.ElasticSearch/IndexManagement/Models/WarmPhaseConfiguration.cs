// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Configuration for the warm phase of index lifecycle management.
/// </summary>
public sealed class WarmPhaseConfiguration : PhaseConfiguration
{
	/// <summary>
	/// Gets the number of replicas for warm indices.
	/// </summary>
	/// <value> The number of replicas for indices in warm phase. </value>
	public int? NumberOfReplicas { get; init; }

	/// <summary>
	/// Gets a value indicating whether to shrink the index in warm phase.
	/// </summary>
	/// <value> The number of shards to shrink Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </value>
	public int? ShrinkNumberOfShards { get; init; }

	/// <summary>
	/// Gets the priority for warm indices.
	/// </summary>
	/// <value> The priority value for warm indices. </value>
	public int? Priority { get; init; }
}
