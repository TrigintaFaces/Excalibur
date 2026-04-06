// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Splunk;

/// <summary>
/// Validates <see cref="SplunkBatchOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class SplunkBatchOptionsValidator : IValidateOptions<SplunkBatchOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SplunkBatchOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxBatchSize is < 1 or > 10000)
		{
			failures.Add($"{nameof(SplunkBatchOptions.MaxBatchSize)} must be between 1 and 10000 (was {options.MaxBatchSize}).");
		}

		if (options.MaxRetryAttempts is < 0 or > 10)
		{
			failures.Add($"{nameof(SplunkBatchOptions.MaxRetryAttempts)} must be between 0 and 10 (was {options.MaxRetryAttempts}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
