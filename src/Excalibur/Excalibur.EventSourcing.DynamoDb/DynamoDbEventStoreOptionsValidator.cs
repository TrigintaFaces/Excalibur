// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.DynamoDb;

/// <summary>
/// Validates <see cref="DynamoDbEventStoreOptions"/> at startup so a misconfigured event store fails fast
/// instead of surfacing as a deep runtime error on the first append or query.
/// </summary>
internal sealed class DynamoDbEventStoreOptionsValidator : IValidateOptions<DynamoDbEventStoreOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, DynamoDbEventStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.EventsTableName))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(DynamoDbEventStoreOptions.EventsTableName)} is required and must not be empty or whitespace.");
		}

		if (string.IsNullOrWhiteSpace(options.PartitionKeyAttribute))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(DynamoDbEventStoreOptions.PartitionKeyAttribute)} is required and must not be empty or whitespace.");
		}

		if (string.IsNullOrWhiteSpace(options.SortKeyAttribute))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(DynamoDbEventStoreOptions.SortKeyAttribute)} is required and must not be empty or whitespace.");
		}

		if (options.MaxBatchSize is < 1 or > 100)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(DynamoDbEventStoreOptions.MaxBatchSize)} must be between 1 and 100 (the DynamoDB " +
				"TransactWriteItems limit).");
		}

		if (options.StreamsPollIntervalMs < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(DynamoDbEventStoreOptions.StreamsPollIntervalMs)} must be greater than zero.");
		}

		if (!options.Throughput.UseOnDemandCapacity)
		{
			if (options.Throughput.ReadCapacityUnits < 1)
			{
				return ValidateOptionsResult.Fail(
					$"{nameof(DynamoDbThroughputOptions.ReadCapacityUnits)} must be greater than zero when " +
					$"{nameof(DynamoDbThroughputOptions.UseOnDemandCapacity)} is disabled.");
			}

			if (options.Throughput.WriteCapacityUnits < 1)
			{
				return ValidateOptionsResult.Fail(
					$"{nameof(DynamoDbThroughputOptions.WriteCapacityUnits)} must be greater than zero when " +
					$"{nameof(DynamoDbThroughputOptions.UseOnDemandCapacity)} is disabled.");
			}
		}

		return ValidateOptionsResult.Success;
	}
}
