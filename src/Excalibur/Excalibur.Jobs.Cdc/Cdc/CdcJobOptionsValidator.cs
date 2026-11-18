// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
		else
		{
			for (var index = 0; index < options.DatabaseConfigs.Count; index++)
			{
				var database = options.DatabaseConfigs[index];
				var path = $"{nameof(CdcJobOptions.DatabaseConfigs)}[{index}]";
				if (database is null)
				{
					failures.Add($"{path} must not be null.");
					continue;
				}

				if (string.IsNullOrWhiteSpace(database.DatabaseName))
				{
					failures.Add($"{path}.{nameof(database.DatabaseName)} must be non-empty.");
				}

				if (string.IsNullOrWhiteSpace(database.DatabaseConnectionIdentifier))
				{
					failures.Add($"{path}.{nameof(database.DatabaseConnectionIdentifier)} must be non-empty.");
				}

				if (string.IsNullOrWhiteSpace(database.StateConnectionIdentifier))
				{
					failures.Add($"{path}.{nameof(database.StateConnectionIdentifier)} must be non-empty.");
				}

				if (database.Tables.Count == 0)
				{
					failures.Add($"{path}.{nameof(database.Tables)} must contain at least one tracked table.");
				}

				for (var tableIndex = 0; tableIndex < database.Tables.Count; tableIndex++)
				{
					var table = database.Tables[tableIndex];
					if (table is null || string.IsNullOrWhiteSpace(table.TableName))
					{
						failures.Add($"{path}.{nameof(database.Tables)}[{tableIndex}].TableName must be non-empty.");
					}
				}
			}
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
