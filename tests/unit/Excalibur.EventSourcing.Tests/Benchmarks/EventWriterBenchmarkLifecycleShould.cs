// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using Excalibur.Dispatch.Performance.Benchmarks;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Behavior")]
public sealed class EventWriterBenchmarkLifecycleShould
{
    [Fact]
    public void TargetOnlyWritersAndReleaseAllFixtureReferencesIdempotently()
    {
        var fixture = new EventSourcingBenchmarks { EventCount = 1 };
        fixture.Setup();
        fixture.WriterIterationCleanup();
        fixture.WriterIterationCleanup();
        fixture.Cleanup();
        foreach (var name in new[] { "_eventStore", "_preloadedAggregateId", "_snapshotAggregateId" })
        {
            typeof(EventSourcingBenchmarks).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture).ShouldBeNull();
        }

        var setup = typeof(EventSourcingBenchmarks).GetMethod(nameof(EventSourcingBenchmarks.WriterIterationSetup))!
            .GetCustomAttribute<IterationSetupAttribute>()!;
        var cleanup = typeof(EventSourcingBenchmarks).GetMethod(nameof(EventSourcingBenchmarks.WriterIterationCleanup))!
            .GetCustomAttribute<IterationCleanupAttribute>()!;
        string[] expected = [nameof(EventSourcingBenchmarks.AppendEvents_NewStream), nameof(EventSourcingBenchmarks.AppendEvents_ExistingStream), nameof(EventSourcingBenchmarks.ConcurrentAppend_DifferentAggregates)];
        setup.Targets.ShouldBe(expected);
        cleanup.Targets.ShouldBe(expected);
        typeof(EventSourcingBenchmarks).GetMethod(nameof(EventSourcingBenchmarks.WriterIterationSetup))!.ReturnType.ShouldBe(typeof(void));
        typeof(EventSourcingBenchmarks).GetMethod(nameof(EventSourcingBenchmarks.WriterIterationCleanup))!.ReturnType.ShouldBe(typeof(void));
    }

    [Theory]
    [InlineData("AppendEvents_NewStream", 1)]
    [InlineData("AppendEvents_NewStream", 10)]
    [InlineData("AppendEvents_NewStream", 100)]
    [InlineData("AppendEvents_ExistingStream", 1)]
    [InlineData("AppendEvents_ExistingStream", 10)]
    [InlineData("AppendEvents_ExistingStream", 100)]
    [InlineData("ConcurrentAppend_DifferentAggregates", 1)]
    [InlineData("ConcurrentAppend_DifferentAggregates", 10)]
    [InlineData("ConcurrentAppend_DifferentAggregates", 100)]
    public async Task IsolateEachOriginalWriterInvocation(string method, int eventCount)
    {
        var fixture = new EventSourcingBenchmarks { EventCount = eventCount };
        fixture.Setup();
        try
        {
            for (var iteration = 0; iteration < 4; iteration++)
            {
                InvokeHook(fixture, "WriterIterationSetup");
                var streams = Streams(fixture);
                streams.Count.ShouldBe(2, "each iteration must start with only the two seeded streams");
                streams.Values.Sum(events => events.Count).ShouldBe(300);
                var seedKeys = streams.Keys.ToHashSet(StringComparer.Ordinal);
                var seedEventIds = streams.ToDictionary(pair => pair.Key, pair => pair.Value.Select(evt => evt.EventId).ToArray());
                VerifyStream(await fixture.LoadAggregate_FullStream(), 100);
                VerifyStream(await fixture.SnapshotLoad_NoSnapshot(), 200);

                try
                {
                    if (method == "AppendEvents_NewStream")
                    {
                        var result = await fixture.AppendEvents_NewStream();
                        result.Success.ShouldBeTrue();
                        result.NextExpectedVersion.ShouldBe(eventCount - 1L);
                    }
                    else if (method == "AppendEvents_ExistingStream")
                    {
                        var result = await fixture.AppendEvents_ExistingStream();
                        result.Success.ShouldBeTrue();
                        result.NextExpectedVersion.ShouldBe(eventCount + 9L);
                    }
                    else
                    {
                        await fixture.ConcurrentAppend_DifferentAggregates();
                    }

                    var newStreams = streams.Where(pair => !seedKeys.Contains(pair.Key)).Select(pair => pair.Value).ToArray();
                    newStreams.Length.ShouldBe(method == "ConcurrentAppend_DifferentAggregates" ? 10 : 1);
                    var expectedLength = eventCount + (method == "AppendEvents_ExistingStream" ? 10 : 0);
                    foreach (var events in newStreams)
                    {
                        VerifyStream(events, expectedLength);
                    }

                    streams.Values.Sum(events => events.Count).ShouldBe(300 + (newStreams.Length * expectedLength));
                    VerifyStream(await fixture.LoadAggregate_FullStream(), 100);
                    VerifyStream(await fixture.SnapshotLoad_NoSnapshot(), 200);
                    foreach (var seed in seedEventIds)
                    {
                        streams[seed.Key].Select(evt => evt.EventId).ShouldBe(seed.Value);
                    }
                }
                finally
                {
                    InvokeHook(fixture, "WriterIterationCleanup");
                }

                typeof(EventSourcingBenchmarks).GetField("_eventStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture).ShouldBeNull();
                typeof(EventSourcingBenchmarks).GetField("_preloadedAggregateId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture).ShouldBeNull();
                typeof(EventSourcingBenchmarks).GetField("_snapshotAggregateId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture).ShouldBeNull();
            }
        }
        finally
        {
            InvokeHook(fixture, "Cleanup");
        }
    }

    private static void VerifyStream(IReadOnlyList<StoredEvent> events, int count)
    {
        events.Count.ShouldBe(count);
        events.Select(evt => evt.EventId).Distinct(StringComparer.Ordinal).Count().ShouldBe(count);
        events.Select(evt => evt.AggregateId).Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
        for (var i = 0; i < events.Count; i++)
        {
            events[i].Version.ShouldBe((long)i);
            events[i].AggregateType.ShouldBe("BenchmarkAggregate");
            Encoding.UTF8.GetString(events[i].EventData).ShouldBe($"benchmark-{i}");
        }
    }

    private static Dictionary<string, List<StoredEvent>> Streams(EventSourcingBenchmarks fixture)
    {
        var store = typeof(EventSourcingBenchmarks).GetField("_eventStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!;
        return (Dictionary<string, List<StoredEvent>>)store.GetType().GetField("_streams", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;
    }

    // Optional during the red run: absent hooks must expose retained state on the second iteration.
    private static void InvokeHook(EventSourcingBenchmarks fixture, string name) =>
        typeof(EventSourcingBenchmarks).GetMethod(name)?.Invoke(fixture, null);
}
