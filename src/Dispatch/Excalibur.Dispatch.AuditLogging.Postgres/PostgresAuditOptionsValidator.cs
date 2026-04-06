// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Postgres;

/// <summary>
/// AOT-safe validator for <see cref="PostgresAuditOptions"/>.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class PostgresAuditOptionsValidator : IValidateOptions<PostgresAuditOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, PostgresAuditOptions options)
	{
		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add($"{nameof(options.ConnectionString)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			failures.Add($"{nameof(options.SchemaName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			failures.Add($"{nameof(options.TableName)} is required.");
		}

		if (options.BatchSize is < 1 or > 100000)
		{
			failures.Add($"{nameof(options.BatchSize)} must be between 1 and 100000.");
		}

		if (options.RetentionCleanupBatchSize is < 1 or > 1000000)
		{
			failures.Add($"{nameof(options.RetentionCleanupBatchSize)} must be between 1 and 1000000.");
		}

		if (options.CommandTimeoutSeconds is < 1 or > 3600)
		{
			failures.Add($"{nameof(options.CommandTimeoutSeconds)} must be between 1 and 3600.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
