// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Represents the result of a key rotation operation.
/// </summary>
/// <remarks>
/// Key rotation creates a new key version while keeping the previous version available for decryption of existing data.
/// </remarks>
public sealed record KeyRotationResult
{
	/// <summary>
	/// Gets a value indicating whether the rotation was successful.
	/// </summary>
	public required bool Success { get; init; }

	/// <summary>
	/// Gets the metadata for the newly created key. Null if rotation failed.
	/// </summary>
	public KeyMetadata? NewKey { get; init; }

	/// <summary>
	/// Gets the metadata for the previous (now decrypt-only) key. Null if there was no previous key.
	/// </summary>
	public KeyMetadata? PreviousKey { get; init; }

	/// <summary>
	/// Gets an error message if the rotation failed. Null if successful.
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Gets the timestamp when the rotation was performed.
	/// </summary>
	public DateTimeOffset RotatedAt { get; init; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Creates a successful rotation result.
	/// </summary>
	/// <param name="newKey"> The newly created key metadata. </param>
	/// <param name="previousKey"> The previous key metadata (now decrypt-only). </param>
	/// <returns> A successful rotation result. </returns>
	public static KeyRotationResult Succeeded(KeyMetadata newKey, KeyMetadata? previousKey = null) =>
		new() { Success = true, NewKey = newKey, PreviousKey = previousKey };

	/// <summary>
	/// Creates a failed rotation result.
	/// </summary>
	/// <param name="errorMessage"> The error message describing the failure. </param>
	/// <returns> A failed rotation result. </returns>
	public static KeyRotationResult Failed(string errorMessage) =>
		new() { Success = false, ErrorMessage = errorMessage };
}
