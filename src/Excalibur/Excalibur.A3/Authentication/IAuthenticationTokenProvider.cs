// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authentication;

/// <summary>
/// Provides functionality to retrieve an authentication token representing the current user's identity and claims.
/// </summary>
public interface IAuthenticationTokenProvider
{
	/// <summary>
	/// Asynchronously retrieves the current authentication token.
	/// </summary>
	/// <param name="authenticationServiceEndpoint">The authentication service endpoint URL.</param>
	/// <param name="authenticationServiceAudience">The authentication service audience.</param>
	/// <param name="serviceAccountName">The service account name.</param>
	/// <param name="serviceAccountPrivateKeyPath">The path to the service account private key file.</param>
	/// <param name="serviceAccountPrivateKeyPassword">The password for the service account private key.</param>
	/// <returns>
	/// A <see cref="Task{TResult}" /> that resolves to an instance of <see cref="IAuthenticationToken" />, representing the current
	/// user's authentication details.
	/// </returns>
	/// <remarks>
	/// This method can be used to obtain information about the user's authentication state, identity, claims, and other related details
	/// through an implementation of <see cref="IAuthenticationToken" />.
	/// </remarks>
	Task<IAuthenticationToken> GetAuthenticationTokenAsync(
		string authenticationServiceEndpoint,
		string authenticationServiceAudience,
		string serviceAccountName,
		string serviceAccountPrivateKeyPath,
		string serviceAccountPrivateKeyPassword);
}
