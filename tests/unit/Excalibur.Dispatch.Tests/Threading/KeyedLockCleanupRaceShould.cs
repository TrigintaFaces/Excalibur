// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Reflection;

using Excalibur.Dispatch.Threading;

namespace Excalibur.Dispatch.Tests.Threading;

/// <summary>
/// Sprint 843 MS-3 (<c>ftq4u3</c>) engage-tests for the <see cref="KeyedLock"/> cleanup race — the
/// reference-counted per-key semaphore lifecycle.
/// <para>
/// Author≠implementer (TestsDeveloper). Deterministic via <see cref="Barrier"/>/<see cref="Interlocked"/>
/// — never <c>sleep</c>/timing. Wait/Join bounds are deadlock-avoidance only.
/// </para>
/// <list type="bullet">
/// <item><b>EC-2</b> (deterministic non-vacuity): double-<c>Dispose</c> of a handle is idempotent —
/// RED on pre-fix (no <c>_disposed</c> guard → second <c>Release()</c> hits the disposed semaphore).</item>
/// <item><b>AC-1/AC-2</b>: high-contention acquire/dispose churn on one key surfaces NO
/// <see cref="ObjectDisposedException"/> to a waiter and NEVER allows two simultaneous holders —
/// RED on pre-fix (the cleanup race throws / double-holds).</item>
/// <item><b>AC-3/EC-3</b>: after quiescence the per-key entry is removed (bounded memory).</item>
/// <item><b>AC-4</b>: a key fully released and removed can be re-acquired with a fresh semaphore.</item>
/// </list>
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class KeyedLockCleanupRaceShould
{
    // CI-scaled. This class drives 16 workers x 250 iterations against ONE key, so all 4,000
    // acquisitions are fully serialized by design -- the contention IS the test.
    //
    // THE BOUND IS A HARNESS DEADLINE, NOT AN INVARIANT. The properties this class guards -- no
    // waiter observes a disposal race, and never two holders of one key -- are asserted on their own
    // and are now asserted EVEN WHEN THE DEADLINE EXPIRES, so a timeout can no longer erase the
    // evidence of whether the lock actually held.
    //
    // WHY THE START GATE IS ASYNCHRONOUS, AND WHY IT MUST STAY THAT WAY.
    // This test previously gated its workers on Barrier.SignalAndWait inside Task.Run. That shape
    // requires all 16 workers to hold a thread-pool thread SIMULTANEOUSLY: a barrier cannot release
    // until the last participant arrives, and each waiter blocks the thread it is on. On an agent
    // whose pool is already occupied by sibling assemblies, the barrier cannot complete until the
    // pool injects the missing threads, which it does at roughly one per second.
    //
    // Measured directly, isolating the gate with NO lock work at all -- 16 workers, same shape:
    //     idle pool      ->      2 ms
    //     occupied pool  -> 27,286 ms
    // Same barrier, same worker count, 13,600x apart, entirely from pool occupancy. That is the
    // whole deadline, spent before the first acquisition. It presents as a hang in KeyedLock and is
    // nothing of the sort; the previous CI timeout was almost certainly this and the response then
    // was to scale the bound, which treats the symptom.
    //
    // The async gate below preserves the property the barrier was there for -- every worker starts
    // together, so the contention is real -- while occupying no threads to do it. Do not replace it
    // with Barrier, ManualResetEventSlim.Wait, or any other blocking primitive.
    private static readonly TimeSpan Bound =
        global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30));

    /// <summary>
    /// EC-2 (deterministic, no concurrency): disposing the same handle twice must be idempotent — it must
    /// not release the semaphore twice or throw. RED on pre-fix (no <c>_disposed</c> guard → the second
    /// <c>Release()</c> targets the already-disposed semaphore and throws).
    /// </summary>
    [Fact]
    public async Task BeIdempotent_WhenHandleDisposedTwice()
    {
        var keyedLock = new KeyedLock();

        var handle = await keyedLock.AcquireAsync("k", CancellationToken.None);
        handle.Dispose();

        // Second dispose must be a no-op, not a throw.
        Should.NotThrow(() => handle.Dispose());

        // And the lock must still be usable afterwards (no corrupted state / no leaked reference).
        using var reacquired = await keyedLock.AcquireAsync("k", CancellationToken.None);
        reacquired.ShouldNotBeNull();
    }

    /// <summary>
    /// AC-1 + AC-2: under heavy interleaved acquire/dispose churn on a SINGLE key, no waiter observes an
    /// <see cref="ObjectDisposedException"/> and at most one holder ever exists. RED on pre-fix — the
    /// dispose-removes-disposed-semaphore race throws to a waiter and/or admits two holders for one key.
    /// </summary>
    [Fact]
    public async Task NeverThrowToWaiterOrDoubleHold_UnderConcurrentChurnOnOneKey()
    {
        var keyedLock = new KeyedLock();
        const int workers = 16;
        const int iterations = 250;

        // The start gate is ASYNCHRONOUS, and that is load-bearing — see the note below.
        var arrived = 0;
        var allArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var concurrentHolders = 0;
        var exclusivityViolations = 0;
        var completedIterations = 0;
        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();

        async Task Worker()
        {
            if (Interlocked.Increment(ref arrived) == workers)
            {
                _ = allArrived.TrySetResult();
            }

            // await, never block: a worker waiting here occupies no thread.
            await allArrived.Task.WaitAsync(Bound).ConfigureAwait(false);

            for (var i = 0; i < iterations; i++)
            {
                try
                {
                    using (await keyedLock.AcquireAsync("hot", CancellationToken.None).ConfigureAwait(false))
                    {
                        // Inside the critical section there must be exactly one holder.
                        var inside = Interlocked.Increment(ref concurrentHolders);
                        if (inside != 1)
                        {
                            _ = Interlocked.Increment(ref exclusivityViolations);
                        }

                        _ = Interlocked.Decrement(ref concurrentHolders);
                    }

                    _ = Interlocked.Increment(ref completedIterations);
                }
                catch (Exception ex)
                {
                    failures.Enqueue(ex);
                }
            }
        }

        var tasks = Enumerable.Range(0, workers).Select(_ => Task.Run(Worker)).ToArray();

        // A TIMEOUT MUST NOT DESTROY THE EVIDENCE. Previously the bound threw straight out of this
        // method, so the two assertions below never ran and CI could not tell "the lock is broken"
        // from "the agent was busy". Catch it, then assert the invariants anyway and report progress.
        var timedOut = false;
        try
        {
            await Task.WhenAll(tasks).WaitAsync(Bound);
        }
        catch (TimeoutException)
        {
            timedOut = true;
        }

        var progress = Volatile.Read(ref completedIterations);

        failures.ShouldBeEmpty(
            "no waiter may observe an ObjectDisposedException from the keyed-semaphore cleanup race");
        exclusivityViolations.ShouldBe(0, "at most one holder may exist per key at any instant");

        // Only now, with the real invariants confirmed, report the deadline — and say how far it got,
        // because "0 of 4000" (stuck) and "3900 of 4000" (merely slow) are different defects.
        timedOut.ShouldBeFalse(
            $"the churn did not finish within {Bound.TotalSeconds:F0}s: {progress} of {workers * iterations} " +
            "iterations completed. Zero or near-zero progress indicates a genuine stall in KeyedLock; " +
            "near-complete progress indicates the deadline is too tight for the agent, not a lock defect.");
    }

    /// <summary>
    /// AC-3 / EC-3: after every reference to a key is released, the entry is removed so memory stays
    /// bounded (no leaked semaphores after quiescence).
    /// </summary>
    [Fact]
    public async Task RemoveEntry_AfterAllReferencesReleased()
    {
        var keyedLock = new KeyedLock();

        for (var i = 0; i < 50; i++)
        {
            using var handle = await keyedLock.AcquireAsync($"key-{i}", CancellationToken.None);
        }

        EntryCount(keyedLock).ShouldBe(0, "fully-released keys must be removed from the backing map");
    }

    /// <summary>
    /// AC-4: a key that was fully released and removed can be acquired again with a fresh semaphore.
    /// </summary>
    [Fact]
    public async Task CreateFreshSemaphore_WhenKeyReacquiredAfterRemoval()
    {
        var keyedLock = new KeyedLock();

        var first = await keyedLock.AcquireAsync("recycled", CancellationToken.None);
        first.Dispose();
        EntryCount(keyedLock).ShouldBe(0);

        using var second = await keyedLock.AcquireAsync("recycled", CancellationToken.None);
        second.ShouldNotBeNull();
        EntryCount(keyedLock).ShouldBe(1);
    }

    /// <summary>
    /// EC-1: when a queued waiter's <c>WaitAsync</c> is cancelled, its reserved reference is released —
    /// the cancelled caller never becomes a holder and leaks no reference, so once the real holder releases
    /// the entry is fully removed. (RED if a cancelled acquire leaked its reference: the entry would survive.)
    /// </summary>
    [Fact]
    public async Task ReleaseReferenceAndNotLeak_WhenWaitCancelled()
    {
        var keyedLock = new KeyedLock();

        // Holder takes "k" (semaphore count → 0); a second acquire on "k" must queue on WaitAsync.
        var holder = await keyedLock.AcquireAsync("k", CancellationToken.None);

        using var cts = new CancellationTokenSource();
        var blocked = keyedLock.AcquireAsync("k", cts.Token);

        // Cancel the queued waiter — it must throw and release the reference it reserved before awaiting.
        await cts.CancelAsync();
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await blocked);

        // Only the holder's reference remains (the cancelled waiter did not leak one).
        EntryCount(keyedLock).ShouldBe(1);

        // Releasing the holder drops the last reference → entry removed (no leaked reference).
        holder.Dispose();
        EntryCount(keyedLock).ShouldBe(0);
    }

    /// <summary>
    /// Reads the count of the private per-key backing map via reflection (works for both the pre-fix
    /// <c>Dictionary&lt;string, SemaphoreSlim&gt;</c> and the fixed <c>Dictionary&lt;string, Entry&gt;</c>).
    /// </summary>
    private static int EntryCount(KeyedLock keyedLock)
    {
        var field = typeof(KeyedLock).GetField("_locks", BindingFlags.NonPublic | BindingFlags.Instance);
        field.ShouldNotBeNull("KeyedLock should keep its per-key map in a field named _locks");
        var map = (ICollection)field!.GetValue(keyedLock)!;
        return map.Count;
    }
}
