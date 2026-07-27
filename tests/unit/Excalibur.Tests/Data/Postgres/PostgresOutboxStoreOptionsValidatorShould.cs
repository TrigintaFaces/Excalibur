// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Outbox;
using Excalibur.Outbox.Partitioning;
using Excalibur.Outbox.Postgres;

using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Data.Postgres;

/// <summary>
///     Unit tests for PostgresOutboxStoreOptionsValidator.
/// </summary>
/// <remarks>
/// lz7us9 — the validator gained the cross-options Lamport-R1 floor-vs-poll invariant, so its primary ctor now
/// takes <c>IOptions&lt;OutboxProcessingOptions&gt;</c> + <c>IOptions&lt;OutboxPartitionOptions&gt;</c> (the
/// old parameterless <c>new()</c> no longer compiles). The pre-existing field-validation cases are retained
/// (constructed with a small processing poll and no partitioning, so a valid options object with the default
/// floor of 30s clears the floor check), and STRENGTHENED with the floor-vs-poll arms below — including the
/// partition-poll arm (F above the processing poll but at/below the partition poll), whose failure can only
/// come from the partition branch. These are pure-validator unit assertions; the WIRE that the validator is
/// actually registered + fires via <c>ValidateOnStart</c> is covered by the real-ServiceProvider 5th-arm
/// (<c>SqlServerOutboxFailureFloorValidationShould</c> and its per-provider siblings).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxStoreOptionsValidatorShould
{
	// The floor check compares against effectivePoll = partitionActive ? Max(processing, partition) : processing.
	// Default: a small processing poll (5s) + no partitioning, so a valid options object (default floor 30s > 5s)
	// clears the floor check and the pre-existing field-validation cases behave exactly as before.
	private static PostgresOutboxStoreOptionsValidator CreateValidator(
		double processingPollSeconds = 5,
		(OutboxPartitionStrategy Strategy, double PollSeconds)? partition = null)
	{
		var processing = Options.Create(new OutboxProcessingOptions
		{
			PollingInterval = TimeSpan.FromSeconds(processingPollSeconds),
		});

		var partitionOptions = new OutboxPartitionOptions();
		if (partition is { } p)
		{
			partitionOptions.Strategy = p.Strategy;
			partitionOptions.PollingInterval = TimeSpan.FromSeconds(p.PollSeconds);
		}

		return new PostgresOutboxStoreOptionsValidator(processing, Options.Create(partitionOptions));
	}

	private static PostgresOutboxStoreOptions ValidOptions() => new()
	{
		SchemaName = "valid_schema",
		OutboxTableName = "valid_outbox",
		DeadLetterTableName = "valid_dead_letters",
		ReservationTimeout = 300,
	};

	private readonly PostgresOutboxStoreOptionsValidator _validator = CreateValidator();

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

	// lz7us9 SAFETY (processing-poll bound, partitioning OFF): F <= processing poll => fail.
	// Non-vacuous: RED against a validator without the floor-vs-poll invariant (it would return Success).
	[Fact]
	public void FailWhenFailureBackoffFloorAtOrBelowProcessingPoll()
	{
		// Arrange — F(5s) <= processing poll(10s).
		var validator = CreateValidator(processingPollSeconds: 10);
		var options = ValidOptions();
		options.FailureBackoffFloorSeconds = 5;

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("FailureBackoffFloorSeconds");
		result.FailureMessage.ShouldContain("PollingInterval");
	}

	// lz7us9 SAFETY (partition-poll bound): F is ABOVE the processing poll (5s) but AT/BELOW the partition
	// poll (20s). A validator that checked ONLY the processing poll would return Success — so the failure here
	// can only come from the partition-poll branch, proving the partition interval is enforced on the
	// partitioned drain path (wording-independent, structural).
	[Fact]
	public void FailWhenFailureBackoffFloorAtOrBelowPartitionPoll_EvenWhenAboveProcessingPoll()
	{
		// Arrange — F(10s) > processing(5s) but F(10s) <= partition(20s), partitioning ACTIVE.
		var validator = CreateValidator(
			processingPollSeconds: 5,
			partition: (OutboxPartitionStrategy.ByTenantHash, 20));
		var options = ValidOptions();
		options.FailureBackoffFloorSeconds = 10;

		// Act
		var result = validator.Validate(null, options);

		// Assert — only the partition-poll branch can reject F(10) > processing(5).
		result.Failed.ShouldBeTrue(
			"F above the processing poll but at/below the partition poll must fail on the partition-poll branch");
	}

	// lz7us9 LIVENESS (no over-rejection): F strictly above every active poll => Success.
	[Fact]
	public void SucceedWhenFailureBackoffFloorAboveEveryActivePoll()
	{
		// Arrange — F(30s) > Max(processing 5s, partition 20s).
		var validator = CreateValidator(
			processingPollSeconds: 5,
			partition: (OutboxPartitionStrategy.ByTenantHash, 20));
		var options = ValidOptions();
		options.FailureBackoffFloorSeconds = 30;

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
	}
}
