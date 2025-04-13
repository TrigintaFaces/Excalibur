using System.IdentityModel.Tokens.Jwt;

using Excalibur.A3.Exceptions;

namespace Excalibur.A3.Authentication;

/// <summary>
///     Represents an authentication token and provides methods to access claims and related information.
/// </summary>
public class AuthenticationToken : IAuthenticationToken
{
	/// <summary>
	///     Represents an anonymous authentication token.
	/// </summary>
	public static readonly IAuthenticationToken Anonymous = new AuthenticationToken();

	/// <summary>
	///     Initializes a new instance of the <see cref="AuthenticationToken" /> class with a default empty JWT.
	/// </summary>
	public AuthenticationToken() => Jwt = new JwtSecurityToken();

	/// <summary>
	///     Initializes a new instance of the <see cref="AuthenticationToken" /> class with the specified JWT.
	/// </summary>
	/// <param name="jwt"> The JWT to use for this authentication token. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="jwt" /> is null. </exception>
	/// <exception cref="NotAuthenticatedException"> Thrown if required claims are missing. </exception>
	internal AuthenticationToken(JwtSecurityToken jwt)
	{
		ArgumentNullException.ThrowIfNull(jwt);

		if (string.IsNullOrEmpty(jwt.Claims.FirstOrDefault(x => x.Type.Equals(AuthClaims.Upn, StringComparison.Ordinal))?.Value))
		{
			throw NotAuthenticatedException.BecauseMissingClaim(AuthClaims.Upn);
		}

		if (string.IsNullOrEmpty(jwt.Claims.FirstOrDefault(x => x.Type.Equals(AuthClaims.Name, StringComparison.Ordinal))?.Value))
		{
			throw NotAuthenticatedException.BecauseMissingClaim(AuthClaims.Name);
		}

		if (string.IsNullOrEmpty(jwt.Claims.FirstOrDefault(x => x.Type.Equals(AuthClaims.Email, StringComparison.Ordinal))?.Value))
		{
			throw NotAuthenticatedException.BecauseMissingClaim(AuthClaims.Email);
		}

		Jwt = jwt;
	}

	/// <summary>
	///     Gets or sets the authentication state associated with this token.
	/// </summary>
	public AuthenticationState AuthenticationState { get; set; }

	/// <summary>
	///     Gets or sets the underlying JWT for this authentication token.
	/// </summary>
	public JwtSecurityToken? Jwt { get; protected set; }

	/// <inheritdoc />
	string? IAuthenticationToken.Jwt
	{
		get => Jwt?.RawData;
		set => Jwt = string.IsNullOrEmpty(value) ? new JwtSecurityToken() : new JwtSecurityToken(value);
	}

	/// <summary>
	///     Gets the first name claim value from the JWT, if available.
	/// </summary>
	public string? FirstName => Jwt?.Claims.FirstOrDefault(x => x.Type.Equals(AuthClaims.Given_Name, StringComparison.Ordinal))?.Value;

	/// <summary>
	///     Gets the last name claim value from the JWT, if available.
	/// </summary>
	public string? LastName => Jwt?.Claims.FirstOrDefault(x => x.Type.Equals(AuthClaims.Family_Name, StringComparison.Ordinal))?.Value;

	/// <summary>
	///     Gets the full name claim value from the JWT, or "Anonymous" if not available.
	/// </summary>
	public string FullName =>
		Jwt?.Claims.FirstOrDefault(x => x.Type.Equals(AuthClaims.Name, StringComparison.Ordinal))?.Value ?? "Anonymous";

	/// <summary>
	///     Gets the login (email) claim value from the JWT, if available.
	/// </summary>
	public string? Login => Jwt?.Claims.FirstOrDefault(x => x.Type.Equals(AuthClaims.Email, StringComparison.Ordinal))?.Value;

	/// <summary>
	///     Gets the user identifier (UPN) claim value from the JWT, if available.
	/// </summary>
	public string? UserId => Jwt?.Claims.FirstOrDefault(x => x.Type.Equals(AuthClaims.Upn, StringComparison.Ordinal))?.Value;

	/// <summary>
	///     Determines whether the token will expire by the specified date and time.
	/// </summary>
	/// <param name="expiration"> The date and time to check against the token's validity. </param>
	/// <returns> <c> true </c> if the token will expire by the specified date and time; otherwise, <c> false </c>. </returns>
	public bool WillExpireBy(DateTime expiration) => Jwt == null || expiration <= Jwt.ValidFrom || expiration >= Jwt.ValidTo;

	/// <inheritdoc />
	public bool IsAnonymous() => !IsAuthenticated();

	/// <inheritdoc />
	public bool IsAuthenticated() => AuthenticationState == AuthenticationState.Authenticated;
}
