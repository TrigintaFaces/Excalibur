using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

using Excalibur.A3.Exceptions;
using Excalibur.Domain;

using IdentityModel;
using IdentityModel.Client;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Excalibur.A3.Authentication;

/// <summary>
///     Provides functionality to retrieve and manage authentication tokens.
/// </summary>
public class AuthenticationTokenProvider(HttpClient httpClient, IMemoryCache cache) : IAuthenticationTokenProvider
{
	private static readonly object SyncRoot = new();
	private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(8);
	private static readonly TimeSpan TokenExpirationThreshold = TimeSpan.FromMinutes(10);

	/// <summary>
	///     Retrieves the authentication token for the current application context.
	/// </summary>
	/// <returns> An instance of <see cref="IAuthenticationToken" /> representing the authentication token. </returns>
	public Task<IAuthenticationToken> GetAuthenticationTokenAsync()
	{
		var authenticationToken = cache.Get<AuthenticationToken>(ApplicationContext.ServiceAccountName);

		if (authenticationToken == null || authenticationToken.WillExpireBy(DateTime.UtcNow.Add(TokenExpirationThreshold)))
		{
			lock (SyncRoot)
			{
				authenticationToken = cache.Get<AuthenticationToken>(ApplicationContext.ServiceAccountName);

				if (authenticationToken == null || authenticationToken.WillExpireBy(DateTime.UtcNow.Add(TokenExpirationThreshold)))
				{
					var jwt = RequestToken()
						.ConfigureAwait(false)
						.GetAwaiter()
						.GetResult();

					authenticationToken = cache.Set(
						ApplicationContext.ServiceAccountName,
						new AuthenticationToken(jwt),
						DateTimeOffset.UtcNow.Add(TokenLifetime));
				}
			}
		}

		return Task.FromResult<IAuthenticationToken>(authenticationToken);
	}

	/// <summary>
	///     Creates a client assertion token using the provided certificate and audience.
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
				new(JwtClaimTypes.IssuedAt, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
			},
			now.UtcDateTime,
			now.Add(TokenLifetime).UtcDateTime,
			new SigningCredentials(new X509SecurityKey(certificate), SecurityAlgorithms.RsaSha256));

		var tokenHandler = new JwtSecurityTokenHandler();

		return tokenHandler.WriteToken(token);
	}

	/// <summary>
	///     Sends a request to the authentication server to obtain a new JWT token.
	/// </summary>
	/// <returns> A <see cref="JwtSecurityToken" /> representing the new token. </returns>
	/// <exception cref="NotAuthenticatedException"> Thrown if the discovery document or token request fails. </exception>
	private async Task<JwtSecurityToken> RequestToken()
	{
		var disco = (await httpClient.GetDiscoveryDocumentAsync(ApplicationContext.AuthenticationServiceEndpoint).ConfigureAwait(false))
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

		var clientId = ApplicationContext.ServiceAccountName;
		var certPath = ApplicationContext.ServiceAccountPrivateKeyPath;
		var certPassword = ApplicationContext.ServiceAccountPrivateKeyPassword;

		using var certificate = new X509Certificate2(certPath, certPassword);
		var clientToken = CreateClientToken(certificate, clientId,
			disco.TokenEndpoint ?? throw new InvalidOperationException("Discovery document TokenEndpoint is null."));

		using var tokenRequest = new ClientCredentialsTokenRequest
		{
			Address = disco.TokenEndpoint,
			ClientId = clientId,
			Scope = ApplicationContext.AuthenticationServiceAudience,
			ClientAssertion = { Type = OidcConstants.ClientAssertionTypes.JwtBearer, Value = clientToken }
		};

		var response = await httpClient.RequestClientCredentialsTokenAsync(tokenRequest).ConfigureAwait(false);

		// Check for errors
		NotAuthenticatedException.ThrowIf(response.IsError, (int)response.HttpStatusCode, response.Error, response.Exception);

		return new JwtSecurityToken(response.AccessToken);
	}
}
