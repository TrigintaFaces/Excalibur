// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Regression lock for iknhdg — external signal delivery to a running workflow instance. Delivery is
/// exactly-once into the journal (dedup on the producer-supplied signal id, consumed at a deterministic step
/// ordinal), wakes a parked instance, and replays from the journal without re-consuming from the inbox.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class WorkflowSignalDeliveryShould
{
    private const string WorkflowName = "approval";
    private const string SignalName = "Approved";

    [Fact]
    public async Task WakeAParkedInstance_AndReturnTheSignalPayload()
    {
        var sink = new Sink();
        await using var provider = BuildProvider(sink, crash: new CrashFlag(false));
        const string instanceId = "inst-wake";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // The body parks on WaitForSignalAsync; run it on a background task so the test can deliver.
        var run = Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "in", cts.Token);
        });

        // Wait until the instance is durably started (parked at the signal), then deliver.
        await WaitUntilAsync(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            return await executor.GetStateAsync(instanceId, CancellationToken.None) is not null;
        });

        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.SignalAsync(instanceId, SignalName, "sig-1", "yes", CancellationToken.None);
        }

        await run;
        sink.Values.ShouldContain("yes");
    }

    [Fact]
    public async Task DedupDuplicateDelivery_JournalingTheSignalExactlyOnce()
    {
        var sink = new Sink();
        await using var provider = BuildProvider(sink, crash: new CrashFlag(false));
        var journal = provider.GetRequiredService<IEventStore>();
        const string instanceId = "inst-dedup";

        // Deliver the same signal id twice BEFORE starting — the second admission is a no-op.
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.SignalAsync(instanceId, SignalName, "sig-dup", "once", CancellationToken.None);
            await executor.SignalAsync(instanceId, SignalName, "sig-dup", "twice", CancellationToken.None);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "in", CancellationToken.None);
        }

        // Exactly one SignalReceived journaled; the payload is the first (only) admitted signal.
        (await CountAsync(journal, instanceId, nameof(SignalReceived))).ShouldBe(1);
        sink.Values.ShouldHaveSingleItem().ShouldBe("once");
    }

    [Fact]
    public async Task ReplayConsumedSignalFromJournal_WithoutReConsuming()
    {
        var sink = new Sink();
        var crash = new CrashFlag(shouldCrash: true);
        await using var provider = BuildProvider(sink, crash);
        var journal = provider.GetRequiredService<IEventStore>();
        const string instanceId = "inst-replay-signal";

        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.SignalAsync(instanceId, SignalName, "sig-r", "approved", CancellationToken.None);
        }

        // Phase 1 — consume the signal, then crash before completion. SignalReceived is journaled.
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await executor.StartAsync(WorkflowName, instanceId, "in", CancellationToken.None));
        }

        (await CountAsync(journal, instanceId, nameof(SignalReceived))).ShouldBe(1);

        // Phase 2 — resume: the signal replays from the journal (no re-consume), and the instance completes.
        crash.ShouldCrash = false;
        await using (var scope = provider.CreateAsyncScope())
        {
            var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
            await executor.StartAsync(WorkflowName, instanceId, "in", CancellationToken.None);
            (await executor.GetStatusAsync(instanceId, CancellationToken.None))
                .ShouldBe(WorkflowStatus.Completed);
        }

        (await CountAsync(journal, instanceId, nameof(SignalReceived)))
            .ShouldBe(1, "a consumed signal is replayed from the journal, never re-consumed");
    }

    private static ServiceProvider BuildProvider(Sink sink, CrashFlag crash)
    {
        var services = new ServiceCollection();
        services.AddInMemoryEventStore();
        services.AddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("inmemory"));
        services.AddSingleton<IEventSerializer>(new JsonEventSerializer());
        services.AddSingleton(sink);
        services.AddSingleton(crash);
        services.AddWorkflows();
        services.AddWorkflow(WorkflowName, async (context, input, cancellationToken) =>
        {
            var payload = await context.WaitForSignalAsync<string>(SignalName, cancellationToken);
            sink.Values.Add(payload);
            if (crash.ShouldCrash)
            {
                throw new InvalidOperationException("simulated crash before completion");
            }

            return payload;
        });
        return services.BuildServiceProvider();
    }

    private static async ValueTask<int> CountAsync(IEventStore journal, string instanceId, string eventType)
    {
        var stored = await journal.LoadAsync(instanceId, "WorkflowInstance", CancellationToken.None);
        return stored.Count(se => se.EventType.Contains(eventType, StringComparison.Ordinal));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("Condition not met within the timeout.");
    }

    private sealed class Sink
    {
        public List<string> Values { get; } = [];
    }

    private sealed class CrashFlag(bool shouldCrash)
    {
        public bool ShouldCrash { get; set; } = shouldCrash;
    }
}
