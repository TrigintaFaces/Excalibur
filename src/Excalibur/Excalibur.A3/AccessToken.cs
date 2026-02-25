// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Claims;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.Domain.Exceptions;

namespace Excalibur.A3;

/// <summary>
/// Simple implementation of <see cref="IAccessToken" /> used for message context population.
/// </summary>
public sealed class AccessToken : IAccessToken
{
	private readonly IAuthenticationToken _authenticationToken;
	private readonly IAuthorizationPolicy _authorizationPolicy;

	/// <summary>
	/// Initializes a new instance of the <see cref="AccessToken" /> class with the specified authentication and authorization policies.
	/// </summary>
	/// <param name="authenticationToken"> The authentication token for the user. </param>
	/// <param name="authorizationPolicy"> The authorization policy for the user. </param>
	/// <exception cref="InvalidOperationException">
	/// Thrown if the user identifiers in the authentication token and authorization policy do not match.
	/// </exception>
	public AccessToken(IAuthenticationToken authenticationToken, IAuthorizationPolicy authorizationPolicy)
	{
		ArgumentNullException.ThrowIfNull(authenticationToken);
		ArgumentNullException.ThrowIfNull(authorizationPolicy);

		DomainException.ThrowIf(!string.Equals(authenticationToken.UserId, authorizationPolicy.UserId, StringComparison.Ordinal),
			"The IAuthenticationToken and IAuthorizationPolicy users do not match.");

		_authenticationToken = authenticationToken;
		_authorizationPolicy = authorizationPolicy;
	}

	/// <inheritdoc />
	public AuthenticationState AuthenticationState => _authenticationToken.AuthenticationState;

	/// <inheritdoc />
	public string? Jwt { get => _authenticationToken.Jwt; set => _authenticationToken.Jwt = value; }

	/// <inheritdoc />
	public IEnumerable<Claim>? Claims => _authenticationToken.Claims;

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
	string IAccessToken.UserId => _authenticationToken.UserId ?? string.Empty;

	/// <inheritdoc />
	string? IAuthenticationToken.UserId => _authenticationToken.UserId;

	/// <inheritdoc />
	string? IAuthorizationPolicy.UserId => _authorizationPolicy.UserId;

	/// <summary>
	/// Creates a <see cref="AccessToken" /> from the provided user and tenant identifiers.
	/// </summary>
	/// <param name="userId"> The user identifier. </param>
	/// <param name="tenantId"> The tenant identifier. </param>
	/// <returns> A <see cref="AccessToken" /> instance. </returns>
	public static AccessToken FromValues(string? userId, string tenantId)
	{
		var authToken = new BasicAuthenticationToken(userId);
		var policy = new BasicAuthorizationPolicy(tenantId, userId ?? string.Empty);

		return new AccessToken(authToken, policy);
	}

	/// <inheritdoc />
	public bool IsAuthorized(string activityName, string? resourceId = null) => _authorizationPolicy.IsAuthorized(activityName, resourceId);

	/// <inheritdoc />
	public bool HasGrant(string activityName) => _authorizationPolicy.HasGrant(activityName);

	/// <inheritdoc />
	public bool HasGrant<TActivity>() => _authorizationPolicy.HasGrant<TActivity>();

	/// <inheritdoc />
	public bool HasGrant(string resourceType, string resourceId) => _authorizationPolicy.HasGrant(resourceType, resourceId);

	/// <inheritdoc />
	public bool HasGrant<TResourceType>(string resourceId) => _authorizationPolicy.HasGrant<TResourceType>(resourceId);

	/// <inheritdoc />
	public bool IsAnonymous() => !IsAuthenticated();

	/// <inheritdoc />
	public bool IsAuthenticated() => AuthenticationState == AuthenticationState.Authenticated;

	private sealed class BasicAuthenticationToken(string? id) : IAuthenticationToken
	{
		public AuthenticationState AuthenticationState { get; } = string.IsNullOrWhiteSpace(id)
			? AuthenticationState.Anonymous
			: AuthenticationState.Authenticated;

		public string? Jwt { get; set; }

		public IEnumerable<Claim>? Claims => null;

		public string? FirstName => null;

		public string? LastName => null;

		public string FullName => string.Empty;

		public string? Login => null;

		public string? UserId { get; } = id;

		public bool IsAnonymous() => AuthenticationState == AuthenticationState.Anonymous;

		public bool IsAuthenticated() => AuthenticationState == AuthenticationState.Authenticated;
	}

	private sealed class BasicAuthorizationPolicy(string tenantId, string uid) : IAuthorizationPolicy
	{
		public string TenantId { get; } = tenantId;

		public string? UserId { get; } = uid;

		public bool IsAuthorized(string activityName, string? resourceId = null) => false;

		public bool HasGrant(string activityName) => false;

		public bool HasGrant<TActivity>() => false;

		public bool HasGrant(string resourceType, string resourceId) => false;

		public bool HasGrant<TResourceType>(string resourceId) => false;
	}
}
