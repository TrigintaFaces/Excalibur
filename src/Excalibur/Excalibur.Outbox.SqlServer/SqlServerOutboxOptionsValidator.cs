// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Validation;

using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Validates <see cref="SqlServerOutboxOptions"/> at startup via ValidateOnStart.
/// Ensures a connection has been configured through the builder.
/// </summary>
internal sealed class SqlServerOutboxOptionsValidator : IValidateOptions<SqlServerOutboxOptions>
{
	/// <summary>
	/// Gets or sets a value indicating whether the builder configured a connection
	/// via <see cref="ISqlServerOutboxBuilder.ConnectionFactory"/> or
	/// <see cref="ISqlServerOutboxBuilder.ConnectionStringName"/>.
	/// </summary>
	internal bool HasBuilderConnection { get; init; }

	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, SqlServerOutboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("SQL Server outbox options cannot be null.");
		}

		if (!HasBuilderConnection && string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			return ValidateOptionsResult.Fail(
				"No connection configured for Outbox. " +
				"Call ConnectionString(), ConnectionStringName(), ConnectionFactory(), " +
				"or BindConfiguration() inside UseSqlServer().");
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
