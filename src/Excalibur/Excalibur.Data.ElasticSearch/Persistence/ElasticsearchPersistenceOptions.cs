// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.ElasticSearch.Persistence;

/// <summary>
/// Configuration options for the Elasticsearch persistence provider.
/// </summary>
public sealed class ElasticsearchPersistenceOptions
{
	/// <summary>
	/// Gets or sets the provider name. This is the consumer-configured instance name, reported as
	/// <c>Name</c> on the provider and in its metrics. It identifies <i>which configured instance</i>
	/// answered, and is distinct from the engine identity reported under the <c>Provider</c> metrics
	/// key, which is a fixed literal. When unset it defaults to <c>"elasticsearch"</c>.
	/// </summary>
	/// <value>The provider name, or <see langword="null"/> to use the default.</value>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the prefix for index names created by this provider.
	/// </summary>
	/// <value>The index name prefix. Defaults to "excalibur-".</value>
	public string IndexPrefix { get; set; } = "excalibur-";

	/// <summary>
	/// Gets or sets the refresh policy to apply after write operations.
	/// </summary>
	/// <value>
	/// The refresh policy. Defaults to <see cref="ElasticsearchRefreshPolicy.WaitFor"/>.
	/// </value>
	public ElasticsearchRefreshPolicy RefreshPolicy { get; set; } = ElasticsearchRefreshPolicy.WaitFor;

	/// <summary>
	/// Gets or sets the number of primary shards for new indices.
	/// </summary>
	/// <value>The number of primary shards. Defaults to 1.</value>
	[Range(1, 1024)]
	public int NumberOfShards { get; set; } = 1;

	/// <summary>
	/// Gets or sets the number of replica shards for new indices.
	/// </summary>
	/// <value>The number of replica shards. Defaults to 1.</value>
	[Range(0, 10)]
	public int NumberOfReplicas { get; set; } = 1;

	/// <summary>
	/// Gets or sets the maximum number of results returned per query.
	/// </summary>
	/// <value>The maximum result count. Defaults to 1000.</value>
	[Range(1, 10000)]
	public int MaxResultCount { get; set; } = 1000;
}
