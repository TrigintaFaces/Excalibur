// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Validates <see cref="ProviderOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks including:
/// <list type="bullet">
///   <item><description>RetryPolicy.BaseDelayMs must be less than or equal to RetryPolicy.MaxDelayMs</description></item>
///   <item><description>DefaultTimeoutMs must be positive</description></item>
/// </list>
/// </remarks>
public sealed class ProviderOptionsValidator : IValidateOptions<ProviderOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ProviderOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.DefaultTimeoutMs <= 0)
		{
			failures.Add(
				$"{nameof(ProviderOptions.DefaultTimeoutMs)} must be greater than 0 (was {options.DefaultTimeoutMs}).");
		}

		if (options.RetryPolicy is not null)
		{
			if (options.RetryPolicy.BaseDelayMs > options.RetryPolicy.MaxDelayMs)
			{
				failures.Add(
					$"{nameof(RetryPolicyOptions)}.{nameof(RetryPolicyOptions.BaseDelayMs)} ({options.RetryPolicy.BaseDelayMs}) " +
					$"must be less than or equal to {nameof(RetryPolicyOptions)}.{nameof(RetryPolicyOptions.MaxDelayMs)} ({options.RetryPolicy.MaxDelayMs}).");
			}

			if (options.RetryPolicy.BaseDelayMs <= 0)
			{
				failures.Add(
					$"{nameof(RetryPolicyOptions)}.{nameof(RetryPolicyOptions.BaseDelayMs)} must be greater than 0 (was {options.RetryPolicy.BaseDelayMs}).");
			}

			if (options.RetryPolicy.MaxDelayMs <= 0)
			{
				failures.Add(
					$"{nameof(RetryPolicyOptions)}.{nameof(RetryPolicyOptions.MaxDelayMs)} must be greater than 0 (was {options.RetryPolicy.MaxDelayMs}).");
			}
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
