// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Validates <see cref="ErasureOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Performs cross-property constraint checks beyond what <see cref="System.ComponentModel.DataAnnotations"/> can express.
/// </summary>
internal sealed class ErasureOptionsValidator : IValidateOptions<ErasureOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ErasureOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MinimumGracePeriod < TimeSpan.Zero)
		{
			failures.Add($"{nameof(ErasureOptions.MinimumGracePeriod)} must not be negative (was {options.MinimumGracePeriod}).");
		}

		if (options.DefaultGracePeriod < options.MinimumGracePeriod)
		{
			failures.Add(
				$"{nameof(ErasureOptions.DefaultGracePeriod)} ({options.DefaultGracePeriod}) " +
				$"must be >= {nameof(ErasureOptions.MinimumGracePeriod)} ({options.MinimumGracePeriod}).");
		}

		if (options.DefaultGracePeriod > options.MaximumGracePeriod)
		{
			failures.Add(
				$"{nameof(ErasureOptions.DefaultGracePeriod)} ({options.DefaultGracePeriod}) " +
				$"must be <= {nameof(ErasureOptions.MaximumGracePeriod)} ({options.MaximumGracePeriod}).");
		}

		if (options.MaximumGracePeriod > TimeSpan.FromDays(30))
		{
			failures.Add(
				$"{nameof(ErasureOptions.MaximumGracePeriod)} ({options.MaximumGracePeriod}) " +
				$"must not exceed the 30-day GDPR deadline.");
		}

		if (options.Execution.BatchSize < 1)
		{
			failures.Add($"{nameof(ErasureExecutionOptions.BatchSize)} must be >= 1 (was {options.Execution.BatchSize}).");
		}

		if (options.Execution.MaxRetryAttempts < 0)
		{
			failures.Add($"{nameof(ErasureExecutionOptions.MaxRetryAttempts)} must be >= 0 (was {options.Execution.MaxRetryAttempts}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
