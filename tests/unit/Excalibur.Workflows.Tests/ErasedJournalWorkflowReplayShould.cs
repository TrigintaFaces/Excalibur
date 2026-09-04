// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Regression lock: a workflow whose journal has been GDPR-erased must refuse to replay, and must say
/// that the journal was erased rather than blaming corruption or an engine version mismatch.
/// </summary>
/// <remarks>
/// <para>
/// <b>Refusing is the correct behaviour here, and it is deliberately the opposite of what the stream-wide
/// readers do.</b> A journal entry is the record that stops an already-completed activity being executed
/// a second time. Skipping a tombstone would replay past a hole and re-run work that already ran, with
/// its external side effects. So unlike a projection — which can safely omit an erased subject and stay
/// correct for everyone else — a workflow replay has nothing correct left to produce and must stop.
/// </para>
/// <para>
/// <b>The lock binds the message, not just the throw.</b> The pre-fix code already refused, so a test
/// asserting only that it throws passes on the defect: the erased entry fell through to the engine's
/// unknown-journal-type error, which told the operator the journal "may be corrupt or was written by an
/// incompatible engine version". That sends someone to diagnose data corruption they do not have, after
/// they did the lawful thing. Asserting the diagnosis is the whole point of this lock.
/// </para>
/// <para>
/// <b>Real store, real erasure.</b> The journal is the shipping in-memory event store, and the tombstone
/// is produced by its own <c>EraseEventsAsync</c> path rather than hand-stubbed.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class ErasedJournalWorkflowReplayShould
{
    private const string WorkflowName = "order";
    private const string ActivityName = "charge";

    // The aggregate type the executor journals a workflow instance under. Mirrored here as a literal
    // because the production constant is internal to the workflow engine.
    private const string JournalAggregateType = "WorkflowInstance";

    [Fact]
    public async Task RefuseToReplayAnErasedJournalAndSayItWasErased()
    {
        var counter = new InvocationCounter();
        await using var provider = BuildProvider(counter);

        const string instanceId = "inst-erased-journal";

        // Phase 1 - run a workflow so it has a real, durable journal.
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "acct-42", CancellationToken.None);
            (await executor.GetStatusAsync(instanceId, CancellationToken.None))
                .ShouldBe(WorkflowStatus.Completed);
        }

        counter.Count.ShouldBe(1);

        // Phase 2 - the subject exercises their right to erasure: the journal is tombstoned in place.
        var store = provider.GetRequiredService<IEventStore>();
        var erasedCount = await ((IEventStoreErasure)store).EraseEventsAsync(
            instanceId, JournalAggregateType, Guid.NewGuid(), CancellationToken.None);
        erasedCount.ShouldBeGreaterThan(0, "the workflow journal must actually be tombstoned");

        // Phase 3 - a resume now meets a hole in the record of what already happened.
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();

            var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await executor.StartAsync(WorkflowName, instanceId, "acct-42", CancellationToken.None));

            // The diagnosis must name the erasure. Pre-fix the erased entry fell through to the generic
            // unknown-journal-event-type error and reported corruption or an engine mismatch: RED here,
            // green on a test that only asserted "it throws".
            ex.Message.ShouldContain(
                "has been erased",
                Case.Insensitive,
                "an erased journal must be reported as erased, not as corruption");
            ex.Message.ShouldNotContain(
                "Unknown workflow journal event type",
                Case.Insensitive,
                "a lawful erasure must not fall through to the unknown-type error, which blames "
                + "corruption or an incompatible engine version");
        }

        // And the refusal is what protects the side effect: the already-charged activity was not re-run.
        counter.Count.ShouldBe(
            1,
            "refusing to replay is what stops a completed activity being executed a second time");
    }

    [Fact]
    public async Task StillReportAnUnknownJournalEventTypeAsSuch()
    {
        // Over-reach guard: only the reserved erasure marker gets the erasure diagnosis. A journal entry
        // whose type is genuinely unknown must still be reported as corruption or an engine mismatch,
        // so a real defect is never dressed up as a lawful erasure. Green on both surfaces.
        var counter = new InvocationCounter();
        await using var provider = BuildProvider(counter);

        const string instanceId = "inst-unknown-journal-type";

        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "acct-42", CancellationToken.None);
        }

        // Append an entry the journal engine cannot resolve - genuine corruption, not an erasure.
        var store = provider.GetRequiredService<IEventStore>();
        var existing = await store.LoadAsync(instanceId, JournalAggregateType, CancellationToken.None);
        var corrupted = await store.AppendAsync(
            instanceId,
            JournalAggregateType,
            new List<IDomainEvent> { new NotAJournalEvent { AggregateId = instanceId } },
            existing[^1].Version,
            CancellationToken.None);
        corrupted.Success.ShouldBeTrue("the unresolvable journal entry must actually be appended");

        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();

            var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
                await executor.StartAsync(WorkflowName, instanceId, "acct-42", CancellationToken.None));

            ex.Message.ShouldContain(
                "Unknown workflow journal event type",
                Case.Insensitive,
                "a genuinely unresolvable entry keeps the corruption/engine-mismatch diagnosis");
            ex.Message.ShouldNotContain(
                "has been erased",
                Case.Insensitive,
                "genuine corruption must never be reclassified as a lawful erasure");
        }
    }

    private static ServiceProvider BuildProvider(InvocationCounter counter)
    {
        var services = new ServiceCollection();

        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());
        services.AddSingleton(counter);

        services.AddWorkflows();
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
            await context.CallActivityAsync<string>(ActivityName, input, cancellationToken));
        services.AddActivity<ChargeActivity, string, string>(ActivityName);

        return services.BuildServiceProvider();
    }

    private sealed class InvocationCounter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Increment() => Interlocked.Increment(ref _count);
    }

    private sealed class ChargeActivity(InvocationCounter counter) : IActivity<string, string>
    {
        public ValueTask<string> ExecuteAsync(string input, CancellationToken cancellationToken)
        {
            counter.Increment();
            return ValueTask.FromResult($"charged:{input}");
        }
    }

    [MessageName("Test.NotAJournalEvent")]
    private sealed class NotAJournalEvent : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();

        public required string AggregateId { get; init; }

        public long Version { get; init; }

        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;


        public IDictionary<string, object>? Metadata { get; init; }
    }
}
