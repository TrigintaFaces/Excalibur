// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Replay-hardening regression locks:
/// <list type="bullet">
/// <item><b>kzuulf (W2-L7)</b> — a fired durable timer is read from the journal on replay, never
/// recomputed: resuming a workflow whose timer already fired appends no second <c>TimerFired</c> and does
/// not re-wait.</item>
/// <item><b>8z84ru (W3-L13)</b> — the runtime non-determinism guard fails loud when a deterministic
/// primitive that escaped the analyzer changes the context-call shape at an already-journaled step (a
/// <c>UtcNowAsync</c> step replayed as a <c>NewGuidAsync</c> step).</item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowReplayHardeningShould
{
    private const string WorkflowName = "replay-hardening";

    [Fact]
    public async Task ReadFiredTimerFromJournalOnReplay_WithoutReFiring()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero));
        var crash = new CrashFlag(shouldCrash: true);
        await using var provider = BuildTimerProvider(clock, crash);
        var journal = provider.GetRequiredService<IEventStore>();
        const string instanceId = "inst-timer-replay";

        // Phase 1 — the timer fires (zero delay), the following activity runs, then the body crashes before
        // completion. TimerFired is journaled exactly once.
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await executor.StartAsync(WorkflowName, instanceId, "in", CancellationToken.None));
        }

        (await CountAsync(journal, instanceId, nameof(TimerFired))).ShouldBe(1);

        // Phase 2 — resume. The timer step must short-circuit off the journaled TimerFired: no second fire,
        // no re-wait. A recompute-on-replay mutant would append a second TimerFired here.
        crash.ShouldCrash = false;
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "in", CancellationToken.None);
            (await executor.GetStatusAsync(instanceId, CancellationToken.None))
                .ShouldBe(WorkflowStatus.Completed);
        }

        (await CountAsync(journal, instanceId, nameof(TimerFired)))
            .ShouldBe(1, "a fired timer is read from the journal on replay, never re-fired");
        (await CountAsync(journal, instanceId, nameof(TimerCreated))).ShouldBe(1);
    }

    [Fact]
    public async Task FailLoud_WhenDeterministicPrimitiveShapeDivergesOnReplay()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 7, 12, 0, 0, TimeSpan.Zero));
        var op = new PrimitiveSwitch(useGuid: false);
        var crash = new CrashFlag(shouldCrash: true);
        await using var provider = BuildPrimitiveProvider(clock, op, crash);
        const string instanceId = "inst-primitive-divergence";

        // Phase 1 — step 0 is a UtcNowAsync read, journaled, then the body crashes.
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await executor.StartAsync(WorkflowName, instanceId, "in", CancellationToken.None));
        }

        // Phase 2 — the body is "edited" so step 0 is now a NewGuidAsync generation. Resuming must fail loud
        // rather than replay a recorded time read as an identifier.
        op.UseGuid = true;
        crash.ShouldCrash = false;
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            var ex = await Should.ThrowAsync<WorkflowNonDeterminismException>(async () =>
                await executor.StartAsync(WorkflowName, instanceId, "in", CancellationToken.None));
            ex.StepOrdinal.ShouldBe(0);
            ex.Expected.ShouldBe("utcnow");
            ex.Actual.ShouldBe("newguid");
        }
    }

    private static ServiceProvider BuildTimerProvider(FakeTimeProvider clock, CrashFlag crash)
    {
        var services = BaseServices(clock);
        services.AddSingleton(crash);
        services.AddWorkflows();
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
        {
            await context.CreateTimerAsync(TimeSpan.Zero, cancellationToken);
            var r = await context.CallActivityAsync<string>("after", input, cancellationToken);
            if (crash.ShouldCrash)
            {
                throw new InvalidOperationException("simulated crash before completion");
            }

            return r;
        });
        services.AddActivity<PassthroughActivity, string, string>("after");
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildPrimitiveProvider(
        FakeTimeProvider clock,
        PrimitiveSwitch op,
        CrashFlag crash)
    {
        var services = BaseServices(clock);
        services.AddSingleton(crash);
        services.AddWorkflows();
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
        {
            // The "escaped" branch selects which deterministic primitive runs at step 0; editing it between
            // deploys changes the journaled step shape.
            object value = op.UseGuid
                ? await context.NewGuidAsync(cancellationToken)
                : await context.UtcNowAsync(cancellationToken);
            if (crash.ShouldCrash)
            {
                throw new InvalidOperationException("simulated crash before completion");
            }

            return value.ToString();
        });
        return services.BuildServiceProvider();
    }

    private static ServiceCollection BaseServices(FakeTimeProvider clock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());
        return services;
    }

    private static async ValueTask<int> CountAsync(IEventStore journal, string instanceId, string eventType)
    {
        var stored = await journal.LoadAsync(instanceId, "WorkflowInstance", CancellationToken.None);
        return stored.Count(se => se.EventType.Contains(eventType, StringComparison.Ordinal));
    }

    private sealed class CrashFlag(bool shouldCrash)
    {
        public bool ShouldCrash { get; set; } = shouldCrash;
    }

    private sealed class PrimitiveSwitch(bool useGuid)
    {
        public bool UseGuid { get; set; } = useGuid;
    }

    private sealed class PassthroughActivity : IActivity<string, string>
    {
        public ValueTask<string> ExecuteAsync(string input, CancellationToken cancellationToken) =>
            ValueTask.FromResult($"done:{input}");
    }
}
