// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.Reflection;

using BenchmarkDotNet.Attributes;

using Excalibur.Benchmarks.Caching;
using Excalibur.Caching.AdaptiveTtl;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.Caching.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Pattern", "BenchmarkRegression")]
public sealed class AdaptiveTtlWriterBenchmarkLifecycleShould
{
    [Theory]
    [InlineData(nameof(AdaptiveTtlBenchmarks.CacheSet), 100)]
    [InlineData(nameof(AdaptiveTtlBenchmarks.CacheSet), 1000)]
    [InlineData(nameof(AdaptiveTtlBenchmarks.SyncSet), 100)]
    [InlineData(nameof(AdaptiveTtlBenchmarks.SyncSet), 1000)]
    public async Task IsolateEachOriginalWriterWithoutEvictingSeededEntries(string method, int cacheSize)
    {
        var fixture = new AdaptiveTtlBenchmarks { CacheSize = cacheSize };
        var setup = typeof(AdaptiveTtlBenchmarks).GetMethod("WriterIterationSetup");
        var cleanup = typeof(AdaptiveTtlBenchmarks).GetMethod("WriterIterationCleanup");
        object? previousCache = null;
        string? previousWriterKey = null;
        fixture.GlobalSetup();
        try
        {
            for (var cycle = 0; cycle < 4; cycle++)
            {
                setup?.Invoke(fixture, null);
                try
                {
                    var inner = (MemoryDistributedCache)Field(fixture, "_innerCache")!;
                    var memory = inner.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                        .Select(field => field.GetValue(inner)).OfType<MemoryCache>().Single();
                    var adaptive = Field(fixture, "_adaptiveCache")!;
                    var metadata = (IDictionary)Field(adaptive, "_metadata")!;
                    memory.Count.ShouldBe(cacheSize);
                    metadata.Count.ShouldBe(0);
                    if (previousCache is not null)
                    {
                        ReferenceEquals(previousCache, inner).ShouldBeFalse();
                        inner.Get(previousWriterKey!).ShouldBeNull();
                    }

                    var payload = ((byte[])Field(fixture, "_testData")!).ToArray();
                    var seedKeys = Enumerable.Range(0, cacheSize).Select(index => $"key-{index}").ToHashSet(StringComparer.Ordinal);
                    memory.Keys.Cast<string>().ToHashSet(StringComparer.Ordinal).SetEquals(seedKeys).ShouldBeTrue();
                    foreach (var key in seedKeys)
                    {
                        inner.Get(key).ShouldBe(payload);
                    }

                    if (method == nameof(AdaptiveTtlBenchmarks.CacheSet))
                    {
                        await fixture.CacheSet();
                    }
                    else
                    {
                        fixture.SyncSet();
                    }

                    memory.Count.ShouldBe(cacheSize + 1);
                    metadata.Count.ShouldBe(1);
                    var writerKey = memory.Keys.Cast<string>().Single(key => !seedKeys.Contains(key));
                    writerKey.ShouldStartWith(method == nameof(AdaptiveTtlBenchmarks.CacheSet) ? "new-key-" : "sync-key-");
                    Guid.TryParseExact(writerKey[(writerKey.LastIndexOf('-') + 1)..], "N", out _).ShouldBeTrue();
                    metadata.Contains(writerKey).ShouldBeTrue();
                    inner.Get(writerKey).ShouldBe(payload);
                    foreach (var key in seedKeys)
                    {
                        inner.Get(key).ShouldBe(payload);
                    }

                    previousCache = inner;
                    previousWriterKey = writerKey;
                }
                finally
                {
                    cleanup?.Invoke(fixture, null);
                }

                if (cleanup is not null)
                {
                    foreach (var field in typeof(AdaptiveTtlBenchmarks).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                        .Where(field => !field.FieldType.IsValueType))
                    {
                        field.GetValue(fixture).ShouldBeNull(field.Name);
                    }

                    cleanup.Invoke(fixture, null);
                    fixture.CacheSize.ShouldBe(cacheSize);
                }
            }
        }
        finally
        {
            fixture.GlobalCleanup();
        }
    }

    [Fact]
    public void TargetOnlyTheTwoUniqueKeyWriters()
    {
        var targets = new[] { nameof(AdaptiveTtlBenchmarks.CacheSet), nameof(AdaptiveTtlBenchmarks.SyncSet) };
        var setup = typeof(AdaptiveTtlBenchmarks).GetMethod("WriterIterationSetup")!;
        var cleanup = typeof(AdaptiveTtlBenchmarks).GetMethod("WriterIterationCleanup")!;
        setup.ReturnType.ShouldBe(typeof(void));
        cleanup.ReturnType.ShouldBe(typeof(void));
        setup.GetCustomAttribute<IterationSetupAttribute>()!.Targets.ShouldBe(targets);
        cleanup.GetCustomAttribute<IterationCleanupAttribute>()!.Targets.ShouldBe(targets);
    }

    [Fact]
    public async Task WaitForOwnedTimerDrainBeforeCleanupReturns()
    {
        var fixture = new AdaptiveTtlBenchmarks { CacheSize = 100 };
        fixture.GlobalSetup();
        var clock = new GatedTimeProvider();
        await ((AdaptiveTtlCache)Field(fixture, "_adaptiveCache")!).DisposeAsync();
        var replacement = new AdaptiveTtlCache(
            (MemoryDistributedCache)Field(fixture, "_innerCache")!,
            (IAdaptiveTtlStrategy)Field(fixture, "_strategy")!,
            NullLogger<AdaptiveTtlCache>.Instance,
            (ISystemLoadMonitor)Field(fixture, "_loadMonitor")!,
            clock);
        typeof(AdaptiveTtlBenchmarks).GetField("_adaptiveCache", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(fixture, replacement);
        var cleanup = Task.Run(fixture.GlobalCleanup);
        try
        {
            await clock.Timer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cleanup.IsCompleted.ShouldBeFalse();
            clock.Timer.SynchronousDisposals.ShouldBe(0);
            Field(fixture, "_adaptiveCache").ShouldBeNull();
            Field(fixture, "_innerCache").ShouldBeNull();
        }
        finally
        {
            clock.Timer.Release.TrySetResult();
            await cleanup.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.GlobalCleanup();
        }
    }

    private static object? Field(object value, string name) =>
        value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);

    private sealed class GatedTimeProvider : TimeProvider
    {
        public GatedTimer Timer { get; } = new();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => Timer;
    }

    private sealed class GatedTimer : ITimer
    {
        private int _synchronousDisposals;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int SynchronousDisposals => Volatile.Read(ref _synchronousDisposals);

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
            Interlocked.Increment(ref _synchronousDisposals);
            Entered.TrySetResult();
        }

        public ValueTask DisposeAsync()
        {
            Entered.TrySetResult();
            return new ValueTask(Release.Task);
        }
    }
}
