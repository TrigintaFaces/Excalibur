// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Validates <see cref="AwsSqsRetryOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class AwsSqsRetryOptionsValidator : IValidateOptions<AwsSqsRetryOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AwsSqsRetryOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("AWS SQS retry options cannot be null.");
		}

		if (options.MaxRetryAttempts is < 0 or > 20)
		{
			return ValidateOptionsResult.Fail(
				$"MaxRetryAttempts must be between 0 and 20, but was {options.MaxRetryAttempts}.");
		}

		return ValidateOptionsResult.Success;
	}
}
