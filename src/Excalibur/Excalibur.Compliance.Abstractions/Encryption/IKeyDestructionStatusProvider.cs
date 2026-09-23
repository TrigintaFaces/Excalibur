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
}
