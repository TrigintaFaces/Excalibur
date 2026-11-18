// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using BenchmarkDotNet.Attributes;

using Excalibur.Benchmarks.EventSourcing;
using Excalibur.EventSourcing.InMemory;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class InMemoryEventStoreBenchmarksShould
{
    [Fact]
    public void ScopeIterationHooksToOnlyTheFourWritingMethods()
    {
        var expected = new[]
        {
            nameof(InMemoryEventStoreBenchmarks.AppendSingleEvent),
            nameof(InMemoryEventStoreBenchmarks.AppendBatch_10Events),
            nameof(InMemoryEventStoreBenchmarks.AppendBatch_100Events),
            nameof(InMemoryEventStoreBenchmarks.ConcurrentAppend_10Aggregates),
        };
        var setup = typeof(InMemoryEventStoreBenchmarks).GetMethod("SetupWriteIteration");
        var cleanup = typeof(InMemoryEventStoreBenchmarks).GetMethod("CleanupWriteIteration");
        setup.ShouldNotBeNull();
        cleanup.ShouldNotBeNull();
        setup.ReturnType.ShouldBe(typeof(void));
        cleanup.ReturnType.ShouldBe(typeof(void));
        setup.GetCustomAttribute<IterationSetupAttribute>()!.Targets.Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal));
        cleanup.GetCustomAttribute<IterationCleanupAttribute>()!.Targets.Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AllowSetupAndRepeatedCleanupWithoutAWrite()
    {
        var fixture = new InMemoryEventStoreBenchmarks();
        try
        {
            await fixture.GlobalSetup();
            await InvokeHookIfPresentAsync(fixture, "SetupWriteIteration");
            await InvokeHookIfPresentAsync(fixture, "CleanupWriteIteration");
            await InvokeHookIfPresentAsync(fixture, "CleanupWriteIteration");
            RetainedEvents(fixture).ShouldBe(0);
        }
        finally
        {
            fixture.GlobalCleanup();
        }
    }

    [Theory]
    [InlineData(nameof(InMemoryEventStoreBenchmarks.AppendSingleEvent), 1)]
    [InlineData(nameof(InMemoryEventStoreBenchmarks.AppendBatch_10Events), 10)]
    [InlineData(nameof(InMemoryEventStoreBenchmarks.AppendBatch_100Events), 100)]
    [InlineData(nameof(InMemoryEventStoreBenchmarks.ConcurrentAppend_10Aggregates), 10)]
    public async Task RetainAllAppendedEventsWithinAWindow(string method, int added)
    {
        var fixture = new InMemoryEventStoreBenchmarks();
        try
        {
            await fixture.GlobalSetup();
            for (var call = 1; call <= 4; call++)
            {
                await InvokeAsync(fixture, method);
                RetainedEvents(fixture).ShouldBe(11110 + call * added);
            }
        }
        finally
        {
            fixture.GlobalCleanup();
        }
    }

    [Theory]
    [InlineData(nameof(InMemoryEventStoreBenchmarks.AppendSingleEvent), 1)]
    [InlineData(nameof(InMemoryEventStoreBenchmarks.AppendBatch_10Events), 10)]
    [InlineData(nameof(InMemoryEventStoreBenchmarks.AppendBatch_100Events), 100)]
    [InlineData(nameof(InMemoryEventStoreBenchmarks.ConcurrentAppend_10Aggregates), 10)]
    public async Task ReleaseWriteWindowAndReseedOriginalReadPopulation(string method, int added)
    {
        var fixture = new InMemoryEventStoreBenchmarks();
        try
        {
            await fixture.GlobalSetup();
            for (var cycle = 0; cycle < 3; cycle++)
            {
                await InvokeHookIfPresentAsync(fixture, "SetupWriteIteration");
                RetainedEvents(fixture).ShouldBe(11110);
                Store(fixture).GetHeadPosition().ShouldBe(11110);
                var seed = (await fixture.LoadAggregate_100Events()).ToArray();
                (await fixture.LoadAggregate_10Events()).Count.ShouldBe(10);
                (await fixture.LoadAggregate_100Events()).Count.ShouldBe(100);
                (await fixture.LoadAggregate_1000Events()).Count.ShouldBe(1000);
                (await fixture.LoadAggregate_10000Events()).Count.ShouldBe(10000);
                await InvokeAsync(fixture, method);
                RetainedEvents(fixture).ShouldBe(11110 + added);
                Store(fixture).GetHeadPosition().ShouldBe(11110 + added);
                (await fixture.LoadAggregate_100Events()).ShouldBe(seed);
                await InvokeHookIfPresentAsync(fixture, "CleanupWriteIteration");
                RetainedEvents(fixture).ShouldBe(0);
                Store(fixture).GetHeadPosition().ShouldBe(0);
            }
        }
        finally
        {
            fixture.GlobalCleanup();
        }
    }

    private static async Task InvokeAsync(InMemoryEventStoreBenchmarks fixture, string method)
    {
        await ((Task)typeof(InMemoryEventStoreBenchmarks).GetMethod(method)!.Invoke(fixture, parameters: null)!);
    }

    private static async Task InvokeHookIfPresentAsync(InMemoryEventStoreBenchmarks fixture, string name)
    {
        var result = typeof(InMemoryEventStoreBenchmarks).GetMethod(name)?.Invoke(fixture, parameters: null);
        if (result is Task task)
        {
            await task;
        }
    }

    private static int RetainedEvents(InMemoryEventStoreBenchmarks fixture) => Store(fixture).GetEventCount();

    private static InMemoryEventStore Store(InMemoryEventStoreBenchmarks fixture)
    {
        var store = typeof(InMemoryEventStoreBenchmarks).GetField("_eventStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!;
        return (InMemoryEventStore)store;
    }
}
