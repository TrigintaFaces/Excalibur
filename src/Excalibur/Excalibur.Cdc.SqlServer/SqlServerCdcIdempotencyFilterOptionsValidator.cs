// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.SqlServer;

internal sealed class SqlServerCdcIdempotencyFilterOptionsValidator
	: IValidateOptions<SqlServerCdcIdempotencyFilterOptions>
{
	public ValidateOptionsResult Validate(string? name, SqlServerCdcIdempotencyFilterOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("SQL Server CDC idempotency filter options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			return ValidateOptionsResult.Fail("SchemaName is required.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			return ValidateOptionsResult.Fail("TableName is required.");
		}

		if (options.RetentionPeriod <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail("RetentionPeriod must be positive.");
		}

		if (options.CleanupBatchSize <= 0)
		{
			return ValidateOptionsResult.Fail("CleanupBatchSize must be positive.");
		}

		return ValidateOptionsResult.Success;
	}
}
