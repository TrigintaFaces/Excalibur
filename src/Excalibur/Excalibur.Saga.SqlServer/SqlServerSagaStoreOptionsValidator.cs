// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Saga.SqlServer;

internal sealed class SqlServerSagaStoreOptionsValidator : IValidateOptions<SqlServerSagaStoreOptions>
{
	public ValidateOptionsResult Validate(string? name, SqlServerSagaStoreOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("SQL Server saga store options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			return ValidateOptionsResult.Fail("SchemaName is required.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			return ValidateOptionsResult.Fail("TableName is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
