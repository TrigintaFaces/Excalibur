// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Provides data for the EncryptionKeyRotated event.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="EncryptionKeyRotatedEventArgs" /> class.
/// </remarks>
/// <param name="classification"> The data classification level that was rotated. </param>
/// <param name="newKeyVersion"> The version of the new encryption key. </param>
/// <param name="previousKeyVersion"> The version of the previous encryption key. </param>
/// <param name="affectedDocumentCount"> The number of documents that were re-encrypted. </param>
/// <param name="rotatedAt"> The timestamp when the key rotation occurred. </param>
public sealed class EncryptionKeyRotatedEventArgs(
	DataClassification classification,
	string newKeyVersion,
	string previousKeyVersion,
	int affectedDocumentCount,
	DateTimeOffset rotatedAt) : EventArgs
{
	/// <summary>
	/// Gets the data classification level that was rotated.
	/// </summary>
	/// <value> The classification level for which keys were rotated. </value>
	public DataClassification Classification { get; } = classification;

	/// <summary>
	/// Gets the version of the new encryption key.
	/// </summary>
	/// <value> The version identifier for the new key. </value>
	public string NewKeyVersion { get; } = newKeyVersion ?? throw new ArgumentNullException(nameof(newKeyVersion));

	/// <summary>
	/// Gets the version of the previous encryption key.
	/// </summary>
	/// <value> The version identifier for the previous key. </value>
	public string PreviousKeyVersion { get; } = previousKeyVersion ?? throw new ArgumentNullException(nameof(previousKeyVersion));

	/// <summary>
	/// Gets the number of documents that were re-encrypted with the new key.
	/// </summary>
	/// <value> The count of documents affected by the key rotation. </value>
	public int AffectedDocumentCount { get; } = affectedDocumentCount;

	/// <summary>
	/// Gets the timestamp when the key rotation occurred.
	/// </summary>
	/// <value> The UTC timestamp of the key rotation operation. </value>
	public DateTimeOffset RotatedAt { get; } = rotatedAt;
}
