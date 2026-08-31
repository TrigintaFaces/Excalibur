// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.OpenSearch;

/// <summary>
/// Configures the connection target for the OpenSearch client this package registers.
/// </summary>
/// <remarks>
/// Bound by <c>AddExcaliburOpenSearch(os =&gt; os.BindConfiguration(...))</c>. Exactly one of
/// <see cref="Url" /> or <see cref="Urls" /> must be set; <see cref="Urls" /> selects a
/// round-robin static connection pool. Retry, timeout, and circuit-breaker behaviour are
/// configured on the <c>ConnectionSettings</c> you supply to the client, not here.
/// </remarks>
public sealed class OpenSearchConfigurationOptions
{
	/// <summary>
	/// Gets the URL of the OpenSearch cluster.
	/// </summary>
	/// <value>
	/// A <see cref="Uri" /> representing the base URL of the OpenSearch cluster. This property is required when using single-node configuration.
	/// </value>
	public Uri? Url { get; init; }

	/// <summary>
	/// Gets the collection of URLs for multi-node cluster configuration.
	/// </summary>
	/// <value>
	/// A collection of <see cref="Uri" /> representing the URLs of the OpenSearch cluster nodes. Used for cluster configuration with
	/// connection pooling.
	/// </value>
	public IEnumerable<Uri>? Urls { get; init; }
}
