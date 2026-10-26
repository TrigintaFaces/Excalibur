// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
	/// <summary>Gets or sets generated JSON metadata for projection documents. Required for native AOT.</summary>
	/// <value>The application source-generated resolver, or null for the default serializer.</value>
	public System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver? SourceTypeInfoResolver { get; set; }

	/// <summary>
	/// Gets or sets the ElasticSearch node URI for single-node configuration.
	/// </summary>
	/// <remarks>
	/// Ignored when <see cref="NodeUris"/> is set with one or more URIs. Set this or
	/// <see cref="NodeUris"/>: there is no default, so a cluster nobody configured is refused at
	/// start-up instead of being silently addressed on the local machine.
	/// </remarks>
	/// <value>Empty until configured. Required unless <see cref="NodeUris"/> is set.</value>
	public string NodeUri { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the cluster node URIs for multi-node configuration.
	/// </summary>
	/// <remarks>
	/// When set with one or more URIs, overrides <see cref="NodeUri"/> and uses a
	/// connection pool. The pool type is determined by <see cref="ConnectionPoolType"/>.
	/// </remarks>
	/// <value>Defaults to <see langword="null"/> (single-node via <see cref="NodeUri"/>).</value>
	public List<Uri>? NodeUris { get; set; }

	/// <summary>
	/// Gets or sets the connection pool type for multi-node clusters.
	/// </summary>
	/// <remarks>
	/// Only used when <see cref="NodeUris"/> is set with one or more URIs.
	/// </remarks>
	/// <value>Defaults to <see cref="ElasticSearch.ConnectionPoolType.Static"/>.</value>
	public ConnectionPoolType ConnectionPoolType { get; set; } = ConnectionPoolType.Static;

	/// <summary>
	/// Gets or sets the request timeout in seconds.
	/// </summary>
	/// <value>Defaults to 30 seconds.</value>
	public int RequestTimeoutSeconds { get; set; } = 30;

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
	/// Gets or sets the authentication options for the ElasticSearch connection.
	/// </summary>
	/// <value>Authentication options including username/password and API key.</value>
	public ElasticSearchProjectionAuthOptions Auth { get; set; } = new();

	/// <summary>
	/// Gets or sets the index configuration options for projection indices.
	/// </summary>
	/// <value>Index naming, shard/replica, refresh, and mapping-convention options.</value>
	public ElasticSearchProjectionIndexOptions Index { get; set; } = new();

	/// <summary>
	/// Whether the registration supplies the client itself, so no node address is read from here.
	/// </summary>
	/// <remarks>
	/// Set by the client-factory registration, which KNOWS it hands the store a client. Without it the
	/// required-node check refuses a correct registration: the node address is a fallback used only by
	/// the paths that build a client, not a requirement of a path that is given one. Internal and not
	/// settable from configuration on purpose -- it records how the store was registered, which is not a
	/// consumer setting and must not be bindable.
	/// </remarks>
	internal bool ClientSuppliedByRegistration { get; set; }

	/// <summary>
	/// Copies these options for one shard, replacing only the shard's own coordinates.
	/// </summary>
	/// <param name="nodeUri">The shard's node address, which replaces the configured cluster.</param>
	/// <param name="indexPrefix">The shard's index prefix.</param>
	/// <returns>A new options instance carrying every other configured value.</returns>
	/// <remarks>
	/// <para>
	/// WHY THIS LIVES HERE, beside the properties it copies. A shard store used to be built from a
	/// freshly-constructed options object carrying nothing but these two coordinates, which silently
	/// dropped every other registered setting -- the index NAME most damagingly, because two read models
	/// that had been given distinct names to keep them apart went back to sharing one index per shard.
	/// It also dropped replica and shard counts, the refresh interval, the search-visibility flags, the
	/// mapping convention and the credential. A copy written next to the declarations is one a new
	/// property's author can see; one written in the sharding package is not.
	/// </para>
	/// <para>
	/// <c>NodeUris</c> is cleared deliberately: it takes precedence over <c>NodeUri</c>, so leaving a
	/// configured cluster's node list in place would send every shard back to the default cluster and the
	/// shard's own address would never be used.
	/// </para>
	/// </remarks>
	internal ElasticSearchProjectionStoreOptions CloneForShard(string nodeUri, string indexPrefix) => new()
	{
		// The shard's own coordinates.
		NodeUri = nodeUri,
		NodeUris = null,

		// Everything else the consumer configured, carried across.
		SourceTypeInfoResolver = SourceTypeInfoResolver,
		ConnectionPoolType = ConnectionPoolType,
		RequestTimeoutSeconds = RequestTimeoutSeconds,
		EnableDebugMode = EnableDebugMode,
		Auth = new ElasticSearchProjectionAuthOptions
		{
			ApiKey = Auth.ApiKey,
			Username = Auth.Username,
			Password = Auth.Password,
		},
		Index = new ElasticSearchProjectionIndexOptions
		{
			IndexPrefix = indexPrefix,
			IndexName = Index.IndexName,
			NumberOfShards = Index.NumberOfShards,
			NumberOfReplicas = Index.NumberOfReplicas,
			CreateIndexOnInitialize = Index.CreateIndexOnInitialize,
			RefreshInterval = Index.RefreshInterval,
			WaitForSearchVisibilityOnWrite = Index.WaitForSearchVisibilityOnWrite,
			EnsureSearchVisibilityOnProjectionCheckpoint = Index.EnsureSearchVisibilityOnProjectionCheckpoint,
			IndexMappingConvention = Index.IndexMappingConvention,
		},
	};

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (Index.WaitForSearchVisibilityOnWrite && Index.RefreshInterval.Trim() == "-1")
		{
			throw new InvalidOperationException("WaitForSearchVisibilityOnWrite requires an enabled refresh interval.");
		}
		// A supplied client makes the node address dead, so neither requiring nor parsing one is
		// meaningful. The checks below are all about the node address; nothing else is skipped.
		if (ClientSuppliedByRegistration)
		{
			return;
		}

		if (NodeUris is { Count: > 0 })
		{
			foreach (var uri in NodeUris)
			{
				if (uri is null || !uri.IsAbsoluteUri)
				{
					throw new InvalidOperationException(
						$"All entries in NodeUris must be valid absolute URIs. Invalid: '{uri}'.");
				}
			}
		}
		else
		{
			if (string.IsNullOrWhiteSpace(NodeUri))
			{
				throw new InvalidOperationException(
					"No ElasticSearch node is configured for this projection store. Set NodeUris to the "
					+ "cluster's node addresses, or NodeUri to a single node -- either in code, or by "
					+ "binding a configuration section with AddElasticSearchProjections(section, ...).");
			}

			if (!Uri.TryCreate(NodeUri, UriKind.Absolute, out _))
			{
				throw new InvalidOperationException($"NodeUri '{NodeUri}' is not a valid URI.");
			}
		}

	}
}
