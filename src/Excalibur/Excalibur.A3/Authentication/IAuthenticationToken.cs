// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security.Claims;

namespace Excalibur.A3.Authentication;

/// <summary>
/// Represents an authentication token containing identity and claims information.
/// </summary>
public interface IAuthenticationToken
{
	/// <summary>
	/// Gets the current authentication state of the token. Indicates whether the user is anonymous, authenticated, or fully identified.
	/// </summary>
	/// <value>The current authentication state of the token.</value>
	AuthenticationState AuthenticationState { get; }

	/// <summary>
	/// Gets or sets the raw JWT (JSON Web Token) string associated with the authentication token. This property can be null if the
	/// token has not been issued or is not applicable.
	/// </summary>
	/// <value>The raw JWT string, or <see langword="null"/> if not applicable.</value>
	string? Jwt { get; set; }

	/// <summary>
	/// Gets the claims for this authentication token.
	/// </summary>
	/// <value>The collection of claims, or <see langword="null"/> if no claims are available.</value>
	IEnumerable<Claim>? Claims { get; }

	/// <summary>
	/// Gets the first name of the authenticated user. Returns null if the user's identity is not available or not set.
	/// </summary>
	/// <value>The first name of the user, or <see langword="null"/> if not available.</value>
	string? FirstName { get; }

	/// <summary>
	/// Gets the last name of the authenticated user. Returns null if the user's identity is not available or not set.
	/// </summary>
	/// <value>The last name of the user, or <see langword="null"/> if not available.</value>
	string? LastName { get; }

	/// <summary>
	/// Gets the full name of the authenticated user, constructed from <see cref="FirstName" /> and <see cref="LastName" />. Returns an
	/// empty string if neither property is set.
	/// </summary>
	/// <value>The full name of the authenticated user, or an empty string if neither property is set.</value>
	string FullName { get; }

	/// <summary>
	/// Gets the login identifier (e.g., email address or username) of the authenticated user. Returns null if the user's login
	/// information is not available.
	/// </summary>
	/// <value>The login identifier, or <see langword="null"/> if not available.</value>
	string? Login { get; }

	/// <summary>
	/// Gets the unique user identifier (e.g., GUID or system-specific identifier) associated with the authentication token. Returns
	/// null if the user ID is not available or applicable.
	/// </summary>
	/// <value>The unique user identifier, or <see langword="null"/> if not available.</value>
	string? UserId { get; }

	/// <summary>
	/// Determines whether the provided authentication token represents an anonymous user.
	/// </summary>
	/// <returns> <c> true </c> if the token represents an anonymous user; otherwise, <c> false </c>. </returns>
	bool IsAnonymous();

	/// <summary>
	/// Determines whether the provided authentication token represents an authenticated user.
	/// </summary>
	/// <returns> <c> true </c> if the token represents an authenticated user; otherwise, <c> false </c>. </returns>
	bool IsAuthenticated();
}
