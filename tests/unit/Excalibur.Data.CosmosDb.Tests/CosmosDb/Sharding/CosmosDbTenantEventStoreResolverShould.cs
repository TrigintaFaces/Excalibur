// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections.Concurrent;
using Excalibur.Data.Sharding;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.CosmosDb;
using Excalibur.EventSourcing.CosmosDb.Sharding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.Tests.CosmosDb.Sharding;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Disposal")]
[Trait("Pattern", "Regression")]
public sealed class CosmosDbTenantEventStoreResolverShould
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
        await fixture.Resolver.DisposeAsync(); await fixture.Resolver.DisposeAsync();
        foreach (var client in fixture.Clients) { client.DisposeCalls.ShouldBe(1); }
    }

    [Fact]
    public async Task RefuseToResolveAfterDisposalWithoutInvokingAnyNativeFactory()
    {
        await using var fixture = new Fixture();
        await fixture.Resolver.DisposeAsync();
        Should.Throw<ObjectDisposedException>(() => fixture.Resolver.Resolve("tenant-1"));
        fixture.Clients.ShouldBeEmpty();
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
        fixture.AfterClient = client => client.ContainerFailure = original;
        Should.Throw<IOException>(() => fixture.Resolver.Resolve("tenant-1")).ShouldBeSameAs(original);
        fixture.Clients.Count.ShouldBe(1);
        var failed = fixture.Clients.Single();
        failed.DisposeCalls.ShouldBe(1);
        fixture.AfterClient = null;
        fixture.Resolver.Resolve("tenant-1").ShouldNotBeNull();
        fixture.Clients.Count.ShouldBe(2);
        await fixture.Resolver.DisposeAsync();
        foreach (var client in fixture.Clients) { client.DisposeCalls.ShouldBe(1); }
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
        fixture.Clients.Single().DisposeCalls.ShouldBe(1);
    }

    [Fact]
    public async Task DirectStoreDisposalBorrowsRatherThanDisposesSuppliedNativeClients()
    {
        var client = new RecordingClient();
        var store = new CosmosDbEventStore(client, Options.Create(new CosmosDbEventStoreOptions
        { DatabaseName = "events-db", EventsContainerName = "events" }), NullLogger<CosmosDbEventStore>.Instance, TestTenantContext.SingleTenant);
        await store.DisposeAsync();
        client.DisposeCalls.ShouldBe(0);
        client.Dispose();
    }

    private sealed class RecordingClient : CosmosClient
    {
        private int _disposeCalls;
        internal int DisposeCalls => Volatile.Read(ref _disposeCalls);
        internal Exception? ContainerFailure;
        public override Container GetContainer(string databaseId, string containerId) =>
            ContainerFailure is { } failure ? throw failure : A.Fake<Container>();

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Interlocked.Increment(ref _disposeCalls); }
            // CosmosClient's protected mocking constructor creates no ClientContext to dispose.
            base.Dispose(false);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal readonly ConcurrentBag<RecordingClient> Clients = [];
        internal Action<RecordingClient>? AfterClient;
        internal CosmosDbTenantEventStoreResolver Resolver { get; }
        internal Fixture()
        {
            var map = A.Fake<ITenantShardMap>();
            A.CallTo(() => map.GetShardInfo(A<string>._)).ReturnsLazily((string tenant) =>
                new ShardInfo(tenant == "other-shard" ? "second" : "first", "unused", Region: "us-east-1", IndexPrefix: "events"));
            var loggers = A.Fake<ILoggerFactory>();
            A.CallTo(() => loggers.CreateLogger(A<string>._)).Returns(NullLogger.Instance);
            Resolver = new CosmosDbTenantEventStoreResolver(map, loggers,
                Options.Create(new CosmosDbEventStoreOptions { DatabaseName = "events-db", EventsContainerName = "events" }),
                TestTenantContext.SingleTenant, (_, _) =>
                {
                    var client = new RecordingClient();
                    Clients.Add(client); AfterClient?.Invoke(client); return client;
                });
        }
        public ValueTask DisposeAsync() => Resolver.DisposeAsync();
    }
}
