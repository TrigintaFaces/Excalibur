// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents the result of an authorization operation.
/// </summary>
public sealed class AuthorizationResult : IAuthorizationResult
{
	/// <summary>
	/// Gets a value indicating whether the authorization was successful.
	/// </summary>
	/// <value><see langword="true"/> when the request is authorized; otherwise, <see langword="false"/>.</value>
	public bool IsAuthorized { get; init; }

	/// <summary>
	/// Gets the failure message when authorization fails.
	/// </summary>
	/// <value>The optional reason describing the authorization failure.</value>
	public string? FailureMessage { get; init; }

	/// <summary>
	/// Creates a successful authorization result.
	/// </summary>
	/// <returns> An <see cref="AuthorizationResult" /> indicating success. </returns>
	public static AuthorizationResult Success() => new() { IsAuthorized = true };

	/// <summary>
	/// Creates a failed authorization result with the specified message.
	/// </summary>
	/// <param name="message"> The failure message. </param>
	/// <returns> An <see cref="AuthorizationResult" /> indicating failure. </returns>
	public static AuthorizationResult Failed(string message) => new() { IsAuthorized = false, FailureMessage = message };
}
