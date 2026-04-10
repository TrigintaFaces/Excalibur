// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Outbox.Partitioning;

namespace Excalibur.Outbox.Tests.Partitioning;

/// <summary>
/// Tests for <see cref="PartitionedOutboxBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class PartitionedOutboxBuilderExtensionsShould
{
	// --- Null guards ---

	[Fact]
	public void ThrowWhenBuilderIsNull_ActionOverload()
	{
		IOutboxBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() =>
			builder.UsePartitionedProcessing(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UsePartitionedProcessing((Action<OutboxPartitionOptions>)null!));
	}

	[Fact]
	public void ThrowWhenBuilderIsNull_ConfigurationOverload()
	{
		IOutboxBuilder builder = null!;
		var config = new ConfigurationBuilder().Build();
		Should.Throw<ArgumentNullException>(() =>
			builder.UsePartitionedProcessing(config));
	}

	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		var builder = CreateBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UsePartitionedProcessing((IConfiguration)null!));
	}

	// --- Strategy.None early return ---

	[Fact]
	public void ReturnEarlyWhenStrategyIsNone()
	{
		var builder = CreateBuilder();
		var result = builder.UsePartitionedProcessing(o => o.Strategy = OutboxPartitionStrategy.None);

		result.ShouldBeSameAs(builder);
		builder.Services.ShouldNotContain(sd => sd.ServiceType == typeof(IOutboxPartitioner));
	}

	// --- ByTenantHash strategy ---

	[Fact]
	public void RegisterHashPartitionerForByTenantHashStrategy()
	{
		var builder = CreateBuilder();
		builder.UsePartitionedProcessing(o =>
		{
			o.Strategy = OutboxPartitionStrategy.ByTenantHash;
			o.PartitionCount = 4;
		});

		using var sp = builder.Services.BuildServiceProvider();
		var partitioner = sp.GetService<IOutboxPartitioner>();
		partitioner.ShouldNotBeNull();
		partitioner.ShouldBeOfType<HashOutboxPartitioner>();
		partitioner.PartitionCount.ShouldBe(4);
	}

	// --- PerShard strategy ---

	[Fact]
	public void RegisterShardPartitionerForPerShardStrategy()
	{
		var shardMap = A.Fake<ITenantShardMap>();
		var builder = CreateBuilder();
		builder.Services.AddSingleton(shardMap);
		builder.Services.AddLogging();

		builder.UsePartitionedProcessing(o =>
		{
			o.Strategy = OutboxPartitionStrategy.PerShard;
			o.ShardIds = ["shard-a", "shard-b"];
		});

		using var sp = builder.Services.BuildServiceProvider();
		var partitioner = sp.GetService<IOutboxPartitioner>();
		partitioner.ShouldNotBeNull();
		partitioner.ShouldBeOfType<ShardOutboxPartitioner>();
		partitioner.PartitionCount.ShouldBe(2);
	}

	// --- TryAdd semantics ---

	[Fact]
	public void NotOverwriteExistingPartitioner()
	{
		var existing = new HashOutboxPartitioner(2);
		var builder = CreateBuilder();
		builder.Services.AddSingleton<IOutboxPartitioner>(existing);

		builder.UsePartitionedProcessing(o =>
		{
			o.Strategy = OutboxPartitionStrategy.ByTenantHash;
			o.PartitionCount = 16;
		});

		using var sp = builder.Services.BuildServiceProvider();
		var partitioner = sp.GetRequiredService<IOutboxPartitioner>();
		partitioner.ShouldBeSameAs(existing);
		partitioner.PartitionCount.ShouldBe(2); // original, not overwritten
	}

	// --- Configuration overload ---

	[Fact]
	public void RegisterPartitionerFromConfigurationSection()
	{
		var shardMap = A.Fake<ITenantShardMap>();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Strategy"] = "ByTenantHash",
				["PartitionCount"] = "6",
			})
			.Build();

		var builder = CreateBuilder();
		builder.Services.AddSingleton(shardMap); // not needed for hash, but harmless
		builder.UsePartitionedProcessing(config);

		using var sp = builder.Services.BuildServiceProvider();
		var partitioner = sp.GetRequiredService<IOutboxPartitioner>();
		partitioner.ShouldBeOfType<HashOutboxPartitioner>();
		partitioner.PartitionCount.ShouldBe(6);
	}

	// --- Fluent return ---

	[Fact]
	public void ReturnSameBuilderForChaining()
	{
		var builder = CreateBuilder();
		var result = builder.UsePartitionedProcessing(o =>
		{
			o.Strategy = OutboxPartitionStrategy.ByTenantHash;
			o.PartitionCount = 2;
		});

		result.ShouldBeSameAs(builder);
	}

	// --- Options validation registered ---

	[Fact]
	public void RegisterOptionsValidatorForActionOverload()
	{
		var builder = CreateBuilder();
		builder.UsePartitionedProcessing(o =>
		{
			o.Strategy = OutboxPartitionStrategy.ByTenantHash;
			o.PartitionCount = 4;
		});

		builder.Services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<OutboxPartitionOptions>));
	}

	private static OutboxBuilder CreateBuilder() =>
		new(new ServiceCollection(), new global::Excalibur.Outbox.OutboxConfiguration());
}
