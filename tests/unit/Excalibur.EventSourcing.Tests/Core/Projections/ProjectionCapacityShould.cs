// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Row = Excalibur.EventSourcing.Tests.Core.Projections.ProjectionCapacityTestRow;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "EventSourcing")]
public sealed class ProjectionCapacityShould
{
    [Fact]
    public async Task RejectLagAndCursorMapIoWhenTheSharedBudgetIsFull()
    {
        using var capacity = new ProjectionCapacity(Options.Create(new ProjectionCapacityOptions
        {
            MaxConcurrentOperations = 1, QueueLimit = 0,
        }));
        using var provider = new ServiceCollection().AddSingleton(capacity).BuildServiceProvider();
        var release = Signal();
        var active = capacity.ExecuteAsync<int>("other-resource", ProjectionCapacityKind.Write, async _ =>
        {
            await release.Task;
            return 1;
        }, CancellationToken.None).AsTask();
        try
        {
            var query = A.Fake<Excalibur.EventSourcing.Queries.IGlobalStreamQuery>();
            var checkpoints = A.Fake<Excalibur.EventSourcing.Subscriptions.ISubscriptionCheckpointStore>();
            var lag = new ProjectionLagReadModel(checkpoints, query, capacity: capacity);
            await Should.ThrowAsync<InvalidOperationException>(() => lag.GetLagAsync(CancellationToken.None).AsTask());
            A.CallTo(() => query.GetHeadPositionAsync(A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => checkpoints.EnumerateCheckpointsAsync(A<CancellationToken>._)).MustNotHaveHappened();
            var wrote = false;
            await Should.ThrowAsync<InvalidOperationException>(() => ProjectionCapacitySource.WriteAsync(provider,
                new object(), _ => { wrote = true; return Task.CompletedTask; }, CancellationToken.None));
            wrote.ShouldBeFalse();
        }
        finally
        {
            release.TrySetResult();
            await active;
        }
    }

    [Fact]
    public void UseOracleDataSourceWhenTheDriverDatabasePropertyIsEmpty()
    {
        const string connectionString = "Data Source=localhost:1521/FREEPDB1;User Id=probe;Password=unused";
        using var connection = new global::Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        connection.Database.ShouldBeEmpty();
        var query = new Excalibur.EventSourcing.Oracle.OracleGlobalStreamQuery(
            () => new global::Oracle.ManagedDataAccess.Client.OracleConnection(connectionString), "APP", "EVENTS");
        var checkpoints = new Excalibur.EventSourcing.Oracle.OracleSubscriptionCheckpointStore(
            () => new global::Oracle.ManagedDataAccess.Client.OracleConnection(connectionString), "APP");
        var resource = ((IProjectionDestinationScope)query).Scope;
        resource.ShouldBe("oracle/" + Uri.EscapeDataString(connection.DataSource));
        resource.ShouldBe(((IProjectionDestinationScope)checkpoints).Scope);
        resource.ShouldNotContain("unused");
    }

    [Fact]
    public async Task RejectStorageCalledSynchronouslyByAnAdmissionMetricListener()
    {
        using var capacity = Create();
        using var listener = new MeterListener();
        var attempted = false;
        var rejected = false;
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == "Excalibur.EventSourcing.ProjectionCapacity"
                && instrument.Name == "projection.capacity.waiting") { owner.EnableMeasurementEvents(instrument); }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
        {
            if (attempted || value != -1) { return; }
            attempted = true;
            // Do not block the test on a regression: record whether rejection was immediate.
            var nested = capacity.ExecuteAsync("database", ProjectionCapacityKind.Read,
                _ => ValueTask.FromResult(1), CancellationToken.None).AsTask();
            rejected = nested.IsFaulted && nested.Exception?.InnerException is InvalidOperationException;
        });
        listener.Start();
        (await capacity.ExecuteAsync("database", ProjectionCapacityKind.Read, _ => ValueTask.FromResult(2), CancellationToken.None)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(2);
        attempted.ShouldBeTrue();
        rejected.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void RejectInvalidOptionsThroughTheRegisteredOptionsPipeline(int invalidProperty)
    {
        var services = new ServiceCollection();
        services.AddProjectionRebuild();
        services.Configure<ProjectionCapacityOptions>(options =>
        {
            switch (invalidProperty)
            {
                case 0: options.MaxConcurrentOperations = 0; break;
                case 1: options.MaxConcurrentReads = 0; break;
                case 2: options.MaxConcurrentWrites = 0; break;
                case 3: options.MaxConcurrentRebuilds = 0; break;
                case 4: options.QueueLimit = -1; break;
                case 5: options.MaxResources = 0; break;
            }
        });
        using var provider = services.BuildServiceProvider();
        Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<ProjectionCapacity>());
    }

