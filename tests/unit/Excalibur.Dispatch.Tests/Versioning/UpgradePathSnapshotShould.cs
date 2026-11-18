// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections;
using System.Reflection;
using Excalibur.Dispatch.Versioning;

namespace Excalibur.Dispatch.Tests.Versioning;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Concurrency")]
public sealed class UpgradePathSnapshotShould
{
    [Fact]
    public async Task Keep_old_search_results_out_of_new_snapshot_after_invalidation()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var oldEdge = new Edge(1, 2);
        var resolver = new UpgradePathResolver<Edge>(edge =>
        {
            if (ReferenceEquals(edge, oldEdge))
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Old search gate");
            }
            return edge.From;
        }, edge => edge.To);
        IReadOnlyList<Edge> oldGraph = [oldEdge, new(0, 3)];
        IReadOnlyList<Edge> newGraph = [new(1, 2), new(0, 3), new(2, 3)];
        var pending = Task.Run(() => resolver.Resolve("T", oldGraph, 1, 3));
        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            resolver.Invalidate();
        }
        finally
        {
            release.Set();
        }
        (await pending.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeNull();
        var current = resolver.Resolve("T", newGraph, 1, 3);
        current.ShouldNotBeNull();
        current.Count.ShouldBe(2);
        current[1].To.ShouldBe(3);
    }

    [Fact]
    public void Bound_memoization_by_registered_endpoints_not_arbitrary_queries()
    {
        var resolver = new UpgradePathResolver<Edge>(edge => edge.From, edge => edge.To);
        IReadOnlyList<Edge> graph = [new(1, int.MaxValue)];
        resolver.Resolve("T", graph, 1, int.MaxValue).ShouldNotBeNull();
        var initial = CountEntries(resolver);
        for (var value = 2; value < 1026; value++)
        {
            resolver.Resolve("T", graph, -value, int.MaxValue).ShouldBeNull();
            resolver.Resolve("T", graph, 1, value).ShouldBeNull();
            resolver.Resolve("arbitrary-" + value, graph, 1, int.MaxValue).ShouldNotBeNull();
        }
        CountEntries(resolver).ShouldBe(initial);
    }

    [Fact]
    public void Release_old_snapshot_paths_while_the_resolver_and_new_graph_remain_live()
    {
        var resolver = new UpgradePathResolver<Edge>(edge => edge.From, edge => edge.To);
        var oldEdge = PopulateOldSnapshot(resolver);
        var currentEdge = new Edge(1, 2);
        IReadOnlyList<Edge> current = [currentEdge];
        resolver.Resolve("T", current, 1, 2)![0].ShouldBeSameAs(currentEdge);
        for (var attempt = 0; attempt < 3 && oldEdge.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        oldEdge.IsAlive.ShouldBeFalse();
        GC.KeepAlive(resolver);
        GC.KeepAlive(current);
    }

    [Fact]
    public void Keep_paths_alive_while_their_snapshot_is_live()
    {
        var resolver = new UpgradePathResolver<Edge>(edge => edge.From, edge => edge.To);
        IReadOnlyList<Edge> graph = [new(1, 2)];
        var edge = new WeakReference(graph[0]);
        resolver.Resolve("T", graph, 1, 2).ShouldNotBeNull();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        edge.IsAlive.ShouldBeTrue();
        GC.KeepAlive(graph);
        GC.KeepAlive(resolver);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference PopulateOldSnapshot(UpgradePathResolver<Edge> resolver)
    {
        var edge = new Edge(1, 2);
        IReadOnlyList<Edge> graph = [edge];
        resolver.Resolve("T", graph, 1, 2).ShouldNotBeNull();
        return new WeakReference(edge);
    }

    // Inspect retained memo entries, not allocation noise or elapsed time. Both supported internal
    // layouts are recognized so the RED arm can run against the pre-fix resolver.
    private static int CountEntries(object resolver)
    {
        var oldCache = resolver.GetType().GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);
        if (oldCache is not null) return ((ICollection)oldCache.GetValue(resolver)!).Count;
        var field = resolver.GetType().GetField("_snapshots", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Unknown resolver memo layout");
        var snapshots = (IEnumerable)field.GetValue(resolver)!;
        var count = 0;
        foreach (var pair in snapshots)
        {
            var memo = pair.GetType().GetProperty("Value")!.GetValue(pair)!;
            count += ((ICollection)memo.GetType().GetProperty("Paths")!.GetValue(memo)!).Count;
        }
        return count;
    }

    private sealed record Edge(int From, int To);
}
