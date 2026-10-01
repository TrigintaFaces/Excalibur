// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// An optional capability for an <see cref="IKeyManagementProvider"/> that can say, authoritatively, whether a
/// key's material has been irreversibly destroyed.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IKeyManagementProvider.GetKeyAsync"/> returns <see langword="null"/> for a key it cannot find,
/// and on some backends a key that has been deleted but is still recoverable is exactly such a key: Azure Key
/// Vault, for example, answers a soft-deleted key as not found for its whole retention period. For those
/// backends "not found" does not mean "destroyed", so it cannot be used to confirm an erasure.
/// </para>
/// <para>
/// A key provider MUST implement this capability, and advertise it by answering for it from
/// <see cref="IServiceProvider.GetService(Type)"/>, for erasures that destroy its keys to complete.
/// <see cref="IErasureVerificationService.VerifyKeyDeletionAsync"/> never falls back to the key lookup: without
/// this capability a key is never confirmed destroyed, and an erasure that depends on it stays awaiting
/// destruction and is never certified. A provider with no recovery window implements it trivially -- a key it
/// does not hold is destroyed -- which is a statement the provider makes, not one erasure assumes. It is a
/// segregated capability rather than a member of <see cref="IKeyManagementProvider"/> or
/// <see cref="IKeyManagementAdmin"/>, so that a provider that has no way to answer is never made to invent one.
/// </para>
/// <para>
/// <b>Two questions, two overloads.</b> The handle-scoped overload answers "may we attest?" -- asked once, at
/// erasure completion, about the state of a handle now. It is NOT a read-path predicate, because it reports on
/// the handle rather than on the key that produced any particular envelope, and the two come apart in both
/// directions:
/// </para>
/// <para>
/// A handle SPLIT INTERNALLY -- one version destroyed or trimmed while a later one lives, which rotation
/// produces and which HashiCorp Vault and AWS KMS both permit -- answers <see langword="false"/> although the
/// version an envelope names is gone, because only an all-versions-destroyed handle is a destroyed handle.
/// That is what the version-scoped overload is for, and a read asks it: a read decrypts one envelope, and an
/// envelope names one version. Not every backend can split a handle -- Azure Key Vault has no per-version
/// delete, so a delete takes the key with every version it holds and the two overloads necessarily agree
/// there. That is a statement each backend makes for itself, never one a caller may assume.
/// </para>
/// <para>
/// A handle RE-OCCUPIED after a destruction holds live material, so both overloads answer
/// <see langword="false"/> while an older envelope's key is gone. Nothing asked here establishes that a
/// destroyed handle is never re-occupied: that is a property of how handles are composed, and it stays the
/// caller's to establish before reading any answer as a statement about one particular ciphertext.
/// </para>
/// </remarks>
public interface IKeyDestructionStatusProvider
{
	/// <summary>
	/// Determines whether the backend still holds any recoverable copy of a key's material.
	/// </summary>
	/// <param name="keyId">The key identifier, in the same form accepted by <see cref="IKeyManagementProvider.GetKeyAsync"/>.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> only when the backend holds no recoverable copy of the key -- it was destroyed, or
	/// never existed. <see langword="false"/> when the key is live, or deleted but still recoverable.
	/// </returns>
	/// <remarks>
	/// A failure to obtain an answer is thrown, never reported as either value, so a caller can never mistake
	/// "could not ask" for "destroyed".
	/// </remarks>
	Task<bool> IsKeyDestroyedAsync(string keyId, CancellationToken cancellationToken);

	/// <summary>
	/// Determines whether the backend still holds any recoverable copy of the material behind ONE version of a key.
	/// </summary>
	/// <param name="keyId">The key identifier, in the same form accepted by <see cref="IKeyManagementProvider.GetKeyVersionAsync"/>.</param>
	/// <param name="version">The key version the ciphertext was produced under, as carried on its envelope.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> only when the backend holds no recoverable copy of that version's material -- it was
	/// destroyed, or never existed. <see langword="false"/> when it is live, or deleted but still recoverable.
	/// </returns>
	/// <remarks>
	/// This is the overload a READ asks, and it must be asked of the same object the read operates on: a read
	/// decrypts one envelope, and an envelope names one version. The handle-scoped overload cannot stand in for
	/// it, because a handle carrying one destroyed version and one live version is not a destroyed handle. A
	/// backend with no per-version destruction answers this exactly as it answers the handle.
	/// <para>
	/// A failure to obtain an answer is thrown, never reported as either value, so a caller can never mistake
	/// "could not ask" for "destroyed".
	/// </para>
	/// </remarks>
	Task<bool> IsKeyDestroyedAsync(string keyId, int version, CancellationToken cancellationToken);

	/// <summary>
	/// Determines whether the backend still holds any recoverable copy of the material of ONE GENERATION of a
	/// key handle.
	/// </summary>
	/// <param name="keyId">The key handle, in the same form accepted by <see cref="IKeyManagementProvider.GetKeyAsync"/>.</param>
	/// <param name="generation">
	/// The generation identifier the ciphertext was produced under, as carried on its envelope and reported by
	/// <see cref="KeyMetadata.Generation"/>.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> only when the backend holds no recoverable copy of that generation's material.
	/// <see langword="false"/> when it is live, or deleted but still recoverable.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>This is the overload a crypto-shred READ must ask, and the only one whose answer cannot be changed by
	/// a later provisioning.</b> The handle-scoped and version-scoped overloads both describe the handle as it
	/// is NOW: destroy a subject's key and let an ordinary write provision another at the same handle, and both
	/// report "not destroyed" — truthfully, about different material than the reader is holding. A generation is
	/// minted once and never reused, so a destroyed generation stays destroyed no matter what follows it.
	/// </para>
	/// <para>
	/// A generation this handle has never held answers <see langword="true"/>: the backend holds no recoverable
	/// copy of it. That is the same codomain limit the handle-scoped overload has — it cannot separate
	/// "destroyed" from "never existed" — and it is safe HERE in a way it is not there, because a generation
	/// identifier is not guessable from the subject, so a caller holding one is holding evidence that the
	/// material existed.
	/// </para>
	/// <para>
	/// A failure to obtain an answer is thrown, never reported as either value, so a caller can never mistake
	/// "could not ask" for "destroyed".
	/// </para>
	/// </remarks>
	Task<bool> IsKeyDestroyedAsync(string keyId, string generation, CancellationToken cancellationToken);
}
