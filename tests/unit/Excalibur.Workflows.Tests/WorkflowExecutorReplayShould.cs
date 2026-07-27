// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Independent (author != implementer) durable-execution regression lock for the replay-core engine
/// (<c>WorkflowExecutor</c>). Binds emitted behaviour — a real serialize -> append -> load journal
/// round-trip through the shipping in-memory event store — not a mocked store. Proves the load-bearing
/// durable-execution invariant: a workflow that crashes after an activity has completed and been journaled
/// resumes on a fresh executor (a process restart) WITHOUT re-invoking the already-completed activity, and
/// completes exactly once. This is the kill -> resume gate for <c>4pj13s</c> (deterministic replay) and the
/// at-least-once / effect-once gate for <c>0b4p6p</c>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowExecutorReplayShould
{
    private const string WorkflowName = "order";
    private const string ActivityName = "charge";

    [Fact]
    public async Task ResumeAfterCrash_ReplaysJournal_WithoutReInvokingCompletedActivity()
    {
        var counter = new InvocationCounter();
        var crash = new CrashFlag(shouldCrash: true);
        await using var provider = BuildProvider(counter, crash);

        const string instanceId = "inst-crash-resume";

        // Phase 1 — first execution crashes AFTER the activity has completed and been journaled, but
        // BEFORE the workflow itself completes. The activity's effect is applied exactly once here.
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            var crashEx = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await executor.StartAsync(WorkflowName, instanceId, "acct-42", CancellationToken.None));
            crashEx.Message.ShouldContain("simulated process crash");
        }

        counter.Count.ShouldBe(1, "the activity must have executed exactly once on the first run");

        // The instance is durably mid-flight: started, activity journaled, but not completed and not faulted.
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            (await executor.GetStatusAsync(instanceId, CancellationToken.None))
                .ShouldBe(WorkflowStatus.Running);
        }

        // Phase 2 — "process restart": a FRESH executor instance replays the SAME durable journal and
        // resumes. The completed activity's journaled result short-circuits re-execution.
        crash.ShouldCrash = false;
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "acct-42", CancellationToken.None);
            (await executor.GetStatusAsync(instanceId, CancellationToken.None))
                .ShouldBe(WorkflowStatus.Completed);
        }

        // LOAD-BEARING ASSERTION: across the crash + restart the completed activity was NOT re-invoked
        // (journal-native ActivityCompleted short-circuit; idempotency key instanceId:stepOrdinal).
        // A non-durable / non-replaying engine would re-run it here -> counter == 2 -> RED.
        counter.Count.ShouldBe(1, "the completed activity must not be re-invoked on replay/resume");
    }

    [Fact]
    public async Task ReStartingCompletedInstance_IsIdempotentNoOp()
    {
        var counter = new InvocationCounter();
        var crash = new CrashFlag(shouldCrash: false);
        await using var provider = BuildProvider(counter, crash);

        const string instanceId = "inst-idempotent-complete";

        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "acct-7", CancellationToken.None);
            (await executor.GetStatusAsync(instanceId, CancellationToken.None))
                .ShouldBe(WorkflowStatus.Completed);
        }

        counter.Count.ShouldBe(1);

        // Re-starting an already-completed instance is a no-op — the activity is not re-invoked.
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "acct-7", CancellationToken.None);
        }

        counter.Count.ShouldBe(1, "an already-completed instance must not re-run its activity");
    }

    private static ServiceProvider BuildProvider(InvocationCounter counter, CrashFlag crash)
    {
        var services = new ServiceCollection();

        // Real, shipping in-memory event store as the durable workflow journal (singleton, so the
        // journal survives across scopes = across a simulated process restart). Bridge the keyed
        // registration to the non-keyed IEventStore the executor injects.
        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());

        services.AddSingleton(counter);
        services.AddSingleton(crash);

        services.AddWorkflows();
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
        {
            var charged = await context.CallActivityAsync<string>(ActivityName, input, cancellationToken);
            if (crash.ShouldCrash)
            {
                throw new InvalidOperationException(
                    "simulated process crash after the activity completed, before workflow completion");
            }

            return charged;
        });
        services.AddActivity<ChargeActivity, string, string>(ActivityName);

        return services.BuildServiceProvider();
    }

    /// <summary>Counts real activity executions (increments only when the body actually runs, never on replay).</summary>
    private sealed class InvocationCounter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Increment() => Interlocked.Increment(ref _count);
    }

    /// <summary>Mutable crash toggle so the same registered workflow body crashes on the first run and resumes on the second.</summary>
    private sealed class CrashFlag(bool shouldCrash)
    {
        public bool ShouldCrash { get; set; } = shouldCrash;
    }

    private sealed class ChargeActivity(InvocationCounter counter) : IActivity<string, string>
    {
        public ValueTask<string> ExecuteAsync(string input, CancellationToken cancellationToken)
        {
            counter.Increment();
            return ValueTask.FromResult($"charged:{input}");
        }
    }
}
