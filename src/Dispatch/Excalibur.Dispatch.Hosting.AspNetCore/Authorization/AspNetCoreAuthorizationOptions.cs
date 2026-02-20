// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Configuration options for the ASP.NET Core authorization bridge middleware.
/// </summary>
/// <remarks>
/// These options control how the <see cref="AspNetCoreAuthorizationMiddleware"/> evaluates
/// ASP.NET Core <c>[Authorize]</c> attributes on message and handler types. When
/// <see cref="RequireAuthenticatedUser"/> is <see langword="true"/> (the default), requests
/// without an <c>HttpContext</c> or with an unauthenticated principal are rejected with a 403 response.
/// Set it to <see langword="false"/> to allow pass-through in scenarios such as background job dispatching.
/// </remarks>
public sealed class AspNetCoreAuthorizationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the authorization middleware is enabled.
	/// </summary>
	/// <value><see langword="true"/> if the middleware should evaluate authorization; otherwise, <see langword="false"/>. The default is <see langword="true"/>.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether an authenticated user (via <c>HttpContext.User</c>) is required.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to reject messages when <c>HttpContext</c> is unavailable or the user is not authenticated;
	/// <see langword="false"/> to skip authorization and pass through to the next middleware. The default is <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// Set this to <see langword="false"/> for services that dispatch messages from background workers or hosted services
	/// where no HTTP context is available.
	/// </remarks>
	public bool RequireAuthenticatedUser { get; set; } = true;

	/// <summary>
	/// Gets or sets the default authorization policy name to apply when an <c>[Authorize]</c> attribute
	/// specifies no explicit policy.
	/// </summary>
	/// <value>The default policy name, or <see langword="null"/> to use only role/claim checks. The default is <see langword="null"/>.</value>
	public string? DefaultPolicy { get; set; }
}
