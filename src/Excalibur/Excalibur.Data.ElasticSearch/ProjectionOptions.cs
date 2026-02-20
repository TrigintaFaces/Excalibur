// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures projection management settings for ElasticSearch read models.
/// </summary>
public sealed class ProjectionOptions
{
	/// <summary>
	/// Gets the index prefix for projection indices and lifecycle metadata.
	/// </summary>
	/// <value> The index prefix for projection-related indices. </value>
	public string IndexPrefix { get; init; } = "projections";

	/// <summary>
	/// Gets the error handling configuration for projections.
	/// </summary>
	/// <value> The error handling settings for projection operations. </value>
	public ProjectionErrorHandlingOptions ErrorHandling { get; init; } = new();

	/// <summary>
	/// Gets the retry policy for projection operations.
	/// </summary>
	/// <value> The retry settings specific to projection indexing. </value>
	public ProjectionRetryOptions RetryPolicy { get; init; } = new();

	/// <summary>
	/// Gets the consistency tracking configuration.
	/// </summary>
	/// <value> The settings for tracking eventual consistency between write and read models. </value>
	public ConsistencyTrackingOptions ConsistencyTracking { get; init; } = new();

	/// <summary>
	/// Gets the schema evolution configuration.
	/// </summary>
	/// <value> The settings for managing projection schema changes and migrations. </value>
	public SchemaEvolutionOptions SchemaEvolution { get; init; } = new();

	/// <summary>
	/// Gets the rebuild manager configuration.
	/// </summary>
	/// <value> The settings for rebuilding projections from event stores. </value>
	public RebuildManagerOptions RebuildManager { get; init; } = new();
}
