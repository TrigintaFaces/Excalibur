# Excalibur.Workflows

Embedded durable-execution engine for the Excalibur framework. Run durable workflows on the event store
you already have — no separate server, sidecar, or dedicated database.

A workflow body is ordinary async C#. Every non-deterministic decision (activity calls, and — in later
waves — timers, signals, time, and identifiers) flows through an `IWorkflowContext` and is recorded to an
append-only journal. On crash and restart the engine replays that journal: activities whose completion is
already journaled return their recorded result without re-executing, and execution resumes at the first
un-journaled step.

The example below defines the application payloads and activity as well as registration and invocation.
Configure the host's real `IEventStore` and `IEventSerializer` first, then call `CheckoutWorkflow.Configure`
before building that host. Supply your own `ICardPayments` implementation; it must deduplicate charges by
`ChargeRequest.IdempotencyKey`. Call `StartAsync` with that host's service provider and a stable order ID.
The interface and records below belong to the application, not the framework.

```csharp
using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Workflows;
using Microsoft.Extensions.DependencyInjection;

public static class CheckoutWorkflow
{
    public static void Configure(IServiceCollection services, ICardPayments payments)
    {
        services.AddSingleton<ICardPayments>(payments);
        services
            .AddWorkflows(WorkflowPayloadContext.Default)
            .AddActivity<ChargeCard, ChargeRequest, Receipt>("charge-card")
            .AddWorkflow("checkout", async (ctx, input, ct) =>
            {
                var order = (Order)input;
                return await ctx.CallActivityAsync<Receipt>("charge-card", order.Charge, ct);
            });
    }

    public static ValueTask StartAsync(IServiceProvider services, Order order, CancellationToken ct) =>
        services.GetRequiredService<IWorkflowExecutor>()
            .StartAsync("checkout", instanceId: order.Id, input: order, cancellationToken: ct);
}

public sealed record Order(string Id, ChargeRequest Charge);
public sealed record ChargeRequest(string IdempotencyKey, decimal Amount);
public sealed record Receipt(string PaymentId);

public interface ICardPayments
{
    ValueTask<Receipt> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken);
}

public sealed class ChargeCard(ICardPayments payments) : IActivity<ChargeRequest, Receipt>
{
    public ValueTask<Receipt> ExecuteAsync(ChargeRequest input, CancellationToken cancellationToken) =>
        payments.ChargeAsync(input, cancellationToken);
}

[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(ChargeRequest))]
[JsonSerializable(typeof(Receipt))]
internal sealed partial class WorkflowPayloadContext : JsonSerializerContext;
```

Supply generated metadata for every additional activity, workflow and signal payload type.

The non-null resolver overload has no implicit reflection fallback; an uncovered payload fails by type.
The parameterless `AddWorkflows()` remains available for ordinary JIT hosts and preserves late
`WorkflowOptions.PayloadTypeInfoResolver` configuration, but it now carries honest trimming and dynamic-code
warnings. Even if you previously set that property to a generated context, migrate to the explicit overload
to select the annotation-free route. Options are composed once per service provider and are read-only.
Repeated identical explicit resolvers are idempotent; different explicit resolvers, or a different non-null
resolver in options, are refused. A harmless parameterless call never overrides an explicit resolver.

Activities run **at-least-once** and therefore **must be idempotent**: a crash between an activity running
and its completion being journaled causes it to run again on replay.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
