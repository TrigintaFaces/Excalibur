// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents options for index optimization operations.
/// </summary>
public sealed class IndexOptimizationOptions
{
	/// <summary>
	/// Gets a value indicating whether to optimize refresh interval.
	/// </summary>
	/// <value> True to optimize refresh interval based on usage patterns. </value>
	public bool OptimizeRefreshInterval { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to optimize replica count.
	/// </summary>
	/// <value> True to adjust replica count based on cluster resources. </value>
	public bool OptimizeReplicaCount { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to force merge optimization.
	/// </summary>
	/// <value> True to perform force merge for better search performance. </value>
	public bool ForceMerge { get; init; }

	/// <summary>
	/// Gets the target number of segments for force merge.
	/// </summary>
	/// <value> The target segment count. If null, uses Elasticsearch default. </value>
	public int? TargetSegmentCount { get; init; }
}
