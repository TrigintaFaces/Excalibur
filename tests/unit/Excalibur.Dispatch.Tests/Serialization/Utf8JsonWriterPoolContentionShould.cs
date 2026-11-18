// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Buffers;
using System.Reflection;
using System.Text.Json;
using System.Threading;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// The pool's lifecycle lock is deliberately ASYMMETRIC, and these two arms pin both directions of
/// that asymmetry so neither can drift back.
/// <list type="bullet">
/// <item>A warm thread-local hit reads a per-thread array and must NOT take the pool-wide lock.</item>
/// <item>The global-pool consult MUST take it: the bucket scan rotates option mismatches back into
/// the queue bounded by the count read on entry, and that bound is only sound while the population is
/// stable.</item>
/// </list>
/// <para>
/// These are white-box by necessity. The property under test is the presence or absence of a lock
/// acquisition, which no public surface reports, and asserting it through throughput would be a timing
/// test and therefore non-deterministic. Holding the real lock from another thread is what makes the
/// assertions deterministic rather than a measurement of contention.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Serialization")]
[Trait("Pattern", "Concurrency")]
public sealed class Utf8JsonWriterPoolContentionShould
{
    private static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ServedBudget = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BlockedBudget = TimeSpan.FromMilliseconds(750);

    /// <summary>
    /// Returns the pool's own <see cref="Lock"/>. The TYPE matters: <c>lock (obj)</c> over a
    /// <see cref="Lock"/> boxed to <see cref="object"/> compiles to MONITOR-based locking, which does
    /// not exclude the <c>Lock.EnterScope()</c> the C# lock statement emits inside the pool. An arm
    /// written that way holds a different primitive, excludes nothing, and passes whatever the pool
    /// does -- which is exactly how the first version of these arms passed against the unfixed code.
    /// </summary>
    private static Lock LifecycleLockOf(Utf8JsonWriterPool pool)
    {
        var field = typeof(Utf8JsonWriterPool).GetField(
            "_lifecycleLock",
            BindingFlags.Instance | BindingFlags.NonPublic);

        field.ShouldNotBeNull(
            "Utf8JsonWriterPool._lifecycleLock was renamed or removed; these arms no longer bind the "
            + "property they claim to and must be updated rather than deleted.");

        var value = field!.GetValue(pool) as Lock;
        value.ShouldNotBeNull(
            "_lifecycleLock is no longer a System.Threading.Lock. These arms must take the same lock "
            + "primitive the pool takes, or they exclude nothing.");
        return value!;
    }

    [Fact]
    public void ServeAWarmThreadLocalHitWhileTheLifecycleLockIsHeld()
    {
        using var pool = new Utf8JsonWriterPool(threadLocalCacheSize: 4, enableAdaptiveSizing: false);
        var lifecycleLock = LifecycleLockOf(pool);

        using var warmed = new ManualResetEventSlim(false);
        using var lockHeld = new ManualResetEventSlim(false);
        using var served = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        Utf8JsonWriter? rented = null;
        Exception? failure = null;

        // The renter must warm ITS OWN thread's cache BEFORE the lock is held. The warming rent is a
        // total miss, so it consults the global pool and legitimately takes the lock -- warming while
        // the lock is held would measure that consult rather than the fast path, which is how the
        // first version of this arm failed against the fixed code.
        var renter = new Thread(() =>
        {
            try
            {
                var buffer = new ArrayBufferWriter<byte>();
                var warm = pool.Rent(buffer);
                pool.ReturnToPool(warm);
                warmed.Set();

                lockHeld.Wait(HoldTime).ShouldBeTrue("the holder never acquired the lifecycle lock");

                // The measured call: this thread's cache now holds a compatible writer.
                rented = pool.Rent(buffer);
                served.Set();
            }
            catch (Exception ex)
            {
                failure = ex;
                served.Set();
            }
        })
        { IsBackground = true };

        var holder = new Thread(() =>
        {
            warmed.Wait(HoldTime);
            using (lifecycleLock.EnterScope())
            {
                lockHeld.Set();
                _ = release.Wait(HoldTime);
            }
        })
        { IsBackground = true };

        renter.Start();
        holder.Start();

        var completed = served.Wait(ServedBudget);
        release.Set();
        renter.Join(HoldTime);
        holder.Join(HoldTime);

        failure.ShouldBeNull();
        completed.ShouldBeTrue(
            "a warm thread-local cache hit waited on the pool-wide lifecycle lock. The thread-local "
            + "cache is per-thread by construction and needs no pool-wide synchronisation; with one "
            + "serializer singleton, every message in the process pays that lock otherwise.");
        rented.ShouldNotBeNull();
        pool.ReturnToPool(rented!);
    }

    [Fact]
    public void SerializeTheGlobalPoolConsultBehindTheLifecycleLock()
    {
        // The other direction of the asymmetry, pinned deliberately. threadLocalCacheSize: 0 forces
        // every rent through the global-pool consult, which MUST keep the lock: the bucket scan
        // rotates option mismatches back into the queue bounded by the count read on entry, and that
        // bound is only sound while the population is stable. If a future change hoists this consult
        // out of the lock to buy throughput, this arm reddens and that trade gets made deliberately
        // rather than silently -- the failure mode is a false miss that allocates instead of reusing.
        using var pool = new Utf8JsonWriterPool(maxPoolSize: 4, threadLocalCacheSize: 0, enableAdaptiveSizing: false);
        var lifecycleLock = LifecycleLockOf(pool);

        using var lockHeld = new ManualResetEventSlim(false);
        using var served = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        var holder = new Thread(() =>
        {
            using (lifecycleLock.EnterScope())
            {
                lockHeld.Set();
                _ = release.Wait(HoldTime);
            }
        })
        { IsBackground = true };

        holder.Start();
        lockHeld.Wait(HoldTime).ShouldBeTrue("the holder never acquired the lifecycle lock");

        Utf8JsonWriter? rented = null;
        var renter = new Thread(() =>
        {
            rented = pool.Rent(new ArrayBufferWriter<byte>());
            served.Set();
        })
        { IsBackground = true };

        renter.Start();
        var completedWhileHeld = served.Wait(BlockedBudget);

        release.Set();
        served.Wait(HoldTime).ShouldBeTrue("the rental never completed even after the lock was released");
        renter.Join(HoldTime);
        holder.Join(HoldTime);

        completedWhileHeld.ShouldBeFalse(
            "the global-pool consult completed while the lifecycle lock was held, so the bucket scan "
            + "no longer runs against a stable population. That scan rotates option mismatches back "
            + "into the queue bounded by the count it read on entry; without the lock it can miss a "
            + "matching writer and allocate instead. If this was hoisted deliberately, update this arm "
            + "and say so -- do not delete it.");
        rented.ShouldNotBeNull();
        pool.ReturnToPool(rented!);
    }
}
