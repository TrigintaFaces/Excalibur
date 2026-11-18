// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Harnesses.CacheInvalidation;

internal static class Program
{
    public static async Task Main()
    {
        Check(!RuntimeFeature.IsDynamicCodeSupported, "dynamic code must be disabled; a native result requires running the published native executable");
        await using var owner = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
        var backend = new SharedBackend(owner.GetRequiredService<IDistributedCache>());
        var clock = new ManualClock();
        await using var first = CreateProvider(backend, clock);
        await using var second = CreateProvider(backend, clock);
        var tracker = second.GetRequiredService<ICacheTagTracker>();
        var before = await tracker.GetOrCreateStampAsync("orders", CancellationToken.None);
        Check(before == await tracker.GetOrCreateStampAsync("orders", CancellationToken.None), "warm stamp control");
        var unrelated = await tracker.GetOrCreateStampAsync("unrelated", CancellationToken.None);
        var native = first.GetRequiredService<HybridCache>();
        await native.SetAsync("key", "before", tags: ["orders"]);
        await first.GetRequiredService<ICacheInvalidationService>().InvalidateTagsAsync(["orders"], CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(6));
        Check(before != await tracker.GetOrCreateStampAsync("orders", CancellationToken.None), "cross-instance stamp propagation");
        Check(unrelated == await tracker.GetOrCreateStampAsync("unrelated", CancellationToken.None), "unrelated stamp preserved");
        Check("after" == await native.GetOrCreateAsync("key", _ => ValueTask.FromResult("after"), tags: ["orders"]), "native invalidation preserved");
        Console.WriteLine("PASS: NativeAOT registered direct tag invalidation, warmed remote tracker, unrelated stamp and native cache controls.");
    }

    // This probe exercises registered invalidation and native string caching, never Dispatch's
    // runtime-type result deserialization. It does not establish AOT support for that separate path.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only tag tracking/invalidation and native string caching are exercised; result deserialization is not invoked.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only tag tracking/invalidation and native string caching are exercised; result deserialization is not invoked.")]
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

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }

    private sealed class SharedBackend(IDistributedCache inner) : IDistributedCache
    {
        public byte[]? Get(string key) => inner.Get(key);
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => inner.GetAsync(key, token);
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => inner.Set(key, value, options);
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => inner.SetAsync(key, value, options, token);
        public void Refresh(string key) => inner.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
        public void Remove(string key) => inner.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);
    }
}
