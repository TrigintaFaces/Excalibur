// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Manages the lifecycle of per-subject encryption keys that underpin crypto-shredding.
/// </summary>
/// <remarks>
/// <para>
/// Each data subject is assigned its own key handle. Encrypting a subject's personal data under that key
/// makes the data recoverable only while the key exists; destroying the key erases the subject
/// irreversibly (crypto-shredding), which satisfies the right-to-erasure without mutating every stored
/// record.
/// </para>
/// <para>
/// Subject identifiers are pseudonymized through the registered data-subject hasher before being used as
/// key handles, so raw identifiers never leak into the key store.
/// </para>
/// <para>
/// Key material MUST be produced by a cryptographically secure random number generator
/// (<see cref="System.Security.Cryptography.RandomNumberGenerator"/>) via the underlying key provider —
/// never from <see cref="System.Guid"/> or <see cref="System.Random"/>. This is a security invariant of
/// crypto-shredding: predictable key material would allow shredded data to be reconstructed.
/// </para>
/// </remarks>
public interface ISubjectKeyManager
{
	/// <summary>
	/// Returns the key handle for a data subject, creating a new cryptographically-random key if the
	/// subject does not yet have one.
	/// </summary>
	/// <param name="subjectId">
	/// The raw data-subject identifier. It is pseudonymized through the registered data-subject hasher
	/// before being resolved to a key handle.
	/// </param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the key handle identifying the subject's active key.</returns>
	ValueTask<string> GetOrCreateKeyAsync(string subjectId, CancellationToken cancellationToken);

	/// <summary>
	/// Crypto-erases a data subject by destroying every version of the subject's key.
	/// </summary>
	/// <remarks>
	/// The operation is idempotent: destroying an already-destroyed (or never-created) subject key
	/// completes successfully without error.
	/// </remarks>
	/// <param name="subjectId">
	/// The raw data-subject identifier. It is pseudonymized through the registered data-subject hasher
	/// before its key versions are located and destroyed.
	/// </param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when all key versions for the subject have been destroyed.</returns>
	ValueTask DestroyKeyAsync(string subjectId, CancellationToken cancellationToken);
}
