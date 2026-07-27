// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Security.Azure;

/// <summary>
/// Validates <see cref="AzureKeyVaultKeyProviderOptions"/> at startup via the <c>ValidateOnStart</c>
/// pipeline. A missing or malformed vault URI fails loud at startup rather than at first key retrieval.
/// </summary>
internal sealed class AzureKeyVaultKeyProviderOptionsValidator
	: IValidateOptions<AzureKeyVaultKeyProviderOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AzureKeyVaultKeyProviderOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.VaultUri))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(options.VaultUri)} is required (e.g. https://my-vault.vault.azure.net/).");
		}

		if (!Uri.TryCreate(options.VaultUri, UriKind.Absolute, out var uri)
			|| (uri.Scheme != Uri.UriSchemeHttps))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(options.VaultUri)} must be an absolute https URI.");
		}

		if (options.CacheTtlSeconds is < 1 or > 86_400)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(options.CacheTtlSeconds)} must be between 1 and 86400.");
		}

		if (options.CacheMaxEntries is < 1 or > 1_000_000)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(options.CacheMaxEntries)} must be between 1 and 1000000.");
		}

		if (options.RotatedKeySizeBytes is < 16 or > 512)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(options.RotatedKeySizeBytes)} must be between 16 and 512.");
		}

		return ValidateOptionsResult.Success;
	}
}
