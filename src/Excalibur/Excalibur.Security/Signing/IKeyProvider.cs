// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Security;

/// <summary>
/// Provides signing keys from secure storage.
/// </summary>
public interface IKeyProvider
{
	/// <summary>
	/// Retrieves an existing signing key by identifier.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <strong>Fail-closed contract:</strong> this method is <em>retrieve-only</em>. When
	/// <paramref name="keyId"/> is unknown, an implementation MUST fail closed by throwing — throwing an
	/// exception from the signing exception family is recommended so the failure is caught gracefully by
	/// the signing pipeline. It MUST NOT generate, fabricate, or return a substitute or randomly-minted
	/// key.
	/// </para>
	/// <para>
	/// Key <em>creation</em> is the exclusive responsibility of <see cref="StoreKeyAsync"/> and
	/// <see cref="RotateKeyAsync"/>. Minting a key inside a retrieval path would silently sign messages
	/// with a key no verifier could know, defeating verification — so it is forbidden by this contract.
	/// </para>
	/// </remarks>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the key bytes for the specified <paramref name="keyId"/>.</returns>
	Task<byte[]> GetKeyAsync(string keyId, CancellationToken cancellationToken);

	/// <summary>
	/// Stores a new signing key.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="key">The key material to store.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes when the key is stored.</returns>
	Task StoreKeyAsync(string keyId, byte[] key, CancellationToken cancellationToken);

	/// <summary>
	/// Rotates a signing key.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the new key material.</returns>
	Task<byte[]> RotateKeyAsync(string keyId, CancellationToken cancellationToken);
}
