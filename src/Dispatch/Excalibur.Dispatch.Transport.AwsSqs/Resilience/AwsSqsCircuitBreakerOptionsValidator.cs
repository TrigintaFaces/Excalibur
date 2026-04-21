// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Validates <see cref="AwsSqsCircuitBreakerOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class AwsSqsCircuitBreakerOptionsValidator : IValidateOptions<AwsSqsCircuitBreakerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AwsSqsCircuitBreakerOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("AWS SQS circuit breaker options cannot be null.");
		}

		if (options.FailureThreshold is < 1 or > 100)
		{
			return ValidateOptionsResult.Fail(
				$"FailureThreshold must be between 1 and 100, but was {options.FailureThreshold}.");
		}

		return ValidateOptionsResult.Success;
	}
}
