// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "EventSourcing")]
public sealed class ProjectionRebuildCapacityShould
{
    [Fact]
    public async Task RefuseUnacknowledgedRebuildBeforeMetadataInitialization()
    {
        var query = A.Fake<IGlobalStreamQuery>();
        var store = A.Fake<IProjectionStore<ProjectionRebuildTestState>>();
        var scope = A.Fake<IAsyncProjectionDestinationScope>();
        A.CallTo(() => store.GetService(typeof(IAsyncProjectionDestinationScope))).Returns(scope);
        using var provider = CreateProvider(query, store, sharedCapacity: true);
        using var sut = new ProjectionRebuildService(provider, A.Fake<IEventSerializer>(),
            Options.Create(new ProjectionRebuildOptions()), NullLogger<ProjectionRebuildService>.Instance);
        await Should.ThrowAsync<InvalidOperationException>(() => sut.RebuildAsync<ProjectionRebuildTestState>(CancellationToken.None));
        A.CallTo(() => scope.GetScopeAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task BoundActiveQueriesAndRetainCapacityWhenQueuedWorkIsCancelled(int maximum)
    {
        var query = A.Fake<IGlobalStreamQuery>();
        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily((GlobalStreamPosition _, int _, CancellationToken ct) => new ValueTask<IReadOnlyList<StoredEvent>>(ReadAsync(ct)));
        async Task<IReadOnlyList<StoredEvent>> ReadAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref entered);
            await release.Task.WaitAsync(ct);
            return [];
        }

        using var provider = CreateProvider(query);
        using var sut = CreateService(provider, maximum);
        using var queuedCancellation = new CancellationTokenSource();
        var first = sut.RebuildAsync<ProjectionRebuildTestState>(TestContext.Current.CancellationToken);
        var second = maximum == 2 ? sut.RebuildAsync<Second>(TestContext.Current.CancellationToken) : Task.CompletedTask;
        entered.ShouldBe(maximum);
        var queued = sut.RebuildAsync<Third>(queuedCancellation.Token);
        queued.IsCompleted.ShouldBeFalse();
        entered.ShouldBe(maximum);
        await Should.ThrowAsync<InvalidOperationException>(() => sut.RebuildAsync<Third>(TestContext.Current.CancellationToken));
        await queuedCancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => queued);
        (await sut.GetStatusAsync<Third>(TestContext.Current.CancellationToken)).State.ShouldBe(ProjectionRebuildState.Failed);
        var retry = sut.RebuildAsync<Third>(TestContext.Current.CancellationToken);
        retry.IsCompleted.ShouldBeFalse();
        entered.ShouldBe(maximum);
        release.SetResult();
        await Task.WhenAll(first, second, retry).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        entered.ShouldBe(maximum + 1);
        (await sut.GetStatusAsync<Third>(TestContext.Current.CancellationToken)).State.ShouldBe(ProjectionRebuildState.Completed);
    }

    [Fact]
    public async Task ReleaseCapacityAfterQueryFailure()
    {
        var query = A.Fake<IGlobalStreamQuery>();
        var calls = 0;
        A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(() => Interlocked.Increment(ref calls) == 1
                ? ValueTask.FromException<IReadOnlyList<StoredEvent>>(new InvalidOperationException("source unavailable"))
                : ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]));
        using var provider = CreateProvider(query);
        using var sut = CreateService(provider, 1);
        await Should.ThrowAsync<InvalidOperationException>(() => sut.RebuildAsync<ProjectionRebuildTestState>(TestContext.Current.CancellationToken));
        await sut.RebuildAsync<Second>(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        (await sut.GetStatusAsync<Second>(TestContext.Current.CancellationToken)).State.ShouldBe(ProjectionRebuildState.Completed);
    }

    [Fact]
    public async Task NeverReportCompletedWhenCancelledDuringEmptyRead()
    {
        using var cancellation = new CancellationTokenSource();
        var query = A.Fake<IGlobalStreamQuery>();
        A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                cancellation.Cancel();
                return ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);
            });
        using var provider = CreateProvider(query);
        using var sut = CreateService(provider, 1);
        await Should.ThrowAsync<OperationCanceledException>(() => sut.RebuildAsync<ProjectionRebuildTestState>(cancellation.Token));
        (await sut.GetStatusAsync<ProjectionRebuildTestState>(TestContext.Current.CancellationToken)).State.ShouldBe(ProjectionRebuildState.Failed);
        await sut.RebuildAsync<Second>(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HoldCapacityThroughFlushAndReleaseItAfterFailureOrCancellation(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var flushEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var flushRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = A.Fake<IProjectionStore<ProjectionRebuildTestState>>();
        var positioned = A.Fake<IPositionedProjectionStore<ProjectionRebuildTestState>>();
        A.CallTo(() => store.GetService(typeof(IProjectionTenantBoundary))).Returns(ProjectionTenantBoundary.EstateWide);
        A.CallTo(() => store.GetService(typeof(IPositionedProjectionStore<ProjectionRebuildTestState>))).Returns(positioned);
        A.CallTo(() => positioned.GetWithPositionAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<(ProjectionRebuildTestState?, ProjectionPosition)>((null, ProjectionPosition.Unplaceable)));
        A.CallTo(() => positioned.UpsertAtPositionAsync(A<string>._, A<ProjectionRebuildTestState>._, A<long?>._, A<long>._, A<CancellationToken>._))
            .ReturnsLazily((string _, ProjectionRebuildTestState _, long? _, long _, CancellationToken ct) => WriteAsync(ct));
        async Task<ProjectionAdvanceResult> WriteAsync(CancellationToken ct)
        {
            flushEntered.SetResult();
            await flushRelease.Task.WaitAsync(ct);
            throw new InvalidOperationException("destination unavailable");
        }
        var calls = 0;
        var query = A.Fake<IGlobalStreamQuery>();
        IReadOnlyList<StoredEvent> batch = [new StoredEvent("event", "key", "Aggregate", "Counted",
            [1], null, 1, DateTimeOffset.UnixEpoch) { GlobalPosition = 1 }];
        A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromResult(Interlocked.Increment(ref calls) == 1 ? batch : Array.Empty<StoredEvent>()));
        var serializer = A.Fake<IEventSerializer>();
        A.CallTo(() => serializer.ResolveType("Counted")).Returns(typeof(Counted));
        A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, typeof(Counted))).Returns(new Counted());
        using var provider = CreateProvider(query, store);
        provider.GetRequiredService<MultiStreamProjection<ProjectionRebuildTestState>>().AddHandler<Counted>(static (_, _) => { });
        using var sut = CreateService(provider, 1, serializer);
        var first = sut.RebuildAsync<ProjectionRebuildTestState>(cancellation.Token);
        await Task.WhenAny(first, flushEntered.Task).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        if (first.IsCompleted) { await first; }
        flushEntered.Task.IsCompleted.ShouldBeTrue();
        calls.ShouldBe(2);
        var second = sut.RebuildAsync<Second>(TestContext.Current.CancellationToken);
        second.IsCompleted.ShouldBeFalse();
        calls.ShouldBe(2);
        if (cancel)
        {
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => first);
        }
        else
        {
            flushRelease.SetResult();
            await Should.ThrowAsync<InvalidOperationException>(() => first);
        }
        await second.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        calls.ShouldBe(3);
        (await sut.GetStatusAsync<Second>(TestContext.Current.CancellationToken)).State.ShouldBe(ProjectionRebuildState.Completed);
    }

    [Fact]
    public async Task KeepDistinctTypesWithTheSameShortNameIndependent()
    {
        var query = A.Fake<IGlobalStreamQuery>();
        var release = new TaskCompletionSource<IReadOnlyList<StoredEvent>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(() => new ValueTask<IReadOnlyList<StoredEvent>>(release.Task));
        using var provider = CreateProvider(query);
        using var sut = CreateService(provider, 2);
        var first = sut.RebuildAsync<One.SameName>(TestContext.Current.CancellationToken);
        (await sut.GetStatusAsync<Two.SameName>(TestContext.Current.CancellationToken)).State.ShouldBe(ProjectionRebuildState.Idle);
        var second = sut.RebuildAsync<Two.SameName>(TestContext.Current.CancellationToken);
        second.IsCompleted.ShouldBeFalse();
        release.SetResult([]);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        (await sut.GetAllStatusesAsync(TestContext.Current.CancellationToken)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task KeepDuplicateRebuildRejectionAndQueuedCancellationStatusWithSharedCapacity()
    {
        var query = A.Fake<IGlobalStreamQuery>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IReadOnlyList<StoredEvent>>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
            .ReturnsLazily(() => { entered.TrySetResult(); return new ValueTask<IReadOnlyList<StoredEvent>>(release.Task); });
        using var provider = CreateProvider(query, sharedCapacity: true);
        using var sut = CreateService(provider, 2);
        var first = sut.RebuildAsync<ProjectionRebuildTestState>(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Should.ThrowAsync<InvalidOperationException>(() => sut.RebuildAsync<ProjectionRebuildTestState>(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5)));
        using var cancellation = new CancellationTokenSource();
        var second = sut.RebuildAsync<Second>(cancellation.Token);
        (await sut.GetStatusAsync<Second>(CancellationToken.None)).State.ShouldBe(ProjectionRebuildState.Rebuilding);
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => second);
        (await sut.GetStatusAsync<Second>(CancellationToken.None)).State.ShouldBe(ProjectionRebuildState.Failed);
        release.SetResult([]);
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.RebuildAsync<Second>(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        (await sut.GetStatusAsync<Second>(CancellationToken.None)).State.ShouldBe(ProjectionRebuildState.Completed);
    }

    private static ServiceProvider CreateProvider(IGlobalStreamQuery query, IProjectionStore<ProjectionRebuildTestState>? firstStore = null, bool sharedCapacity = false)
    {
        var services = new ServiceCollection();
        if (sharedCapacity) { services.AddProjectionRebuild(); }
        services.AddSingleton(query);
        services.AddSingleton<ISubscriptionCheckpointStore, InMemorySubscriptionCheckpointStore>();
        services.AddSingleton<IOptions<GlobalStreamProjectionOptions>>(Options.Create(new GlobalStreamProjectionOptions()));
        AddProjection<ProjectionRebuildTestState>(services);
        AddProjection<Second>(services);
        AddProjection<Third>(services);
        AddProjection<One.SameName>(services);
        AddProjection<Two.SameName>(services);
        if (firstStore is not null) { services.AddSingleton(firstStore); }
        return services.BuildServiceProvider();
    }

    private static void AddProjection<T>(IServiceCollection services) where T : class, new()
    {
        services.AddSingleton(new MultiStreamProjection<T>());
        services.AddSingleton<IProjectionStore<T>>(new InMemoryProjectionStore<T>());
    }

    private static ProjectionRebuildService CreateService(IServiceProvider provider, int maximum, IEventSerializer? serializer = null) =>
        new(provider, serializer ?? A.Fake<IEventSerializer>(), Options.Create(new ProjectionRebuildOptions
        {
            OfflineRebuildAcknowledged = true,
            MaxParallelism = maximum,
        }), NullLogger<ProjectionRebuildService>.Instance);

    [MessageName("Test.RebuildCapacity.Counted")]
    private sealed record Counted : DomainEvent;

    internal static class One { internal sealed class SameName; }
    internal static class Two { internal sealed class SameName; }
    internal sealed class Second;
    internal sealed class Third;
}
