// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// Provides field-level encryption and decryption services.
/// </summary>
/// <remarks>
/// <para>
/// This is the canonical encryption interface for the Excalibur framework.
/// </para>
/// <para>
/// <b>Authenticated encryption is REQUIRED, and the requirement is a property rather than an algorithm.</b>
/// <see cref="DecryptAsync" /> MUST throw <see cref="EncryptionException" /> when the ciphertext, the key metadata or the
/// associated data has been modified since <see cref="EncryptAsync" /> returned it, and MUST NOT return plaintext for
/// modified input. Callers rely on this: a successful <see cref="DecryptAsync" /> is evidence that the data is
/// unmodified, not merely that it was decryptable. Framework components that handle encrypted data reason from that
/// guarantee, so an implementation which decrypts modified input silently weakens every component built on it.
/// </para>
/// <para>
/// Stating the property rather than naming a cipher is deliberate: AES-256-GCM, ChaCha20-Poly1305, AES-GCM-SIV and the
/// authenticated modes offered by cloud KMS providers all satisfy it, while an unauthenticated mode such as AES-CBC
/// without a MAC does not, whatever its key length. Implementations are free to choose any construction that keeps the
/// requirement.
/// </para>
/// <para>
/// Implementations SHOULD be verified against <c>EncryptionProviderConformanceTestKit</c>, which exercises the
/// requirement directly: it modifies a ciphertext byte and asserts the failure, modifies the associated data and asserts
/// the failure, and pairs both with an untampered round-trip that must still succeed.
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
	/// <returns>
	/// The decrypted plaintext data. A successful return is evidence that the ciphertext, key metadata and associated
	/// data are unmodified since encryption; see the authenticated-encryption requirement on <see cref="IEncryptionProvider" />.
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="encryptedData" /> is null. </exception>
	/// <exception cref="EncryptionException">
	/// Thrown when decryption fails. Implementations MUST throw this when the ciphertext, the key metadata or the
	/// associated data has been modified, and MUST NOT return plaintext for modified input. Also thrown when the key
	/// cannot be found or the context does not match the one used during encryption.
	/// </exception>
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
