// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Saga;

/// <summary>
/// Validates <see cref="AdvancedSagaOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces <c>[Range(typeof(TimeSpan), ...)]</c> attributes with AOT-safe checks.
/// </summary>
internal sealed class AdvancedSagaOptionsValidator : IValidateOptions<AdvancedSagaOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AdvancedSagaOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.DefaultTimeout < TimeSpan.FromSeconds(1) || options.DefaultTimeout > TimeSpan.FromDays(1))
		{
			failures.Add(
				$"{nameof(AdvancedSagaOptions.DefaultTimeout)} must be between 00:00:01 and 24:00:00 (was {options.DefaultTimeout}).");
		}

		if (options.DefaultStepTimeout < TimeSpan.FromSeconds(1) || options.DefaultStepTimeout > TimeSpan.FromHours(1))
		{
			failures.Add(
				$"{nameof(AdvancedSagaOptions.DefaultStepTimeout)} must be between 00:00:01 and 01:00:00 (was {options.DefaultStepTimeout}).");
		}

		if (options.RetryBaseDelay < TimeSpan.FromMilliseconds(100) || options.RetryBaseDelay > TimeSpan.FromMinutes(10))
		{
			failures.Add(
				$"{nameof(AdvancedSagaOptions.RetryBaseDelay)} must be between 00:00:00.100 and 00:10:00 (was {options.RetryBaseDelay}).");
		}

		if (options.CleanupInterval < TimeSpan.FromMinutes(1) || options.CleanupInterval > TimeSpan.FromDays(1))
		{
			failures.Add(
				$"{nameof(AdvancedSagaOptions.CleanupInterval)} must be between 00:01:00 and 24:00:00 (was {options.CleanupInterval}).");
		}

		if (options.CompletedSagaRetention < TimeSpan.FromMinutes(1) || options.CompletedSagaRetention > TimeSpan.FromDays(365))
		{
			failures.Add(
				$"{nameof(AdvancedSagaOptions.CompletedSagaRetention)} must be between 00:01:00 and 365.00:00:00 (was {options.CompletedSagaRetention}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
