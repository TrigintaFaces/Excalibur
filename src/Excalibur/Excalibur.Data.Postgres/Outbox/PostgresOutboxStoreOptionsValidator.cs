// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Data.Postgres.Outbox;

/// <summary>
/// Validates Postgres outbox store configuration options.
/// </summary>
public sealed class PostgresOutboxStoreOptionsValidator : IValidateOptions<PostgresOutboxStoreOptions>
{
	/// <summary>
	/// Validates the provided Postgres outbox store options.
	/// </summary>
	/// <param name="name"> The name of the options instance being validated. </param>
	/// <param name="options"> The Postgres outbox store options to validate. </param>
	/// <returns> A validation result indicating success or failure with appropriate error messages. </returns>
	public ValidateOptionsResult Validate(string? name, PostgresOutboxStoreOptions options)
	{
		// Validate that options object is not null
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Postgres outbox store options cannot be null.");
		}

		// Validate schema name
		if (string.IsNullOrWhiteSpace(options.SchemaName))
		{
			return ValidateOptionsResult.Fail("Schema name cannot be null or empty.");
		}

		// Validate outbox table name
		if (string.IsNullOrWhiteSpace(options.OutboxTableName))
		{
			return ValidateOptionsResult.Fail("Outbox table name cannot be null or empty.");
		}

		// Validate dead letter table name
		if (string.IsNullOrWhiteSpace(options.DeadLetterTableName))
		{
			return ValidateOptionsResult.Fail("Dead letter table name cannot be null or empty.");
		}

		// Validate schema and table names don't contain invalid characters
		if (!IsValidIdentifier(options.SchemaName))
		{
			return ValidateOptionsResult.Fail(
					$"Schema name '{options.SchemaName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (!IsValidIdentifier(options.OutboxTableName))
		{
			return ValidateOptionsResult.Fail(
					$"Outbox table name '{options.OutboxTableName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (!IsValidIdentifier(options.DeadLetterTableName))
		{
			return ValidateOptionsResult.Fail(
					$"Dead letter table name '{options.DeadLetterTableName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		// Validate reservation timeout
		if (options.ReservationTimeout <= 0)
		{
			return ValidateOptionsResult.Fail("Reservation timeout must be greater than 0.");
		}

		// Prevent using the same table name for outbox and dead letter
		if (string.Equals(options.OutboxTableName, options.DeadLetterTableName, StringComparison.OrdinalIgnoreCase))
		{
			return ValidateOptionsResult.Fail("Outbox table name and dead letter table name cannot be the same.");
		}

		return ValidateOptionsResult.Success;
	}

	/// <summary>
	/// Validates that a schema or table name contains only valid characters.
	/// </summary>
	/// <param name="identifier"> The schema or table name to validate. </param>
	/// <returns> True if the identifier is valid; otherwise, false. </returns>
	private static bool IsValidIdentifier(string identifier)
	{
		// Postgres identifiers should start with a letter or underscore and contain only alphanumeric characters and underscores
		if (string.IsNullOrWhiteSpace(identifier))
		{
			return false;
		}

		// Check first character
		var firstChar = identifier[0];
		if (!char.IsLetter(firstChar) && firstChar != '_')
		{
			return false;
		}

		// Check remaining characters
		for (var i = 1; i < identifier.Length; i++)
		{
			var c = identifier[i];
			if (!char.IsLetterOrDigit(c) && c != '_')
			{
				return false;
			}
		}

		return true;
	}
}
