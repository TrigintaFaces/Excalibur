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
	/// Verifies the authentication tag of an encrypted field against its ciphertext, detecting tampering without
	/// exposing the plaintext.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The tag is verified cryptographically: the field's key is resolved by the version stamped on the envelope, the
	/// authenticated associated data is reconstructed from <paramref name="fieldName"/> and the envelope's own crypto
	/// context, and the AEAD tag is checked against the ciphertext. A substituted, truncated, or recomputed tag does
	/// not verify, and neither does a ciphertext moved between fields.
	/// </para>
	/// <para>
	/// <paramref name="fieldName"/> is required because it is bound into the associated data at encryption time. The
	/// envelope alone cannot authenticate itself: it carries the key version, algorithm, and classification, but not
	/// the name of the field it belongs to, and all four are needed to reconstruct what the tag actually covers. Pass
	/// the same field name that was used to encrypt the value.
	/// </para>
	/// <para>
	/// The plaintext recovered during verification is discarded and its buffer zeroed; use
	/// <c>DecryptFieldAsync</c> when the value itself is needed.
	/// </para>
	/// </remarks>
	/// <param name="fieldName"> The name of the field the encrypted value belongs to, as used when it was encrypted. </param>
	/// <param name="encryptedField"> The encrypted field data to validate. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains <see langword="true"/> only when the
	/// authentication tag verifies against the ciphertext. It contains <see langword="false"/> when tampering is
	/// detected, and also whenever integrity cannot be established at all -- an envelope with no authentication tag, an
	/// unrecognized envelope format, an unavailable key version, or an unsupported algorithm. A
	/// <see langword="false"/> result therefore means "not established as intact", never "confirmed tampered".
	/// </returns>
	/// <exception cref="ArgumentException"> Thrown when <paramref name="fieldName"/> is null or empty. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="encryptedField"/> is null. </exception>
	Task<bool> ValidateIntegrityAsync(
		string fieldName,
		EncryptedFieldResult encryptedField,
		CancellationToken cancellationToken);

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
