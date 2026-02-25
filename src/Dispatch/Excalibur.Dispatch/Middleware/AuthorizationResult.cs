// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Result of authorization evaluation.
/// </summary>
public sealed class AuthorizationResult
{
	private AuthorizationResult(bool isAuthorized, string? reason)
	{
		IsAuthorized = isAuthorized;
		Reason = reason;
	}

	/// <summary>
	/// Gets a value indicating whether authorization succeeded.
	/// </summary>
	/// <value>The current <see cref="IsAuthorized"/> value.</value>
	public bool IsAuthorized { get; }

	/// <summary>
	/// Gets the reason for authorization failure, if applicable.
	/// </summary>
	/// <value>The current <see cref="Reason"/> value.</value>
	public string? Reason { get; }

	/// <summary>
	/// Creates a successful authorization result.
	/// </summary>
	public static AuthorizationResult Success() => new(isAuthorized: true, reason: null);

	/// <summary>
	/// Creates a failed authorization result with a reason.
	/// </summary>
	public static AuthorizationResult Failure(string reason) => new(isAuthorized: false, reason);
}
