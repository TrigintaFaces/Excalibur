// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.OpenSearch.Projections;

/// <summary>
/// Configuration options for the OpenSearch projection store.
/// </summary>
/// <remarks>
/// <para>
/// Supports connection configuration for OpenSearch clusters.
/// Each projection type gets its own dedicated index for optimal query performance.
/// </para>
/// </remarks>
public sealed class OpenSearchProjectionStoreOptions
{
	/// <summary>
	/// Gets or sets the OpenSearch node URI for single-node configuration.
	/// </summary>
	public string NodeUri { get; set; } = "https://localhost:9200";

	/// <summary>
	/// Gets or sets the index name prefix. Default: "projections".
	/// </summary>
	public string IndexPrefix { get; set; } = "projections";

	/// <summary>
	/// Gets or sets the request timeout in seconds.
	/// </summary>
	public int RequestTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the number of shards for new indices.
	/// </summary>
	public int NumberOfShards { get; set; } = 1;

	/// <summary>
	/// Gets or sets the number of replicas for new indices.
	/// </summary>
	public int NumberOfReplicas { get; set; }

	/// <summary>
	/// Gets or sets whether to create the index on initialization. Default: true.
	/// </summary>
	public bool CreateIndexOnInitialize { get; set; } = true;

	/// <summary>
	/// Gets or sets the refresh interval for the index.
	/// </summary>
	public string RefreshInterval { get; set; } = "1s";

	/// <summary>
	/// Gets or sets the index name override for this projection store.
	/// </summary>
	public string? IndexName { get; set; }

	/// <summary>
	/// Gets or sets the cluster node URIs for multi-node configuration.
	/// </summary>
	public List<Uri>? NodeUris { get; set; }

	/// <summary>
	/// Gets or sets whether to enable debug mode (buffered request/response bodies).
	/// </summary>
	public bool EnableDebugMode { get; set; }

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	public void Validate()
	{
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
				throw new InvalidOperationException("NodeUri is required when NodeUris is not set.");
			}

			if (!Uri.TryCreate(NodeUri, UriKind.Absolute, out _))
			{
				throw new InvalidOperationException($"NodeUri '{NodeUri}' is not a valid URI.");
			}
		}
	}
}
