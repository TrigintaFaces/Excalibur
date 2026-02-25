// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the result of an authentication credential rotation operation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuthenticationRotationResult" /> class.
/// </remarks>
/// <param name="success"> Whether the rotation operation was successful. </param>
/// <param name="message"> Optional message providing details about the rotation result. </param>
/// <param name="rotatedAt"> The timestamp when the rotation occurred. </param>
/// <param name="nextRotationDue"> The timestamp when the next rotation should occur. </param>
public sealed class AuthenticationRotationResult(
	bool success,
	string? message = null,
	DateTimeOffset? rotatedAt = null,
	DateTimeOffset? nextRotationDue = null)
{
	/// <summary>
	/// Gets a value indicating whether the rotation was successful.
	/// </summary>
	/// <value> True if the credential rotation completed successfully, false otherwise. </value>
	public bool Success { get; } = success;

	/// <summary>
	/// Gets an optional message providing details about the rotation operation.
	/// </summary>
	/// <value> A descriptive message about the rotation result, or null if not provided. </value>
	public string? Message { get; } = message;

	/// <summary>
	/// Gets the timestamp when the rotation occurred.
	/// </summary>
	/// <value> The UTC timestamp of the credential rotation operation. </value>
	public DateTimeOffset RotatedAt { get; } = rotatedAt ?? DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the timestamp when the next rotation should occur.
	/// </summary>
	/// <value> The UTC timestamp for the next scheduled rotation, or null if not applicable. </value>
	public DateTimeOffset? NextRotationDue { get; } = nextRotationDue;

	/// <summary>
	/// Creates a successful rotation result.
	/// </summary>
	/// <param name="message"> Optional success message. </param>
	/// <param name="nextRotationDue"> Optional timestamp for next rotation. </param>
	/// <returns> A new instance representing a successful rotation. </returns>
	public static AuthenticationRotationResult CreateSuccess(string? message = null, DateTimeOffset? nextRotationDue = null)
		=> new(success: true, message, DateTimeOffset.UtcNow, nextRotationDue);

	/// <summary>
	/// Creates a failed rotation result.
	/// </summary>
	/// <param name="message"> Error message describing the failure. </param>
	/// <returns> A new instance representing a failed rotation. </returns>
	public static AuthenticationRotationResult Failure(string message)
		=> new(success: false, message);
}
