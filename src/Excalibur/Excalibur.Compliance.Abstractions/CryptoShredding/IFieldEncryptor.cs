// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Transparently encrypts and decrypts personal-data fields under a data subject's key, so that
/// destroying the key (crypto-shredding) is sufficient to erase the subject.
/// </summary>
/// <remarks>
/// Fields marked with <see cref="PersonalDataAttribute"/> are the intended inputs to this encryptor.
/// </remarks>
public interface IFieldEncryptor
{
	/// <summary>
	/// Encrypts plaintext under the data subject's key, producing a subject-bound ciphertext envelope.
	/// </summary>
	/// <remarks>
	/// This write path fails closed: any encryption failure throws. It MUST NOT return plaintext or a
	/// partially-protected value under any circumstances.
	/// </remarks>
	/// <param name="subjectId">The raw data-subject identifier whose key protects the value.</param>
	/// <param name="plaintext">The plaintext bytes to encrypt.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the subject-bound ciphertext envelope.</returns>
	ValueTask<EncryptedData> EncryptAsync(string subjectId, System.ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken);

	/// <summary>
	/// Decrypts a subject-bound ciphertext envelope back to plaintext.
	/// </summary>
	/// <remarks>
	/// This read path degrades open for shredded subjects: when the subject's key has been destroyed
	/// (the data has been crypto-erased), it returns <see langword="null"/> as a tombstone rather than
	/// throwing. Genuine integrity or algorithm failures still surface as exceptions.
	/// </remarks>
	/// <param name="envelope">The subject-bound ciphertext envelope to decrypt.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>
	/// A task that completes with the decrypted plaintext bytes, or <see langword="null"/> when the
	/// subject's key has been destroyed.
	/// </returns>
	ValueTask<byte[]?> DecryptAsync(EncryptedData envelope, CancellationToken cancellationToken);
}
