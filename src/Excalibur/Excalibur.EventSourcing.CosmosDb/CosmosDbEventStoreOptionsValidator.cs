// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.CosmosDb;

/// <summary>
/// Validates <see cref="CosmosDbEventStoreOptions"/> at startup so a misconfigured event store fails fast
/// instead of surfacing as a deep runtime error on the first append or query.
/// </summary>
internal sealed class CosmosDbEventStoreOptionsValidator : IValidateOptions<CosmosDbEventStoreOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CosmosDbEventStoreOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.DatabaseName))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(CosmosDbEventStoreOptions.DatabaseName)} is required and must not be empty or whitespace.");
		}

		if (string.IsNullOrWhiteSpace(options.EventsContainerName))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(CosmosDbEventStoreOptions.EventsContainerName)} is required and must not be empty or whitespace.");
		}

		if (string.IsNullOrWhiteSpace(options.PartitionKeyPath) || !options.PartitionKeyPath.StartsWith('/'))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(CosmosDbEventStoreOptions.PartitionKeyPath)} is required and must be a Cosmos partition-key " +
				"path beginning with '/' (e.g. \"/streamId\").");
		}

		if (options.DefaultTimeToLiveSeconds < -1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(CosmosDbEventStoreOptions.DefaultTimeToLiveSeconds)} must be -1 (never expire), 0, or a " +
				"positive number of seconds.");
		}

		if (options.MaxBatchSize is < 1 or > 100)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(CosmosDbEventStoreOptions.MaxBatchSize)} must be between 1 and 100 (the Cosmos transactional " +
				"batch limit).");
		}

		if (options.ChangeFeedPollIntervalMs < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(CosmosDbEventStoreOptions.ChangeFeedPollIntervalMs)} must be greater than zero.");
		}

		if (options.CreateContainerIfNotExists && options.ContainerThroughput < 400)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(CosmosDbEventStoreOptions.ContainerThroughput)} must be at least 400 RU/s (the Cosmos minimum) " +
				$"when {nameof(CosmosDbEventStoreOptions.CreateContainerIfNotExists)} is enabled.");
		}

		return ValidateOptionsResult.Success;
	}
}
