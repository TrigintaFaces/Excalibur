// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// An optional capability for an <see cref="IKeyManagementProvider"/> that states whether its backend still
/// holds any recoverable copy of a key handle's material.
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
/// <b>This is a backend attestation, and it is NOT a read predicate.</b> It is asked about a handle, once, at
/// erasure completion -- never about the material behind a particular ciphertext. A backend cannot answer the
/// read's question, because a backend retains nothing about material it never held: "we destroyed this" and
/// "this was never here" are the same observation at the backend, permanently and by construction. The
/// information that separates them existed at one instant and was held by one actor, the destroyer, which is
/// why a read's tombstone is produced from <see cref="IKeyDestructionLedger"/> -- a durable record of a
/// destruction that was performed -- and never from anything asked here.
/// </para>
/// <para>
/// Two further ways a handle's state comes apart from a ciphertext's, both of which make this answer unsafe to
/// read as a statement about one envelope. A handle SPLIT INTERNALLY -- one version destroyed or trimmed while
/// a later one lives, which rotation produces and which HashiCorp Vault and AWS KMS both permit -- answers
/// <see langword="false"/> although the material an older envelope names is gone, because only an
/// all-versions-destroyed handle is a destroyed handle. A handle RE-OCCUPIED after a destruction holds live
/// material, so it answers <see langword="false"/> while an older envelope's key is gone.
/// </para>
/// </remarks>
public interface IKeyDestructionStatusProvider
{
	/// <summary>
	/// Determines whether the backend still holds any recoverable copy of a key handle's material.
	/// </summary>
	/// <param name="keyId">The key identifier, in the same form accepted by <see cref="IKeyManagementProvider.GetKeyAsync"/>.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// <see langword="true"/> only when the backend holds no recoverable copy of the handle's material.
	/// <see langword="false"/> when the key is live, or deleted but still recoverable.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The whole of what an implementation asserts by answering <see langword="true"/> is <i>"I hold no
	/// recoverable copy of this handle."</i> That is a statement about what the backend can serve or restore
	/// now. It is NOT a statement that the material was destroyed, and it is NOT a statement that this backend
	/// destroyed it -- a handle this backend never held answers <see langword="true"/> for the same reason a
	/// purged one does, and nothing here can separate the two.
	/// </para>
	/// <para>
	/// <b>Absence is never evidence of destruction.</b> An implementation MUST NOT derive
	/// <see langword="true"/> from a lookup that merely failed to produce an answer -- a not-found response, a
	/// missing sidecar record, an absent marker, an empty cache entry. Those are states in which the question
	/// was not answered, and reporting them as "destroyed" manufactures an attestation out of a failure. The
	/// only legitimate basis for <see langword="true"/> is a backend statement that no recoverable copy remains.
	/// </para>
	/// <para>
	/// An answer that cannot be obtained is thrown, never reported as either value, so a caller can never
	/// mistake "could not ask" for "destroyed" -- nor for "not destroyed", which would certify an erasure as
	/// incomplete on an outage.
	/// </para>
	/// </remarks>
	Task<bool> IsKeyDestroyedAsync(string keyId, CancellationToken cancellationToken);
}
