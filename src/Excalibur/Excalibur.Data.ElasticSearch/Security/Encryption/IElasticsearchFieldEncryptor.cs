// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for field-level encryption of sensitive data in Elasticsearch documents with support for multiple encryption
/// algorithms and key management integration.
/// </summary>
public interface IElasticsearchFieldEncryptor
{
	/// <summary>
	/// Occurs when a field is encrypted, for audit and monitoring purposes.
	/// </summary>
	event EventHandler<FieldEncryptedEventArgs>? FieldEncrypted;

	/// <summary>
	/// Occurs when a field is decrypted, for audit and monitoring purposes.
	/// </summary>
	event EventHandler<FieldDecryptedEventArgs>? FieldDecrypted;

	/// <summary>
	/// Occurs when encryption key rotation is completed.
	/// </summary>
	event EventHandler<EncryptionKeyRotatedEventArgs>? KeyRotated;

	/// <summary>
	/// Gets the encryption algorithms supported by this encryptor.
	/// </summary>
	/// <value> A collection of supported encryption algorithm names. </value>
	IReadOnlyCollection<string> SupportedAlgorithms { get; }

	/// <summary>
	/// Gets a value indicating whether this encryptor supports key rotation.
	/// </summary>
	/// <value> True if the encryptor supports automatic key rotation, false otherwise. </value>
	bool SupportsKeyRotation { get; }

	/// <summary>
	/// Gets a value indicating whether this encryptor supports integrity validation.
	/// </summary>
	/// <value> True if the encryptor provides data integrity checking, false otherwise. </value>
	bool SupportsIntegrityValidation { get; }

	/// <summary>
	/// Encrypts sensitive field values within an Elasticsearch document based on data classification rules.
	/// </summary>
	/// <param name="document"> The document containing fields to be encrypted. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the document with sensitive fields encrypted
	/// according to security policies.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when encryption fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the document is null. </exception>
	Task<object> EncryptDocumentAsync(object document, CancellationToken cancellationToken);

	/// <summary>
	/// Decrypts encrypted field values within an Elasticsearch document to restore original data.
	/// </summary>
	/// <param name="encryptedDocument"> The document containing encrypted fields to be decrypted. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the document with encrypted fields decrypted to
	/// their original values.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when decryption fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the document is null. </exception>
	Task<object> DecryptDocumentAsync(object encryptedDocument, CancellationToken cancellationToken);

	/// <summary>
	/// Encrypts a specific field value using the appropriate encryption algorithm and key.
	/// </summary>
	/// <param name="fieldName"> The name of the field being encrypted. </param>
	/// <param name="fieldValue"> The value to encrypt. </param>
	/// <param name="classification"> The data classification level for the field. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the encrypted field result including the encrypted
	/// value and metadata.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when field encryption fails due to security constraints. </exception>
	/// <exception cref="ArgumentException"> Thrown when field name or value is invalid. </exception>
	Task<EncryptedFieldResult> EncryptFieldAsync(
		string fieldName,
		object fieldValue,
		DataClassification classification,
		CancellationToken cancellationToken);

	/// <summary>
	/// Decrypts a specific encrypted field value to restore the original data.
	/// </summary>
	/// <param name="fieldName"> The name of the field being decrypted. </param>
	/// <param name="encryptedField"> The encrypted field data containing the encrypted value and metadata. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the decrypted original value. </returns>
	/// <exception cref="SecurityException"> Thrown when field decryption fails due to security constraints. </exception>
	/// <exception cref="ArgumentException"> Thrown when field name or encrypted data is invalid. </exception>
	Task<object> DecryptFieldAsync(string fieldName, EncryptedFieldResult encryptedField, CancellationToken cancellationToken);

	/// <summary>
	/// Determines whether a specific field should be encrypted based on security rules and data classification.
	/// </summary>
	/// <param name="fieldName"> The name of the field to evaluate. </param>
	/// <param name="fieldValue"> The value of the field to evaluate. </param>
	/// <returns> True if the field should be encrypted according to security policies, false otherwise. </returns>
	bool ShouldEncryptField(string fieldName, object? fieldValue);

	/// <summary>
	/// Gets the data classification level for a specific field based on security rules.
	/// </summary>
	/// <param name="fieldName"> The name of the field to classify. </param>
	/// <param name="fieldValue"> The value of the field to classify. </param>
	/// <returns> The data classification level for the field, or Public if no specific classification applies. </returns>
	DataClassification GetFieldClassification(string fieldName, object? fieldValue);

	/// <summary>
	/// Validates that encrypted data integrity is maintained and has not been tampered with.
	/// </summary>
	/// <param name="encryptedField"> The encrypted field data to validate. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the encrypted data integrity is valid, false
	/// if tampering is detected.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when integrity validation fails due to security constraints. </exception>
	Task<bool> ValidateIntegrityAsync(EncryptedFieldResult encryptedField, CancellationToken cancellationToken);

	/// <summary>
	/// Rotates encryption keys for a specific data classification level, re-encrypting affected data.
	/// </summary>
	/// <param name="classification"> The data classification level to rotate keys for. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the key rotation result including success status and
	/// affected document count.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when key rotation fails due to security constraints. </exception>
	Task<EncryptionKeyRotationResult> RotateEncryptionKeysAsync(
		DataClassification classification,
		CancellationToken cancellationToken);
}
