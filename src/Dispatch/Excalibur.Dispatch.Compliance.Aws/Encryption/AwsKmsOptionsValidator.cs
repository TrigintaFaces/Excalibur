// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Aws;

/// <summary>
/// Validates <see cref="AwsKmsOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class AwsKmsOptionsValidator : IValidateOptions<AwsKmsOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AwsKmsOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.KeyAliasPrefix))
		{
			return ValidateOptionsResult.Fail($"{nameof(AwsKmsOptions.KeyAliasPrefix)} is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
