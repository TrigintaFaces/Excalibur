// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Outbox;
using Excalibur.Outbox.Partitioning;

using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Marten;

/// <summary>
/// Validates <see cref="MartenOutboxStoreOptions"/> at startup.
/// </summary>
/// <remarks>
/// Also enforces the cross-options invariant <c>FailureBackoffFloorSeconds &gt; PollingInterval</c> on every
/// active drain path: the failure-backoff floor must exceed the single-processor poll interval
/// (<see cref="OutboxProcessingOptions"/>) and — when partitioning is enabled — the partition poll interval
/// (<see cref="OutboxPartitionOptions"/>). A floor shorter than the poll interval is no floor at all: the
/// failed message is claimable again by the very next poll, which is the retry hot-loop the floor exists to
/// prevent. Caught at startup rather than as a silent runtime regression.
/// </remarks>
/// <param name="processingOptions">The outbox drain processing options.</param>
/// <param name="partitionOptions">The outbox partitioning options.</param>
internal sealed class MartenOutboxStoreOptionsValidator(
	IOptions<OutboxProcessingOptions> processingOptions,
	IOptions<OutboxPartitionOptions> partitionOptions)
	: IValidateOptions<MartenOutboxStoreOptions>
{
	private readonly IOptions<OutboxProcessingOptions> _processingOptions =
		processingOptions ?? throw new ArgumentNullException(nameof(processingOptions));

	private readonly IOptions<OutboxPartitionOptions> _partitionOptions =
		partitionOptions ?? throw new ArgumentNullException(nameof(partitionOptions));

	/// <summary>
	/// Validates the provided Marten outbox store options.
	/// </summary>
	/// <param name="name"> The name of the options instance being validated. </param>
	/// <param name="options"> The Marten outbox store options to validate. </param>
	/// <returns> A validation result indicating success or failure. </returns>
	public ValidateOptionsResult Validate(string? name, MartenOutboxStoreOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("Marten outbox store options cannot be null.");
		}

		if (options.DefaultRetentionPeriod <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail("Default retention period must be positive.");
		}

		if (options.CleanupBatchSize <= 0)
		{
			return ValidateOptionsResult.Fail("Cleanup batch size must be greater than zero.");
		}

		if (options.FailureBackoffFloorSeconds < 1)
		{
			return ValidateOptionsResult.Fail(
				"MartenOutboxStoreOptions.FailureBackoffFloorSeconds must be at least 1.");
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
				$"MartenOutboxStoreOptions.FailureBackoffFloorSeconds ({options.FailureBackoffFloorSeconds}s) must be " +
				$"greater than the {boundBy}; otherwise a failed message is re-claimable on the next poll (a retry hot-loop).");
		}

		return ValidateOptionsResult.Success;
	}
}
