// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Postgres;

/// <summary>
/// Validates <see cref="PostgresHealthBasedLeaderElectionOptions"/> at startup (AOT-safe, reflection-free).
/// </summary>
internal sealed class PostgresHealthBasedLeaderElectionOptionsValidator
	: IValidateOptions<PostgresHealthBasedLeaderElectionOptions>
{
	public ValidateOptionsResult Validate(string? name, PostgresHealthBasedLeaderElectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			failures.Add("SchemaName must not be empty.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			failures.Add("TableName must not be empty.");
		}

		if (options.HealthExpirationSeconds is < 5 or > 3600)
		{
			failures.Add("HealthExpirationSeconds must be between 5 and 3600.");
		}

		if (options.CommandTimeoutSeconds is < 1 or > 300)
		{
			failures.Add("CommandTimeoutSeconds must be between 1 and 300.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
