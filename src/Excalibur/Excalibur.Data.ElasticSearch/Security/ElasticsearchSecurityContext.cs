// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the current Elasticsearch security context including authentication state
/// and active security policies.
/// </summary>
public sealed class ElasticsearchSecurityContext
{
	/// <summary>
	/// Gets or sets a value indicating whether the current session is authenticated.
	/// </summary>
	/// <value><see langword="true"/> if authenticated; otherwise, <see langword="false"/>.</value>
	public bool IsAuthenticated { get; set; }

	/// <summary>
	/// Gets or sets the authenticated principal identity.
	/// </summary>
	/// <value>The principal identity, or <see langword="null"/> if not authenticated.</value>
	public string? PrincipalId { get; set; }

	/// <summary>
	/// Gets or sets the active security mode.
	/// </summary>
	/// <value>The current <see cref="SecurityMode"/>.</value>
	public SecurityMode SecurityMode { get; set; }

	/// <summary>
	/// Gets or sets the authentication type in use.
	/// </summary>
	/// <value>The <see cref="ElasticsearchAuthenticationType"/>.</value>
	public ElasticsearchAuthenticationType AuthenticationType { get; set; }

	/// <summary>
	/// Gets or sets the time the security context was established.
	/// </summary>
	/// <value>The timestamp when the context was created.</value>
	public DateTimeOffset EstablishedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the time the security context expires.
	/// </summary>
	/// <value>The expiration timestamp, or <see langword="null"/> if no expiration.</value>
	public DateTimeOffset? ExpiresAt { get; set; }

	/// <summary>
	/// Gets the collection of active security policies applied to this context.
	/// </summary>
	/// <value>The set of active security policy names.</value>
	public IReadOnlySet<string> ActivePolicies { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}
