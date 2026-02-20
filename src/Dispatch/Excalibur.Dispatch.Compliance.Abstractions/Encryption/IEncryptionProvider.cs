// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Provides field-level encryption and decryption services.
/// </summary>
/// <remarks>
/// <para>
/// This is the canonical encryption interface for the Excalibur framework. Implementations should use AES-256-GCM for
/// authenticated encryption.
/// </para>
/// <para>
/// Key features:
/// - Async-only API for cloud KMS integration
/// - Byte array interface (callers handle serialization)
/// - Context-based key selection and tenant isolation
/// - Support for key rotation and FIPS 140-2 compliance
/// </para>
/// </remarks>
public interface IEncryptionProvider
{
	/// <summary>
	/// Encrypts plaintext data using the specified context.
	/// </summary>
	/// <param name="plaintext"> The data to encrypt. </param>
	/// <param name="context">
	/// The encryption context providing key selection, tenant isolation, and associated data. Use <see cref="EncryptionContext.Default" />
	/// for default settings.
	/// </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The encrypted data including ciphertext and metadata required for decryption. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="plaintext" /> is null. </exception>
	/// <exception cref="EncryptionException"> Thrown when encryption fails. </exception>
	Task<EncryptedData> EncryptAsync(
		byte[] plaintext,
		EncryptionContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Decrypts ciphertext data using the embedded key metadata.
	/// </summary>
	/// <param name="encryptedData"> The encrypted data including ciphertext and key metadata. </param>
	/// <param name="context">
	/// The encryption context for tenant isolation and associated data verification. Must match the context used during encryption for
	/// AAD-bound ciphertexts.
	/// </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> The decrypted plaintext data. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="encryptedData" /> is null. </exception>
	/// <exception cref="EncryptionException"> Thrown when decryption fails (e.g., key not found, tampered data). </exception>
	Task<byte[]> DecryptAsync(
		EncryptedData encryptedData,
		EncryptionContext context,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates that the provider is configured for FIPS 140-2 compliant operations.
	/// </summary>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> True if FIPS 140-2 compliance is validated; otherwise, false. </returns>
	Task<bool> ValidateFipsComplianceAsync(CancellationToken cancellationToken);
}
