// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Validates <see cref="BulkheadOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class BulkheadOptionsValidator : IValidateOptions<BulkheadOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, BulkheadOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxConcurrency < 1)
		{
			failures.Add($"{nameof(BulkheadOptions.MaxConcurrency)} must be >= 1 (was {options.MaxConcurrency}).");
		}

		if (options.MaxQueueLength < 0)
		{
			failures.Add($"{nameof(BulkheadOptions.MaxQueueLength)} must be >= 0 (was {options.MaxQueueLength}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
