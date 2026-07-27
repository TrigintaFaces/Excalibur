// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Jobs.Cdc;

/// <summary>
/// Validates <see cref="CdcJobOptions"/> at startup. Reflection-free (AOT-safe) so the guarantees hold on
/// trimmed/AOT consumer applications.
/// </summary>
internal sealed class CdcJobOptionsValidator : IValidateOptions<CdcJobOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, CdcJobOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.CronSchedule))
		{
			failures.Add($"{nameof(CdcJobOptions.CronSchedule)} must be a non-empty cron expression.");
		}

		if (string.IsNullOrWhiteSpace(options.JobName))
		{
			failures.Add($"{nameof(CdcJobOptions.JobName)} must be a non-empty job name.");
		}

		if (options.DatabaseConfigs is null || options.DatabaseConfigs.Count == 0)
		{
			failures.Add($"{nameof(CdcJobOptions.DatabaseConfigs)} must contain at least one database configuration.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
