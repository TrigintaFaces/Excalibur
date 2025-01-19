using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.Domain.Exceptions;

namespace Excalibur.A3;

/// <summary>
///     Represents an access token implementation that combines authentication and authorization functionality.
/// </summary>
public class AccessToken : IAccessToken
{
	private readonly IAuthenticationToken _authenticationToken;
	private readonly IAuthorizationPolicy _authorizationPolicy;

	/// <summary>
	///     Initializes a new instance of the <see cref="AccessToken" /> class with the specified authentication and authorization policies.
	/// </summary>
	/// <param name="authenticationToken"> The authentication token for the user. </param>
	/// <param name="authorizationPolicy"> The authorization policy for the user. </param>
	/// <exception cref="DomainException">
	///     Thrown if the user identifiers in the authentication token and authorization policy do not match.
	/// </exception>
	public AccessToken(IAuthenticationToken authenticationToken, IAuthorizationPolicy authorizationPolicy)
	{
		ArgumentNullException.ThrowIfNull(authenticationToken);
		ArgumentNullException.ThrowIfNull(authorizationPolicy);
		DomainException.ThrowIf(authenticationToken.UserId != authorizationPolicy.UserId, statusCode: 400,
			"The IAuthenticationToken and IAuthorizationPolicy users do not match.");

		_authenticationToken = authenticationToken;
		_authorizationPolicy = authorizationPolicy;
	}

	/// <inheritdoc />
	public AuthenticationState AuthenticationState => _authenticationToken.AuthenticationState;

	/// <inheritdoc />
	public string? Jwt { get => _authenticationToken.Jwt; set => _authenticationToken.Jwt = value; }

	/// <inheritdoc />
	public string? FirstName => _authenticationToken.FirstName;

	/// <inheritdoc />
	public string? LastName => _authenticationToken.LastName;

	/// <inheritdoc />
	public string FullName => _authenticationToken.FullName;

	/// <inheritdoc />
	public string? Login => _authenticationToken.Login;

	/// <inheritdoc />
	public string TenantId => _authorizationPolicy.TenantId;

	/// <inheritdoc />
	public string? UserId => _authenticationToken.UserId;

	/// <inheritdoc />
	public bool IsAuthorized(string activityName, string? resourceId = null) => _authorizationPolicy.IsAuthorized(activityName, resourceId);

	/// <inheritdoc />
	public bool HasGrant(string activityName) => _authorizationPolicy.HasGrant(activityName);

	/// <inheritdoc />
	public bool HasGrant<TActivity>() => _authorizationPolicy.HasGrant<TActivity>();

	/// <inheritdoc />
	public bool HasGrant(string resourceType, string resourceId) => _authorizationPolicy.HasGrant(resourceType, resourceId);

	/// <inheritdoc />
	public bool HasGrant<TResourceType>(string resourceId) => _authorizationPolicy.HasGrant<TResourceType>();
}
