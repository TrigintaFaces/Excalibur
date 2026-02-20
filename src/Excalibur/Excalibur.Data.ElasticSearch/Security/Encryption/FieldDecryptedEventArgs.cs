// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Provides data for the FieldDecrypted event.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FieldDecryptedEventArgs" /> class.
/// </remarks>
/// <param name="fieldName"> The name of the field that was decrypted. </param>
/// <param name="classification"> The data classification level of the field. </param>
/// <param name="algorithm"> The encryption algorithm that was used. </param>
/// <param name="keyVersion"> The version of the encryption key that was used. </param>
/// <param name="decryptedAt"> The timestamp when the field was decrypted. </param>
public sealed class FieldDecryptedEventArgs(
	string fieldName,
	DataClassification classification,
	string algorithm,
	string keyVersion,
	DateTimeOffset decryptedAt) : EventArgs
{
	/// <summary>
	/// Gets the name of the field that was decrypted.
	/// </summary>
	/// <value> The field name that underwent decryption. </value>
	public string FieldName { get; } = fieldName ?? throw new ArgumentNullException(nameof(fieldName));

	/// <summary>
	/// Gets the data classification level of the field.
	/// </summary>
	/// <value> The sensitivity classification of the decrypted field. </value>
	public DataClassification Classification { get; } = classification;

	/// <summary>
	/// Gets the encryption algorithm that was used.
	/// </summary>
	/// <value> The name of the encryption algorithm. </value>
	public string Algorithm { get; } = algorithm ?? throw new ArgumentNullException(nameof(algorithm));

	/// <summary>
	/// Gets the version of the encryption key that was used.
	/// </summary>
	/// <value> The key version identifier. </value>
	public string KeyVersion { get; } = keyVersion ?? throw new ArgumentNullException(nameof(keyVersion));

	/// <summary>
	/// Gets the timestamp when the field was decrypted.
	/// </summary>
	/// <value> The UTC timestamp of the decryption operation. </value>
	public DateTimeOffset DecryptedAt { get; } = decryptedAt;
}
