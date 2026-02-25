// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the result of a field encryption operation including encrypted data and metadata.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="EncryptedFieldResult" /> class.
/// </remarks>
/// <param name="encryptedValue"> The encrypted field value. </param>
/// <param name="algorithm"> The encryption algorithm used. </param>
/// <param name="keyVersion"> The version of the encryption key used. </param>
/// <param name="initializationVector"> The initialization vector used for encryption. </param>
/// <param name="authenticationTag"> The authentication tag for integrity verification. </param>
/// <param name="classification"> The data classification level of the original field. </param>
public sealed class EncryptedFieldResult(
	string encryptedValue,
	string algorithm,
	string keyVersion,
	string? initializationVector = null,
	string? authenticationTag = null,
	DataClassification classification = DataClassification.Confidential)
{
	/// <summary>
	/// Gets the encrypted field value as a Base64-encoded string.
	/// </summary>
	/// <value> The encrypted data encoded as a Base64 string. </value>
	public string EncryptedValue { get; } = encryptedValue ?? throw new ArgumentNullException(nameof(encryptedValue));

	/// <summary>
	/// Gets the encryption algorithm used for this field.
	/// </summary>
	/// <value> The name of the encryption algorithm. </value>
	public string Algorithm { get; } = algorithm ?? throw new ArgumentNullException(nameof(algorithm));

	/// <summary>
	/// Gets the version of the encryption key used.
	/// </summary>
	/// <value> The key version identifier for key rotation support. </value>
	public string KeyVersion { get; } = keyVersion ?? throw new ArgumentNullException(nameof(keyVersion));

	/// <summary>
	/// Gets the initialization vector used for encryption.
	/// </summary>
	/// <value> The IV as a Base64-encoded string, or null if not applicable. </value>
	public string? InitializationVector { get; } = initializationVector;

	/// <summary>
	/// Gets the authentication tag for integrity verification.
	/// </summary>
	/// <value> The authentication tag as a Base64-encoded string, or null if not supported. </value>
	public string? AuthenticationTag { get; } = authenticationTag;

	/// <summary>
	/// Gets the data classification level of the original field.
	/// </summary>
	/// <value> The sensitivity classification of the encrypted data. </value>
	public DataClassification Classification { get; } = classification;

	/// <summary>
	/// Gets the timestamp when the field was encrypted.
	/// </summary>
	/// <value> The UTC timestamp of the encryption operation. </value>
	public DateTimeOffset EncryptedAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets a value indicating whether this encrypted field includes integrity protection.
	/// </summary>
	/// <value> True if the field has an authentication tag for integrity verification, false otherwise. </value>
	public bool HasIntegrityProtection => !string.IsNullOrEmpty(AuthenticationTag);
}
