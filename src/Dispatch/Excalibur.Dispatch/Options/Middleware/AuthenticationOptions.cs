// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for authentication middleware.
/// </summary>
public sealed class AuthenticationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether authentication is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether authentication is required for all messages.
	/// </summary>
	/// <value> Default is true. </value>
	public bool RequireAuthentication { get; set; } = true;

	/// <summary>
	/// Gets or sets the default authentication scheme.
	/// </summary>
	/// <value> Default is "Bearer". </value>
	public string DefaultScheme { get; set; } = "Bearer";

	/// <summary>
	/// Gets or sets the header name containing the authentication token.
	/// </summary>
	/// <value> Default is "Authorization". </value>
	public string TokenHeader { get; set; } = "Authorization";

	/// <summary>
	/// Gets or sets a value indicating whether to enable authentication caching.
	/// </summary>
	/// <value> Default is true. </value>
	public bool EnableCaching { get; set; } = true;

	/// <summary>
	/// Gets or sets the duration to cache authentication results.
	/// </summary>
	/// <value> Default is 5 minutes. </value>
	public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the maximum number of cached authentications.
	/// </summary>
	/// <value> Default is 1000. </value>
	public int MaxCacheSize { get; set; } = 1000;

	/// <summary>
	/// Gets valid API keys for API key authentication.
	/// </summary>
	/// <value>The current <see cref="ValidApiKeys"/> value.</value>
	public HashSet<string>? ValidApiKeys { get; }

	/// <summary>
	/// Gets or sets message types that allow anonymous access.
	/// </summary>
	/// <value>The current <see cref="AllowAnonymousForTypes"/> value.</value>
	public string[]? AllowAnonymousForTypes { get; set; }
}
