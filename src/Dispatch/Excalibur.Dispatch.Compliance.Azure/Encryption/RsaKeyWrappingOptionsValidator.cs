// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Azure;

/// <summary>
/// Validates <see cref="RsaKeyWrappingOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class RsaKeyWrappingOptionsValidator : IValidateOptions<RsaKeyWrappingOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, RsaKeyWrappingOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.KeyVaultUrl is null)
		{
			failures.Add($"{nameof(RsaKeyWrappingOptions.KeyVaultUrl)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.KeyName))
		{
			failures.Add($"{nameof(RsaKeyWrappingOptions.KeyName)} is required.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
