// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Validation;

using Microsoft.Extensions.Options;

namespace Excalibur.Saga.SqlServer;

internal sealed class SqlServerSagaTimeoutStoreOptionsValidator : IValidateOptions<SqlServerSagaTimeoutStoreOptions>
{
	public ValidateOptionsResult Validate(string? name, SqlServerSagaTimeoutStoreOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("SQL Server saga timeout store options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			return ValidateOptionsResult.Fail("SchemaName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.SchemaName))
		{
			return ValidateOptionsResult.Fail("SchemaName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			return ValidateOptionsResult.Fail("TableName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.TableName))
		{
			return ValidateOptionsResult.Fail("TableName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		return ValidateOptionsResult.Success;
	}
}
