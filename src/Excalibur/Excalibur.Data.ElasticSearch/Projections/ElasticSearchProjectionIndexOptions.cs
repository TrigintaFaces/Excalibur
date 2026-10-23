// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
	/// Gets or sets a value indicating whether the store creates its index on initialization.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	/// <remarks>
	/// <para>
	/// <b>What it creates, so the alternative is a real choice.</b> The index is created with a DECLARED
	/// mapping — your projection's public properties, plus the reserved position field as a
	/// <c>long</c> — honouring the configured shard count, replica count and refresh interval. If
	/// creation fails the store RAISES rather than carrying on, because a store that continued would have
	/// the first write auto-create the index with a dynamically inferred mapping, discarding all of those
	/// settings while reporting healthy.
	/// </para>
	/// <para>
	/// <b>Set it to <see langword="false"/> only when you provision the index yourself.</b> With it off
	/// and the index absent, initialization refuses before issuing a document write. Existing index mappings
	/// are checked against the declared mapping. The
	/// position field is the one a dynamic mapping is most likely to get wrong, because the projection
	/// type does not declare it.
	/// </para>
	/// </remarks>
	public bool CreateIndexOnInitialize { get; set; } = true;

	/// <summary>
	/// Gets or sets the refresh interval for the index.
	/// </summary>
	/// <remarks>
	/// Set to "-1" to disable refresh, or a time value like "1s" for near real-time search.
	/// </remarks>
	/// <value>Defaults to "1s".</value>
	public string RefreshInterval { get; set; } = "1s";

	/// <summary>Gets or sets whether writes wait until their changes are searchable.</summary>
	/// <remarks>
	/// Defaults to false. GET-by-id is realtime regardless of this setting. When enabled, each write
	/// uses refresh=wait_for; automatic refresh must be enabled on the actual index. Prefer bulk writes
	/// for high throughput rather than sequential writes that each wait for a refresh.
	/// </remarks>
	public bool WaitForSearchVisibilityOnWrite { get; set; }

	/// <summary>Gets or sets whether a projection page becomes searchable before its checkpoint advances.</summary>
	/// <remarks>
	/// Defaults to false. The processor refreshes this index once after the page's folds, including
	/// folds skipped because another writer already applied them. Individual writes retain their normal
	/// refresh policy, avoiding one wait_for refresh interval per document. Use this option for
	/// request-driven or background projection pages; use WaitForSearchVisibilityOnWrite for direct writes
	/// that must become searchable before the individual write returns.
	/// </remarks>
	public bool EnsureSearchVisibilityOnProjectionCheckpoint { get; set; }


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
