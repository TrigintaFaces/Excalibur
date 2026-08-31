// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Outbox.Outbox;
using Excalibur.Outbox.Partitioning;

using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Validates Postgres outbox store configuration options.
/// </summary>
/// <remarks>
/// Also enforces the cross-options Lamport-R1 invariant <c>FailureBackoffFloorSeconds &gt; PollingInterval</c>
/// on <b>every active drain path</b>: the failure-backoff floor must exceed the single-processor poll interval
/// (<see cref="OutboxProcessingOptions"/>) and — when partitioning is enabled — the partition poll interval
/// (<see cref="OutboxPartitionOptions"/>), or a failed message could be re-claimed on the very next poll (a
/// retry hot-loop). This is a fail-fast composition validator, loud at startup rather than a silent runtime regression.
/// </remarks>
internal sealed class PostgresOutboxStoreOptionsValidator(
	IOptions<OutboxProcessingOptions> processingOptions,
	IOptions<OutboxPartitionOptions> partitionOptions)
	: IValidateOptions<PostgresOutboxStoreOptions>
{
	private readonly IOptions<OutboxProcessingOptions> _processingOptions =
		processingOptions ?? throw new ArgumentNullException(nameof(processingOptions));

	private readonly IOptions<OutboxPartitionOptions> _partitionOptions =
		partitionOptions ?? throw new ArgumentNullException(nameof(partitionOptions));

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

		var pollingIntervalSeconds = _processingOptions.Value.PollingInterval.TotalSeconds;
		var partition = _partitionOptions.Value;
		var partitionActive = partition.Strategy != OutboxPartitionStrategy.None;
		var partitionPollSeconds = partition.PollingInterval.TotalSeconds;
		var effectivePollSeconds = partitionActive
			? Math.Max(pollingIntervalSeconds, partitionPollSeconds)
			: pollingIntervalSeconds;
		if (options.FailureBackoffFloorSeconds <= effectivePollSeconds)
		{
			var boundBy = partitionActive && partitionPollSeconds > pollingIntervalSeconds
				? $"partition PollingInterval ({partitionPollSeconds}s)"
				: $"outbox PollingInterval ({pollingIntervalSeconds}s)";
			return ValidateOptionsResult.Fail(
				$"PostgresOutboxStoreOptions.FailureBackoffFloorSeconds ({options.FailureBackoffFloorSeconds}s) must be " +
				$"greater than the {boundBy}; otherwise a failed message is re-claimable on the next poll (a retry hot-loop).");
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
