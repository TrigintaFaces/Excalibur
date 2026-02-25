// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the result of a key rotation operation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KeyRotationResult" /> class.
/// </remarks>
/// <param name="success"> Whether the key rotation was successful. </param>
/// <param name="keyName"> The name of the rotated key. </param>
/// <param name="newKeyVersion"> The version of the new key. </param>
/// <param name="previousKeyVersion"> The version of the previous key. </param>
/// <param name="nextRotationDue"> When the next rotation is due. </param>
/// <param name="errorMessage"> Optional error message if rotation failed. </param>
public sealed class KeyRotationResult(
	bool success,
	string? keyName = null,
	string? newKeyVersion = null,
	string? previousKeyVersion = null,
	DateTimeOffset? nextRotationDue = null,
	string? errorMessage = null)
{
	/// <summary>
	/// Gets a value indicating whether the key rotation was successful.
	/// </summary>
	/// <value> True if the key was rotated successfully, false otherwise. </value>
	public bool Success { get; } = success;

	/// <summary>
	/// Gets the name of the rotated key.
	/// </summary>
	/// <value> The unique identifier for the rotated key. </value>
	public string? KeyName { get; } = keyName;

	/// <summary>
	/// Gets the version of the new key after rotation.
	/// </summary>
	/// <value> The version identifier for the new key. </value>
	public string? NewKeyVersion { get; } = newKeyVersion;

	/// <summary>
	/// Gets the version of the previous key before rotation.
	/// </summary>
	/// <value> The version identifier for the previous key. </value>
	public string? PreviousKeyVersion { get; } = previousKeyVersion;

	/// <summary>
	/// Gets when the next rotation is due.
	/// </summary>
	/// <value> The UTC timestamp for the next scheduled rotation. </value>
	public DateTimeOffset? NextRotationDue { get; } = nextRotationDue;

	/// <summary>
	/// Gets the error message if rotation failed.
	/// </summary>
	/// <value> A descriptive error message, or null if rotation succeeded. </value>
	public string? ErrorMessage { get; } = errorMessage;

	/// <summary>
	/// Gets the timestamp when the rotation occurred.
	/// </summary>
	/// <value> The UTC timestamp of key rotation. </value>
	public DateTimeOffset RotatedAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Creates a successful key rotation result.
	/// </summary>
	/// <param name="keyName"> The name of the rotated key. </param>
	/// <param name="newKeyVersion"> The version of the new key. </param>
	/// <param name="previousKeyVersion"> The version of the previous key. </param>
	/// <param name="nextRotationDue"> When the next rotation is due. </param>
	/// <returns> A successful key rotation result. </returns>
	public static KeyRotationResult CreateSuccess(string keyName, string newKeyVersion, string previousKeyVersion,
		DateTimeOffset? nextRotationDue = null)
		=> new(success: true, keyName, newKeyVersion, previousKeyVersion, nextRotationDue);

	/// <summary>
	/// Creates a failed key rotation result.
	/// </summary>
	/// <param name="keyName"> The name of the key that failed to rotate. </param>
	/// <param name="errorMessage"> The error message describing the failure. </param>
	/// <returns> A failed key rotation result. </returns>
	public static KeyRotationResult CreateFailure(string keyName, string errorMessage)
		=> new(success: false, keyName, errorMessage: errorMessage);
}
