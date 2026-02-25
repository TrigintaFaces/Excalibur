// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>
/// Represents the health status of an index.
/// </summary>
public sealed class IndexHealthStatus
{
	/// <summary>
	/// Gets the name of the index.
	/// </summary>
	/// <value> The index name. </value>
	public required string IndexName { get; init; }

	/// <summary>
	/// Gets the health status of the index.
	/// </summary>
	/// <value> The health status (green, yellow, red). </value>
	public required string Status { get; init; }

	/// <summary>
	/// Gets the number of primary shards.
	/// </summary>
	/// <value> The number of primary shards in the index. </value>
	public int PrimaryShards { get; init; }

	/// <summary>
	/// Gets the number of replica shards.
	/// </summary>
	/// <value> The number of replica shards in the index. </value>
	public int ReplicaShards { get; init; }

	/// <summary>
	/// Gets the total number of documents.
	/// </summary>
	/// <value> The total document count in the index. </value>
	public long DocumentCount { get; init; }

	/// <summary>
	/// Gets the total size of the index.
	/// </summary>
	/// <value> The total size of the index including replicas. </value>
	public string? TotalSize { get; init; }
}
