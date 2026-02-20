// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Represents a request to rebuild a projection.
/// </summary>
public sealed class ProjectionRebuildRequest
{
	/// <summary>
	/// Gets the type of projection to rebuild.
	/// </summary>
	/// <value>
	/// The type of projection to rebuild.
	/// </value>
	public required string ProjectionType { get; init; }

	/// <summary>
	/// Gets the target index name for the rebuilt projection.
	/// </summary>
	/// <value>
	/// The target index name for the rebuilt projection.
	/// </value>
	public required string TargetIndexName { get; init; }

	/// <summary>
	/// Gets the optional source index to rebuild from (for index-to-index rebuilds).
	/// </summary>
	/// <value>
	/// The optional source index to rebuild from (for index-to-index rebuilds).
	/// </value>
	public string? SourceIndexName { get; init; }

	/// <summary>
	/// Gets the starting point for the rebuild (e.g., event sequence number or timestamp).
	/// </summary>
	/// <value>
	/// The starting point for the rebuild (e.g., event sequence number or timestamp).
	/// </value>
	public DateTime? FromTimestamp { get; init; }

	/// <summary>
	/// Gets the ending point for the rebuild.
	/// </summary>
	/// <value>
	/// The ending point for the rebuild.
	/// </value>
	public DateTime? ToTimestamp { get; init; }

	/// <summary>
	/// Gets the batch size for processing events during rebuild.
	/// </summary>
	/// <value>
	/// The batch size for processing events during rebuild.
	/// </value>
	public int BatchSize { get; init; } = 1000;

	/// <summary>
	/// Gets a value indicating whether to create a new index or use an existing one.
	/// </summary>
	/// <value>
	/// A value indicating whether to create a new index or use an existing one.
	/// </value>
	public bool CreateNewIndex { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether to use index aliasing for zero-downtime rebuilds.
	/// </summary>
	/// <value>
	/// A value indicating whether to use index aliasing for zero-downtime rebuilds.
	/// </value>
	public bool UseAliasing { get; init; } = true;

	/// <summary>
	/// Gets the maximum degree of parallelism for the rebuild operation.
	/// </summary>
	/// <value>
	/// The maximum degree of parallelism for the rebuild operation.
	/// </value>
	public int MaxDegreeOfParallelism { get; init; } = 4;

	/// <summary>
	/// Gets additional metadata for the rebuild operation.
	/// </summary>
	/// <value>
	/// Additional metadata for the rebuild operation.
	/// </value>
	public IDictionary<string, object>? Metadata { get; init; }
}
