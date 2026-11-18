// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Pattern", "Regression")]
public sealed class HybridCacheDirectInvalidationPropagationShould
{
    [Fact]
    public async Task SnapshotDistinctTagsBeforeAwaitingNativeInvalidation()
    {
        var native = A.Fake<HybridCache>();
        var tracker = A.Fake<ICacheTagTracker>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bumped = new List<string>();
        A.CallTo(() => native.RemoveByTagAsync(A<IEnumerable<string>>._, A<CancellationToken>._))
            .Returns(new ValueTask(release.Task));
        A.CallTo(() => tracker.BumpStampAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string tag, CancellationToken _) => bumped.Add(tag)).Returns(Task.CompletedTask);
        var tags = new List<string> { "a", "b", "a" };
        var run = new HybridCacheInvalidationService(native, tracker).InvalidateTagsAsync(tags, CancellationToken.None);
        tags.Clear();
        tags.Add("changed");
        release.SetResult();
        await run;
        bumped.ShouldBe(["a", "b"]);
    }

    [Fact]
    public async Task PreserveNativeFailureWithoutBumpingTags()
    {
        var native = A.Fake<HybridCache>();
        var tracker = A.Fake<ICacheTagTracker>();
        A.CallTo(() => native.RemoveByTagAsync(A<IEnumerable<string>>._, A<CancellationToken>._))
            .ReturnsLazily(() => ValueTask.FromException(new IOException("native")));
        await Should.ThrowAsync<IOException>(() => new HybridCacheInvalidationService(native, tracker)
            .InvalidateTagsAsync(["a"], CancellationToken.None));
        A.CallTo(() => tracker.BumpStampAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PropagateTrackerFailureAndStopFurtherBumps(bool cancel)
    {
        var native = A.Fake<HybridCache>();
        var tracker = A.Fake<ICacheTagTracker>();
        var bumped = new List<string>();
        using var cancellation = new CancellationTokenSource();
        A.CallTo(() => tracker.BumpStampAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string tag, CancellationToken token) =>
            {
                token.ShouldBe(cancellation.Token);
                bumped.Add(tag);
                if (tag != "b") return Task.CompletedTask;
                if (!cancel) return Task.FromException(new IOException("tracker"));
                cancellation.Cancel();
                return Task.FromCanceled(token);
            });
        var run = new HybridCacheInvalidationService(native, tracker).InvalidateTagsAsync(["a", "b", "c"], cancellation.Token);
        if (cancel) await Should.ThrowAsync<OperationCanceledException>(() => run);
        else await Should.ThrowAsync<IOException>(() => run);
        bumped.ShouldBe(["a", "b"]);
    }

    [Fact]
    public async Task PreserveNativeWildcardAndEmptyTagBehavior()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        await using var provider = services.BuildServiceProvider();
        var native = provider.GetRequiredService<HybridCache>();
        var tracker = A.Fake<ICacheTagTracker>();
        await native.SetAsync("key", "before", tags: ["orders"]);
        await new HybridCacheInvalidationService(native, tracker).InvalidateTagsAsync(["*", "", " ", "\t", null!], CancellationToken.None);
        var value = await native.GetOrCreateAsync("key", _ => ValueTask.FromResult("after"), tags: ["orders"]);
        value.ShouldBe("after");
        A.CallTo(() => tracker.BumpStampAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task PreserveEntriesForEmptyTagsWithoutWildcard()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        await using var provider = services.BuildServiceProvider();
        var native = provider.GetRequiredService<HybridCache>();
        var tracker = A.Fake<ICacheTagTracker>();
        await native.SetAsync("key", "before", tags: ["orders"]);
        await new HybridCacheInvalidationService(native, tracker)
            .InvalidateTagsAsync(["", " ", "\t", null!], CancellationToken.None);
        var value = await native.GetOrCreateAsync("key", _ => ValueTask.FromResult("after"), tags: ["orders"]);
        value.ShouldBe("before");
        A.CallTo(() => tracker.BumpStampAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task PreserveNativeInvalidationWhenTrackerIsOmitted()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        await using var provider = services.BuildServiceProvider();
        var native = provider.GetRequiredService<HybridCache>();
        await native.SetAsync("key", "before", tags: ["orders"]);
        await new HybridCacheInvalidationService(native).InvalidateTagsAsync(["orders"], CancellationToken.None);
        var value = await native.GetOrCreateAsync("key", _ => ValueTask.FromResult("after"), tags: ["orders"]);
        value.ShouldBe("after");
    }

    [Fact]
    public async Task PreserveStoredPayloadAndExpirationDuringTagInvalidation()
    {
        await using var backendOwner = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
        var backend = new SharedBackend(backendOwner.GetRequiredService<IDistributedCache>());
        await using var provider = CreateProvider(backend, new FakeTimeProvider());
        var native = provider.GetRequiredService<HybridCache>();
        await native.SetAsync("retention", "before",
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) }, ["orders"]);
        var originalPayload = await backend.GetAsync("retention");
        originalPayload.ShouldNotBeNull();
        var originalExpiration = backend.Expirations["retention"];
        originalExpiration.ShouldBe(TimeSpan.FromMinutes(5));
        await provider.GetRequiredService<ICacheInvalidationService>()
            .InvalidateTagsAsync(["orders"], CancellationToken.None);
        (await backend.GetAsync("retention")).ShouldBe(originalPayload);
        backend.Expirations["retention"].ShouldBe(originalExpiration);
        backend.RemoveCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RefreshWarmedEntriesAfterTagInvalidation(bool otherInstance, bool middlewareInvalidation)
    {
        await using var backendOwner = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
        var backend = new SharedBackend(backendOwner.GetRequiredService<IDistributedCache>());
        var clock = new FakeTimeProvider();
        await using var first = CreateProvider(backend, clock);
        await using var second = CreateProvider(backend, clock);
        first.GetRequiredService<ICacheTagTracker>().ShouldBeOfType<DistributedCacheTagTracker>();
        second.GetRequiredService<ICacheTagTracker>().ShouldBeOfType<DistributedCacheTagTracker>();
        var reader = otherInstance ? second : first;
        var source = "before";
        var executions = 0;
        DispatchRequestDelegate handler = (_, _, _) =>
        {
            executions++;
            return new ValueTask<IMessageResult>(MessageResult.Success(source));
        };
        async Task<object?> Read(string tag)
        {
            var context = A.Fake<IMessageContext>();
            A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
            var result = await reader.GetRequiredService<CachingMiddleware>().InvokeAsync(
                new Query(tag), context, handler, CancellationToken.None);
            return result.UntypedReturnValue;
        }
        (await Read("orders")).ShouldBe("before");
        (await Read("orders")).ShouldBe("before");
        executions.ShouldBe(1, "the positive cache-hit control must hold before invalidation");
        (await Read("unrelated")).ShouldBe("before");
        executions.ShouldBe(2);
        source = "after";
        if (middlewareInvalidation)
        {
            var context = A.Fake<IMessageContext>();
            A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
            await first.GetRequiredService<CacheInvalidationMiddleware>().InvokeAsync(new InvalidateOrders(), context,
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()), CancellationToken.None);
        }
        else
        {
            await first.GetRequiredService<ICacheInvalidationService>().InvalidateTagsAsync(["orders"], CancellationToken.None);
        }
        clock.Advance(TimeSpan.FromSeconds(6));
        (await Read("orders")).ShouldBe("after");
        executions.ShouldBe(3, "the invalidated entry must execute its handler again");
        (await Read("unrelated")).ShouldBe("before");
        executions.ShouldBe(3, "tag invalidation must not flush unrelated entries");
    }

    private static ServiceProvider CreateProvider(IDistributedCache backend, TimeProvider clock)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(backend);
        services.AddSingleton(clock);
        services.AddDispatchCaching(options =>
        {
            options.Enabled = true;
            options.CacheMode = CacheMode.Hybrid;
            options.TagStampRefreshInterval = TimeSpan.FromSeconds(5);
        });
        return services.BuildServiceProvider();
    }

    private sealed class Query(string tag) : ICacheable<string>
    {
        public int ExpirationSeconds => 300;
        public string GetCacheKey() => "propagation:" + tag;
        public string[] GetCacheTags() => [tag];
        public bool ShouldCache(object? result) => true;
    }

    [InvalidateCache(Tags = ["orders"])]
    private sealed class InvalidateOrders : IDispatchAction;

    // Two independently constructed HybridCache instances share this backend, without Redis or a database.
    private sealed class SharedBackend(IDistributedCache inner) : IDistributedCache
    {
        private int _removeCount;
        public int RemoveCount => Volatile.Read(ref _removeCount);
        public System.Collections.Concurrent.ConcurrentDictionary<string, TimeSpan?> Expirations { get; } = new();
        public byte[]? Get(string key) => inner.Get(key);
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => inner.GetAsync(key, token);
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => inner.Set(key, value, options);
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Expirations[key] = options.AbsoluteExpirationRelativeToNow;
            return inner.SetAsync(key, value, options, token);
        }
        public void Refresh(string key) => inner.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
        public void Remove(string key)
        {
            Interlocked.Increment(ref _removeCount);
            inner.Remove(key);
        }
        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Interlocked.Increment(ref _removeCount);
            return inner.RemoveAsync(key, token);
        }
    }
}
