// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Validation;

using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.SqlServer;

internal sealed class SqlServerOutboxOptionsValidator : IValidateOptions<SqlServerOutboxOptions>
{
	public ValidateOptionsResult Validate(string? name, SqlServerOutboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("SQL Server outbox options cannot be null.");
		}

		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			return ValidateOptionsResult.Fail("SchemaName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.SchemaName))
		{
			return ValidateOptionsResult.Fail("SchemaName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (string.IsNullOrWhiteSpace(options.OutboxTableName))
		{
			return ValidateOptionsResult.Fail("OutboxTableName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.OutboxTableName))
		{
			return ValidateOptionsResult.Fail("OutboxTableName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (string.IsNullOrWhiteSpace(options.TransportsTableName))
		{
			return ValidateOptionsResult.Fail("TransportsTableName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.TransportsTableName))
		{
			return ValidateOptionsResult.Fail("TransportsTableName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (string.IsNullOrWhiteSpace(options.DeadLetterTableName))
		{
			return ValidateOptionsResult.Fail("DeadLetterTableName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.DeadLetterTableName))
		{
			return ValidateOptionsResult.Fail("DeadLetterTableName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		return ValidateOptionsResult.Success;
	}
}