    [Fact]
    public async Task ReleaseResourcePermitAfterApplicationBudgetRejection()
    {
        using var capacity = new ProjectionCapacity(Options.Create(new ProjectionCapacityOptions
        {
            MaxConcurrentOperations = 1, MaxConcurrentWrites = 1, QueueLimit = 0,
        }));
        var release = Signal();
        var owner = capacity.ExecuteAsync<int>("first", ProjectionCapacityKind.Read, async _ =>
        {
            await release.Task;
            return 1;
        }, CancellationToken.None).AsTask();
        await Should.ThrowAsync<InvalidOperationException>(() => capacity.ExecuteAsync("second", ProjectionCapacityKind.Write,
            _ => ValueTask.FromResult(2), CancellationToken.None).AsTask());
        release.SetResult();
        await owner;
        (await capacity.ExecuteAsync("second", ProjectionCapacityKind.Write, _ => ValueTask.FromResult(3), CancellationToken.None)).ShouldBe(3);
    }

    [Fact]
    public async Task PreserveFifoOrderWhenTheMiddleWaiterCancels()
    {
        using var capacity = Create();
        using var cancellation = new CancellationTokenSource();
        var release = Signal();
        var owner = capacity.ExecuteAsync<int>("database", ProjectionCapacityKind.Write, async _ =>
        {
            await release.Task;
            return 0;
        }, CancellationToken.None).AsTask();
        var order = new List<int>();
        Task<int> Queue(int number, CancellationToken token) => capacity.ExecuteAsync("database", ProjectionCapacityKind.Write,
            _ => { order.Add(number); return ValueTask.FromResult(number); }, token).AsTask();
        var first = Queue(1, CancellationToken.None);
        var middle = Queue(2, cancellation.Token);
        var last = Queue(3, CancellationToken.None);
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => middle);
        release.SetResult();
        await Task.WhenAll(owner, first, last).WaitAsync(TimeSpan.FromSeconds(5));
        order.ShouldBe([1, 3]);
    }

    [Fact]
    public async Task AdmitMetadataDiscoveryAndVisibilityRefresh()
    {
        using var capacity = Create();
        var store = A.Fake<IProjectionStore<Row>>();
        var metadata = A.Fake<IAsyncProjectionDestinationScope>();
        var barrier = A.Fake<IProjectionVisibilityBarrier>();
        A.CallTo(() => store.GetService(typeof(IProjectionTenantBoundary))).Returns(ProjectionTenantBoundary.EstateWide);
        A.CallTo(() => store.GetService(typeof(IAsyncProjectionDestinationScope))).Returns(metadata);
        A.CallTo(() => store.GetService(typeof(IProjectionVisibilityBarrier))).Returns(barrier);
        A.CallTo(() => metadata.GetScopeAsync(A<CancellationToken>._)).ReturnsLazily(() => ValueTask.FromResult("database"));
        using var provider = new ServiceCollection().AddSingleton(capacity).AddSingleton(store).BuildServiceProvider();
        var probe = new Excalibur.EventSourcing.DependencyInjection.PositionedProjectionProbe<Row>();
        var releaseMetadata = Signal();
        var metadataOwner = capacity.ExecuteAsync<int>(null, ProjectionCapacityKind.Read, async _ =>
        {
            await releaseMetadata.Task;
            return 1;
        }, CancellationToken.None).AsTask();
        var discovery = probe.GetDestinationScopeAsync(provider, CancellationToken.None).AsTask();
        discovery.IsCompleted.ShouldBeFalse();
        A.CallTo(() => metadata.GetScopeAsync(A<CancellationToken>._)).MustNotHaveHappened();
        releaseMetadata.SetResult();
        await metadataOwner;
        (await discovery.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe("estate/database");

        var releaseWrite = Signal();
        var writer = capacity.ExecuteAsync<int>("database", ProjectionCapacityKind.Write, async _ =>
        {
            await releaseWrite.Task;
            return 1;
        }, CancellationToken.None).AsTask();
        var refresh = probe.EnsureVisibleAsync(provider, CancellationToken.None);
        refresh.IsCompleted.ShouldBeFalse();
        A.CallTo(() => barrier.EnsureVisibleAsync(A<CancellationToken>._)).MustNotHaveHappened();
        releaseWrite.SetResult();
        await writer;
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));
        A.CallTo(() => barrier.EnsureVisibleAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RejectAResourceChangedWhileQueuedBeforeCallingStorage()
    {
        using var capacity = Create();
        var store = A.Fake<IProjectionStore<Row>>();
        var scope = A.Fake<IProjectionDestinationScope>();
        var resource = "first";
        A.CallTo(() => scope.Scope).ReturnsLazily(() => resource);
        A.CallTo(() => store.GetService(typeof(IProjectionDestinationScope))).Returns(scope);
        var release = Signal();
        var owner = capacity.ExecuteAsync<int>("first", ProjectionCapacityKind.Write, async _ =>
        {
            await release.Task;
            return 1;
        }, CancellationToken.None).AsTask();
        var called = false;
        var waiting = capacity.ExecuteStoreAsync(store, ProjectionCapacityKind.Write, _ =>
        {
            called = true;
            return ValueTask.FromResult(2);
        }, CancellationToken.None).AsTask();
        resource = "second";
        release.SetResult();
        await owner;
        await Should.ThrowAsync<InvalidOperationException>(() => waiting);
        called.ShouldBeFalse();
    }

    [Fact]
    public async Task ShareAdmissionAcrossScopesAndDistinctStoresWithTheSameStorageScope()
    {
        var services = new ServiceCollection();
        services.AddProjectionRebuild();
        services.Configure<ProjectionCapacityOptions>(options => options.MaxConcurrentReads = 1);
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        firstScope.ServiceProvider.GetRequiredService<ProjectionCapacity>()
            .ShouldBeSameAs(secondScope.ServiceProvider.GetRequiredService<ProjectionCapacity>());
        var firstStore = A.Fake<IProjectionStore<Row>>();
        var secondStore = A.Fake<IProjectionStore<Row>>();
        var identity = A.Fake<IProjectionDestinationScope>();
        A.CallTo(() => identity.Scope).Returns("sqlserver/shared/database");
        A.CallTo(() => firstStore.GetService(typeof(IProjectionDestinationScope))).Returns(identity);
        A.CallTo(() => secondStore.GetService(typeof(IProjectionDestinationScope))).Returns(identity);
        var release = Signal();
        A.CallTo(() => firstStore.GetByIdAsync("first", A<CancellationToken>._)).ReturnsLazily(async () =>
        {
            await release.Task;
            return (Row?)new Row();
        });
        var first = ProjectionStoreOperations<Row>.Legacy(firstStore).WithCapacity(firstScope.ServiceProvider);
        var second = ProjectionStoreOperations<Row>.Legacy(secondStore).WithCapacity(secondScope.ServiceProvider);
        var reading = first.GetByIdAsync("first", CancellationToken.None);
        var waiting = second.GetByIdAsync("second", CancellationToken.None);
        waiting.IsCompleted.ShouldBeFalse();
        A.CallTo(() => secondStore.GetByIdAsync("second", A<CancellationToken>._)).MustNotHaveHappened();
        release.SetResult();
        await Task.WhenAll(reading, waiting).WaitAsync(TimeSpan.FromSeconds(5));
        A.CallTo(() => secondStore.GetByIdAsync("second", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task BoundCombinedReadsAndWritesAcrossDifferentResources()
    {
        using var capacity = new ProjectionCapacity(Options.Create(new ProjectionCapacityOptions { MaxConcurrentOperations = 1 }));
        var release = Signal();
        var first = capacity.ExecuteAsync<int>("first", ProjectionCapacityKind.Read, async _ =>
        {
            await release.Task;
            return 1;
        }, CancellationToken.None).AsTask();
        var second = capacity.ExecuteAsync("second", ProjectionCapacityKind.Write, _ => ValueTask.FromResult(2), CancellationToken.None).AsTask();
        second.IsCompleted.ShouldBeFalse();
        release.SetResult();
        await first;
        (await second.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(2);
    }

    [Theory]
    [InlineData(null, " ")]
    [InlineData("inmemory/first-store", "inmemory/second-store")]
    public async Task ShareConservativeBuckets(string? firstResource, string? secondResource)
    {
        using var capacity = Create();
        var release = Signal();
        var first = capacity.ExecuteAsync<int>(firstResource, ProjectionCapacityKind.Read, async _ =>
        {
            await release.Task;
            return 1;
        }, CancellationToken.None).AsTask();
        var second = capacity.ExecuteAsync(secondResource, ProjectionCapacityKind.Read, _ => ValueTask.FromResult(2), CancellationToken.None).AsTask();
        second.IsCompleted.ShouldBeFalse();
        release.SetResult();
        await first;
        (await second.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(2);
    }

    [Fact]
    public async Task NeverAdvanceCheckpointWhenAdmissionIsRejected()
    {
        using var capacity = Create(queueLimit: 0);
        var release = Signal();
        var first = capacity.ExecuteAsync<int>("source", ProjectionCapacityKind.Write, async _ =>
        {
            await release.Task;
            return 1;
        }, CancellationToken.None).AsTask();
        var inner = A.Fake<Excalibur.EventSourcing.Subscriptions.ISubscriptionCheckpointStore>(fake => fake.Implements<IProjectionDestinationScope>());
        A.CallTo(() => ((IProjectionDestinationScope)inner).Scope).Returns("source");
        using var provider = new ServiceCollection().AddSingleton(capacity).BuildServiceProvider();
        var checkpoints = ProjectionCapacitySource.Checkpoints(provider, inner);
        await Should.ThrowAsync<InvalidOperationException>(() => checkpoints.AdvanceCheckpointAsync("subscription", "projection",
            Guid.NewGuid(), 1, 2, CancellationToken.None));
        A.CallTo(() => inner.AdvanceCheckpointAsync(A<string>._, A<string>._, A<Guid>._, A<long?>._, A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
        release.SetResult();
        await first;
    }

    [Theory]
    [InlineData("database")]
    [InlineData("another-database")]
    public async Task RejectNestedRebuildWithoutLeakingItsPermit(string nestedResource)
    {
        using var capacity = Create();
        await Should.ThrowAsync<InvalidOperationException>(() => capacity.ExecuteAsync("database", ProjectionCapacityKind.Rebuild,
            _ => capacity.ExecuteAsync(nestedResource, ProjectionCapacityKind.Rebuild, _ => ValueTask.FromResult(1), CancellationToken.None),
            CancellationToken.None).AsTask());
        (await capacity.ExecuteAsync("database", ProjectionCapacityKind.Rebuild, _ => ValueTask.FromResult(2), CancellationToken.None)).ShouldBe(2);
    }

    [Fact]
    public async Task PreservePermitsAndOriginalFailuresWhenMetricListenersThrow()
    {
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == "Excalibur.EventSourcing.ProjectionCapacity") { owner.EnableMeasurementEvents(instrument); }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => throw new InvalidOperationException("broken listener"));
        listener.SetMeasurementEventCallback<double>((_, _, _, _) => throw new InvalidOperationException("broken listener"));
        listener.Start();
        using var capacity = Create();
        var original = new IOException("storage failed");
        var observed = await Should.ThrowAsync<IOException>(() => capacity.ExecuteAsync<int>("database", ProjectionCapacityKind.Write,
            _ => ValueTask.FromException<int>(original), CancellationToken.None).AsTask());
        observed.ShouldBeSameAs(original);
        (await capacity.ExecuteAsync("database", ProjectionCapacityKind.Write, _ => ValueTask.FromResult(2), CancellationToken.None)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(2);
    }

    [Fact]
    public async Task KeepExecutingPermitAfterCancellationUntilIoActuallyCompletes()
    {
        using var capacity = Create();
        using var cancellation = new CancellationTokenSource();
        var entered = Signal();
        var release = Signal();
        var first = capacity.ExecuteAsync("database", ProjectionCapacityKind.Write, async _ =>
        {
            entered.SetResult();
            await release.Task;
            return 1;
        }, cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var secondEntered = false;
        var second = capacity.ExecuteAsync("database", ProjectionCapacityKind.Write, _ =>
        {
            secondEntered = true;
            return ValueTask.FromResult(2);
        }, CancellationToken.None).AsTask();
        second.IsCompleted.ShouldBeFalse();
        secondEntered.ShouldBeFalse();
        release.SetResult();
        (await first).ShouldBe(1);
        (await second.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(2);
    }

    [Theory]
    [InlineData("database")]
    [InlineData("another-database")]
    public async Task RejectNestedStorageWithoutLeakingCapacity(string nestedResource)
    {
        using var capacity = Create();
        await Should.ThrowAsync<InvalidOperationException>(() => capacity.ExecuteAsync("database", ProjectionCapacityKind.Read,
            _ => capacity.ExecuteAsync(nestedResource, ProjectionCapacityKind.Write, _ => ValueTask.FromResult(1), CancellationToken.None),
            CancellationToken.None).AsTask());
        (await capacity.ExecuteAsync("database", ProjectionCapacityKind.Read, _ => ValueTask.FromResult(2), CancellationToken.None)).ShouldBe(2);
    }

    [Fact]
    public async Task KeepResourcesIndependentAndRebuildAdmissionSeparateFromStorage()
    {
        using var capacity = Create();
        var entered = Signal();
        var release = Signal();
        var first = capacity.ExecuteAsync("first", ProjectionCapacityKind.Read, async _ =>
        {
            entered.SetResult();
            await release.Task;
            return 1;
        }, CancellationToken.None).AsTask();
        await entered.Task;
        (await capacity.ExecuteAsync("second", ProjectionCapacityKind.Rebuild,
            token => capacity.ExecuteAsync("second", ProjectionCapacityKind.Read, _ => ValueTask.FromResult(2), token),
            CancellationToken.None)).ShouldBe(2);
        release.SetResult();
        await first;
    }

    [Fact]
    public async Task CancelQueuedWorkWithoutAdmittingOrReleasingExecutingWork()
    {
        using var capacity = Create();
        using var cancellation = new CancellationTokenSource();
        var release = Signal();
        var first = capacity.ExecuteAsync<int>(null, ProjectionCapacityKind.Read, async _ =>
        {
            await release.Task;
            return 1;
        }, CancellationToken.None).AsTask();
        var called = false;
        var queued = capacity.ExecuteAsync(null, ProjectionCapacityKind.Read, _ =>
        {
            called = true;
            return ValueTask.FromResult(2);
        }, cancellation.Token).AsTask();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => queued);
        called.ShouldBeFalse();
        var next = capacity.ExecuteAsync(null, ProjectionCapacityKind.Read, _ => ValueTask.FromResult(3), CancellationToken.None).AsTask();
        next.IsCompleted.ShouldBeFalse();
        release.SetResult();
        await first;
        (await next.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(3);
    }

    [Fact]
    public async Task RejectOverflowAndWakeQueuedWorkOnShutdown()
    {
        using var capacity = Create(queueLimit: 1);
        var release = Signal();
        var first = capacity.ExecuteAsync<int>(null, ProjectionCapacityKind.Read, async _ =>
        {
            await release.Task;
            return 1;
        }, CancellationToken.None).AsTask();
        var queued = capacity.ExecuteAsync(null, ProjectionCapacityKind.Read, _ => ValueTask.FromResult(2), CancellationToken.None).AsTask();
        await Should.ThrowAsync<InvalidOperationException>(() => capacity.ExecuteAsync(null, ProjectionCapacityKind.Read,
            _ => ValueTask.FromResult(3), CancellationToken.None).AsTask());
        capacity.Dispose();
        await Should.ThrowAsync<InvalidOperationException>(() => queued);
        await Should.ThrowAsync<ObjectDisposedException>(() => capacity.ExecuteAsync(null, ProjectionCapacityKind.Read,
            _ => ValueTask.FromResult(4), CancellationToken.None).AsTask());
        release.SetResult();
        (await first).ShouldBe(1);
    }

    [Fact]
    public async Task AllowChildCapturedContextAfterParentOperationHasEnded()
    {
        using var capacity = Create();
        var releaseChild = Signal();
        Task<int>? child = null;
        await capacity.ExecuteAsync("database", ProjectionCapacityKind.Read, _ =>
        {
            child = Task.Run(async () =>
            {
                await releaseChild.Task;
                return await capacity.ExecuteAsync("database", ProjectionCapacityKind.Write,
                    _ => ValueTask.FromResult(42), CancellationToken.None);
            });
            return ValueTask.FromResult(1);
        }, CancellationToken.None);
        releaseChild.SetResult();
        (await child!.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBe(42);
    }

    [Fact]
    public async Task FreezeOptionsAndRefuseResourceEviction()
    {
        var options = new ProjectionCapacityOptions { MaxResources = 1 };
        using var capacity = new ProjectionCapacity(Options.Create(options));
        options.MaxResources = 2;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => capacity.ExecuteAsync("cancelled", ProjectionCapacityKind.Read,
            _ => ValueTask.FromResult(0), cancellation.Token).AsTask());
        await capacity.ExecuteAsync("first", ProjectionCapacityKind.Read, _ => ValueTask.FromResult(1), CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(() => capacity.ExecuteAsync("second", ProjectionCapacityKind.Read,
            _ => ValueTask.FromResult(2), CancellationToken.None).AsTask());
        (await capacity.ExecuteAsync("first", ProjectionCapacityKind.Read, _ => ValueTask.FromResult(3), CancellationToken.None)).ShouldBe(3);
    }

    private static ProjectionCapacity Create(int queueLimit = 8) => new(Options.Create(new ProjectionCapacityOptions
    {
        MaxConcurrentReads = 1, MaxConcurrentWrites = 1, MaxConcurrentRebuilds = 1, QueueLimit = queueLimit,
    }));

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

}

public sealed class ProjectionCapacityTestRow;
