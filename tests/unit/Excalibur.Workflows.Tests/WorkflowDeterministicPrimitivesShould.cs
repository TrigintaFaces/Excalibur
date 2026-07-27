// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Regression lock for the deterministic workflow-context primitives (<c>ctx.UtcNowAsync</c> and
/// <c>ctx.NewGuidAsync</c>). Both are journaled on first execution and, on replay, must reproduce the value
/// observed originally rather than re-reading the wall-clock or generating a fresh identifier — otherwise a
/// deadline or business id derived inside a workflow would change across a restart and break replay.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowDeterministicPrimitivesShould
{
    private const string WorkflowName = "deterministic-primitives";

    [Fact]
    public async Task ReplayJournaledTimeAndGuid_NotWallClock_AcrossRestart()
    {
        // The clock starts at a fixed instant; the first run reads it, then we advance it an hour before the
        // resume. A wall-clock read on resume would observe the advanced time — the journaled read must not.
        var start = new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(start);
        var sink = new PrimitiveSink();
        var crash = new CrashFlag(shouldCrash: true);
        await using var provider = BuildProvider(clock, sink, crash);

        const string instanceId = "inst-deterministic-primitives";

        // Phase 1 — first run journals the time read and the generated guid, then crashes before completion.
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await executor.StartAsync(WorkflowName, instanceId, "input", CancellationToken.None));
        }

        var firstTime = sink.Time.ShouldHaveSingleItem();
        var firstGuid = sink.Guid.ShouldHaveSingleItem();
        firstTime.ShouldBe(start);
        firstGuid.ShouldNotBe(Guid.Empty);

        // Advance wall-clock past the journaled instant, then resume without crashing.
        clock.Advance(TimeSpan.FromHours(1));
        crash.ShouldCrash = false;

        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "input", CancellationToken.None);
        }

        // The resume replayed the body: the second observed time+guid are the journaled values, NOT the
        // advanced wall-clock nor a fresh identifier.
        sink.Time.Count.ShouldBe(2);
        sink.Guid.Count.ShouldBe(2);
        sink.Time[1].ShouldBe(firstTime);
        sink.Time[1].ShouldNotBe(clock.GetUtcNow());
        sink.Guid[1].ShouldBe(firstGuid);
    }

    [Fact]
    public async Task ProduceDistinctGuidsPerStep_WithinOneRun()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero));
        var sink = new PrimitiveSink();
        var crash = new CrashFlag(shouldCrash: false);
        await using var provider = BuildProvider(clock, sink, crash, guidCalls: 2);

        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
        await executor.StartAsync(WorkflowName, "inst-distinct-guids", "input", CancellationToken.None);

        sink.Guid.Count.ShouldBe(2);
        sink.Guid[0].ShouldNotBe(sink.Guid[1]);
    }

    private static ServiceProvider BuildProvider(
        FakeTimeProvider clock,
        PrimitiveSink sink,
        CrashFlag crash,
        int guidCalls = 1)
    {
        var services = new ServiceCollection();

        // Register the fake clock before AddWorkflows so its TryAddSingleton(TimeProvider.System) no-ops.
        services.AddSingleton<TimeProvider>(clock);
        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());
        services.AddSingleton(sink);
        services.AddSingleton(crash);

        services.AddWorkflows();
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
        {
            var now = await context.UtcNowAsync(cancellationToken);
            var ids = new List<Guid>(guidCalls);
            for (var i = 0; i < guidCalls; i++)
            {
                ids.Add(await context.NewGuidAsync(cancellationToken));
            }

            sink.Time.Add(now);
            sink.Guid.Add(ids[0]);
            if (guidCalls > 1)
            {
                sink.Guid.Add(ids[1]);
            }

            if (crash.ShouldCrash)
            {
                throw new InvalidOperationException("simulated process crash before workflow completion");
            }

            return (string)input;
        });

        return services.BuildServiceProvider();
    }

    private sealed class PrimitiveSink
    {
        public List<DateTimeOffset> Time { get; } = [];

        public List<Guid> Guid { get; } = [];
    }

    private sealed class CrashFlag(bool shouldCrash)
    {
        public bool ShouldCrash { get; set; } = shouldCrash;
    }
}
