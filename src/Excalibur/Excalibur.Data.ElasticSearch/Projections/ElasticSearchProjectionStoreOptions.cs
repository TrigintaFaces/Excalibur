// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Configuration options for the ElasticSearch projection store.
/// </summary>
/// <remarks>
/// <para>
/// Supports connection configuration for ElasticSearch clusters with standard
/// Elastic.Clients.Elasticsearch settings. Each projection type gets its own
/// dedicated index for optimal query performance and mapping isolation.
/// </para>
/// </remarks>
public sealed class ElasticSearchProjectionStoreOptions
{
	/// <summary>
	/// Gets or sets the ElasticSearch node URI.
	/// </summary>
	/// <value>Defaults to "http://localhost:9200".</value>
	public string NodeUri { get; set; } = "http://localhost:9200";

	/// <summary>
	/// Gets or sets the index name prefix for projection indices.
	/// </summary>
	/// <remarks>
	/// The full index name is composed as: <c>{IndexPrefix}-{projectionTypeName}</c>
	/// where the projection type name is lowercased.
	/// </remarks>
	/// <value>Defaults to "projections".</value>
	public string IndexPrefix { get; set; } = "projections";

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
	/// Gets or sets a value indicating whether to disable direct streaming.
	/// </summary>
	/// <remarks>
	/// When enabled, request and response bodies are buffered for debugging purposes.
	/// Should be disabled in production for performance.
	/// </remarks>
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

		if (string.IsNullOrWhiteSpace(IndexPrefix))
		{
			throw new InvalidOperationException("IndexPrefix is required.");
		}

		if (!Uri.TryCreate(NodeUri, UriKind.Absolute, out _))
		{
			throw new InvalidOperationException($"NodeUri '{NodeUri}' is not a valid URI.");
		}
	}
}
