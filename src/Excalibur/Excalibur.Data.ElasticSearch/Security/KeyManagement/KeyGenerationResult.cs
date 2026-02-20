// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the result of a key generation operation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="KeyGenerationResult" /> class.
/// </remarks>
/// <param name="success"> Whether the key generation was successful. </param>
/// <param name="keyName"> The name of the generated key. </param>
/// <param name="keyType"> The type of key that was generated. </param>
/// <param name="keySize"> The size of the generated key in bits. </param>
/// <param name="keyVersion"> The version of the generated key. </param>
/// <param name="errorMessage"> Optional error message if generation failed. </param>
public sealed class KeyGenerationResult(
	bool success,
	string? keyName = null,
	EncryptionKeyType? keyType = null,
	int? keySize = null,
	string? keyVersion = null,
	string? errorMessage = null)
{
	/// <summary>
	/// Gets a value indicating whether the key generation was successful.
	/// </summary>
	/// <value> True if the key was generated successfully, false otherwise. </value>
	public bool Success { get; } = success;

	/// <summary>
	/// Gets the name of the generated key.
	/// </summary>
	/// <value> The unique identifier for the generated key. </value>
	public string? KeyName { get; } = keyName;

	/// <summary>
	/// Gets the type of key that was generated.
	/// </summary>
	/// <value> The encryption key type that was created. </value>
	public EncryptionKeyType? KeyType { get; } = keyType;

	/// <summary>
	/// Gets the size of the generated key in bits.
	/// </summary>
	/// <value> The bit length of the generated key. </value>
	public int? KeySize { get; } = keySize;

	/// <summary>
	/// Gets the version of the generated key.
	/// </summary>
	/// <value> The version identifier for the generated key. </value>
	public string? KeyVersion { get; } = keyVersion;

	/// <summary>
	/// Gets the error message if generation failed.
	/// </summary>
	/// <value> A descriptive error message, or null if generation succeeded. </value>
	public string? ErrorMessage { get; } = errorMessage;

	/// <summary>
	/// Gets the timestamp when the key was generated.
	/// </summary>
	/// <value> The UTC timestamp of key generation. </value>
	public DateTimeOffset GeneratedAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Creates a successful key generation result.
	/// </summary>
	/// <param name="keyName"> The name of the generated key. </param>
	/// <param name="keyType"> The type of key that was generated. </param>
	/// <param name="keySize"> The size of the generated key in bits. </param>
	/// <param name="keyVersion"> The version of the generated key. </param>
	/// <returns> A successful key generation result. </returns>
	public static KeyGenerationResult CreateSuccess(string keyName, EncryptionKeyType keyType, int keySize, string keyVersion)
		=> new(success: true, keyName, keyType, keySize, keyVersion);

	/// <summary>
	/// Creates a failed key generation result.
	/// </summary>
	/// <param name="errorMessage"> The error message describing the failure. </param>
	/// <returns> A failed key generation result. </returns>
	public static KeyGenerationResult CreateFailure(string errorMessage)
		=> new(success: false, errorMessage: errorMessage);
}
