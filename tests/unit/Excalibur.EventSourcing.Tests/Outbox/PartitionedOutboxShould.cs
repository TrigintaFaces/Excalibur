// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.Outbox;

namespace Excalibur.EventSourcing.Tests.Outbox;

/// <summary>
/// F.9 (2l3z7y): Unit tests for partitioned outbox -- partitioner routing, DLQ per partition.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PartitionedOutboxShould
{
	// --- HashOutboxPartitioner ---

	[Fact]
	public void HashPartitionerReturnsDeterministicPartition()
	{
		var partitioner = new HashOutboxPartitioner(4);
		var p1 = partitioner.GetPartition("tenant-a");
		var p2 = partitioner.GetPartition("tenant-a");
		p1.ShouldBe(p2); // deterministic
	}

	[Fact]
	public void HashPartitionerDistributesAcrossPartitions()
	{
		var partitioner = new HashOutboxPartitioner(4);
		var partitions = new HashSet<int>();
		for (var i = 0; i < 100; i++)
		{
			partitions.Add(partitioner.GetPartition($"tenant-{i}"));
		}

		// 100 tenants across 4 partitions should hit at least 2
		partitions.Count.ShouldBeGreaterThan(1);
	}

	[Fact]
	public void HashPartitionerReturnsWithinRange()
	{
		var partitioner = new HashOutboxPartitioner(8);
		for (var i = 0; i < 50; i++)
		{
			var p = partitioner.GetPartition($"t-{i}");
			p.ShouldBeGreaterThanOrEqualTo(0);
			p.ShouldBeLessThan(8);
		}
	}

	[Fact]
	public void HashPartitionerExposesPartitionCount()
	{
		new HashOutboxPartitioner(16).PartitionCount.ShouldBe(16);
	}

	[Fact]
	public void HashPartitionerThrowsOnZeroPartitions()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new HashOutboxPartitioner(0));
	}

	[Fact]
	public void HashPartitionerThrowsOnNegativePartitions()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new HashOutboxPartitioner(-1));
	}

	[Fact]
	public void HashPartitionerThrowsOnNullTenantId()
	{
		var partitioner = new HashOutboxPartitioner(4);
		Should.Throw<ArgumentNullException>(() => partitioner.GetPartition(null!));
	}

	[Fact]
	public void HashPartitionerHandlesSinglePartition()
	{
		var partitioner = new HashOutboxPartitioner(1);
		partitioner.GetPartition("any-tenant").ShouldBe(0);
	}

	// --- ShardOutboxPartitioner ---

	[Fact]
	public void ShardPartitionerRoutesToCorrectPartition()
	{
		var shardMap = A.Fake<ITenantShardMap>();
		_ = A.CallTo(() => shardMap.GetShardInfo("tenant-a"))
			.Returns(new ShardInfo("shard-1", "conn1"));
		_ = A.CallTo(() => shardMap.GetShardInfo("tenant-b"))
			.Returns(new ShardInfo("shard-2", "conn2"));

		var partitioner = new ShardOutboxPartitioner(shardMap, ["shard-1", "shard-2"]);

		partitioner.GetPartition("tenant-a").ShouldBe(0);
		partitioner.GetPartition("tenant-b").ShouldBe(1);
	}

	[Fact]
	public void ShardPartitionerReturnsZeroForUnknownShard()
	{
		var shardMap = A.Fake<ITenantShardMap>();
		_ = A.CallTo(() => shardMap.GetShardInfo("tenant-x"))
			.Returns(new ShardInfo("unknown-shard", "conn"));

		var partitioner = new ShardOutboxPartitioner(shardMap, ["shard-1"]);

		partitioner.GetPartition("tenant-x").ShouldBe(0);
	}

	[Fact]
	public void ShardPartitionerExposesPartitionCount()
	{
		var shardMap = A.Fake<ITenantShardMap>();
		var partitioner = new ShardOutboxPartitioner(shardMap, ["a", "b", "c"]);
		partitioner.PartitionCount.ShouldBe(3);
	}

	[Fact]
	public void ShardPartitionerIsCaseInsensitive()
	{
		var shardMap = A.Fake<ITenantShardMap>();
		_ = A.CallTo(() => shardMap.GetShardInfo("tenant-a"))
			.Returns(new ShardInfo("SHARD-1", "conn1"));

		var partitioner = new ShardOutboxPartitioner(shardMap, ["shard-1"]);
		partitioner.GetPartition("tenant-a").ShouldBe(0);
	}

	// --- OutboxPartitionOptions ---

	[Fact]
	public void OptionsDefaultToNoneStrategy()
	{
		var options = new OutboxPartitionOptions();
		options.Strategy.ShouldBe(OutboxPartitionStrategy.None);
		options.PartitionCount.ShouldBe(8);
		options.ProcessorCountPerPartition.ShouldBe(1);
	}
}
