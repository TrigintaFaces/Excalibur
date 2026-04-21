// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Sharding;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// B.12 (0pfdie): Unit tests for tenant shard resolution, routing, unknown tenant, fail-fast.
/// Covers InMemoryTenantShardMap, TenantRoutingEventStore, TenantRoutingProjectionStore.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantShardRoutingShould
{
	private static readonly ShardInfo Shard1 = new("shard-1", "Server=shard1;");
	private static readonly ShardInfo Shard2 = new("shard-2", "Server=shard2;");

	#region InMemoryTenantShardMap

	[Fact]
	public void ResolveMappedTenantToCorrectShard()
	{
		// Arrange
		var map = CreateShardMap(defaultShardId: null);

		// Act
		var result = map.GetShardInfo("tenant-a");

		// Assert
		result.ShouldBe(Shard1);
	}

	[Fact]
	public void ResolveDifferentTenantsToCorrectShards()
	{
		var map = CreateShardMap(defaultShardId: null);

		map.GetShardInfo("tenant-a").ShardId.ShouldBe("shard-1");
		map.GetShardInfo("tenant-b").ShardId.ShouldBe("shard-2");
	}

	[Fact]
	public void BeCaseInsensitiveForTenantLookup()
	{
		var map = CreateShardMap(defaultShardId: null);

		map.GetShardInfo("TENANT-A").ShouldBe(Shard1);
		map.GetShardInfo("Tenant-A").ShouldBe(Shard1);
	}

	[Fact]
	public void ThrowTenantShardNotFoundForUnknownTenantWithoutDefault()
	{
		// Arrange -- no default shard (fail-fast S2)
		var map = CreateShardMap(defaultShardId: null);

		// Act & Assert
		var ex = Should.Throw<TenantShardNotFoundException>(
			() => map.GetShardInfo("unknown-tenant"));
		ex.TenantId.ShouldBe("unknown-tenant");
		ex.Message.ShouldContain("unknown-tenant");
	}

	[Fact]
	public void RouteUnknownTenantToDefaultShard()
	{
		// Arrange -- default shard configured (S2)
		var map = CreateShardMap(defaultShardId: "shard-1");

		// Act
		var result = map.GetShardInfo("unknown-tenant");

		// Assert
		result.ShouldBe(Shard1);
	}

	[Fact]
	public void ThrowOnNullTenantId()
	{
		var map = CreateShardMap(defaultShardId: "shard-1");

		Should.Throw<ArgumentNullException>(() => map.GetShardInfo(null!));
	}

	[Fact]
	public void ThrowOnInvalidDefaultShardId()
	{
		// Arrange -- default shard references non-existent shard
		var shards = new Dictionary<string, ShardInfo>
		{
			["shard-1"] = Shard1
		};
		var tenantMappings = new Dictionary<string, string>
		{
			["tenant-a"] = "shard-1"
		};
		var options = new ShardMapOptions { DefaultShardId = "non-existent-shard" };

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => new InMemoryTenantShardMap(shards, tenantMappings, options));
	}

	[Fact]
	public void ThrowOnTenantMappedToNonExistentShard()
	{
		var shards = new Dictionary<string, ShardInfo>
		{
			["shard-1"] = Shard1
		};
		var tenantMappings = new Dictionary<string, string>
		{
			["tenant-a"] = "non-existent-shard"
		};
		var options = new ShardMapOptions();

		Should.Throw<InvalidOperationException>(
			() => new InMemoryTenantShardMap(shards, tenantMappings, options));
	}

	#endregion

	#region TenantRoutingEventStore

	[Fact]
	public async Task RouteAppendToCorrectShardStore()
	{
		// Arrange
		var tenantAStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => tenantAStore.AppendAsync(
			A<string>._, A<string>._, A<IEnumerable<IDomainEvent>>._, A<long>._, A<CancellationToken>._))
			.Returns(AppendResult.CreateSuccess(1, 1));

		var resolver = A.Fake<ITenantStoreResolver<IEventStore>>();
		_ = A.CallTo(() => resolver.Resolve("tenant-a")).Returns(tenantAStore);

		var tenantId = new TenantId { Value = "tenant-a" };
		var routingStore = new TenantRoutingEventStore(resolver, tenantId);

		// Act
		var result = await routingStore.AppendAsync(
			"agg-1", "Order", Array.Empty<IDomainEvent>(), 0, CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		A.CallTo(() => resolver.Resolve("tenant-a")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RouteLoadToCorrectShardStore()
	{
		// Arrange
		var tenantBStore = A.Fake<IEventStore>();
		_ = A.CallTo(() => tenantBStore.LoadAsync(
			A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var resolver = A.Fake<ITenantStoreResolver<IEventStore>>();
		_ = A.CallTo(() => resolver.Resolve("tenant-b")).Returns(tenantBStore);

		var tenantId = new TenantId { Value = "tenant-b" };
		var routingStore = new TenantRoutingEventStore(resolver, tenantId);

		// Act
		var events = await routingStore.LoadAsync("agg-1", "Order", CancellationToken.None);

		// Assert
		events.ShouldNotBeNull();
		A.CallTo(() => resolver.Resolve("tenant-b")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RouteLoadWithVersionToCorrectShardStore()
	{
		// Arrange
		var store = A.Fake<IEventStore>();
		_ = A.CallTo(() => store.LoadAsync(
			A<string>._, A<string>._, A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var resolver = A.Fake<ITenantStoreResolver<IEventStore>>();
		_ = A.CallTo(() => resolver.Resolve("tenant-a")).Returns(store);

		var tenantId = new TenantId { Value = "tenant-a" };
		var routingStore = new TenantRoutingEventStore(resolver, tenantId);

		// Act
		var events = await routingStore.LoadAsync("agg-1", "Order", 5, CancellationToken.None);

		// Assert
		events.ShouldNotBeNull();
		A.CallTo(() => store.LoadAsync("agg-1", "Order", 5, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ThrowWhenTenantIdIsEmpty()
	{
		// Arrange
		var resolver = A.Fake<ITenantStoreResolver<IEventStore>>();
		var tenantId = new TenantId { Value = string.Empty };
		var routingStore = new TenantRoutingEventStore(resolver, tenantId);

		// Act & Assert
		Should.ThrowAsync<InvalidOperationException>(
			async () => await routingStore.LoadAsync("agg-1", "Order", CancellationToken.None));
	}

	[Fact]
	public void ThrowWhenTenantIdIsNull()
	{
		var resolver = A.Fake<ITenantStoreResolver<IEventStore>>();
		var tenantId = new TenantId { Value = null! };
		var routingStore = new TenantRoutingEventStore(resolver, tenantId);

		Should.ThrowAsync<InvalidOperationException>(
			async () => await routingStore.LoadAsync("agg-1", "Order", CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullResolverInConstructor()
	{
		var tenantId = new TenantId { Value = "tenant-a" };
		Should.Throw<ArgumentNullException>(
			() => new TenantRoutingEventStore(null!, tenantId));
	}

	[Fact]
	public void ThrowOnNullTenantIdInConstructor()
	{
		var resolver = A.Fake<ITenantStoreResolver<IEventStore>>();
		Should.Throw<ArgumentNullException>(
			() => new TenantRoutingEventStore(resolver, null!));
	}

	#endregion

	#region TenantRoutingProjectionStore

	[Fact]
	public async Task RouteProjectionGetByIdToCorrectShard()
	{
		// Arrange
		var projStore = A.Fake<IProjectionStore<TestProjection>>();
		_ = A.CallTo(() => projStore.GetByIdAsync("proj-1", A<CancellationToken>._))
			.Returns(new TestProjection { Name = "from-shard-a" });

		var resolver = A.Fake<ITenantStoreResolver<IProjectionStore<TestProjection>>>();
		_ = A.CallTo(() => resolver.Resolve("tenant-a")).Returns(projStore);

		var tenantId = new TenantId { Value = "tenant-a" };
		var routingStore = new TenantRoutingProjectionStore<TestProjection>(resolver, tenantId);

		// Act
		var result = await routingStore.GetByIdAsync("proj-1", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Name.ShouldBe("from-shard-a");
		A.CallTo(() => resolver.Resolve("tenant-a")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RouteProjectionUpsertToCorrectShard()
	{
		var projStore = A.Fake<IProjectionStore<TestProjection>>();
		var resolver = A.Fake<ITenantStoreResolver<IProjectionStore<TestProjection>>>();
		_ = A.CallTo(() => resolver.Resolve("tenant-b")).Returns(projStore);

		var tenantId = new TenantId { Value = "tenant-b" };
		var routingStore = new TenantRoutingProjectionStore<TestProjection>(resolver, tenantId);

		// Act
		await routingStore.UpsertAsync("proj-1", new TestProjection { Name = "updated" }, CancellationToken.None);

		// Assert
		A.CallTo(() => projStore.UpsertAsync("proj-1", A<TestProjection>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RouteProjectionDeleteToCorrectShard()
	{
		var projStore = A.Fake<IProjectionStore<TestProjection>>();
		var resolver = A.Fake<ITenantStoreResolver<IProjectionStore<TestProjection>>>();
		_ = A.CallTo(() => resolver.Resolve("tenant-a")).Returns(projStore);

		var tenantId = new TenantId { Value = "tenant-a" };
		var routingStore = new TenantRoutingProjectionStore<TestProjection>(resolver, tenantId);

		await routingStore.DeleteAsync("proj-1", CancellationToken.None);

		A.CallTo(() => projStore.DeleteAsync("proj-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RouteProjectionQueryToCorrectShard()
	{
		var projStore = A.Fake<IProjectionStore<TestProjection>>();
		_ = A.CallTo(() => projStore.QueryAsync(A<IDictionary<string, object>?>._, A<QueryOptions?>._, A<CancellationToken>._))
			.Returns(new List<TestProjection> { new() { Name = "result" } });

		var resolver = A.Fake<ITenantStoreResolver<IProjectionStore<TestProjection>>>();
		_ = A.CallTo(() => resolver.Resolve("tenant-a")).Returns(projStore);

		var tenantId = new TenantId { Value = "tenant-a" };
		var routingStore = new TenantRoutingProjectionStore<TestProjection>(resolver, tenantId);

		var results = await routingStore.QueryAsync(null, null, CancellationToken.None);

		results.Count.ShouldBe(1);
	}

	[Fact]
	public async Task RouteProjectionCountToCorrectShard()
	{
		var projStore = A.Fake<IProjectionStore<TestProjection>>();
		_ = A.CallTo(() => projStore.CountAsync(A<IDictionary<string, object>?>._, A<CancellationToken>._))
			.Returns(42L);

		var resolver = A.Fake<ITenantStoreResolver<IProjectionStore<TestProjection>>>();
		_ = A.CallTo(() => resolver.Resolve("tenant-a")).Returns(projStore);

		var tenantId = new TenantId { Value = "tenant-a" };
		var routingStore = new TenantRoutingProjectionStore<TestProjection>(resolver, tenantId);

		var count = await routingStore.CountAsync(null, CancellationToken.None);

		count.ShouldBe(42L);
	}

	[Fact]
	public void ThrowWhenProjectionStoreTenantIdIsEmpty()
	{
		var resolver = A.Fake<ITenantStoreResolver<IProjectionStore<TestProjection>>>();
		var tenantId = new TenantId { Value = string.Empty };
		var routingStore = new TenantRoutingProjectionStore<TestProjection>(resolver, tenantId);

		Should.ThrowAsync<InvalidOperationException>(
			async () => await routingStore.GetByIdAsync("proj-1", CancellationToken.None));
	}

	#endregion

	#region Helpers

	private static InMemoryTenantShardMap CreateShardMap(string? defaultShardId)
	{
		var shards = new Dictionary<string, ShardInfo>
		{
			["shard-1"] = Shard1,
			["shard-2"] = Shard2
		};
		var tenantMappings = new Dictionary<string, string>
		{
			["tenant-a"] = "shard-1",
			["tenant-b"] = "shard-2"
		};
		var options = new ShardMapOptions { DefaultShardId = defaultShardId };
		return new InMemoryTenantShardMap(shards, tenantMappings, options);
	}

	#endregion
}

/// <summary>
/// Test projection type for TenantRoutingProjectionStore tests.
/// Must be public for FakeItEasy proxy generation.
/// </summary>
public sealed class TestProjection
{
	public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Gap-fill tests for TenantRoutingSagaStore and TenantShardHealthCheck (V.9, V.11).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantSagaAndHealthCheckShould
{
	[Fact]
	public async Task RouteSagaLoadToCorrectShard()
	{
		var sagaStore = A.Fake<ISagaStore>();
		_ = A.CallTo(() => sagaStore.LoadAsync<TestSagaState>(A<Guid>._, A<CancellationToken>._))
			.Returns(new TestSagaState());

		var resolver = A.Fake<ITenantStoreResolver<ISagaStore>>();
		_ = A.CallTo(() => resolver.Resolve("tenant-a")).Returns(sagaStore);

		var tenantId = new TenantId { Value = "tenant-a" };
		var routingStore = new TenantRoutingSagaStore(resolver, tenantId);

		var sagaId = Guid.NewGuid();
		var result = await routingStore.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);

		result.ShouldNotBeNull();
		A.CallTo(() => resolver.Resolve("tenant-a")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RouteSagaSaveToCorrectShard()
	{
		var sagaStore = A.Fake<ISagaStore>();
		var resolver = A.Fake<ITenantStoreResolver<ISagaStore>>();
		_ = A.CallTo(() => resolver.Resolve("tenant-b")).Returns(sagaStore);

		var tenantId = new TenantId { Value = "tenant-b" };
		var routingStore = new TenantRoutingSagaStore(resolver, tenantId);

		var state = new TestSagaState();
		await routingStore.SaveAsync(state, CancellationToken.None);

		A.CallTo(() => sagaStore.SaveAsync(state, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ThrowWhenSagaTenantIdIsEmpty()
	{
		var resolver = A.Fake<ITenantStoreResolver<ISagaStore>>();
		var tenantId = new TenantId { Value = string.Empty };
		var routingStore = new TenantRoutingSagaStore(resolver, tenantId);

		Should.ThrowAsync<InvalidOperationException>(
			async () => await routingStore.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None));
	}

	[Fact]
	public async Task HealthCheckReturnsHealthyWhenShardMapOperational()
	{
		var shardMap = A.Fake<ITenantShardMap>();
		var healthCheck = new TenantShardHealthCheck(shardMap);

		var result = await healthCheck.CheckHealthAsync(
			new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext(),
			CancellationToken.None);

		result.Status.ShouldBe(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy);
	}

	[Fact]
	public void ThrowOnNullSagaResolver()
	{
		var tenantId = new TenantId { Value = "t" };
		Should.Throw<ArgumentNullException>(() => new TenantRoutingSagaStore(null!, tenantId));
	}

	[Fact]
	public void ThrowOnNullSagaTenantId()
	{
		var resolver = A.Fake<ITenantStoreResolver<ISagaStore>>();
		Should.Throw<ArgumentNullException>(() => new TenantRoutingSagaStore(resolver, null!));
	}

}

/// <summary>
/// Test saga state for TenantRoutingSagaStore tests.
/// </summary>
public sealed class TestSagaState : SagaState { }
