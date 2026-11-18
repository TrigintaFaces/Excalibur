// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections.Concurrent;
using Excalibur.Data.Sharding;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DynamoDb;
using Excalibur.EventSourcing.DynamoDb.Sharding;
using Microsoft.Extensions.Logging.Abstractions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBStreams;

namespace Excalibur.Data.Tests.DynamoDb.Sharding;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Disposal")]
[Trait("Pattern", "Regression")]
public sealed class DynamoDbTenantEventStoreResolverShould
{
    [Fact]
    public async Task ReuseOneShardAllocateASecondAndDisposeEachNativeClientExactlyOnce()
    {
        await using var fixture = new Fixture();
        var first = fixture.Resolver.Resolve("tenant-1");
        fixture.Resolver.Resolve("tenant-2").ShouldBeSameAs(first);
        first.ShouldBeAssignableTo<IEventStore>();
        var second = fixture.Resolver.Resolve("other-shard");
        second.ShouldNotBeSameAs(first);
        fixture.Clients.Count.ShouldBe(2);
        fixture.Streams.Count.ShouldBe(2);
        await fixture.Resolver.DisposeAsync(); await fixture.Resolver.DisposeAsync();
        foreach (var client in fixture.Clients) { A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly(); }
        foreach (var client in fixture.Streams) { A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly(); }
    }

    [Fact]
    public async Task RefuseToResolveAfterDisposalWithoutInvokingAnyNativeFactory()
    {
        await using var fixture = new Fixture();
        await fixture.Resolver.DisposeAsync();
        Should.Throw<ObjectDisposedException>(() => fixture.Resolver.Resolve("tenant-1"));
        fixture.Clients.ShouldBeEmpty();
        fixture.Streams.ShouldBeEmpty();
    }

    [Fact]
    public async Task TolerateDoubleDisposal()
    {
        var fixture = new Fixture();
        await fixture.Resolver.DisposeAsync(); await fixture.Resolver.DisposeAsync();
        fixture.Clients.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnTheSameStoreForOneShard()
    {
        await using var fixture = new Fixture();
        var first = fixture.Resolver.Resolve("tenant-1");
        fixture.Resolver.Resolve("tenant-2").ShouldBeSameAs(first);
        fixture.Clients.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RollBackOwnedAllocationWhenLaterConstructionFailsAndRetryWithFreshClients()
    {
        await using var fixture = new Fixture();
        var original = new IOException("construction failed after native allocation");
        fixture.StreamsFailure = original;
        Should.Throw<IOException>(() => fixture.Resolver.Resolve("tenant-1")).ShouldBeSameAs(original);
        fixture.Clients.Count.ShouldBe(1);
        var failed = fixture.Clients.Single();
        A.CallTo(() => failed.Dispose()).MustHaveHappenedOnceExactly();
        fixture.StreamsFailure = null; fixture.Streams.ShouldBeEmpty();
        fixture.Resolver.Resolve("tenant-1").ShouldNotBeNull();
        fixture.Clients.Count.ShouldBe(2);
        await fixture.Resolver.DisposeAsync();
        foreach (var client in fixture.Clients) { A.CallTo(() => client.Dispose()).MustHaveHappenedOnceExactly(); }
        fixture.Streams.Count.ShouldBe(1);
        A.CallTo(() => fixture.Streams.Single().Dispose()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task JoinNativeConstructionThenDisposeThePublishedBindingAndSealFurtherResolve()
    {
        await using var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        fixture.AfterClient = _ =>
        {
            entered.TrySetResult();
#pragma warning disable RS0030 // Deliberately hold synchronous native construction across resolver Dispose.
            release.Wait();
#pragma warning restore RS0030
        };
#pragma warning disable RS0030 // Controlled concurrent Resolve/Dispose against the real provider binding publication gate.
        var resolving = Task.Run(() => fixture.Resolver.Resolve("tenant-1"));
        Task shutdown = Task.CompletedTask;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            shutdown = Task.Run(async () => { disposing.TrySetResult(); await fixture.Resolver.DisposeAsync(); });
            await disposing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            shutdown.IsCompleted.ShouldBeFalse();
        }
        finally { release.Set(); }
#pragma warning restore RS0030
        await resolving.WaitAsync(TimeSpan.FromSeconds(5));
        await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
        Should.Throw<ObjectDisposedException>(() => fixture.Resolver.Resolve("other-shard"));
        fixture.Clients.Count.ShouldBe(1);
        A.CallTo(() => fixture.Clients.Single().Dispose()).MustHaveHappenedOnceExactly();
        fixture.Streams.Count.ShouldBe(1);
        A.CallTo(() => fixture.Streams.Single().Dispose()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DirectStoreDisposalBorrowsRatherThanDisposesSuppliedNativeClients()
    {
        var client = A.Fake<IAmazonDynamoDB>(); var streams = A.Fake<IAmazonDynamoDBStreams>();
        var store = new DynamoDbEventStore(client, streams, Options.Create(new DynamoDbEventStoreOptions
        { EventsTableName = "events" }), NullLogger<DynamoDbEventStore>.Instance, TestTenantContext.SingleTenant);
        await store.DisposeAsync();
        A.CallTo(() => client.Dispose()).MustNotHaveHappened();
        A.CallTo(() => streams.Dispose()).MustNotHaveHappened();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal readonly ConcurrentBag<IAmazonDynamoDB> Clients = [];
        internal Action<IAmazonDynamoDB>? AfterClient;
        internal DynamoDbTenantEventStoreResolver Resolver { get; }
        internal readonly ConcurrentBag<IAmazonDynamoDBStreams> Streams = [];
        internal Exception? StreamsFailure;
        internal Fixture()
        {
            var map = A.Fake<ITenantShardMap>();
            A.CallTo(() => map.GetShardInfo(A<string>._)).ReturnsLazily((string tenant) =>
                new ShardInfo(tenant == "other-shard" ? "second" : "first", "unused", Region: "us-east-1", IndexPrefix: "events"));
            var loggers = A.Fake<ILoggerFactory>();
            A.CallTo(() => loggers.CreateLogger(A<string>._)).Returns(NullLogger.Instance);
            Resolver = new DynamoDbTenantEventStoreResolver(map, loggers,
                Options.Create(new DynamoDbEventStoreOptions { EventsTableName = "events" }), TestTenantContext.SingleTenant,
                _ => { var client = A.Fake<IAmazonDynamoDB>(); Clients.Add(client); AfterClient?.Invoke(client); return client; },
                _ =>
                {
                    if (StreamsFailure is { } failure) { throw failure; }
                    var streams = A.Fake<IAmazonDynamoDBStreams>(); Streams.Add(streams); return streams;
                });
        }
        public ValueTask DisposeAsync() => Resolver.DisposeAsync();
    }
}
