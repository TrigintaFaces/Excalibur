// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
	public string NodeUri { get; set; } = string.Empty;

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
	/// Gets or sets a value indicating whether the store creates its index on initialization.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	/// <remarks>
	/// <para>
	/// <b>What it creates, so the alternative is a real choice.</b> The index is created with a DECLARED
	/// mapping -- your projection's public properties, plus the reserved position field as a
	/// <c>long</c> -- honouring the configured shard count, replica count and refresh interval.
	/// </para>
	/// <para>
	/// <b>Set it to <see langword="false"/> only when you provision the index yourself.</b> With it off
	/// and the index absent, the first write makes the cluster auto-create it with a dynamically inferred
	/// mapping, discarding those settings -- or fails outright on a cluster configured with
	/// <c>action.auto_create_index: false</c>. The position field is the one a dynamic mapping is most
	/// likely to get wrong, because the projection type does not declare it.
	/// </para>
	/// </remarks>
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
	/// Validates the options and throws if invalid.
	/// </summary>
	public void Validate()
	{
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
					"No OpenSearch node is configured for this projection store. Set NodeUris to the "
					+ "cluster's node addresses, or NodeUri to a single node. There is no default: a "
					+ "cluster nobody configured is refused rather than silently addressed locally.");
			}

			if (!Uri.TryCreate(NodeUri, UriKind.Absolute, out _))
			{
				throw new InvalidOperationException($"NodeUri '{NodeUri}' is not a valid URI.");
			}
		}
	}
}
