// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Validates <see cref="AwsProviderOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class AwsProviderOptionsValidator : IValidateOptions<AwsProviderOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AwsProviderOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("AWS provider options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.Region))
		{
			return ValidateOptionsResult.Fail(
				"AWS Region is required. Set AwsProviderOptions.Region to the target AWS region (e.g., 'us-east-1').");
		}

		if (options.MaxRetryAttempts is < 0 or > 100)
		{
			return ValidateOptionsResult.Fail(
				$"MaxRetryAttempts must be between 0 and 100, but was {options.MaxRetryAttempts}.");
		}

		return ValidateOptionsResult.Success;
	}
}
