// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Data.DynamoDb.Caching;

/// <summary>
/// Configuration options for DynamoDB DAX (DynamoDB Accelerator) caching.
/// </summary>
public sealed class DaxCacheOptions
{
	/// <summary>
	/// Gets or sets the metadata resolver used to serialize cached items.
	/// </summary>
	/// <remarks>
	/// Supply a source-generated <see cref="JsonSerializerContext"/> for Native AOT.
	/// Context naming policies and converters are retained. A supplied resolver is authoritative:
	/// missing metadata never falls back to reflection. With no resolver, reflection is used only
	/// when supported and enabled; otherwise the first operation needing metadata fails clearly.
	/// Cache misses and capacity skips do not require metadata.
	/// </remarks>
	/// <value>The consumer's resolver, or <see langword="null"/> for supported reflection defaults.</value>
	public IJsonTypeInfoResolver? JsonTypeInfoResolver { get; set; }

	/// <summary>
	/// Gets or sets the DAX cluster endpoint URL.
	/// </summary>
	/// <value>The DAX cluster endpoint (e.g., "dax://my-cluster.region.dax-clusters.amazonaws.com").</value>
	[Required]
	public string ClusterEndpoint { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the default TTL for cached items.
	/// </summary>
	/// <value>The cache item TTL. Defaults to 5 minutes.</value>
	public TimeSpan CacheItemTtl { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the read consistency level for cache operations.
	/// </summary>
	/// <value>The <see cref="DaxReadConsistency"/>. Defaults to <see cref="DaxReadConsistency.Eventual"/>.</value>
	public DaxReadConsistency ReadConsistency { get; set; } = DaxReadConsistency.Eventual;

	/// <summary>
	/// Gets or sets the connection timeout for the DAX cluster.
	/// </summary>
	/// <value>The connection timeout. Defaults to 5 seconds.</value>
	public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the request timeout for individual DAX operations.
	/// </summary>
	/// <value>The request timeout. Defaults to 10 seconds.</value>
	public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
