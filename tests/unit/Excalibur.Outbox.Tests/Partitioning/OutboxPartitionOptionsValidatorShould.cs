// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Partitioning;

namespace Excalibur.Outbox.Tests.Partitioning;

/// <summary>
/// Unit tests for <see cref="OutboxPartitionOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxPartitionOptionsValidatorShould
{
	private readonly OutboxPartitionOptionsValidator _sut = new();

	#region Strategy.None -- always succeeds

	[Fact]
	public void PassWhenStrategyIsNone()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.None
		});

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void PassWhenStrategyIsNone_EvenWithInvalidPartitionCount()
	{
		// None strategy bypasses all other validation
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.None,
			PartitionCount = 0
		});

		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region PartitionCount validation

	[Fact]
	public void FailWhenPartitionCountIsZero()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = 0
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("PartitionCount");
	}

	[Fact]
	public void FailWhenPartitionCountIsNegative()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = -1
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("PartitionCount");
	}

	[Fact]
	public void FailWhenPartitionCountExceeds256()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = 257
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("256");
	}

	[Fact]
	public void PassWhenPartitionCountIs256()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = 256,
			ProcessorCountPerPartition = 1,
			PollingInterval = TimeSpan.FromSeconds(1),
			ErrorBackoffInterval = TimeSpan.FromSeconds(5)
		});

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void PassWhenPartitionCountIs1()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = 1,
			ProcessorCountPerPartition = 1,
			PollingInterval = TimeSpan.FromSeconds(1),
			ErrorBackoffInterval = TimeSpan.FromSeconds(5)
		});

		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region ProcessorCountPerPartition validation

	[Fact]
	public void FailWhenProcessorCountIsZero()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = 4,
			ProcessorCountPerPartition = 0
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ProcessorCountPerPartition");
	}

	[Fact]
	public void FailWhenProcessorCountIsNegative()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = 4,
			ProcessorCountPerPartition = -1
		});

		result.Failed.ShouldBeTrue();
	}

	#endregion

	#region PerShard ShardIds validation

	[Fact]
	public void FailWhenPerShardWithNoShardIds()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.PerShard,
			PartitionCount = 4,
			ProcessorCountPerPartition = 1,
			ShardIds = [],
			PollingInterval = TimeSpan.FromSeconds(1),
			ErrorBackoffInterval = TimeSpan.FromSeconds(5)
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ShardIds");
	}

	[Fact]
	public void PassWhenPerShardWithShardIds()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.PerShard,
			PartitionCount = 2,
			ProcessorCountPerPartition = 1,
			ShardIds = ["shard-1", "shard-2"],
			PollingInterval = TimeSpan.FromSeconds(1),
			ErrorBackoffInterval = TimeSpan.FromSeconds(5)
		});

		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region PollingInterval validation

	[Fact]
	public void FailWhenPollingIntervalIsZero()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = 4,
			ProcessorCountPerPartition = 1,
			PollingInterval = TimeSpan.Zero
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("PollingInterval");
	}

	[Fact]
	public void FailWhenPollingIntervalIsNegative()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = 4,
			ProcessorCountPerPartition = 1,
			PollingInterval = TimeSpan.FromSeconds(-1)
		});

		result.Failed.ShouldBeTrue();
	}

	#endregion

	#region ErrorBackoffInterval validation

	[Fact]
	public void FailWhenErrorBackoffIntervalIsZero()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = 4,
			ProcessorCountPerPartition = 1,
			PollingInterval = TimeSpan.FromSeconds(1),
			ErrorBackoffInterval = TimeSpan.Zero
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ErrorBackoffInterval");
	}

	#endregion

	#region Multiple failures

	[Fact]
	public void ReportMultipleFailuresAtOnce()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = 0,
			ProcessorCountPerPartition = 0,
			PollingInterval = TimeSpan.Zero,
			ErrorBackoffInterval = TimeSpan.Zero
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("PartitionCount");
		result.FailureMessage.ShouldContain("ProcessorCountPerPartition");
		result.FailureMessage.ShouldContain("PollingInterval");
		result.FailureMessage.ShouldContain("ErrorBackoffInterval");
	}

	#endregion

	#region Null options

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	#endregion

	#region Valid hash strategy (full happy path)

	[Fact]
	public void PassWithValidHashStrategy()
	{
		var result = _sut.Validate(null, new OutboxPartitionOptions
		{
			Strategy = OutboxPartitionStrategy.ByTenantHash,
			PartitionCount = 8,
			ProcessorCountPerPartition = 2,
			PollingInterval = TimeSpan.FromSeconds(1),
			ErrorBackoffInterval = TimeSpan.FromSeconds(5)
		});

		result.Succeeded.ShouldBeTrue();
	}

	#endregion
}
