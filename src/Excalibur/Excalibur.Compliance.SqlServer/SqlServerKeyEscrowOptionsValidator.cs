// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Validation;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.SqlServer;

internal sealed class SqlServerKeyEscrowOptionsValidator : IValidateOptions<SqlServerKeyEscrowOptions>
{
	public ValidateOptionsResult Validate(string? name, SqlServerKeyEscrowOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("SQL Server key escrow options cannot be null.");
		}

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			failures.Add("ConnectionString is required.");
		}

		if (string.IsNullOrWhiteSpace(options.Schema))
		{
			failures.Add("Schema is required.");
		}
		else if (!SqlIdentifierValidator.IsValid(options.Schema))
		{
			failures.Add("Schema contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (string.IsNullOrWhiteSpace(options.TableName))
		{
			failures.Add("TableName is required.");
		}
		else if (!SqlIdentifierValidator.IsValid(options.TableName))
		{
			failures.Add("TableName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (string.IsNullOrWhiteSpace(options.TokensTableName))
		{
			failures.Add("TokensTableName is required.");
		}
		else if (!SqlIdentifierValidator.IsValid(options.TokensTableName))
		{
			failures.Add("TokensTableName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
