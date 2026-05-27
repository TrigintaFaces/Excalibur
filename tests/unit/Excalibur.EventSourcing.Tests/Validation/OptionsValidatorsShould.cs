// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
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
	public void PassShardMapWhenShardMapNotRegistered()
	{
		// Deferred-registration scenario: validator activates before ITenantShardMap
		// is added. Passes cleanly so downstream startup can continue. [bd-51k0mc]
		var result = new ShardMapOptionsValidator().Validate(null,
			new Data.Sharding.ShardMapOptions());
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void PassShardMapWithShardingEnabled()
	{
		var shardMap = new FakeTenantShardMap(["shard-1"]);
		var result = new ShardMapOptionsValidator(shardMap).Validate(null,
			new Data.Sharding.ShardMapOptions { DefaultShardId = "shard-1" });
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailShardMapWhenDefaultShardIdMissing()
	{
		var shardMap = new FakeTenantShardMap(["shard-1"]);
		var result = new ShardMapOptionsValidator(shardMap).Validate(null,
			new Data.Sharding.ShardMapOptions { DefaultShardId = "shard-missing" });
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailShardMapWhenNoShardsRegistered()
	{
		var shardMap = new FakeTenantShardMap([]);
		var result = new ShardMapOptionsValidator(shardMap).Validate(null,
			new Data.Sharding.ShardMapOptions());
		result.Failed.ShouldBeTrue();
	}

	private sealed class FakeTenantShardMap(IReadOnlyCollection<string> shards) : Data.Sharding.ITenantShardMap
	{
		public Data.Sharding.ShardInfo GetShardInfo(string tenantId) =>
			throw new NotSupportedException();

		public IReadOnlyCollection<string> GetRegisteredShardIds() => shards;
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
