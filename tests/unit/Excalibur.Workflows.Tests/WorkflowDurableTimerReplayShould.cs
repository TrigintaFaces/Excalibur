// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Independent (author != implementer) durable-execution regression lock for the durable timer primitive
/// (<c>7sk1xe</c> / W2-L6). Binds emitted behaviour through a real serialize -> append -> load journal
/// round-trip on the shipping in-memory event store (singleton = survives across scopes = a process
/// restart) and a controllable <see cref="FakeTimeProvider"/>. Proves the two load-bearing invariants:
/// <list type="number">
/// <item>The timer <b>waits until its due time</b> — a workflow parked on <c>CreateTimerAsync</c> does not
/// advance past it while the clock is short of the due instant (RED on a fire-immediately impl, e.g. one
/// anchored to an unstamped <c>TimerCreated</c> with a default <c>OccurredAt</c>).</item>
/// <item>The timer <b>fires exactly once across a crash + restart</b> — a crash mid-wait leaves
/// <c>TimerCreated</c> durably journaled with no <c>TimerFired</c>; a fresh executor resumes the SAME
/// deadline and fires exactly one <c>TimerFired</c> (RED on any in-memory-only impl that loses the journal
/// on restart and re-waits or double-fires).</item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowDurableTimerReplayShould
{
    private const string WorkflowName = "delayed-order";
    private static readonly TimeSpan TimerDelay = TimeSpan.FromDays(7);

    [Fact]
    public async Task WaitUntilDue_ThenFireExactlyOnce_AcrossRestart()
    {
        var startUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(startUtc);
        var counter = new InvocationCounter();
        await using var provider = BuildProvider(timeProvider, counter);

        // The event store is a singleton so the durable journal survives across scopes = a process restart.
        var journal = provider.GetRequiredService<IEventStore>();
        const string instanceId = "inst-timer-crash-resume";

        // Phase 1 — start: the body parks on the durable timer. The clock is at T0 (7 days short of due),
        // so the timer must WAIT — the body must not pass it, and no TimerFired may be journaled.
        using var crashCts = new CancellationTokenSource();
        var firstRun = Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "acct-1", crashCts.Token);
        });

        // Wait until TimerCreated is durably journaled (the body has reached and persisted the timer).
        await WaitUntilAsync(
            async () => await CountJournaledAsync(journal, instanceId, nameof(TimerCreated)) == 1,
            "the durable timer's TimerCreated should be journaled");

        // Binds invariant 1 (waits until due / no fire-immediately): the parked timer has NOT fired and the
        // body has NOT advanced past it while the clock is short of the due instant.
        (await CountJournaledAsync(journal, instanceId, nameof(TimerFired)))
            .ShouldBe(0, "a durable timer must WAIT until its due time, not fire immediately");
        counter.Count.ShouldBe(0, "the workflow body must be parked on the durable timer, not past it");

        // Kill mid-window — a process crash before the timer fires (cancel the in-flight wait). StartAsync
        // does not catch, so no WorkflowCompleted is journaled: the instance stays durably mid-flight.
        await crashCts.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => firstRun);

        // Durably mid-flight after the crash: TimerCreated persisted, TimerFired NOT.
        (await CountJournaledAsync(journal, instanceId, nameof(TimerCreated))).ShouldBe(1);
        (await CountJournaledAsync(journal, instanceId, nameof(TimerFired))).ShouldBe(0);

        // Phase 2 — process restart: a FRESH executor resumes the SAME durable journal. Advance the clock
        // past the due time anchored to the journaled TimerCreated instant (survives restart).
        timeProvider.Advance(TimerDelay + TimeSpan.FromDays(1));
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "acct-1", CancellationToken.None);
            (await executor.GetStatusAsync(instanceId, CancellationToken.None))
                .ShouldBe(WorkflowStatus.Completed);
        }

        // LOAD-BEARING (invariant 2): across the crash + restart the durable timer fired EXACTLY ONCE and the
        // body ran past it exactly once. An in-memory-only impl loses the journal on restart -> re-waits
        // (never completes with the clock advanced) or double-fires (TimerFired == 2) -> RED.
        (await CountJournaledAsync(journal, instanceId, nameof(TimerFired)))
            .ShouldBe(1, "a durable timer must fire exactly once across a crash + restart");
        counter.Count.ShouldBe(1, "the workflow body must advance past the timer exactly once");
    }

    private static ServiceProvider BuildProvider(FakeTimeProvider timeProvider, InvocationCounter counter)
    {
        var services = new ServiceCollection();

        // Real shipping in-memory event store as the durable journal (singleton so it survives across scopes
        // = across a simulated process restart). Bridge the keyed registration to the non-keyed IEventStore.
        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());

        // Controllable clock — the durable timer schedules its wake on this, so the test advances time
        // deterministically (no wall-clock). Registered as TimeProvider before AddWorkflows().
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton(counter);

        services.AddWorkflows();
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
        {
            await context.CreateTimerAsync(TimerDelay, cancellationToken);

            // Runs only once the durable timer has fired — the "past the timer" side effect.
            counter.Increment();
            return input;
        });

        return services.BuildServiceProvider();
    }

    private static async Task<int> CountJournaledAsync(
        IEventStore journal,
        string instanceId,
        string eventTypeName)
    {
        // StoredEvent.EventType is the assembly-qualified type name written by the store, which contains the
        // journal event's type name — a reliable discriminator for counting without deserialization.
        var stored = await journal.LoadAsync(instanceId, "WorkflowInstance", CancellationToken.None);
        return stored.Count(se => se.EventType.Contains(eventTypeName, StringComparison.Ordinal));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string because)
    {
        // Poll a durable condition with a bounded timeout (deterministic, no fixed sleep).
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new Xunit.Sdk.XunitException($"Timed out waiting: {because}.");
    }

    /// <summary>Counts real "past the timer" body executions (never increments while the timer is parked).</summary>
    private sealed class InvocationCounter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Increment() => Interlocked.Increment(ref _count);
    }
}
