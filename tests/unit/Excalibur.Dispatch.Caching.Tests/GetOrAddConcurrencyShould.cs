// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Sprint 843 MS-1 (<c>xj7lfn</c>, keystone) engage-tests for <see cref="LruCache{TKey,TValue}"/>.GetOrAdd —
/// the factory-outside-lock concurrency fix and the <c>gblom</c> inline-insert guardrail.
/// <para>
/// Author≠implementer (TestsDeveloper). All proofs are deterministic via
/// <see cref="ManualResetEventSlim"/>/<see cref="Barrier"/> rendezvous — never <c>sleep</c>/timing.
/// Join/Wait timeouts are deadlock-avoidance bounds only; correctness is asserted on the signaled state.
/// </para>
/// <list type="bullet">
/// <item><b>AC-1</b> (primary non-vacuity): a different-key op completes while a factory is in flight —
/// RED on pre-fix (factory holds <c>_lock</c>).</item>
/// <item><b>AC-2</b> (gblom guardrail + terminal-outcome metrics): two concurrent same-key misses commit
/// exactly one entry, both callers see the winner, telemetry = 1 miss + 1 hit. RED on a re-entrant-<c>Set()</c>
/// commit mutant (callers would observe different values).</item>
/// <item><b>AC-3</b>: a live entry never invokes the factory.</item>
/// <item><b>AC-4</b>: a factory that re-enters the same cache (different key) does not deadlock/corrupt.</item>
/// <item><b>EC-1</b>: a throwing factory leaves no poisoned entry.</item>
/// </list>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class GetOrAddConcurrencyShould
{
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// AC-1 → FR-2/FR-3/NFR-1 (primary non-vacuity): the factory MUST NOT run while holding <c>_lock</c>,
    /// so a cache operation on a DIFFERENT key completes while the factory is still in flight.
    /// RED on pre-fix: the different-key op blocks on <c>_lock</c> until the factory returns.
    /// </summary>
    [Fact]
    public void NotHoldLockAcrossFactory_SoConcurrentDifferentKeyOpCompletes()
    {
        using var cache = new LruCache<string, int>(16);
        using var factoryEntered = new ManualResetEventSlim(false);
        using var releaseFactory = new ManualResetEventSlim(false);
        using var differentKeyOpDone = new ManualResetEventSlim(false);

        // Seed a different key so the probe's TryGetValue is a pure read (no factory of its own).
        cache.Set("other", 42);

        var factoryThread = new Thread(() =>
            cache.GetOrAdd("A", _ =>
            {
                factoryEntered.Set();
                // Hold inside the factory until the test releases it.
                releaseFactory.Wait(JoinTimeout);
                return 1;
            }))
        { IsBackground = true, Name = "ac1-factory" };

        var probeThread = new Thread(() =>
        {
            _ = cache.TryGetValue("other", out _);
            differentKeyOpDone.Set();
        })
        { IsBackground = true, Name = "ac1-probe" };

        try
        {
            factoryThread.Start();
            factoryEntered.Wait(StartTimeout).ShouldBeTrue("the factory should have started");

            // Probe runs while the factory is provably still in flight (releaseFactory not yet set).
            probeThread.Start();

            // Deterministic assertion target: the different-key op finishes WHILE the factory is held.
            // Pre-fix the probe is blocked on _lock → never signals → false → RED.
            var completedWhileFactoryHeld = differentKeyOpDone.Wait(StartTimeout);

            completedWhileFactoryHeld.ShouldBeTrue(
                "a cache op on a DIFFERENT key must not block on an in-flight factory " +
                "(the factory must run outside _lock)");
        }
        finally
        {
            releaseFactory.Set();
            _ = factoryThread.Join(JoinTimeout);
            _ = probeThread.Join(JoinTimeout);
        }
    }

    /// <summary>
    /// AC-2 → FR-4/FR-6 (gblom guardrail + terminal-outcome metrics): two threads released together to
    /// miss on the SAME key — with distinct factory outputs — commit exactly one entry, both observe the
    /// committed winner, and telemetry records exactly 1 miss + 1 hit (NOT 2 misses).
    /// Stays GREEN pre-fix (serialized-but-single-insert) and post-fix (double-check); goes RED on a
    /// re-entrant-<c>Set()</c> commit mutant (the loser would overwrite the winner → different values).
    /// </summary>
    [Fact]
    public void OnConcurrentSameKeyMiss_CommitSingleEntry_BothSeeWinner_OneMissOneHit()
    {
        using var cache = new LruCache<string, int>(16);
        using var barrier = new Barrier(2);
        var factoryCalls = 0;
        var results = new int[2];

        void Run(int index)
        {
            _ = barrier.SignalAndWait(JoinTimeout);
            results[index] = cache.GetOrAdd("K", _ =>
            {
                var n = Interlocked.Increment(ref factoryCalls);
                return 1000 + n; // distinct per invocation, so an overwrite would be observable
            });
        }

        var t0 = new Thread(() => Run(0)) { IsBackground = true, Name = "ac2-t0" };
        var t1 = new Thread(() => Run(1)) { IsBackground = true, Name = "ac2-t1" };
        t0.Start();
        t1.Start();
        t0.Join(JoinTimeout).ShouldBeTrue("t0 should finish");
        t1.Join(JoinTimeout).ShouldBeTrue("t1 should finish");

        // Exactly one entry committed for the contended key.
        cache.Count.ShouldBe(1);

        // Both callers observe the SAME committed winner — the inline-insert + double-check guardrail
        // (a re-entrant Set() commit would let the loser overwrite, yielding different values).
        results[0].ShouldBe(results[1]);

        // Metrics by terminal outcome: one genuine insert (miss) + one double-check winner (hit).
        var stats = cache.Statistics;
        stats.Misses.ShouldBe(1, "exactly one genuine insert should count as a miss");
        stats.Hits.ShouldBe(1, "the double-check loser returns the winner and counts as a hit");
    }

    /// <summary>
    /// AC-3 → FR-1: when a live entry exists, GetOrAdd returns it WITHOUT invoking the factory.
    /// </summary>
    [Fact]
    public void NotInvokeFactory_WhenLiveEntryExists()
    {
        using var cache = new LruCache<string, int>(16);
        _ = cache.GetOrAdd("K", _ => 7);

        var calls = 0;
        var value = cache.GetOrAdd("K", _ =>
        {
            calls++;
            return 99;
        });

        value.ShouldBe(7);
        calls.ShouldBe(0);
    }

    /// <summary>
    /// AC-4 → FR-4 (reentrancy regression lock): a factory that re-enters the same cache for a DIFFERENT
    /// key must not deadlock or corrupt LRU/eviction accounting, and must return correct values.
    /// </summary>
    [Fact]
    public void AllowReentrantFactoryOnDifferentKey_WithoutDeadlockOrCorruption()
    {
        using var cache = new LruCache<string, int>(16);

        var outer = cache.GetOrAdd("outer", _ =>
        {
            // Re-enter the SAME cache instance for a different key from inside the factory.
            var inner = cache.GetOrAdd("inner", static innerKey => 2);
            return inner + 10;
        });

        outer.ShouldBe(12);
        cache.TryGetValue("inner", out var innerValue).ShouldBeTrue();
        innerValue.ShouldBe(2);
        cache.TryGetValue("outer", out var outerValue).ShouldBeTrue();
        outerValue.ShouldBe(12);
        cache.Count.ShouldBe(2);
    }

    /// <summary>
    /// EC-1: a throwing factory propagates the exception and leaves NO poisoned/partial entry; the key
    /// remains absent and the cache size is unchanged.
    /// </summary>
    [Fact]
    public void NotLeavePoisonedEntry_WhenFactoryThrows()
    {
        using var cache = new LruCache<string, int>(16);

        _ = Should.Throw<InvalidOperationException>(() =>
            cache.GetOrAdd("K", _ => throw new InvalidOperationException("factory boom")));

        cache.TryGetValue("K", out _).ShouldBeFalse();
        cache.Count.ShouldBe(0);
    }
}
