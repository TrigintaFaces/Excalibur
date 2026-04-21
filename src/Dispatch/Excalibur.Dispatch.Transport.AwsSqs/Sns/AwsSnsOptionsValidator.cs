// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Validates <see cref="AwsSnsOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class AwsSnsOptionsValidator : IValidateOptions<AwsSnsOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AwsSnsOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("AWS SNS options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.TopicArn))
		{
			return ValidateOptionsResult.Fail(
				"AWS SNS TopicArn is required. Set AwsSnsOptions.TopicArn to the target SNS topic ARN.");
		}

		if (string.IsNullOrWhiteSpace(options.Region))
		{
			return ValidateOptionsResult.Fail(
				"AWS Region is required. Set AwsSnsOptions.Region to the target AWS region (e.g., 'us-east-1').");
		}

		if (options.MaxRetryAttempts is < 0 or > 100)
		{
			return ValidateOptionsResult.Fail(
				$"MaxRetryAttempts must be between 0 and 100, but was {options.MaxRetryAttempts}.");
		}

		if (options.Connection.MaxErrorRetry < 0)
		{
			return ValidateOptionsResult.Fail(
				$"Connection.MaxErrorRetry must be >= 0, but was {options.Connection.MaxErrorRetry}.");
		}

		return ValidateOptionsResult.Success;
	}
}
