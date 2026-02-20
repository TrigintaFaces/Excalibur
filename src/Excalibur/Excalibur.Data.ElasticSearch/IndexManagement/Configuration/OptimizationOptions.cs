// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Configures index optimization settings.
/// </summary>
public sealed class OptimizationOptions
{
	/// <summary>
	/// Gets a value indicating whether automatic optimization is enabled.
	/// </summary>
	/// <value> True to enable automatic optimization, false otherwise. </value>
	public bool AutoOptimize { get; init; } = true;

	/// <summary>
	/// Gets the merge policy for segments.
	/// </summary>
	/// <value> The merge policy name. Defaults to "tiered". </value>
	public string MergePolicy { get; init; } = "tiered";

	/// <summary>
	/// Gets the maximum segments per tier.
	/// </summary>
	/// <value> The maximum number of segments. Defaults to 10. </value>
	public int MaxSegmentsPerTier { get; init; } = 10;

	/// <summary>
	/// Gets a value indicating whether to force merge on rollover.
	/// </summary>
	/// <value> True to force merge old indices, false otherwise. </value>
	public bool ForceMergeOnRollover { get; init; } = true;

	/// <summary>
	/// Gets the compression level for stored fields.
	/// </summary>
	/// <value> The compression level (best_speed or best_compression). Defaults to "best_compression". </value>
	public string CompressionLevel { get; init; } = "best_compression";
}
