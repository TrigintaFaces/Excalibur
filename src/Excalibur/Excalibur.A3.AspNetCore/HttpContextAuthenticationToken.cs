// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Claims;

using Excalibur.A3.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// Projects the <see cref="ClaimsPrincipal"/> that ASP.NET Core authentication already established for
/// the request onto the <see cref="IAuthenticationToken"/> that grant authorization reads.
/// </summary>
/// <remarks>
/// This type performs no authentication of its own: it never validates a token, never contacts an
/// identity provider, and never reparses a bearer token into claims. Whatever authentication scheme the
/// host configured has already produced the principal, and this is the adapter onto it. Outside an HTTP
/// request — a background worker resolving the same interface — there is no principal, so the caller is
/// anonymous and grant authorization fails closed.
/// </remarks>
internal sealed class HttpContextAuthenticationToken(
	IHttpContextAccessor httpContextAccessor,
	IOptions<GrantAuthorizationHttpOptions> options) : IAuthenticationToken
{
	private const string BearerPrefix = "Bearer ";

	private readonly GrantAuthorizationHttpOptions _options = options.Value;

	private string? _jwtOverride;

	private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

	/// <inheritdoc />
	public AuthenticationState AuthenticationState =>
		IsAuthenticated() ? AuthenticationState.Authenticated : AuthenticationState.Anonymous;

	/// <inheritdoc />
	/// <remarks>
	/// Reads the raw bearer token from the request's <c>Authorization</c> header so a handler can forward
	/// the caller's credential onward. Assigning a value overrides it for the remainder of the request;
	/// the assignment never alters the principal, which is owned by ASP.NET Core authentication.
	/// </remarks>
	public string? Jwt
	{
		get
		{
			if (_jwtOverride is not null)
			{
				return _jwtOverride;
			}

			var header = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

			return header is not null && header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
				? header[BearerPrefix.Length..]
				: null;
		}

		set => _jwtOverride = value;
	}

	/// <inheritdoc />
	public IEnumerable<Claim>? Claims => Principal?.Claims;

	/// <inheritdoc />
	public string? FirstName => FindFirst(ClaimTypes.GivenName, "given_name");

	/// <inheritdoc />
	public string? LastName => FindFirst(ClaimTypes.Surname, "family_name");

	/// <inheritdoc />
	public string FullName
	{
		get
		{
			var name = FindFirst(ClaimTypes.Name, "name");
			if (!string.IsNullOrEmpty(name))
			{
				return name;
			}

			var first = FirstName;
			var last = LastName;

			return string.IsNullOrEmpty(first) || string.IsNullOrEmpty(last)
				? first ?? last ?? string.Empty
				: string.Concat(first, " ", last);
		}
	}

	/// <inheritdoc />
	public string? Login => FindFirst(ClaimTypes.Email, "email", ClaimTypes.Upn);

	/// <inheritdoc />
	/// <remarks>
	/// Resolved from the first claim type in
	/// <see cref="GrantAuthorizationHttpOptions.UserIdClaimTypes"/> that the principal carries with a
	/// non-empty value.
	/// </remarks>
	public string? UserId
	{
		get
		{
			var principal = Principal;
			if (principal is null)
			{
				return null;
			}

			foreach (var claimType in _options.UserIdClaimTypes)
			{
				var value = principal.FindFirstValue(claimType);
				if (!string.IsNullOrEmpty(value))
				{
					return value;
				}
			}

			return null;
		}
	}

	/// <inheritdoc />
	public bool IsAnonymous() => !IsAuthenticated();

	/// <inheritdoc />
	public bool IsAuthenticated() => Principal?.Identity?.IsAuthenticated == true;

	private string? FindFirst(params string[] claimTypes)
	{
		var principal = Principal;
		if (principal is null)
		{
			return null;
		}

		foreach (var claimType in claimTypes)
		{
			var value = principal.FindFirstValue(claimType);
			if (!string.IsNullOrEmpty(value))
			{
				return value;
			}
		}

		return null;
	}
}
