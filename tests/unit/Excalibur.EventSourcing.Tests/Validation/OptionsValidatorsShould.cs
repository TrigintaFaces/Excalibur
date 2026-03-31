// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Outbox;
using Excalibur.EventSourcing.ParallelCatchUp;
using Excalibur.EventSourcing.Snapshots;
using Excalibur.EventSourcing.Sharding;
using Excalibur.EventSourcing.TieredStorage;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Validation;

/// <summary>
/// T.14 (wyb0vl): Tests for all 5 IValidateOptions validators from Sprint 732.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OptionsValidatorsShould
{
	// --- AutoSnapshotOptionsValidator ---

	[Fact]
	public void PassAutoSnapshotWithValidOptions()
	{
		var validator = new AutoSnapshotOptionsValidator();
		var result = validator.Validate(null, new AutoSnapshotOptions
		{
			EventCountThreshold = 100,
			TimeThreshold = TimeSpan.FromMinutes(5),
			VersionThreshold = 50
		});
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void PassAutoSnapshotWithAllNull()
	{
		var result = new AutoSnapshotOptionsValidator().Validate(null, new AutoSnapshotOptions());
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailAutoSnapshotWithZeroEventCount()
	{
		var result = new AutoSnapshotOptionsValidator().Validate(null,
			new AutoSnapshotOptions { EventCountThreshold = 0 });
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailAutoSnapshotWithNegativeTimeThreshold()
	{
		var result = new AutoSnapshotOptionsValidator().Validate(null,
			new AutoSnapshotOptions { TimeThreshold = TimeSpan.FromSeconds(-1) });
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailAutoSnapshotWithNegativeVersion()
	{
		var result = new AutoSnapshotOptionsValidator().Validate(null,
			new AutoSnapshotOptions { VersionThreshold = -5 });
		result.Failed.ShouldBeTrue();
	}

	// --- OutboxPartitionOptionsValidator ---

	[Fact]
	public void PassOutboxWithNoneStrategy()
	{
		var result = new OutboxPartitionOptionsValidator().Validate(null,
			new OutboxPartitionOptions { Strategy = OutboxPartitionStrategy.None });
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void PassOutboxWithValidHashStrategy()
	{
		var result = new OutboxPartitionOptionsValidator().Validate(null,
			new OutboxPartitionOptions
			{
				Strategy = OutboxPartitionStrategy.ByTenantHash,
				PartitionCount = 8,
				ProcessorCountPerPartition = 1,
				PollingInterval = TimeSpan.FromSeconds(1),
				ErrorBackoffInterval = TimeSpan.FromSeconds(5)
			});
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailOutboxWithZeroPartitionCount()
	{
		var result = new OutboxPartitionOptionsValidator().Validate(null,
			new OutboxPartitionOptions
			{
				Strategy = OutboxPartitionStrategy.ByTenantHash,
				PartitionCount = 0
			});
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailOutboxWithExcessivePartitionCount()
	{
		var result = new OutboxPartitionOptionsValidator().Validate(null,
			new OutboxPartitionOptions
			{
				Strategy = OutboxPartitionStrategy.ByTenantHash,
				PartitionCount = 500
			});
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailOutboxPerShardWithNoShardIds()
	{
		var result = new OutboxPartitionOptionsValidator().Validate(null,
			new OutboxPartitionOptions
			{
				Strategy = OutboxPartitionStrategy.PerShard,
				PartitionCount = 4
			});
		result.Failed.ShouldBeTrue();
	}

	// --- ParallelCatchUpOptionsValidator ---

	[Fact]
	public void PassCatchUpWithValidOptions()
	{
		var result = new ParallelCatchUpOptionsValidator().Validate(null,
			new ParallelCatchUpOptions
			{
				Strategy = CatchUpStrategy.RangePartitioned,
				WorkerCount = 4,
				BatchSize = 1000,
				CheckpointInterval = 5000
			});
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void PassCatchUpWithSequentialStrategy()
	{
		var result = new ParallelCatchUpOptionsValidator().Validate(null,
			new ParallelCatchUpOptions { Strategy = CatchUpStrategy.Sequential });
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailCatchUpWithZeroWorkers()
	{
		var result = new ParallelCatchUpOptionsValidator().Validate(null,
			new ParallelCatchUpOptions
			{
				Strategy = CatchUpStrategy.RangePartitioned,
				WorkerCount = 0
			});
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailCatchUpWithZeroBatchSize()
	{
		var result = new ParallelCatchUpOptionsValidator().Validate(null,
			new ParallelCatchUpOptions
			{
				Strategy = CatchUpStrategy.RangePartitioned,
				BatchSize = 0
			});
		result.Failed.ShouldBeTrue();
	}

	// --- ShardMapOptionsValidator ---

	[Fact]
	public void PassShardMapWithNoSharding()
	{
		var result = new ShardMapOptionsValidator().Validate(null,
			new Data.Abstractions.Sharding.ShardMapOptions { EnableTenantSharding = false });
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void PassShardMapWithShardingEnabled()
	{
		var result = new ShardMapOptionsValidator().Validate(null,
			new Data.Abstractions.Sharding.ShardMapOptions
			{
				EnableTenantSharding = true,
				DefaultShardId = "shard-1"
			});
		result.Succeeded.ShouldBeTrue();
	}

	// --- ArchivePolicyValidator ---

	[Fact]
	public void PassArchivePolicyWithValidMaxAge()
	{
		var result = new ArchivePolicyValidator().Validate(null,
			new ArchivePolicy { MaxAge = TimeSpan.FromDays(90) });
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailArchivePolicyWithAllNull()
	{
		// At least one criterion required
		var result = new ArchivePolicyValidator().Validate(null, new ArchivePolicy());
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailArchivePolicyWithNegativeMaxAge()
	{
		var result = new ArchivePolicyValidator().Validate(null,
			new ArchivePolicy { MaxAge = TimeSpan.FromDays(-1) });
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailArchivePolicyWithZeroRetainRecentCount()
	{
		var result = new ArchivePolicyValidator().Validate(null,
			new ArchivePolicy { RetainRecentCount = 0 });
		result.Failed.ShouldBeTrue();
	}
}
