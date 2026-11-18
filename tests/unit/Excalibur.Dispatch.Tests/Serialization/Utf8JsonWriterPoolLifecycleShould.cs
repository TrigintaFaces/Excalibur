// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Buffers;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Serialization")]
[Trait("Pattern", "Concurrency")]
public sealed class Utf8JsonWriterPoolLifecycleShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveIncompatibleGlobalWriterWhenHashBucketCollides(bool includeCompatibleWriter)
    {
        using var pool = new Utf8JsonWriterPool(maxPoolSize: 2, threadLocalCacheSize: 0, enableAdaptiveSizing: false);
        var compact = new JsonWriterOptions { Indented = false };
        var indented = new JsonWriterOptions { Indented = true };
        var incompatible = pool.Rent(new ArrayBufferWriter<byte>(), compact);
        pool.ReturnToPool(incompatible);
        var compatible = pool.Rent(new ArrayBufferWriter<byte>(), indented);
        var normalized = compatible.Options;
        if (includeCompatibleWriter) pool.ReturnToPool(compatible);
        else compatible.Dispose();

        // At quiescence, inject the state a hash collision produces. This avoids
        // relying on randomized HashCode seeds or a probabilistic collision search.
        var field = typeof(Utf8JsonWriterPool).GetField("_globalPool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var buckets = (System.Collections.Concurrent.ConcurrentDictionary<int, System.Collections.Concurrent.ConcurrentQueue<Utf8JsonWriter>>)field.GetValue(pool)!;
        var hash = typeof(Utf8JsonWriterPool).GetMethod("GetOptionsHashCode", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var compactKey = (int)hash.Invoke(null, [incompatible.Options])!;
        var requestedKey = (int)hash.Invoke(null, [normalized])!;
        buckets.TryRemove(compactKey, out var collision).ShouldBeTrue();
        collision.ShouldNotBeNull();
        if (includeCompatibleWriter && compactKey != requestedKey)
        {
            buckets.TryRemove(requestedKey, out var matches).ShouldBeTrue();
            matches.ShouldNotBeNull();
            matches.TryDequeue(out var matchingWriter).ShouldBeTrue();
            collision.Enqueue(matchingWriter!);
        }
        buckets[requestedKey] = collision;

        var buffer = new ArrayBufferWriter<byte>();
        var rented = pool.Rent(buffer, indented);
        try
        {
            rented.ShouldNotBeSameAs(incompatible);
            rented.Options.Indented.ShouldBeTrue();
            if (includeCompatibleWriter) rented.ShouldBeSameAs(compatible);
            rented.WriteStartObject();
            rented.WriteNumber("value", 42);
            rented.WriteEndObject();
            rented.Flush();
            System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan).ShouldContain(Environment.NewLine);
            pool.Count.ShouldBe(1);
            collision.Count.ShouldBe(1);
            collision.TryPeek(out var retained).ShouldBeTrue();
            retained.ShouldBeSameAs(incompatible);
        }
        finally
        {
            pool.ReturnToPool(rented);
        }

        // Restore each synthetic entry to its real bucket, then hold both
        // leases simultaneously: neither may be issued twice or disposed early.
        buckets.TryRemove(requestedKey, out _).ShouldBeTrue();
        var compactQueue = new System.Collections.Concurrent.ConcurrentQueue<Utf8JsonWriter>();
        var indentedQueue = compactKey == requestedKey
            ? compactQueue
            : new System.Collections.Concurrent.ConcurrentQueue<Utf8JsonWriter>();
        while (collision.TryDequeue(out var cached))
        {
            (cached.Options.Indented ? indentedQueue : compactQueue).Enqueue(cached);
        }
        buckets[compactKey] = compactQueue;
        buckets[requestedKey] = indentedQueue;
        var compactLease = pool.Rent(new ArrayBufferWriter<byte>(), compact);
        var indentedLease = pool.Rent(new ArrayBufferWriter<byte>(), indented);
        compactLease.ShouldBeSameAs(incompatible);
        indentedLease.ShouldBeSameAs(rented);
        compactLease.ShouldNotBeSameAs(indentedLease);
        pool.Count.ShouldBe(0);
        pool.ReturnToPool(compactLease);
        pool.ReturnToPool(indentedLease);
        pool.Count.ShouldBe(2);
        pool.Clear();
        pool.Count.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => incompatible.Reset());
        Should.Throw<ObjectDisposedException>(() => rented.Reset());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReleaseWorkerOnlyCachedWritersFromUntouchedCleanupThread(bool dispose)
    {
        using var pool = new Utf8JsonWriterPool(enableAdaptiveSizing: false);
        Utf8JsonWriter? returned = null;
        Exception? workerError = null;
        var worker = new Thread(() =>
        {
            try
            {
                returned = pool.Rent(new ArrayBufferWriter<byte>());
                pool.ReturnToPool(returned);
            }
            catch (Exception error)
            {
                workerError = error;
            }
        });
        worker.Start();
        worker.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        workerError.ShouldBeNull();
        returned.ShouldNotBeNull();

        if (dispose)
        {
            pool.Dispose();
        }
        else
        {
            pool.Clear();
        }

        Should.Throw<ObjectDisposedException>(() => returned.Reset());
        GC.KeepAlive(pool);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveOutstandingLeaseAndDisposeItsLateReturn(bool dispose)
    {
        using var pool = new Utf8JsonWriterPool(enableAdaptiveSizing: false);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = pool.Rent(buffer);
        if (dispose) pool.Dispose();
        else pool.Clear();
        writer.WriteNumberValue(42);
        writer.Flush();
        System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan).ShouldBe("42");
        pool.ReturnToPool(writer);
        if (dispose)
        {
            Should.Throw<ObjectDisposedException>(() => writer.Reset());
            Should.Throw<ObjectDisposedException>(() => pool.Rent(new ArrayBufferWriter<byte>()));
            Should.Throw<ObjectDisposedException>(() => pool.PreWarm(1));
        }
        else
        {
            var rented = pool.Rent(new ArrayBufferWriter<byte>());
            rented.ShouldBeSameAs(writer);
            pool.ReturnToPool(rented);
        }
    }

    [Fact]
    public void AllowDisposalWhileReturnFlushesCallerBuffer()
    {
        using var pool = new Utf8JsonWriterPool(enableAdaptiveSizing: false);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var buffer = new BlockingAdvanceBuffer(entered, release);
        var writer = pool.Rent(buffer);
        writer.WriteNumberValue(42);
        Exception? error = null;
        var returning = new Thread(() =>
        {
            try { pool.ReturnToPool(writer); }
            catch (Exception exception) { error = exception; }
        });
        returning.Start();
        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            // Disposal must not await a caller-controlled Flush or dispose its active writer.
            pool.Dispose();
        }
        finally
        {
            release.Set();
            returning.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        }
        error.ShouldBeNull();
        buffer.WrittenText.ShouldBe("42");
        Should.Throw<ObjectDisposedException>(() => writer.Reset());
    }

    [Fact]
    public void DrainConcurrentRentPrewarmAndRepeatedDispose()
    {
        using var pool = new Utf8JsonWriterPool(enableAdaptiveSizing: true);
        using var start = new Barrier(5);
        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var workers = Enumerable.Range(0, 4).Select(index => new Thread(() =>
        {
            try
            {
                start.SignalAndWait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                for (var iteration = 0; iteration < 200; iteration++)
                {
                    if (index <= 1)
                    {
                        pool.Dispose();
                        continue;
                    }
                    Utf8JsonWriter writer;
                    try
                    {
                        if (index == 2) pool.PreWarmWithStrategy(4, PreWarmStrategy.Balanced);
                        writer = pool.Rent(new ArrayBufferWriter<byte>());
                    }
                    catch (ObjectDisposedException) { break; }
                    writer.WriteNumberValue(iteration);
                    pool.ReturnToPool(writer);
                }
            }
            catch (Exception exception) { failures.Enqueue(exception); }
        })).ToArray();
        foreach (var worker in workers) worker.Start();
        start.SignalAndWait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        foreach (var worker in workers) worker.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        failures.ShouldBeEmpty();
        pool.Dispose();
        pool.Count.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => pool.Rent(new ArrayBufferWriter<byte>()));
    }


    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void PreserveRentalOwnershipAcrossReentrantListener(bool disposePool, bool throwFromListener)
    {
        using var meter = new Meter("WriterLifecycle.Rent");
        using var pool = new Utf8JsonWriterPool(enableAdaptiveSizing: false, meter: meter);
        var knownWriter = pool.Rent(new ArrayBufferWriter<byte>());
        pool.ReturnToPool(knownWriter);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (ReferenceEquals(instrument.Meter, meter) && instrument.Name == "jsonwriter.pool.rent.threadlocal")
                observer.EnableMeasurementEvents(instrument);
        };
        var expected = new InvalidOperationException("Listener failure");
        var calls = 0;
        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            calls++;
            if (disposePool) pool.Dispose();
            if (throwFromListener) throw expected;
        });
        listener.Start();
        var buffer = new ArrayBufferWriter<byte>();
        if (throwFromListener)
        {
            Should.Throw<InvalidOperationException>(() => pool.Rent(buffer)).ShouldBeSameAs(expected);
            Should.Throw<ObjectDisposedException>(() => knownWriter.Reset());
        }
        else
        {
            var issued = pool.Rent(buffer);
            issued.ShouldBeSameAs(knownWriter);
            issued.WriteNumberValue(42);
            pool.ReturnToPool(issued);
            System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan).ShouldBe("42");
            Should.Throw<ObjectDisposedException>(() => issued.Reset());
        }
        calls.ShouldBe(1);
    }

    [Fact]
    public void PreserveRerentedWriterWhenReturnListenerThrows()
    {
        using var meter = new Meter("WriterLifecycle.Return");
        using var pool = new Utf8JsonWriterPool(enableAdaptiveSizing: false, meter: meter);
        var original = pool.Rent(new ArrayBufferWriter<byte>());
        var nextBuffer = new ArrayBufferWriter<byte>();
        Utf8JsonWriter? rerented = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (ReferenceEquals(instrument.Meter, meter) && instrument.Name == "jsonwriter.pool.return.threadlocal")
                observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            rerented = pool.Rent(nextBuffer);
            throw new InvalidOperationException("Return listener failure after ownership transfer");
        });
        listener.Start();
        pool.ReturnToPool(original);
        listener.Dispose();
        rerented.ShouldBeSameAs(original);
        rerented.WriteNumberValue(42);
        pool.ReturnToPool(rerented);
        System.Text.Encoding.UTF8.GetString(nextBuffer.WrittenSpan).ShouldBe("42");
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReleaseVacatedCacheReferences(bool rentAgain)
    {
        using var pool = new Utf8JsonWriterPool(enableAdaptiveSizing: false);
        var weak = LeaveVacatedSlot(pool, rentAgain);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        weak.IsAlive.ShouldBeFalse();
        GC.KeepAlive(pool);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference LeaveVacatedSlot(Utf8JsonWriterPool pool, bool rentAgain)
    {
        var writer = pool.Rent(new ArrayBufferWriter<byte>());
        pool.ReturnToPool(writer);
        var weak = new WeakReference(writer);
        if (rentAgain)
        {
            var issued = pool.Rent(new ArrayBufferWriter<byte>());
            issued.ShouldBeSameAs(writer);
            // A caller may dispose its own lease instead of returning it.
            issued.Dispose();
        }
        else pool.Clear();
        return weak;
    }


    [Fact]
    public void IgnoreSizingCallbackQueuedBeforeDisposal()
    {
        using var pool = new Utf8JsonWriterPool(maxPoolSize: 64, enableAdaptiveSizing: true);
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var ownershipLock = (Lock)typeof(Utf8JsonWriterPool).GetField("_lifecycleLock", flags)!.GetValue(pool)!;
        var callback = typeof(Utf8JsonWriterPool).GetMethod("AdaptiveSizeCheck", flags)!;
        using var queued = new ManualResetEventSlim();
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            queued.Set();
            try { callback.Invoke(pool, new object?[] { null }); }
            catch (Exception error) { failure = error; }
        });
        lock (ownershipLock)
        {
            worker.Start();
            queued.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            pool.Dispose();
        }
        worker.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        failure.ShouldBeNull();
        pool.MaxPoolSize.ShouldBe(64);
        pool.GetStatistics().PoolContractions.ShouldBe(0);
        pool.Count.ShouldBe(0);
    }

    [Fact]
    public void PreserveExactCountersAfterConcurrentRentalsAndStatisticsReads()
    {
        using var pool = new Utf8JsonWriterPool(enableAdaptiveSizing: false);
        using var start = new Barrier(5);
        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var workers = Enumerable.Range(0, 4).Select(_ => new Thread(() =>
        {
            try
            {
                start.SignalAndWait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                for (var i = 0; i < 1000; i++)
                {
                    var writer = pool.Rent(new ArrayBufferWriter<byte>());
                    pool.ReturnToPool(writer);
                    var stats = pool.GetStatistics();
                    stats.TotalRented.ShouldBeGreaterThan(0);
                    stats.ThreadLocalHits.ShouldBeGreaterThanOrEqualTo(0);
                    stats.ThreadLocalMisses.ShouldBeGreaterThanOrEqualTo(0);
                }
            }
            catch (Exception error) { failures.Enqueue(error); }
        })).ToArray();
        foreach (var worker in workers) worker.Start();
        start.SignalAndWait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        foreach (var worker in workers) worker.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        failures.ShouldBeEmpty();
        var final = pool.GetStatistics();
        final.TotalRented.ShouldBe(4000);
        final.TotalReturned.ShouldBe(4000);
        final.ThreadLocalMisses.ShouldBe(4);
        final.ThreadLocalHits.ShouldBe(3996);
        final.ActiveWriters.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveCounterWraparound(bool cacheHit)
    {
        using var pool = new Utf8JsonWriterPool(enableAdaptiveSizing: false);
        if (cacheHit) pool.ReturnToPool(pool.Rent(new ArrayBufferWriter<byte>()));
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(Utf8JsonWriterPool).GetField("_totalRented", flags)!.SetValue(pool, long.MaxValue);
        typeof(Utf8JsonWriterPool).GetField(cacheHit ? "_threadLocalHits" : "_threadLocalMisses", flags)!.SetValue(pool, long.MaxValue);
        var writer = pool.Rent(new ArrayBufferWriter<byte>());
        pool.ReturnToPool(writer);
        pool.TotalRented.ShouldBe(long.MinValue);
        var stats = pool.GetStatistics();
        (cacheHit ? stats.ThreadLocalHits : stats.ThreadLocalMisses).ShouldBe(long.MinValue);
    }

    private sealed class BlockingAdvanceBuffer(ManualResetEventSlim entered, ManualResetEventSlim release) : IBufferWriter<byte>
    {
        private readonly ArrayBufferWriter<byte> _inner = new();
        public string WrittenText => System.Text.Encoding.UTF8.GetString(_inner.WrittenSpan);
        public void Advance(int count)
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Flush was not released.");
            _inner.Advance(count);
        }
        public Memory<byte> GetMemory(int sizeHint = 0) => _inner.GetMemory(sizeHint);
        public Span<byte> GetSpan(int sizeHint = 0) => _inner.GetSpan(sizeHint);
    }
}
