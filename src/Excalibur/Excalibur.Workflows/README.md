# Excalibur.Workflows

Embedded durable-execution engine for the Excalibur framework. Run durable workflows on the event store
you already have — no separate server, sidecar, or dedicated database.

A workflow body is ordinary async C#. Every non-deterministic decision (activity calls, and — in later
waves — timers, signals, time, and identifiers) flows through an `IWorkflowContext` and is recorded to an
append-only journal. On crash and restart the engine replays that journal: activities whose completion is
already journaled return their recorded result without re-executing, and execution resumes at the first
un-journaled step.

```csharp
services
    .AddWorkflows()
    .AddActivity<ChargeCard, ChargeRequest, Receipt>("charge-card")
    .AddWorkflow("checkout", async (ctx, input, ct) =>
    {
        var order = (Order)input;
        var receipt = await ctx.CallActivityAsync<Receipt>("charge-card", order.Charge, ct);
        return receipt;
    });

// Start (or resume) a durable instance:
await executor.StartAsync("checkout", instanceId: order.Id, input: order, ct);
```

Activities run **at-least-once** and therefore **must be idempotent**: a crash between an activity running
and its completion being journaled causes it to run again on replay.
