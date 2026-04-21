// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines maintenance operations for field-level encryption including integrity validation
/// and key rotation.
/// </summary>
public interface IElasticsearchFieldEncryptionMaintenance
{
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
		ElasticSearchDataClassification classification,
		CancellationToken cancellationToken);
}
