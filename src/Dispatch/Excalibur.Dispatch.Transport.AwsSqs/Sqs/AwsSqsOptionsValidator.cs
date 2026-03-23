// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Validates <see cref="AwsSqsOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class AwsSqsOptionsValidator : IValidateOptions<AwsSqsOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AwsSqsOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("AWS SQS options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.Region))
		{
			return ValidateOptionsResult.Fail(
				"AWS SQS Region is required. Set AwsSqsOptions.Region to the target AWS region (e.g., 'us-east-1').");
		}

		return ValidateOptionsResult.Success;
	}
}
