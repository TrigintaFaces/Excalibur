// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Provides data for the FieldEncrypted event.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FieldEncryptedEventArgs" /> class.
/// </remarks>
/// <param name="fieldName"> The name of the field that was encrypted. </param>
/// <param name="classification"> The data classification level of the field. </param>
/// <param name="algorithm"> The encryption algorithm used. </param>
/// <param name="keyVersion"> The version of the encryption key used. </param>
/// <param name="encryptedAt"> The timestamp when the field was encrypted. </param>
public sealed class FieldEncryptedEventArgs(
	string fieldName,
	DataClassification classification,
	string algorithm,
	string keyVersion,
	DateTimeOffset encryptedAt) : EventArgs
{
	/// <summary>
	/// Gets the name of the field that was encrypted.
	/// </summary>
	/// <value> The field name that underwent encryption. </value>
	public string FieldName { get; } = fieldName ?? throw new ArgumentNullException(nameof(fieldName));

	/// <summary>
	/// Gets the data classification level of the field.
	/// </summary>
	/// <value> The sensitivity classification of the encrypted field. </value>
	public DataClassification Classification { get; } = classification;

	/// <summary>
	/// Gets the encryption algorithm used.
	/// </summary>
	/// <value> The name of the encryption algorithm. </value>
	public string Algorithm { get; } = algorithm ?? throw new ArgumentNullException(nameof(algorithm));

	/// <summary>
	/// Gets the version of the encryption key used.
	/// </summary>
	/// <value> The key version identifier. </value>
	public string KeyVersion { get; } = keyVersion ?? throw new ArgumentNullException(nameof(keyVersion));

	/// <summary>
	/// Gets the timestamp when the field was encrypted.
	/// </summary>
	/// <value> The UTC timestamp of the encryption operation. </value>
	public DateTimeOffset EncryptedAt { get; } = encryptedAt;
}
