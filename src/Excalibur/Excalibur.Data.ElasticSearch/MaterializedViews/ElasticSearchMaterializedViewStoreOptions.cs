// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.MaterializedViews;

/// <summary>
/// Configuration options for <see cref="ElasticSearchMaterializedViewStore"/>.
/// </summary>
public sealed class ElasticSearchMaterializedViewStoreOptions
{
	/// <summary>
	/// Default views index name.
	/// </summary>
	public const string DefaultViewsIndexName = "materialized-views";

	/// <summary>
	/// Default positions index name.
	/// </summary>
	public const string DefaultPositionsIndexName = "materialized-view-positions";

	/// <summary>
	/// Gets or sets the Elasticsearch node URI.
	/// </summary>
	/// <value>Defaults to "http://localhost:9200".</value>
	public string NodeUri { get; set; } = "http://localhost:9200";

	/// <summary>
	/// Gets or sets the index name for materialized views.
	/// </summary>
	/// <value>Defaults to "materialized-views".</value>
	public string ViewsIndexName { get; set; } = DefaultViewsIndexName;

	/// <summary>
	/// Gets or sets the index name for position tracking.
	/// </summary>
	/// <value>Defaults to "materialized-view-positions".</value>
	public string PositionsIndexName { get; set; } = DefaultPositionsIndexName;

	/// <summary>
	/// Gets or sets the request timeout in seconds.
	/// </summary>
	/// <value>Defaults to 30 seconds.</value>
	public int RequestTimeoutSeconds { get; set; } = 30;

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
	/// Gets or sets a value indicating whether to create indices on initialization.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateIndexOnInitialize { get; set; } = true;

	/// <summary>
	/// Gets or sets the refresh interval for the indices.
	/// </summary>
	/// <value>Defaults to "1s".</value>
	public string RefreshInterval { get; set; } = "1s";

	/// <summary>
	/// Gets or sets the username for basic authentication.
	/// </summary>
	/// <value>Defaults to <see langword="null"/> (no authentication).</value>
	public string? Username { get; set; }

	/// <summary>
	/// Gets or sets the password for basic authentication.
	/// </summary>
	/// <value>Defaults to <see langword="null"/> (no authentication).</value>
	public string? Password { get; set; }

	/// <summary>
	/// Gets or sets the API key for API key authentication.
	/// </summary>
	/// <value>Defaults to <see langword="null"/> (no API key authentication).</value>
	public string? ApiKey { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable debug mode.
	/// </summary>
	/// <value>Defaults to <see langword="false"/>.</value>
	public bool EnableDebugMode { get; set; }

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(NodeUri))
		{
			throw new InvalidOperationException("NodeUri is required.");
		}

		if (!Uri.TryCreate(NodeUri, UriKind.Absolute, out _))
		{
			throw new InvalidOperationException($"NodeUri '{NodeUri}' is not a valid URI.");
		}

		if (string.IsNullOrWhiteSpace(ViewsIndexName))
		{
			throw new InvalidOperationException("ViewsIndexName is required.");
		}

		if (string.IsNullOrWhiteSpace(PositionsIndexName))
		{
			throw new InvalidOperationException("PositionsIndexName is required.");
		}
	}
}
