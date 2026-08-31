// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Data.Sharding;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.CosmosDb;
using Excalibur.EventSourcing.CosmosDb.Sharding;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.CosmosDb.Sharding;

/// <summary>
/// Locks client-lifetime ownership for the Cosmos DB tenant shard resolver.
/// </summary>
/// <remarks>
/// <para>
/// Whoever creates the client disposes it. The resolver constructs a Cosmos client per shard, so it
/// owns it: disposing the resolver must dispose everything it built, or a long-running multi-tenant
/// host holds one connection pool per shard open for the life of the process with nothing able to
/// release it.
/// </para>
/// <para>
/// The recording arm and the disposing arm are both asserted. Either alone passes against a resolver
/// that leaks: recording without disposing is the leak itself, and disposing an empty ledger is what a
/// resolver that never recorded anything does.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Disposal")]
public sealed class CosmosDbTenantEventStoreResolverShould
{
	private const string ShardConnectionString =
		"AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;";

	private static CosmosDbTenantEventStoreResolver CreateResolver()
	{
		var shardMap = A.Fake<ITenantShardMap>();
		A.CallTo(() => shardMap.GetShardInfo(A<string>._))
			.Returns(new ShardInfo("shard-1", ShardConnectionString, DatabaseName: "events-db", IndexPrefix: "events"));

		var loggerFactory = A.Fake<ILoggerFactory>();
		A.CallTo(() => loggerFactory.CreateLogger(A<string>._)).Returns(NullLogger.Instance);

		var options = Options.Create(new CosmosDbEventStoreOptions
		{
			DatabaseName = "events-db",
			EventsContainerName = "events",
		});

		return new CosmosDbTenantEventStoreResolver(
			shardMap, loggerFactory, options, TestTenantContext.SingleTenant);
	}

	private static ConcurrentBag<IDisposable> CreatedClients(CosmosDbTenantEventStoreResolver resolver) =>
		(ConcurrentBag<IDisposable>)resolver.GetType()
			.GetField("_createdClients", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(resolver)!;

	[Fact]
	public async Task RecordEveryClientItConstructs_SoDisposalCanReachThem()
	{
		var resolver = CreateResolver();

		_ = resolver.Resolve("tenant-1");

		// The client is constructed here and referenced by nothing the host can reach, so an
		// unrecorded one can never be disposed.
		CreatedClients(resolver).Count.ShouldBe(1);

		await resolver.DisposeAsync();
	}

	[Fact]
	public async Task DisposeTheClientsItCreated()
	{
		var resolver = CreateResolver();
		var owned = A.Fake<IDisposable>();
		CreatedClients(resolver).Add(owned);

		await resolver.DisposeAsync();

		A.CallTo(() => owned.Dispose()).MustHaveHappened();
	}

	[Fact]
	public async Task RefuseToResolveAfterDisposal()
	{
		var resolver = CreateResolver();
		await resolver.DisposeAsync();

		_ = Should.Throw<ObjectDisposedException>(() => resolver.Resolve("tenant-1"));
	}

	[Fact]
	public async Task TolerateDoubleDisposal()
	{
		var resolver = CreateResolver();

		await resolver.DisposeAsync();
		await resolver.DisposeAsync();
	}

	[Fact]
	public async Task ReturnTheSameStoreForOneShard()
	{
		var resolver = CreateResolver();

		var first = resolver.Resolve("tenant-1");
		var second = resolver.Resolve("tenant-2");

		first.ShouldBeSameAs(second);
		first.ShouldBeAssignableTo<IEventStore>();

		await resolver.DisposeAsync();
	}
}
