// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Scheduling;

/// <summary>
/// Validates <see cref="CronScheduleOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class CronScheduleOptionsValidator : IValidateOptions<CronScheduleOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CronScheduleOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.DefaultTimeZone is null)
		{
			failures.Add($"{nameof(CronScheduleOptions.DefaultTimeZone)} is required.");
		}

		if (options.MaxMissedExecutions < 0)
		{
			failures.Add($"{nameof(CronScheduleOptions.MaxMissedExecutions)} must be >= 0 (was {options.MaxMissedExecutions}).");
		}

		if (options.ExecutionToleranceWindow < TimeSpan.Zero)
		{
			failures.Add($"{nameof(CronScheduleOptions.ExecutionToleranceWindow)} must be >= zero (was {options.ExecutionToleranceWindow}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
