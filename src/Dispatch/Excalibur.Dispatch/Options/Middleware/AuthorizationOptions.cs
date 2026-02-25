// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for authorization middleware.
/// </summary>
public sealed class AuthorizationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether authorization is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to allow anonymous access when no subject is present.
	/// </summary>
	/// <value> Default is false. </value>
	public bool AllowAnonymousAccess { get; set; }

	/// <summary>
	/// Gets or sets message types that bypass authorization.
	/// </summary>
	/// <value>The current <see cref="BypassAuthorizationForTypes"/> value.</value>
	public string[]? BypassAuthorizationForTypes { get; set; }

	/// <summary>
	/// Gets or sets the default authorization policy name.
	/// </summary>
	/// <value>The current <see cref="DefaultPolicyName"/> value.</value>
	public string? DefaultPolicyName { get; set; } = "Default";
}
