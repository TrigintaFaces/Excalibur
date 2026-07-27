// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Validation;
using Excalibur.Outbox.Outbox;
using Excalibur.Outbox.Partitioning;

using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Validates <see cref="SqlServerOutboxOptions"/> at startup via ValidateOnStart.
/// Ensures a connection has been configured through the builder.
/// </summary>
/// <remarks>
/// Also enforces the cross-options Lamport-R1 invariant <c>Processing.FailureBackoffFloorSeconds &gt;
/// PollingInterval</c> on <b>every active drain path</b>: the failure-backoff floor must exceed the
/// single-processor poll interval (<see cref="OutboxProcessingOptions"/>) and — when partitioning is enabled —
/// the partition poll interval (<see cref="OutboxPartitionOptions"/>), or a failed message could be re-claimed
/// on the very next poll (a retry hot-loop). This is a fail-fast composition validator, loud at startup.
/// </remarks>
internal sealed class SqlServerOutboxOptionsValidator(
	IOptions<OutboxProcessingOptions> processingOptions,
	IOptions<OutboxPartitionOptions> partitionOptions)
	: IValidateOptions<SqlServerOutboxOptions>
{
	private readonly IOptions<OutboxProcessingOptions> _processingOptions =
		processingOptions ?? throw new ArgumentNullException(nameof(processingOptions));

	private readonly IOptions<OutboxPartitionOptions> _partitionOptions =
		partitionOptions ?? throw new ArgumentNullException(nameof(partitionOptions));

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

		if (string.IsNullOrWhiteSpace(options.Tables.SchemaName))
		{
			return ValidateOptionsResult.Fail("SchemaName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.Tables.SchemaName))
		{
			return ValidateOptionsResult.Fail("SchemaName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (string.IsNullOrWhiteSpace(options.Tables.OutboxTableName))
		{
			return ValidateOptionsResult.Fail("OutboxTableName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.Tables.OutboxTableName))
		{
			return ValidateOptionsResult.Fail("OutboxTableName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (string.IsNullOrWhiteSpace(options.Tables.TransportsTableName))
		{
			return ValidateOptionsResult.Fail("TransportsTableName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.Tables.TransportsTableName))
		{
			return ValidateOptionsResult.Fail("TransportsTableName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (string.IsNullOrWhiteSpace(options.Tables.DeadLetterTableName))
		{
			return ValidateOptionsResult.Fail("DeadLetterTableName is required.");
		}

		if (!SqlIdentifierValidator.IsValid(options.Tables.DeadLetterTableName))
		{
			return ValidateOptionsResult.Fail("DeadLetterTableName contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		var pollingIntervalSeconds = _processingOptions.Value.PollingInterval.TotalSeconds;
		var partition = _partitionOptions.Value;
		var partitionActive = partition.Strategy != OutboxPartitionStrategy.None;
		var partitionPollSeconds = partition.PollingInterval.TotalSeconds;
		var effectivePollSeconds = partitionActive
			? Math.Max(pollingIntervalSeconds, partitionPollSeconds)
			: pollingIntervalSeconds;
		if (options.Processing.FailureBackoffFloorSeconds <= effectivePollSeconds)
		{
			var boundBy = partitionActive && partitionPollSeconds > pollingIntervalSeconds
				? $"partition PollingInterval ({partitionPollSeconds}s)"
				: $"outbox PollingInterval ({pollingIntervalSeconds}s)";
			return ValidateOptionsResult.Fail(
				$"SqlServerOutboxOptions.Processing.FailureBackoffFloorSeconds ({options.Processing.FailureBackoffFloorSeconds}s) " +
				$"must be greater than the {boundBy}; otherwise a failed message is re-claimable on the next poll (a retry hot-loop).");
		}

		return ValidateOptionsResult.Success;
	}
}
