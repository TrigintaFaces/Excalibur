// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Outbox;

namespace Excalibur.Tests.Data.Postgres;

/// <summary>
///     Unit tests for PostgresOutboxStoreOptionsValidator.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PostgresOutboxStoreOptionsValidatorShould
{
	private readonly PostgresOutboxStoreOptionsValidator _validator = new();

	[Fact]
	public void ReturnSuccessForValidOptions()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			SchemaName = "valid_schema",
			OutboxTableName = "valid_outbox",
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void FailForNullOptions()
	{
		// Act
		var result = _validator.Validate(null, null!);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("cannot be null");
	}

	[Fact]
	public void FailForNullSchemaName()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			SchemaName = null!,
			OutboxTableName = "valid_outbox",
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Schema name cannot be null or empty");
	}

	[Fact]
	public void FailForEmptySchemaName()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			SchemaName = string.Empty,
			OutboxTableName = "valid_outbox",
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Schema name cannot be null or empty");
	}

	[Fact]
	public void FailForInvalidSchemaNameWithSpecialCharacters()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			SchemaName = "invalid-schema!",
			OutboxTableName = "valid_outbox",
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("contains invalid characters");
	}

	[Fact]
	public void FailForSchemaNameStartingWithNumber()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			SchemaName = "123invalid",
			OutboxTableName = "valid_outbox",
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("contains invalid characters");
	}

	[Fact]
	public void FailForNullOutboxTableName()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = null!,
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Outbox table name cannot be null or empty");
	}

	[Fact]
	public void FailForEmptyOutboxTableName()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = string.Empty,
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Outbox table name cannot be null or empty");
	}

	[Fact]
	public void FailForWhitespaceOutboxTableName()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = " ",
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Outbox table name cannot be null or empty");
	}

	[Fact]
	public void FailForNullDeadLetterTableName()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = "valid_outbox",
			DeadLetterTableName = null!,
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Dead letter table name cannot be null or empty");
	}

	[Fact]
	public void FailForEmptyDeadLetterTableName()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = "valid_outbox",
			DeadLetterTableName = string.Empty,
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Dead letter table name cannot be null or empty");
	}

	[Fact]
	public void FailForInvalidOutboxTableNameWithSpecialCharacters()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = "invalid-table!",
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("contains invalid characters");
	}

	[Fact]
	public void FailForInvalidDeadLetterTableNameWithSpecialCharacters()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = "valid_outbox",
			DeadLetterTableName = "invalid-table!",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("contains invalid characters");
	}

	[Fact]
	public void FailForTableNameStartingWithNumber()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = "123invalid",
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("contains invalid characters");
	}

	[Fact]
	public void SucceedForTableNameStartingWithUnderscore()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = "_valid_outbox",
			DeadLetterTableName = "_valid_dead_letters",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void FailForNegativeReservationTimeout()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = "valid_outbox",
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = -1,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Reservation timeout must be greater than 0");
	}

	[Fact]
	public void FailForZeroReservationTimeout()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = "valid_outbox",
			DeadLetterTableName = "valid_dead_letters",
			ReservationTimeout = 0,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Reservation timeout must be greater than 0");
	}

	[Fact]
	public void FailForSameTableNames()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = "same_table",
			DeadLetterTableName = "same_table",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("cannot be the same");
	}

	[Fact]
	public void FailForSameTableNamesCaseInsensitive()
	{
		// Arrange
		var options = new PostgresOutboxStoreOptions
		{
			OutboxTableName = "SAME_TABLE",
			DeadLetterTableName = "same_table",
			ReservationTimeout = 300,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("cannot be the same");
	}
}
