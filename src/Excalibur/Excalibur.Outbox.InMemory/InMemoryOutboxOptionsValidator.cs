// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Outbox;
using Excalibur.Outbox.Partitioning;

using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.InMemory;

/// <summary>
/// Validates <see cref="InMemoryOutboxOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Also enforces the cross-options Lamport-R1 invariant <c>FailureBackoffFloorSeconds &gt; PollingInterval</c>
/// on <b>every active drain path</b>: the failure-backoff floor must exceed the single-processor poll interval
/// (<see cref="OutboxProcessingOptions"/>) and — when partitioning is enabled — the partition poll interval
/// (<see cref="OutboxPartitionOptions"/>), or a failed message could be re-claimed on the very next poll (the
/// hot-loop the floor exists to prevent). This is a fail-fast composition validator, made loud at startup
/// rather than a silent runtime regression.
/// </remarks>
internal sealed class InMemoryOutboxOptionsValidator(
	IOptions<OutboxProcessingOptions> processingOptions,
	IOptions<OutboxPartitionOptions> partitionOptions)
	: IValidateOptions<InMemoryOutboxOptions>
{
	private readonly IOptions<OutboxProcessingOptions> _processingOptions =
		processingOptions ?? throw new ArgumentNullException(nameof(processingOptions));

	private readonly IOptions<OutboxPartitionOptions> _partitionOptions =
		partitionOptions ?? throw new ArgumentNullException(nameof(partitionOptions));

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, InMemoryOutboxOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail("InMemoryOutboxOptions cannot be null.");
		}

		if (options.MaxMessages < 0)
		{
			return ValidateOptionsResult.Fail(
				"InMemoryOutboxOptions.MaxMessages must be zero or greater.");
		}

		if (options.DefaultRetentionPeriod <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				"InMemoryOutboxOptions.DefaultRetentionPeriod must be greater than zero.");
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
				$"InMemoryOutboxOptions.FailureBackoffFloorSeconds ({options.FailureBackoffFloorSeconds}s) must be " +
				$"greater than the {boundBy}; otherwise a failed message is re-claimable on the next poll (a retry hot-loop).");
		}

		return ValidateOptionsResult.Success;
	}
}
