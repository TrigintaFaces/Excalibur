// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines core field-level encryption and decryption operations for Elasticsearch documents.
/// </summary>
public interface IElasticsearchFieldEncryption
{
	/// <summary>
	/// Gets the encryption algorithms supported by this encryptor.
	/// </summary>
	/// <value> A collection of supported encryption algorithm names. </value>
	IReadOnlyCollection<string> SupportedAlgorithms { get; }

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
		ElasticSearchDataClassification classification,
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
}
