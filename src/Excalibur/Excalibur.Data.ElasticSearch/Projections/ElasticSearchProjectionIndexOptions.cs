// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Index configuration options for the ElasticSearch projection store.
/// </summary>
/// <remarks>
/// Controls index naming, shard/replica layout, refresh behavior, and the mapping
/// convention applied when creating projection indices.
/// </remarks>
public sealed class ElasticSearchProjectionIndexOptions
{
	/// <summary>
	/// Gets or sets the index name prefix for projection indices.
	/// </summary>
	/// <remarks>
	/// The full index name is composed as: <c>{IndexPrefix}-{name}</c> where <c>name</c>
	/// is either <see cref="IndexName"/> (if set) or the lowercased projection type name.
	/// Set to empty or whitespace to omit the prefix entirely.
	/// </remarks>
	/// <value>Defaults to "projections".</value>
	public string IndexPrefix { get; set; } = "projections";

	/// <summary>
	/// Gets or sets the index name override for this projection store.
	/// </summary>
	/// <remarks>
	/// When set, replaces the projection type name in the index naming convention.
	/// The full index name becomes <c>{IndexPrefix}-{IndexName}</c> when both are set,
	/// or just <c>{IndexName}</c> when <see cref="IndexPrefix"/> is empty.
	/// When not set, the default <c>{IndexPrefix}-{projectionTypeName}</c> convention applies.
	/// </remarks>
	/// <value>Defaults to <see langword="null"/> (uses projection type name).</value>
	public string? IndexName { get; set; }

	/// <summary>
	/// Gets or sets the number of shards for new indices.
	/// </summary>
	/// <value>Defaults to 1.</value>
	public int NumberOfShards { get; set; } = 1;

	/// <summary>
	/// Gets or sets the number of replicas for new indices.
	/// </summary>
	/// <value>Defaults to 0.</value>
	public int NumberOfReplicas { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to create the index on initialization.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateIndexOnInitialize { get; set; } = true;

	/// <summary>
	/// Gets or sets the refresh interval for the index.
	/// </summary>
	/// <remarks>
	/// Set to "-1" to disable refresh, or a time value like "1s" for near real-time search.
	/// </remarks>
	/// <value>Defaults to "1s".</value>
	public string RefreshInterval { get; set; } = "1s";

	/// <summary>
	/// Gets or sets the index mapping convention for customizing how CLR types
	/// are mapped to Elasticsearch field types during index creation.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When <see langword="null"/> (default), the framework applies the standard
	/// <see cref="DefaultIndexMappingConvention"/> which maps CLR types to Elasticsearch
	/// types via reflection (string → keyword, int → long, DateTime → date, etc.).
	/// </para>
	/// <para>
	/// Set this to a custom <see cref="IIndexMappingConvention"/> to override the default
	/// behavior — for example, mapping strings as <c>text</c> fields with keyword sub-fields,
	/// or applying custom analyzers to specific property patterns.
	/// </para>
	/// </remarks>
	/// <value>Defaults to <see langword="null"/> (uses <see cref="DefaultIndexMappingConvention"/>).</value>
	public IIndexMappingConvention? IndexMappingConvention { get; set; }
}
