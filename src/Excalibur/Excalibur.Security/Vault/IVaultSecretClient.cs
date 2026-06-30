// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Security.Vault;

/// <summary>
/// Narrow internal seam over the HashiCorp Vault KV&#160;v2 HTTP API used by
/// <see cref="HashiCorpVaultCredentialStore"/>. Exists so tests can substitute the
/// transport without committing real credentials or standing up a live Vault: the
/// <see cref="VaultSecretClientAdapter"/> is the only place that touches live HTTP
/// call sites. Not a
/// consumer-facing abstraction; do not make this public.
/// </summary>
internal interface IVaultSecretClient
{
	/// <summary>
	/// Reads the <c>value</c> field of the secret stored at <paramref name="key"/>.
	/// </summary>
	/// <param name="key"> The secret key/path (relative to the KV mount). </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>
	/// The stored secret value, or <see langword="null"/> when the secret does not
	/// exist. A backend/transport failure throws rather than returning <see langword="null"/>
	/// so a genuine error is never mistaken for a missing secret.
	/// </returns>
	Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken);

	/// <summary>
	/// Writes <paramref name="value"/> as the <c>value</c> field of the secret at
	/// <paramref name="key"/>, creating or updating it.
	/// </summary>
	/// <param name="key"> The secret key/path (relative to the KV mount). </param>
	/// <param name="value"> The secret value to persist. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task that completes when the write is durably accepted by Vault. </returns>
	Task SetSecretAsync(string key, string value, CancellationToken cancellationToken);
}
