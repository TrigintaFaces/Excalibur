// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Pattern", "Regression")]
public sealed class DistributedCacheTagTrackerBoundedMemoShould
{
    [Fact]
    public async Task PreserveBackendStampLifetimeWhenMemoIsFull()
    {
        var backend = A.Fake<IDistributedCache>();
        var lifetimes = new List<TimeSpan?>();
        var lifetime = TimeSpan.FromDays(3);
        A.CallTo(() => backend.GetAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult<byte[]?>(null));
        A.CallTo(() => backend.SetAsync(A<string>._, A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
            .Invokes((string _, byte[] _, DistributedCacheEntryOptions options, CancellationToken _) =>
                lifetimes.Add(options.AbsoluteExpirationRelativeToNow)).Returns(Task.CompletedTask);
        var tracker = new DistributedCacheTagTracker(backend,
            MsOptions.Create(new CacheOptions { TagTrackerCapacity = 1, TagStampLifetime = lifetime }));
        for (var i = 0; i < 4; i++)
        {
            await tracker.GetOrCreateStampAsync("read:" + i, CancellationToken.None);
            await tracker.BumpStampAsync("bump:" + i, CancellationToken.None);
        }
        lifetimes.Count.ShouldBe(8);
        lifetimes.ShouldAllBe(value => value == lifetime);
        tracker.MemoizedTagCount.ShouldBe(1);
        A.CallTo(() => backend.RemoveAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => backend.Remove(A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task InvokeClockCallbacksOutsideMemoLock()
    {
        await using var provider = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
        var clock = new CallbackClock();
        var tracker = new DistributedCacheTagTracker(provider.GetRequiredService<IDistributedCache>(),
            MsOptions.Create(new CacheOptions()), clock);
        await tracker.GetOrCreateStampAsync("orders", CancellationToken.None);
        clock.Timestamp = TimeSpan.FromSeconds(6).Ticks;
        var workers = new List<Task>();
        var completions = new List<ManualResetEventSlim>();
        clock.OnFrequency = () =>
        {
            var completion = new ManualResetEventSlim();
            completions.Add(completion);
            var worker = Task.Run(async () =>
            {
                try { await tracker.BumpStampAsync("unrelated", CancellationToken.None); }
                finally { completion.Set(); }
            });
            workers.Add(worker);
            // A callback under the memo lock would prevent the other thread's bump from finishing.
            var finished = completion.Wait(TimeSpan.FromSeconds(5));
            finished.ShouldBeTrue("clock callbacks must not hold the memo lock");
        };
        try { await tracker.GetOrCreateStampAsync("orders", CancellationToken.None); }
        finally
        {
            await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(10));
            foreach (var completion in completions) completion.Dispose();
        }
    }

    private sealed class CallbackClock : TimeProvider
    {
        public long Timestamp { get; set; }
        public Action? OnFrequency { get; set; }
        public override long GetTimestamp() => Timestamp;
        public override long TimestampFrequency
        {
            get { OnFrequency?.Invoke(); return TimeSpan.TicksPerSecond; }
        }
    }

    [Fact]
    public async Task FollowBackendWriteOrderWhenBumpsCompleteInReverseOrder()
    {
        var backend = A.Fake<IDistributedCache>();
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = 0;
        byte[]? stored = null;
        A.CallTo(() => backend.SetAsync(A<string>._, A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
            .ReturnsLazily((string _, byte[] value, DistributedCacheEntryOptions _, CancellationToken _) =>
            {
                stored = value;
                return ++writes == 1 ? releaseFirst.Task : Task.CompletedTask;
            });
        A.CallTo(() => backend.GetAsync(A<string>._, A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult(stored));
        var tracker = new DistributedCacheTagTracker(backend, MsOptions.Create(new CacheOptions()));
        var first = tracker.BumpStampAsync("orders", CancellationToken.None);
        first.IsCompleted.ShouldBeFalse();
        await tracker.BumpStampAsync("orders", CancellationToken.None);
        var latest = System.Text.Encoding.UTF8.GetString(stored!);
        releaseFirst.SetResult();
        await first;
        (await tracker.GetOrCreateStampAsync("orders", CancellationToken.None)).ShouldBe(latest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryAfterSharedResolutionFails(bool cancel)
    {
        var backend = A.Fake<IDistributedCache>();
        var resolution = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => backend.GetAsync(A<string>._, A<CancellationToken>._)).Returns(resolution.Task);
        var tracker = new DistributedCacheTagTracker(backend, MsOptions.Create(new CacheOptions { TagTrackerCapacity = 1 }));
        var first = tracker.GetOrCreateStampAsync("orders", CancellationToken.None);
        var second = tracker.GetOrCreateStampAsync("orders", CancellationToken.None);
        first.ShouldBeSameAs(second);
        if (cancel) resolution.SetCanceled();
        else resolution.SetException(new IOException("controlled backend failure"));
        if (cancel) await Should.ThrowAsync<OperationCanceledException>(() => first);
        else await Should.ThrowAsync<IOException>(() => first);
        A.CallTo(() => backend.GetAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<byte[]?>(System.Text.Encoding.UTF8.GetBytes("recovered")));
        (await tracker.GetOrCreateStampAsync("orders", CancellationToken.None)).ShouldBe("recovered");
        tracker.MemoizedTagCount.ShouldBe(1);
    }

    [Fact]
    public async Task DiscardDelayedResolutionAfterSuccessfulBump()
    {
        var backend = A.Fake<IDistributedCache>();
        var oldRead = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        byte[]? stored = System.Text.Encoding.UTF8.GetBytes("original");
        var reads = 0;
        A.CallTo(() => backend.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => ++reads == 1 ? oldRead.Task : Task.FromResult<byte[]?>(stored));
        A.CallTo(() => backend.SetAsync(A<string>._, A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
            .Invokes((string _, byte[] value, DistributedCacheEntryOptions _, CancellationToken _) => stored = value)
            .Returns(Task.CompletedTask);
        var tracker = new DistributedCacheTagTracker(backend, MsOptions.Create(new CacheOptions()));
        var pending = tracker.GetOrCreateStampAsync("orders", CancellationToken.None);
        await tracker.BumpStampAsync("orders", CancellationToken.None);
        oldRead.SetResult(System.Text.Encoding.UTF8.GetBytes("original"));
        (await pending).ShouldBe("original", "this reader overlapped invalidation");
        (await tracker.GetOrCreateStampAsync("orders", CancellationToken.None))
            .ShouldBe(System.Text.Encoding.UTF8.GetString(stored!));
    }

    [Fact]
    public async Task BoundConcurrentAdmissions()
    {
        await using var provider = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
        var tracker = new DistributedCacheTagTracker(provider.GetRequiredService<IDistributedCache>(),
            MsOptions.Create(new CacheOptions { TagTrackerCapacity = 8 }));
        await Parallel.ForEachAsync(Enumerable.Range(0, 256), new ParallelOptions { MaxDegreeOfParallelism = 16 },
            async (i, token) => { await tracker.GetOrCreateStampAsync("parallel:" + i, token); });
        tracker.MemoizedTagCount.ShouldBeLessThanOrEqualTo(8);
    }

    [Fact]
    public async Task NotRepublishOldResolutionAfterReentrantBump()
    {
        var backend = A.Fake<IDistributedCache>();
        var stored = System.Text.Encoding.UTF8.GetBytes("original");
        DistributedCacheTagTracker? tracker = null;
        var firstRead = true;
        A.CallTo(() => backend.SetAsync(A<string>._, A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
            .Invokes((string _, byte[] bytes, DistributedCacheEntryOptions _, CancellationToken _) => stored = bytes)
            .Returns(Task.CompletedTask);
        A.CallTo(() => backend.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                var snapshot = stored;
                if (firstRead)
                {
                    firstRead = false;
                    tracker!.BumpStampAsync("orders", CancellationToken.None).IsCompletedSuccessfully.ShouldBeTrue();
                }
                return Task.FromResult<byte[]?>(snapshot);
            });
        tracker = new DistributedCacheTagTracker(backend, MsOptions.Create(new CacheOptions()));
        (await tracker.GetOrCreateStampAsync("orders", CancellationToken.None)).ShouldBe("original");
        (await tracker.GetOrCreateStampAsync("orders", CancellationToken.None))
            .ShouldBe(System.Text.Encoding.UTF8.GetString(stored));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundRetainedMemoWithoutDeletingBackendStamps(bool bump)
    {
        await using var provider = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
        var backend = provider.GetRequiredService<IDistributedCache>();
        var tracker = new DistributedCacheTagTracker(backend,
            MsOptions.Create(new CacheOptions { TagTrackerCapacity = 8 }));
        for (var i = 0; i < 128; i++)
        {
            var tag = "bounded:" + i;
            if (bump) await tracker.BumpStampAsync(tag, CancellationToken.None);
            else await tracker.GetOrCreateStampAsync(tag, CancellationToken.None);
        }
        tracker.MemoizedTagCount.ShouldBeLessThanOrEqualTo(8);
        for (var i = 0; i < 128; i++)
        {
            (await backend.GetAsync("dispatch:tagver:bounded:" + i)).ShouldNotBeNull();
        }
    }
}
