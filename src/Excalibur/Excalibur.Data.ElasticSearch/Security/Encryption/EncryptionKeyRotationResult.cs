// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the result of an encryption key rotation operation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="EncryptionKeyRotationResult" /> class.
/// </remarks>
/// <param name="success"> Whether the key rotation was successful. </param>
/// <param name="classification"> The data classification level that was rotated. </param>
/// <param name="newKeyVersion"> The version of the new encryption key. </param>
/// <param name="previousKeyVersion"> The version of the previous encryption key. </param>
/// <param name="affectedDocumentCount"> The number of documents that were re-encrypted. </param>
/// <param name="errorMessage"> Optional error message if rotation failed. </param>
public sealed class EncryptionKeyRotationResult(
	bool success,
	DataClassification classification,
	string? newKeyVersion = null,
	string? previousKeyVersion = null,
	int affectedDocumentCount = 0,
	string? errorMessage = null)
{
	/// <summary>
	/// Gets a value indicating whether the key rotation was successful.
	/// </summary>
	/// <value> True if the key rotation completed successfully, false otherwise. </value>
	public bool Success { get; } = success;

	/// <summary>
	/// Gets the data classification level that was rotated.
	/// </summary>
	/// <value> The classification level for which keys were rotated. </value>
	public DataClassification Classification { get; } = classification;

	/// <summary>
	/// Gets the version of the new encryption key.
	/// </summary>
	/// <value> The version identifier for the new key, or null if rotation failed. </value>
	public string? NewKeyVersion { get; } = newKeyVersion;

	/// <summary>
	/// Gets the version of the previous encryption key.
	/// </summary>
	/// <value> The version identifier for the previous key, or null if not available. </value>
	public string? PreviousKeyVersion { get; } = previousKeyVersion;

	/// <summary>
	/// Gets the number of documents that were re-encrypted with the new key.
	/// </summary>
	/// <value> The count of documents affected by the key rotation. </value>
	public int AffectedDocumentCount { get; } = affectedDocumentCount;

	/// <summary>
	/// Gets the error message if rotation failed.
	/// </summary>
	/// <value> A descriptive error message, or null if rotation succeeded. </value>
	public string? ErrorMessage { get; } = errorMessage;

	/// <summary>
	/// Gets the timestamp when the key rotation occurred.
	/// </summary>
	/// <value> The UTC timestamp of the key rotation operation. </value>
	public DateTimeOffset RotatedAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Creates a successful key rotation result.
	/// </summary>
	/// <param name="classification"> The data classification level that was rotated. </param>
	/// <param name="newKeyVersion"> The version of the new encryption key. </param>
	/// <param name="previousKeyVersion"> The version of the previous encryption key. </param>
	/// <param name="affectedDocumentCount"> The number of documents that were re-encrypted. </param>
	/// <returns> A successful key rotation result. </returns>
	public static EncryptionKeyRotationResult CreateSuccess(
		DataClassification classification,
		string newKeyVersion,
		string previousKeyVersion,
		int affectedDocumentCount = 0)
		=> new(success: true, classification, newKeyVersion, previousKeyVersion, affectedDocumentCount);

	/// <summary>
	/// Creates a failed key rotation result.
	/// </summary>
	/// <param name="classification"> The data classification level that failed to rotate. </param>
	/// <param name="errorMessage"> The error message describing the failure. </param>
	/// <returns> A failed key rotation result. </returns>
	public static EncryptionKeyRotationResult CreateFailure(DataClassification classification, string errorMessage)
		=> new(success: false, classification, errorMessage: errorMessage);
}
