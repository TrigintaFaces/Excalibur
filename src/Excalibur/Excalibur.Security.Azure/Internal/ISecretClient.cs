// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure;
using Azure.Security.KeyVault.Secrets;

namespace Excalibur.Security.Azure.Internal;

/// <summary>
/// Narrow internal seam over <see cref="SecretClient"/> used by
/// <see cref="AzureKeyVaultCredentialStore"/>. Exists so tests can substitute
/// the SDK without reflecting on private fields and without depending on which
/// <see cref="SecretClient"/> overloads remain virtual in a given SDK minor
/// version. Not a consumer-facing abstraction; do not make this public.
/// </summary>
internal interface ISecretClient
{
	/// <summary>
	/// Retrieves the latest version of a secret from the underlying vault.
	/// </summary>
	/// <param name="name"> The secret name. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The secret response wrapped in <see cref="Response{T}"/>. </returns>
	Task<Response<KeyVaultSecret>> GetSecretAsync(string name, CancellationToken cancellationToken);

	/// <summary>
	/// Stores (or updates) a secret in the underlying vault.
	/// </summary>
	/// <param name="secret"> The secret to store. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The stored secret response wrapped in <see cref="Response{T}"/>. </returns>
	Task<Response<KeyVaultSecret>> SetSecretAsync(KeyVaultSecret secret, CancellationToken cancellationToken);
}
