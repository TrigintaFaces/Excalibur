// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines version-addressed secret retrieval and retention operations for key management providers that support key
/// rotation without destroying prior key material.
/// </summary>
/// <remarks>
/// <para>
/// This interface exists to keep ciphertext recoverable across key rotation. When a key is rotated, ciphertext that was
/// produced with an earlier key version must still be decryptable, which requires the provider to retain prior versions
/// and to expose retrieval addressed by an explicit version identifier rather than always returning the current key.
/// </para>
/// <para>
/// It is composed into <see cref="IElasticsearchKeyProvider"/> alongside the storage, management, and events sub-interfaces
/// so consumers that only need versioned retrieval can depend on this focused contract.
/// </para>
/// </remarks>
public interface IVersionedSecretStorage
{
	/// <summary>
	/// Retrieves the secret value stored for an exact key version.
	/// </summary>
	/// <param name="keyName"> The unique identifier for the secret. </param>
	/// <param name="version"> The exact version identifier of the secret to retrieve. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the secret value for the requested
	/// version, or <see langword="null"/> if no secret exists for that key name and version.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when secret retrieval fails due to security constraints. </exception>
	/// <exception cref="ArgumentException"> Thrown when the key name or version is null, empty, or invalid. </exception>
	Task<string?> GetSecretVersionAsync(string keyName, string version, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves the current version identifier for a key.
	/// </summary>
	/// <param name="keyName"> The unique identifier for the secret. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the current version identifier for the
	/// key, or <see langword="null"/> if the key does not exist.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when version resolution fails due to security constraints. </exception>
	/// <exception cref="ArgumentException"> Thrown when the key name is null, empty, or invalid. </exception>
	Task<string?> GetCurrentVersionAsync(string keyName, CancellationToken cancellationToken);
}
