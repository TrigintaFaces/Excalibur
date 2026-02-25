// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

using Excalibur.A3.Exceptions;

using IdentityModel;
using IdentityModel.Client;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Excalibur.A3.Authentication;

/// <summary>
/// Provides functionality to retrieve and manage authentication tokens.
/// </summary>
public sealed class AuthenticationTokenProvider(HttpClient httpClient, IMemoryCache cache) : IAuthenticationTokenProvider
{
#if NET9_0_OR_GREATER

	private static readonly Lock SyncRoot = new();

#else

	private static readonly object SyncRoot = new();

#endif
	private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(8);
	private static readonly TimeSpan TokenExpirationThreshold = TimeSpan.FromMinutes(10);

	/// <summary>
	/// Retrieves the authentication token for the current application context.
	/// </summary>
	/// <param name="authenticationServiceEndpoint">The authentication service endpoint URL.</param>
	/// <param name="authenticationServiceAudience">The authentication service audience.</param>
	/// <param name="serviceAccountName">The service account name.</param>
	/// <param name="serviceAccountPrivateKeyPath">The path to the service account private key file.</param>
	/// <param name="serviceAccountPrivateKeyPassword">The password for the service account private key.</param>
	/// <returns> An instance of <see cref="IAuthenticationToken" /> representing the authentication token. </returns>
	public Task<IAuthenticationToken> GetAuthenticationTokenAsync(
		string authenticationServiceEndpoint,
		string authenticationServiceAudience,
		string serviceAccountName,
		string serviceAccountPrivateKeyPath,
		string serviceAccountPrivateKeyPassword)
	{
		var authenticationToken = cache.Get<AuthenticationToken>(serviceAccountName);

		if (authenticationToken?.WillExpireBy(DateTimeOffset.UtcNow.Add(TokenExpirationThreshold).UtcDateTime) != false)
		{
			lock (SyncRoot)
			{
				authenticationToken = cache.Get<AuthenticationToken>(serviceAccountName);

				if (authenticationToken?.WillExpireBy(DateTimeOffset.UtcNow.Add(TokenExpirationThreshold).UtcDateTime) != false)
				{
					var jwt = RequestTokenAsync(
							authenticationServiceEndpoint,
							authenticationServiceAudience,
							serviceAccountName,
							serviceAccountPrivateKeyPath,
							serviceAccountPrivateKeyPassword)
						.ConfigureAwait(false)
						.GetAwaiter()
						.GetResult();

					authenticationToken = cache.Set(
						serviceAccountName,
						new AuthenticationToken(jwt),
						DateTimeOffset.UtcNow.Add(TokenLifetime));
				}
			}
		}

		return Task.FromResult<IAuthenticationToken>(authenticationToken);
	}

	/// <summary>
	/// Creates a client assertion token using the provided certificate and audience.
	/// </summary>
	/// <param name="certificate"> The X509 certificate to sign the token. </param>
	/// <param name="clientId"> The client identifier for the token. </param>
	/// <param name="audience"> The audience for the token. </param>
	/// <returns> A string representation of the signed JWT. </returns>
	private static string CreateClientToken(X509Certificate2 certificate, string clientId, string audience)
	{
		var now = DateTimeOffset.UtcNow;
		var token = new JwtSecurityToken(
			clientId,
			audience,
			new List<Claim>
			{
				new(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture)),
				new(JwtClaimTypes.Subject, clientId),
				new(JwtClaimTypes.IssuedAt, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
			},
			now.UtcDateTime,
			now.Add(TokenLifetime).UtcDateTime,
			new SigningCredentials(new X509SecurityKey(certificate), SecurityAlgorithms.RsaSha256));

		var tokenHandler = new JwtSecurityTokenHandler();

		return tokenHandler.WriteToken(token);
	}

	/// <summary>
	/// Sends a request to the authentication server to obtain a new JWT token.
	/// </summary>
	/// <returns> A <see cref="JwtSecurityToken" /> representing the new token. </returns>
	/// <exception cref="NotAuthenticatedException"> Thrown if the discovery document or token request fails. </exception>
	/// <exception cref="InvalidOperationException"></exception>
	private async Task<JwtSecurityToken> RequestTokenAsync(
		string authenticationServiceEndpoint,
		string authenticationServiceAudience,
		string serviceAccountName,
		string serviceAccountPrivateKeyPath,
		string serviceAccountPrivateKeyPassword)
	{
		var disco = await httpClient.GetDiscoveryDocumentAsync(authenticationServiceEndpoint).ConfigureAwait(false)
					?? throw new InvalidOperationException("Discovery document is null.");

		NotAuthenticatedException.ThrowIf(
			disco.IsError && (int)disco.HttpStatusCode >= 100 && (int)disco.HttpStatusCode <= 599,
			(int)disco.HttpStatusCode,
			disco.Error,
			disco.Exception);

		NotAuthenticatedException.ThrowIf(
			disco.IsError,
			500, $"{disco.Error}\t(No HTTP status returned)",
			disco.Exception);

#if NET9_0_OR_GREATER
		using var certificate = X509CertificateLoader.LoadPkcs12FromFile(serviceAccountPrivateKeyPath, serviceAccountPrivateKeyPassword);
#else
		using var certificate = new X509Certificate2(serviceAccountPrivateKeyPath, serviceAccountPrivateKeyPassword);
#endif
		var clientToken = CreateClientToken(certificate, serviceAccountName,
			disco.TokenEndpoint ?? throw new InvalidOperationException("Discovery document TokenEndpoint is null."));

		using var tokenRequest = new ClientCredentialsTokenRequest
		{
			Address = disco.TokenEndpoint,
			Scope = authenticationServiceAudience,
			ClientAssertion = { Type = OidcConstants.ClientAssertionTypes.JwtBearer, Value = clientToken },
			ClientCredentialStyle = ClientCredentialStyle.PostBody,
		};

		var response = await httpClient.RequestClientCredentialsTokenAsync(tokenRequest).ConfigureAwait(false);

		// Check for errors
		NotAuthenticatedException.ThrowIf(response.IsError, (int)response.HttpStatusCode, response.Error, response.Exception);

		return new JwtSecurityToken(response.AccessToken);
	}
}
