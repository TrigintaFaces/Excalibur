// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents conditions that trigger index rollover operations.
/// </summary>
public sealed class RolloverConditions
{
	/// <summary>
	/// Gets the maximum age before rollover.
	/// </summary>
	/// <value> The maximum age before triggering rollover. </value>
	public TimeSpan? MaxAge { get; init; }

	/// <summary>
	/// Gets the maximum index size before rollover.
	/// </summary>
	/// <value> The maximum size before triggering rollover (e.g., "50GB"). </value>
	public string? MaxSize { get; init; }

	/// <summary>
	/// Gets the maximum number of documents before rollover.
	/// </summary>
	/// <value> The maximum document count before triggering rollover. </value>
	public long? MaxDocs { get; init; }

	/// <summary>
	/// Gets the maximum primary shard size before rollover.
	/// </summary>
	/// <value> The maximum primary shard size before triggering rollover. </value>
	public string? MaxPrimaryShardSize { get; init; }
}
