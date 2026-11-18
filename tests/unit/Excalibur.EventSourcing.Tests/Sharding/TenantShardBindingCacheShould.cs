// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Sharding;
using Excalibur.EventSourcing.Sharding;
using FakeItEasy;

namespace Excalibur.EventSourcing.Tests.Sharding;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class TenantShardBindingCacheShould
{
    [Fact]
    public async Task RollBackEachPartialFactoryAttemptAndAllowARetry()
    {
        await using var cache = new TenantShardBindingCache();
        var shard = new ShardInfo("physical", "unused");
        var first = new TrackedResource();
        var second = new TrackedResource();
        var original = new InvalidOperationException("store constructor failed");
        var thrown = Should.Throw<InvalidOperationException>(() => cache.Get(shard, _ =>
            ShardReadinessBinding.Construct(stage =>
            {
                stage.Own(first); stage.Own(second); throw original;
            })));
        thrown.ShouldBeSameAs(original);
        first.Disposals.ShouldBe(1); second.Disposals.ShouldBe(1);
        var live = new TrackedResource();
        var store = A.Fake<IEventStore>();
        var binding = cache.Get(shard, _ => ShardReadinessBinding.Construct(stage =>
        { stage.Own(live); return stage.Complete(store, _ => Task.CompletedTask); }));
        cache.Get(shard, _ => throw new InvalidOperationException("cached hit invoked factory")).ShouldBeSameAs(binding);
        await cache.DisposeAsync();
        live.Disposals.ShouldBe(1);
        first.Disposals.ShouldBe(1); second.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task PreserveConstructionAndCleanupCausesWhileReleasingOtherResources()
    {
        var live = new TrackedResource();
        var failure = new InvalidOperationException("creation");
        var cleanup = new IOException("cleanup");
        var aggregate = Should.Throw<AggregateException>(() => ShardReadinessBinding.Construct(stage =>
        {
            stage.Own(live); stage.Own(new ThrowingResource(cleanup)); throw failure;
        }));
        aggregate.InnerExceptions.ShouldContain(failure);
        aggregate.InnerExceptions.ShouldContain(cleanup);
        live.Disposals.ShouldBe(1);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task KeepCachedHitsLockFreeWhileAnotherShardIsBeingConstructed()
    {
        await using var cache = new TenantShardBindingCache();
        var existing = cache.Get(new("cached", "unused"), _ => NewBinding());
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var miss = Task.Run(() => cache.Get(new("slow", "unused"), _ =>
        { entered.Set(); release.Wait(TestContext.Current.CancellationToken); return NewBinding(); }), TestContext.Current.CancellationToken);
        try
        {
            entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ShouldBeTrue();
            var hit = Task.Run(() => cache.Get(new("cached", "unused"), _ => throw new InvalidOperationException()), TestContext.Current.CancellationToken);
            (await hit.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).ShouldBeSameAs(existing);
        }
        finally { release.Set(); await miss; }
    }

    [Fact]
    public async Task DisposeBindingsPublishedByAnAlreadyRunningFactoryAndRejectNewPublication()
    {
        var cache = new TenantShardBindingCache();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var disposeEntered = new ManualResetEventSlim();
        var resource = new TrackedResource();
        var miss = Task.Run(() => cache.Get(new("late", "unused"), _ => ShardReadinessBinding.Construct(stage =>
        {
            stage.Own(resource); entered.Set(); release.Wait(TestContext.Current.CancellationToken);
            return stage.Complete(A.Fake<IEventStore>(), _ => Task.CompletedTask);
        })), TestContext.Current.CancellationToken);
        entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ShouldBeTrue();
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task CompleteDisposalAsync()
        {
            try { await cache.DisposeAsync(); disposed.TrySetResult(); }
            catch (Exception error) { disposed.TrySetException(error); }
        }
        var disposingThread = new Thread(() =>
        {
            disposeEntered.Set();
            _ = CompleteDisposalAsync();
        }) { IsBackground = true };
        disposingThread.Start();
        try
        {
            disposeEntered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ShouldBeTrue();
            // Wait until the actual disposal call blocks on the construction monitor, rather than
            // merely scheduling it. With the old unsynchronized disposal it completes prematurely.
            SpinWait.SpinUntil(() => disposed.Task.IsCompleted ||
                (disposingThread.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(5)).ShouldBeTrue();
            disposed.Task.IsCompleted.ShouldBeFalse();
        }
        finally { release.Set(); }
        await miss;
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        resource.Disposals.ShouldBe(1);
        await cache.DisposeAsync();
        resource.Disposals.ShouldBe(1);
        var called = false;
        Should.Throw<ObjectDisposedException>(() => cache.Get(new("after", "unused"), _ => { called = true; return NewBinding(); }));
        called.ShouldBeFalse();
    }


    [Fact]
    public void ReleaseNonOwningBindingsSynchronouslyAndRejectLaterPublication()
    {
        var cache = new TenantShardBindingCache();
        cache.Get(new("sql", "unused"), _ => NewBinding());
        cache.Dispose();
        cache.Dispose();
        Should.Throw<ObjectDisposedException>(() => cache.Get(new("sql", "unused"), _ => NewBinding()));
    }

    [Fact]
    public async Task RefuseToSilentlyDropOwnedResourcesThroughTheSynchronousNonOwningPath()
    {
        var cache = new TenantShardBindingCache();
        var resource = new TrackedResource();
        cache.Get(new("owned", "unused"), _ => ShardReadinessBinding.Construct(stage =>
        { stage.Own(resource); return stage.Complete(A.Fake<IEventStore>(), _ => Task.CompletedTask); }));
        Should.Throw<InvalidOperationException>(() => cache.Dispose());
        resource.Disposals.ShouldBe(0);
        await cache.DisposeAsync();
        resource.Disposals.ShouldBe(1);
    }

    private static ShardReadinessBinding NewBinding() => ShardReadinessBinding.Construct(stage =>
        stage.Complete(A.Fake<IEventStore>(), _ => Task.CompletedTask));
    private sealed class TrackedResource : IDisposable
    {
        internal int Disposals;
        public void Dispose() => Interlocked.Increment(ref Disposals);
    }
    private sealed class ThrowingResource(Exception error) : IDisposable { public void Dispose() => throw error; }
}
