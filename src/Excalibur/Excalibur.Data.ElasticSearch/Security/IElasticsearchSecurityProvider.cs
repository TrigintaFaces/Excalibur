// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for Elasticsearch security operations including authentication,
/// authorization, and security context management.
/// </summary>
/// <remarks>
/// <para>
/// Reference: <c>Microsoft.AspNetCore.Authentication.IAuthenticationService</c> pattern --
/// minimal interface (3 methods) for core security operations. Advanced capabilities
/// (encryption, key management, auditing) are accessed via the existing specialized interfaces:
/// <see cref="IElasticsearchAuthenticationProvider"/>, <see cref="Encryption.IElasticsearchFieldEncryptor"/>,
/// <see cref="KeyManagement.IElasticsearchKeyProvider"/>.
/// </para>
/// </remarks>
public interface IElasticsearchSecurityProvider
{
	/// <summary>
	/// Authenticates a request against the configured Elasticsearch security backend.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The authentication result indicating success or failure.</returns>
	Task<AuthenticationResult> AuthenticateAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Authorizes a data access operation against the configured security policies.
	/// </summary>
	/// <param name="operation">The data access operation to authorize.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the operation is authorized; otherwise, <see langword="false"/>.</returns>
	Task<bool> AuthorizeAsync(DataAccessOperation operation, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves the current security context including authentication state and active policies.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The current <see cref="ElasticsearchSecurityContext"/>.</returns>
	Task<ElasticsearchSecurityContext> GetSecurityContextAsync(CancellationToken cancellationToken);
}
