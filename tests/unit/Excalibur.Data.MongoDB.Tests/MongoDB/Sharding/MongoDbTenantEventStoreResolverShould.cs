// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.MongoDB;
using Excalibur.EventSourcing.MongoDB.Sharding;

namespace Excalibur.Data.Tests.MongoDB.Sharding;

/// <summary>
/// Verifies the <see cref="MongoDbTenantEventStoreResolver"/> IAsyncDisposable implementation
/// and ObjectDisposedException guard on <see cref="ITenantStoreResolver{T}.Resolve"/>.
/// </summary>
/// <remarks>
/// Sprint 817: bd-4l1ntw — MongoDbTenantEventStoreResolver IAsyncDisposable fix.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "MongoDB")]
[Trait("Feature", "Sharding")]
public sealed class MongoDbTenantEventStoreResolverShould : UnitTestBase
{
	private static MongoDbTenantEventStoreResolver CreateResolver()
	{
		var shardMap = A.Fake<ITenantShardMap>();
		A.CallTo(() => shardMap.GetShardInfo(A<string>._))
			.Returns(new ShardInfo("shard-1", "mongodb://localhost:27017", "testdb"));

		var loggerFactory = A.Fake<ILoggerFactory>();
		A.CallTo(() => loggerFactory.CreateLogger(A<string>._))
			.Returns(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

		var options = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = "mongodb://localhost:27017",
			DatabaseName = "testdb",
			CollectionName = "events"
		});

		return new MongoDbTenantEventStoreResolver(shardMap, loggerFactory, options, null, null);
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		var resolver = CreateResolver();

		resolver.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void ResolveEventStore_ForTenant()
	{
		// Arrange
		var resolver = CreateResolver();

		// Act
		var store = resolver.Resolve("tenant-1");

		// Assert
		store.ShouldNotBeNull();
		store.ShouldBeAssignableTo<IEventStore>();
	}

	[Fact]
	public async Task ThrowObjectDisposedException_WhenResolveCalledAfterDispose()
	{
		// Arrange
		var resolver = CreateResolver();
		await resolver.DisposeAsync();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => resolver.Resolve("tenant-1"));
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		var resolver = CreateResolver();

		// Act & Assert — double dispose should not throw
		await resolver.DisposeAsync();
		await resolver.DisposeAsync();
	}

	[Fact]
	public void ReturnSameStore_ForSameTenant()
	{
		// Arrange
		var resolver = CreateResolver();

		// Act
		var store1 = resolver.Resolve("tenant-1");
		var store2 = resolver.Resolve("tenant-1");

		// Assert — cached, same instance
		store1.ShouldBeSameAs(store2);
	}
}
