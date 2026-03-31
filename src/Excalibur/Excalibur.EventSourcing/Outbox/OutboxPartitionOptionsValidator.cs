// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Outbox;

/// <summary>
/// Validates <see cref="OutboxPartitionOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
internal sealed class OutboxPartitionOptionsValidator : IValidateOptions<OutboxPartitionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, OutboxPartitionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.Strategy == OutboxPartitionStrategy.None)
		{
			return ValidateOptionsResult.Success;
		}

		var failures = new List<string>();

		if (options.PartitionCount <= 0)
		{
			failures.Add($"{nameof(OutboxPartitionOptions.PartitionCount)} must be greater than 0 (was {options.PartitionCount}).");
		}
		else if (options.PartitionCount > 256)
		{
			failures.Add($"{nameof(OutboxPartitionOptions.PartitionCount)} must not exceed 256 (was {options.PartitionCount}).");
		}

		if (options.ProcessorCountPerPartition <= 0)
		{
			failures.Add($"{nameof(OutboxPartitionOptions.ProcessorCountPerPartition)} must be greater than 0 (was {options.ProcessorCountPerPartition}).");
		}

		if (options.Strategy == OutboxPartitionStrategy.PerShard && options.ShardIds.Count == 0)
		{
			failures.Add($"{nameof(OutboxPartitionOptions.ShardIds)} must contain at least one shard ID when Strategy is PerShard.");
		}

		if (options.PollingInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(OutboxPartitionOptions.PollingInterval)} must be greater than TimeSpan.Zero (was {options.PollingInterval}).");
		}

		if (options.ErrorBackoffInterval <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(OutboxPartitionOptions.ErrorBackoffInterval)} must be greater than TimeSpan.Zero (was {options.ErrorBackoffInterval}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
