// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// Transparently encrypts and decrypts personal-data fields under a data subject's key, so that
/// destroying the key (crypto-shredding) is sufficient to erase the subject.
/// </summary>
/// <remarks>
/// Fields marked with <see cref="PersonalDataAttribute"/> are the intended inputs to this encryptor.
/// <para>
/// Default crypto-shredding registrations retain this collaborator in singleton services. Replacement
/// implementations retained by that default graph must support concurrent calls and lifetime-compatible
/// dependencies. Existing registrations are preserved; scoped replacements are not promoted or made
/// singleton-safe by this registration.
/// </para>
/// </remarks>
public interface IFieldEncryptor
{
	/// <summary>
	/// Encrypts plaintext under the data subject's key, producing a subject-bound ciphertext envelope.
	/// </summary>
	/// <remarks>
	/// This write path fails closed: any encryption failure throws. It MUST NOT return plaintext or a
	/// partially-protected value under any circumstances.
	/// A resolved tenant context is required before key creation. The framework's single-tenant default
	/// supplies that identity; an unresolved context is refused rather than treated as the default tenant.
	/// </remarks>
	/// <param name="subjectId">The raw data-subject identifier whose key protects the value.</param>
	/// <param name="retentionScope">
	/// Where this value sits — the aggregate type and the capacity the subject holds there — or
	/// <see cref="RetentionScope.NotInAnAggregate"/> when it is not stored inside an aggregate. It selects
	/// the subject's key unless that capacity is under a declared erasure retention, in which case it
	/// selects the capacity's own key — which is what lets a legally-retained record stay readable after
	/// the subject is erased.
	/// </param>
	/// <param name="plaintext">The plaintext bytes to encrypt.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the subject-bound ciphertext envelope.</returns>
	/// <exception cref="InvalidOperationException">The tenant context has no resolved, nonblank tenant identity.</exception>
	ValueTask<EncryptedData> EncryptAsync(string subjectId, RetentionScope retentionScope, System.ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken);

	/// <summary>
	/// Decrypts a subject-bound ciphertext envelope back to plaintext.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This read path degrades open for shredded subjects: when the subject's key has been destroyed
	/// (the data has been crypto-erased), it returns <see langword="null"/> as a tombstone rather than
	/// throwing. Genuine integrity or algorithm failures still surface as exceptions.
	/// </para>
	/// <para>
	/// <b>A tombstone is not the only non-success outcome.</b> Two envelopes are REFUSED rather than read,
	/// before any key is resolved: one declaring a layout version this implementation does not write, and one
	/// naming no key generation. Both raise an exception carrying
	/// <see cref="EncryptionErrorCode.InvalidCiphertext"/>. Neither is reported as an erasure, because the
	/// material behind such an envelope cannot be identified -- and a key that was destroyed is then
	/// indistinguishable from one provisioned again at the same handle afterwards, which is the difference
	/// between stating that an erasure happened and stating one that did not.
	/// </para>
	/// <para>
	/// <b>No scope parameter, and none is needed.</b> The envelope carries the handle of the key that
	/// produced it, so a value written under a retained aggregate type's key is decrypted under that key
	/// without the caller having to remember which type it came from.
	/// </para>
	/// </remarks>
	/// <param name="envelope">The subject-bound ciphertext envelope to decrypt.</param>
	/// <param name="expectedTenantId">
	/// The owning tenant established by the authorized request or trusted stored-record metadata.
	/// Do not derive this value from the supplied envelope. Missing or mismatched ownership is
	/// rejected before any provider or destruction-ledger lookup, including for erased generations.
	/// </param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>
	/// A task that completes with the decrypted plaintext bytes, or <see langword="null"/> when the
	/// subject's key has been destroyed.
	/// </returns>
	ValueTask<byte[]?> DecryptAsync(EncryptedData envelope, string expectedTenantId, CancellationToken cancellationToken);
}
