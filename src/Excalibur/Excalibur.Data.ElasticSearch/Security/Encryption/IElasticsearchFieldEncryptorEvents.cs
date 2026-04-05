// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines events raised during field encryption operations for audit and monitoring purposes.
/// </summary>
public interface IElasticsearchFieldEncryptorEvents
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
}
