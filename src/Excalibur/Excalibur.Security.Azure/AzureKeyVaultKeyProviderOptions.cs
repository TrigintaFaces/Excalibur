// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Security.Azure;

/// <summary>
/// Options for the Azure Key Vault-backed <see cref="IKeyProvider"/>.
/// </summary>
public sealed class AzureKeyVaultKeyProviderOptions
{
	/// <summary>
	/// Gets or sets the Azure Key Vault URI (e.g. <c>https://my-vault.vault.azure.net/</c>) used to build
	/// the secret client. Required.
	/// </summary>
	public string? VaultUri { get; set; }

	/// <summary>
	/// Gets or sets an optional prefix prepended to every key identifier when resolving the Key Vault
	/// secret name. Lets multiple deployments share one vault without collisions.
	/// </summary>
	public string? SecretNamePrefix { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether retrieved keys are cached locally to reduce Key Vault API
	/// calls. A missing key is never cached (fail-closed). Default is <see langword="true"/>.
	/// </summary>
	public bool EnableCache { get; set; } = true;

	/// <summary>
	/// Gets or sets the time-to-live, in seconds, for a cached key. Default is 300 (5 minutes).
	/// </summary>
	[Range(1, 86_400)]
	public int CacheTtlSeconds { get; set; } = 300;

	/// <summary>
	/// Gets or sets the maximum number of cached keys. When the cache is full new entries are skipped (the
	/// retrieval still succeeds, it is simply not cached). Default is 1024.
	/// </summary>
	[Range(1, 1_000_000)]
	public int CacheMaxEntries { get; set; } = 1024;

	/// <summary>
	/// Gets or sets the size, in bytes, of the random key material minted by
	/// <see cref="IKeyProvider.RotateKeyAsync"/>. Default is 64 (512 bits, suitable for HMAC-SHA-512).
	/// </summary>
	[Range(16, 512)]
	public int RotatedKeySizeBytes { get; set; } = 64;
}
