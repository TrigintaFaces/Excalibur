// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// The pool's per-thread <c>WriterCache</c> is NOT thread-safe — a plain <c>int</c> count over an
/// array — and it is nonetheless reachable from a foreign thread, because <c>ClearCore</c> iterates
/// <c>ThreadLocal.Values</c> (constructed with <c>trackAllValues: true</c>) and clears every tracked
/// thread's cache. Its own comment says so: <i>"Values covers every tracked thread, even if this
/// thread never rented."</i>
/// <para>
/// So the structure owes mutual exclusion across <c>TryRent</c>, <c>TryReturn</c> and <c>Clear</c>.
/// These arms bind that. They are STRESS arms rather than interleaving arms because the window sits
/// between two statements inside the cache and no seam exists to pause it — but the window is wide
/// (an <c>Array.Copy</c> whose length goes negative, and a slot read while it is half-written), so an
/// unsynchronised cache reddens these reliably rather than occasionally.
/// </para>
/// <para>
/// WHY THESE EXIST SEPARATELY FROM THE CONTENTION ARMS: those bind where the pool-wide lock is TAKEN,
/// and they stayed green while this invariant was broken. Lock placement and lock sufficiency are
/// different properties, and a suite that measures only the first will certify the second by silence.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Serialization")]
[Trait("Pattern", "Concurrency")]
public sealed class Utf8JsonWriterPoolCacheRaceShould
{
    private const int Workers = 4;
    private static readonly TimeSpan RunFor = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan JoinBudget = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task SurviveForeignThreadClearWhileWorkersRentAndReturn()
    {
        var pool = new Utf8JsonWriterPool(threadLocalCacheSize: 4, enableAdaptiveSizing: false);
        var faults = new ConcurrentQueue<Exception>();
        using var stop = new ManualResetEventSlim(false);

        // Workers churn their OWN thread-local caches: rent, return, repeat. Each is the only thread
        // that calls TryRent/TryReturn on its cache, which is exactly the "per-thread so it needs no
        // synchronisation" reasoning that the cross-thread Clear below falsifies.
        var workerReady = Enumerable.Range(0, Workers)
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var workers = Enumerable.Range(0, Workers).Select(index => new Thread(() =>
        {
            var buffer = new ArrayBufferWriter<byte>();
            var hasSignaled = false;
            try
            {
                while (!stop.IsSet)
                {
                    var writer = pool.Rent(buffer);
                    pool.ReturnToPool(writer);
                    if (!hasSignaled) { workerReady[index].TrySetResult(); hasSignaled = true; }
                }
            }
            catch (Exception ex)
            {
                faults.Enqueue(ex);
                workerReady[index].TrySetException(ex);
            }
        })
        { IsBackground = true }).ToArray();

        // A foreign thread clearing the pool. Clear() is a public, documented operation -- the obvious
        // response to memory pressure -- and it reaches every tracked thread's cache. It is NOT a
        // teardown-only path and it does not latch _disposed, so this window recurs for as long as the
        // process runs.
        var clearReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clearer = new Thread(() =>
        {
            var hasSignaled = false;
            try
            {
                while (!stop.IsSet)
                {
                    pool.Clear();
                    if (!hasSignaled) { clearReady.TrySetResult(); hasSignaled = true; }
                }
            }
            catch (Exception ex)
            {
                faults.Enqueue(ex);
                clearReady.TrySetException(ex);
            }
        })
        { IsBackground = true };

        var started = new List<Thread>();
        var joined = false;
        Exception? primaryFailure = null;
        try
        {
            foreach (var worker in workers) { worker.Start(); started.Add(worker); }
            clearer.Start(); started.Add(clearer);
            await Task.WhenAll(workerReady.Select(ready => ready.Task).Append(clearReady.Task)).WaitAsync(JoinBudget);
            await Task.Delay(RunFor).ConfigureAwait(false); // delay-ok: bounded stress duration begins only after every worker and the clearer performed real operations.
        }
        catch (Exception error)
        {
            primaryFailure = error;
            throw;
        }
        finally
        {
            joined = StopAndJoinWorkers(stop, started, faults);
            DisposePool(pool, faults);
            if (primaryFailure is not null && faults.Any(error => !ReferenceEquals(error, primaryFailure)))
            {
                primaryFailure.Data["PoolWorkerCleanup"] = new AggregateException(faults.Where(error => !ReferenceEquals(error, primaryFailure)));
            }
        }

        joined.ShouldBeTrue("a worker or clearing thread did not finish");

        if (!faults.IsEmpty)
        {
            var kinds = string.Join(", ", faults.Select(f => f.GetType().Name).Distinct());
            var first = faults.First();
            throw new Xunit.Sdk.XunitException(
                "Renting or returning raced a foreign-thread Clear() and threw. The per-thread "
                + "WriterCache is not thread-safe but IS reachable cross-thread through "
                + $"ClearCore's ThreadLocal.Values walk, so every access to it owes mutual exclusion. "
                + $"Exception kinds: {kinds}. First: {first}");
        }
    }

