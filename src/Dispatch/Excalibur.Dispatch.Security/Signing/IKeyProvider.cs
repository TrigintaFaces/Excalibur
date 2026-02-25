// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Provides signing keys from secure storage.
/// </summary>
public interface IKeyProvider
{
	/// <summary>
	/// Retrieves a signing key by identifier.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>A task that completes with the key bytes.</returns>
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
