// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Outbox;
using Excalibur.Outbox.Partitioning;

using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.ElasticSearch;

/// <summary>
/// Validates <see cref="ElasticsearchOutboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Also enforces the cross-options invariant <c>FailureBackoffFloorSeconds &gt; PollingInterval</c> on every
/// active drain path: the failure-backoff floor must exceed the single-processor poll interval
/// (<see cref="OutboxProcessingOptions"/>) and — when partitioning is enabled — the partition poll interval
/// (<see cref="OutboxPartitionOptions"/>). A floor shorter than the poll interval is no floor at all: the
/// failed message is claimable again by the very next poll, which is the retry hot-loop the floor exists to
/// prevent. Caught at startup rather than as a silent runtime regression.
/// </remarks>
internal sealed class ElasticsearchOutboxOptionsValidator(
	IOptions<OutboxProcessingOptions> processingOptions,
	IOptions<OutboxPartitionOptions> partitionOptions)
	: IValidateOptions<ElasticsearchOutboxOptions>
{
	private readonly IOptions<OutboxProcessingOptions> _processingOptions =
		processingOptions ?? throw new ArgumentNullException(nameof(processingOptions));

	private readonly IOptions<OutboxPartitionOptions> _partitionOptions =
		partitionOptions ?? throw new ArgumentNullException(nameof(partitionOptions));

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ElasticsearchOutboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("ElasticsearchOutboxOptions cannot be null.");
		}

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.IndexName))
		{
			failures.Add("ElasticsearchOutboxOptions.IndexName is required.");
		}

		if (options.DefaultBatchSize is < 1 or > 10000)
		{
			failures.Add("ElasticsearchOutboxOptions.DefaultBatchSize must be between 1 and 10000.");
		}

		if (options.FailureBackoffFloorSeconds < 1)
		{
			failures.Add("ElasticsearchOutboxOptions.FailureBackoffFloorSeconds must be at least 1.");
		}
		else
		{
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
				failures.Add(
					$"ElasticsearchOutboxOptions.FailureBackoffFloorSeconds ({options.FailureBackoffFloorSeconds}s) must be " +
					$"greater than the {boundBy}; otherwise a failed message is re-claimable on the next poll (a retry hot-loop).");
			}
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
