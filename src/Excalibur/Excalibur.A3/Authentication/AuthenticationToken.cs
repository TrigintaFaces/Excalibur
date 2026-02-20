// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Excalibur.A3.Exceptions;

namespace Excalibur.A3.Authentication;

/// <summary>
/// Represents an authentication token and provides methods to access claims and related information.
/// </summary>
public class AuthenticationToken : IAuthenticationToken
{
	/// <summary>
	/// Represents an anonymous authentication token.
	/// </summary>
	public static readonly IAuthenticationToken Anonymous = new AuthenticationToken();

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthenticationToken" /> class with a default empty JWT.
	/// </summary>
	public AuthenticationToken() => Jwt = new JwtSecurityToken();

	/// <summary>
	/// Initializes a new instance of the <see cref="AuthenticationToken" /> class with the specified JWT.
	/// </summary>
	/// <param name="jwt"> The JWT to use for this authentication token. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="jwt" /> is null. </exception>
	/// <exception cref="NotAuthenticatedException"> Thrown if required claims are missing. </exception>
	internal AuthenticationToken(JwtSecurityToken jwt)
	{
		ArgumentNullException.ThrowIfNull(jwt);

		if (string.IsNullOrEmpty(jwt.Claims.FirstOrDefault(static x => x.Type.Equals(AuthClaims.Upn, StringComparison.Ordinal))?.Value))
		{
			throw NotAuthenticatedException.BecauseMissingClaim(AuthClaims.Upn);
		}

		if (string.IsNullOrEmpty(jwt.Claims.FirstOrDefault(static x => x.Type.Equals(AuthClaims.Name, StringComparison.Ordinal))?.Value))
		{
			throw NotAuthenticatedException.BecauseMissingClaim(AuthClaims.Name);
		}

		if (string.IsNullOrEmpty(jwt.Claims.FirstOrDefault(static x => x.Type.Equals(AuthClaims.Email, StringComparison.Ordinal))?.Value))
		{
			throw NotAuthenticatedException.BecauseMissingClaim(AuthClaims.Email);
		}

		Jwt = jwt;
		Claims = jwt.Claims;
	}

	/// <summary>
	/// Gets or sets the authentication state associated with this token.
	/// </summary>
	/// <value>The authentication state associated with this token.</value>
	public AuthenticationState AuthenticationState { get; set; }

	/// <summary>
	/// Gets or sets the underlying JWT for this authentication token.
	/// </summary>
	/// <value>The underlying JWT security token, or <see langword="null"/> if not set.</value>
	public JwtSecurityToken? Jwt { get; protected set; }

	/// <inheritdoc />
	string? IAuthenticationToken.Jwt
	{
		get => Jwt?.RawData;
		set => Jwt = string.IsNullOrEmpty(value) ? new JwtSecurityToken() : new JwtSecurityToken(value);
	}

	/// <summary>
	/// Gets or sets the claims for this authentication token.
	/// </summary>
	/// <value>The collection of claims, or <see langword="null"/> if no claims are set.</value>
	public IEnumerable<Claim>? Claims { get; protected set; }

	/// <summary>
	/// Gets the first name claim value from the JWT, if available.
	/// </summary>
	/// <value>The first name from the JWT claims, or <see langword="null"/> if not available.</value>
	public string? FirstName =>
		Jwt?.Claims.FirstOrDefault(static x => x.Type.Equals(AuthClaims.GivenName, StringComparison.Ordinal))?.Value;

	/// <summary>
	/// Gets the last name claim value from the JWT, if available.
	/// </summary>
	/// <value>The last name from the JWT claims, or <see langword="null"/> if not available.</value>
	public string? LastName =>
		Jwt?.Claims.FirstOrDefault(static x => x.Type.Equals(AuthClaims.FamilyName, StringComparison.Ordinal))?.Value;

	/// <summary>
	/// Gets the full name claim value from the JWT, or "Anonymous" if not available.
	/// </summary>
	/// <value>The full name from the JWT claims, or "Anonymous" if not available.</value>
	public string FullName =>
		Jwt?.Claims.FirstOrDefault(static x => x.Type.Equals(AuthClaims.Name, StringComparison.Ordinal))?.Value ?? "Anonymous";

	/// <summary>
	/// Gets the login (email) claim value from the JWT, if available.
	/// </summary>
	/// <value>The login (email) from the JWT claims, or <see langword="null"/> if not available.</value>
	public string? Login => Jwt?.Claims.FirstOrDefault(static x => x.Type.Equals(AuthClaims.Email, StringComparison.Ordinal))?.Value;

	/// <summary>
	/// Gets the user identifier (UPN) claim value from the JWT, if available.
	/// </summary>
	/// <value>The user identifier from the JWT claims, or <see langword="null"/> if not available.</value>
	public string? UserId => Jwt?.Claims.FirstOrDefault(static x => x.Type.Equals(AuthClaims.Upn, StringComparison.Ordinal))?.Value;

	/// <summary>
	/// Determines whether the token will expire by the specified date and time.
	/// </summary>
	/// <param name="expiration"> The date and time to check against the token's validity. </param>
	/// <returns> <c> true </c> if the token will expire by the specified date and time; otherwise, <c> false </c>. </returns>
	public bool WillExpireBy(DateTime expiration) => Jwt == null || expiration <= Jwt.ValidFrom || expiration >= Jwt.ValidTo;

	/// <inheritdoc />
	public bool IsAnonymous() => !IsAuthenticated();

	/// <inheritdoc />
	public bool IsAuthenticated() => AuthenticationState == AuthenticationState.Authenticated;
}
