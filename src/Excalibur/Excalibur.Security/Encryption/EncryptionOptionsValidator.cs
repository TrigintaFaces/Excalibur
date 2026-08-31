// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Security;

/// <summary>
/// AOT-safe validator for <see cref="EncryptionOptions"/>.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class EncryptionOptionsValidator : IValidateOptions<EncryptionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, EncryptionOptions options)
	{
		if (options.KeyRotationIntervalDays < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(options.KeyRotationIntervalDays)} must be at least 1.");
		}

		// Key material for message encryption comes from ASP.NET Core Data Protection, whose key ring
		// this package registers with no external key provider. Naming a cloud key here therefore
		// changes nothing: keys would still be protected by the host's default (local) mechanism while
		// the configuration reads as though a managed KMS held them. Refuse instead of downgrading
		// silently.
		if (options.AzureKeyVaultUrl is not null)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(options.AzureKeyVaultUrl)} is set, but message encryption protects its keys with the " +
				"host's Data Protection key ring and does not configure a key vault. Leaving this set would " +
				"protect keys locally while the configuration claims otherwise. Clear it and configure the key " +
				"ring on the host instead - call AddDataProtection().ProtectKeysWithAzureKeyVault(...) from the " +
				"Azure.Extensions.AspNetCore.DataProtection.Keys package before adding message encryption.");
		}

		if (!string.IsNullOrEmpty(options.AwsKmsKeyArn))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(options.AwsKmsKeyArn)} is set, but message encryption protects its keys with the " +
				"host's Data Protection key ring and does not configure AWS KMS. Leaving this set would protect " +
				"keys locally while the configuration claims otherwise. Clear it and configure the key ring on " +
				"the host instead, using an AWS Data Protection key-provider package.");
		}

		return ValidateOptionsResult.Success;
	}
}
