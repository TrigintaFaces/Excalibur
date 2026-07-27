// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Validates <see cref="SqsProcessorOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// A non-positive <see cref="SqsProcessorOptions.DrainTimeoutSeconds"/> yields a zero/negative
/// <see cref="SqsProcessorOptions.DrainTimeout"/>, which either disables the shutdown drain entirely or
/// throws <see cref="ArgumentOutOfRangeException"/> when used as a cancellation deadline. Failing fast at
/// startup surfaces the misconfiguration before the process is running.
/// </remarks>
internal sealed class SqsProcessorOptionsValidator : IValidateOptions<SqsProcessorOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SqsProcessorOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.DrainTimeoutSeconds < 1)
		{
			failures.Add(
				$"{nameof(SqsProcessorOptions.DrainTimeoutSeconds)} must be >= 1 (was {options.DrainTimeoutSeconds}).");
		}

		if (options.ProcessorCount < 1)
		{
			failures.Add(
				$"{nameof(SqsProcessorOptions.ProcessorCount)} must be >= 1 (was {options.ProcessorCount}).");
		}

		if (options.MaxConcurrentMessages < 1)
		{
			failures.Add(
				$"{nameof(SqsProcessorOptions.MaxConcurrentMessages)} must be >= 1 (was {options.MaxConcurrentMessages}).");
		}

		if (options.DeleteBatchIntervalMs < 0)
		{
			failures.Add(
				$"{nameof(SqsProcessorOptions.DeleteBatchIntervalMs)} must be >= 0 (was {options.DeleteBatchIntervalMs}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