    [Fact]
    public async Task NotThrowFromDisposeWhileWorkersReturnWriters()
    {
        // The same race from the other side, and it breaks a stronger property: Dispose() must not
        // throw. ClearCore runs inside Dispose, and its per-cache loop re-reads _count while a
        // concurrent TryReturn increments it, so it can read a slot that is half-written.
        var faults = new ConcurrentQueue<Exception>();
        using var stop = new ManualResetEventSlim(false);
        var pool = new Utf8JsonWriterPool(threadLocalCacheSize: 4, enableAdaptiveSizing: false);

        var workerReady = Enumerable.Range(0, Workers)
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var workers = Enumerable.Range(0, Workers).Select(index => new Thread(() =>
        {
            var buffer = new ArrayBufferWriter<byte>();
            var hasSignaled = false;
            while (!stop.IsSet)
            {
                try
                {
                    var writer = pool.Rent(buffer);
                    pool.ReturnToPool(writer);
                    if (!hasSignaled) { workerReady[index].TrySetResult(); hasSignaled = true; }
                }
                catch (ObjectDisposedException)
                {
                    workerReady[index].TrySetException(new ObjectDisposedException(nameof(pool)));
                    // Expected once the pool is disposed: renting from a disposed pool is a host
                    // teardown-ordering fault and this is the documented observation for it.
                    break;
                }
                catch (Exception ex)
                {
                    faults.Enqueue(ex);
                    workerReady[index].TrySetException(ex);
                    break;
                }
            }
        })
        { IsBackground = true }).ToArray();

        var started = new List<Thread>();
        var joined = false;
        var disposalAttempted = false;
        Exception? primaryFailure = null;
        try
        {
            foreach (var worker in workers) { worker.Start(); started.Add(worker); }
            await Task.WhenAll(workerReady.Select(ready => ready.Task)).WaitAsync(JoinBudget);
            disposalAttempted = true;
            DisposePool(pool, faults);
        }
        catch (Exception error)
        {
            primaryFailure = error;
            throw;
        }
        finally
        {
            joined = StopAndJoinWorkers(stop, started, faults);
            if (!disposalAttempted) { DisposePool(pool, faults); }
            if (primaryFailure is not null && faults.Any(error => !ReferenceEquals(error, primaryFailure)))
            {
                primaryFailure.Data["PoolWorkerCleanup"] = new AggregateException(faults.Where(error => !ReferenceEquals(error, primaryFailure)));
            }
        }

        joined.ShouldBeTrue("a worker thread did not finish");

        if (!faults.IsEmpty)
        {
            var kinds = string.Join(", ", faults.Select(f => f.GetType().Name).Distinct());
            throw new Xunit.Sdk.XunitException(
                "Dispose() raced concurrent returns and something other than ObjectDisposedException "
                + "escaped. Dispose must not throw -- a throwing Dispose breaks every using block and "
                + $"container teardown that holds this pool. Exception kinds: {kinds}. "
                + $"First: {faults.First()}");
        }
    }

    private static bool StopAndJoinWorkers(ManualResetEventSlim stop, List<Thread> started, ConcurrentQueue<Exception> faults)
    {
        stop.Set();
        var joined = true;
        foreach (var worker in started)
        {
            try
            {
                if (!worker.Join(JoinBudget))
                {
                    joined = false;
                    faults.Enqueue(new TimeoutException("An owned pool worker did not finish."));
                }
            }
            catch (Exception error)
            {
                joined = false;
                faults.Enqueue(error);
            }
        }

        return joined;
    }

    private static void DisposePool(Utf8JsonWriterPool pool, ConcurrentQueue<Exception> faults)
    {
        try { pool.Dispose(); }
        catch (Exception error) { faults.Enqueue(error); }
    }
}
