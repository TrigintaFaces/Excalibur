// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Serializable implementation of IAuthorizationResult for AOT compatibility.
/// </summary>
public sealed class SerializableAuthorizationResult : IAuthorizationResult
{
	/// <inheritdoc />
	public bool IsAuthorized { get; init; }

	/// <inheritdoc />
	public string? FailureMessage { get; init; }

	/// <summary>
	/// Creates an authorized result.
	/// </summary>
	public static SerializableAuthorizationResult Authorized()
		=> new() { IsAuthorized = true };

	/// <summary>
	/// Creates an unauthorized result.
	/// </summary>
	/// <param name="reason"> The reason for the authorization failure. </param>
	public static SerializableAuthorizationResult Unauthorized(string reason)
		=> new() { IsAuthorized = false, FailureMessage = reason };
}
