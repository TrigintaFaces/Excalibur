---
sidebar_position: 1
title: Introduction to Excalibur
description: Excalibur is a modular .NET framework for messaging, event sourcing, CQRS, and compliance — start with one package and add only what you need.
---

# Excalibur

**Excalibur** is a modular, high-performance .NET framework for building message-driven applications.
Start with `Excalibur.Dispatch` as a type-safe message dispatcher, then add domain modeling, event
sourcing, sagas, or compliance as your architecture requires — every package beyond the first is
optional, and they all share one `Excalibur.*` namespace, so adding one never means rewriting what
you already have.

:::caution 10.0.0 is a pre-release

The API is not frozen until RC, and some shipped behaviour does not yet match what this
documentation describes. Read **[Known issues](./known-issues.md)** before you adopt Excalibur
for anything you depend on.
:::

## Key features

Messaging and the pipeline:

- **[Message dispatching](./core-concepts/actions-and-handlers.md)** — send actions and events to
  handlers with full type safety, no reflection on the hot path
- **[Results and errors](./core-concepts/results-and-errors.md)** — railway-style success/failure
  without exceptions, mapped to the correct HTTP status by one call
- **[Pipeline and middleware](./pipeline/index.md)** — validation, logging, transactions,
  authorization and [more](./middleware/built-in.md) as composable behaviours
- **[Transports](./transports/index.md)** — Kafka, RabbitMQ, Azure Service Bus, AWS SQS,
  Google Pub/Sub, Pulsar, MQTT, IBM MQ, gRPC, and in-memory
- **[Message context](./core-concepts/message-context.md)** — correlation and causation IDs
  propagated automatically for distributed tracing

Domain and persistence:

- **[Domain modeling](./domain-modeling/index.md)** — aggregates, entities, value objects
- **[Event sourcing](./event-sourcing/index.md)** — event stores, snapshots, upcasting, replay
- **[CQRS and projections](./cqrs/index.md)** — read models built from the event stream
- **[Sagas](./sagas/index.md)** and **[durable workflows](./event-sourcing/durable-execution.md)** —
  long-running, multi-step processes that survive restarts
- **[Data access](./data-access/index.md)** — Dapper-based access across SQL Server, PostgreSQL,
  MySQL, Cosmos DB, DynamoDB, MongoDB, Spanner, Firestore, Redis, Elasticsearch and OpenSearch

Reliability and coordination:

- **[Outbox](./patterns/outbox.md)** and **[inbox](./patterns/inbox.md)** — reliable send and
  idempotent receive
- **[Change data capture](./patterns/cdc.md)** — stream database changes as messages
- **[Background jobs](./patterns/jobs.md)** — scheduled and recurring work
- **[Leader election](./leader-election/index.md)** — single-leader coordination across instances
- **[Caching](./performance/caching.md)** — opt-in caching infrastructure

Security, compliance and operations:

- **[Authorization](./security/authorization.md)** — fail-closed by default, speaking native
  ASP.NET Core `[Authorize]` and `AuthorizationHandler`
- **[Audit logging](./compliance/audit-logging.md)** — hash-chained, tamper-evident audit trails
  with SIEM export
- **[Compliance](./compliance/index.md)** — GDPR, SOC 2 and FedRAMP support
- **[Multi-tenancy](./multi-tenancy.md)** — tenant-scoped reads and writes
- **[Observability](./observability/index.md)** — OpenTelemetry traces, metrics and health checks
- **[Operations](./operations/index.md)** — diagnostics and the operations dashboard

Build and ship:

- **[Source generators](./source-generators/index.md)** — compile-time handler discovery, Native
  AOT friendly, no runtime reflection
- **[Testing](./testing/index.md)** — fixtures and conformance kits for your own handlers
- **[Deployment](./deployment/index.md)** — ASP.NET Core, Azure Functions, AWS Lambda, Google Cloud
  Functions, Docker, Kubernetes and on-premises

That is the whole surface. All **194 packages** are listed by category in the
**[Package Matrix](./reference/package-matrix.md)**, including the metapackages that configure an
entire provider in one line. For per-type and per-member detail, see the
**[API reference](pathname:///api/)**.

## Why choose Excalibur?

- **Drop-in replacement for MediatR.** The concepts map one-to-one, and the
  [migration guide](./migration/from-mediatr.md) covers both a compatibility-shim route and a
  full port. Guides also exist for
  [MassTransit](./migration/from-masstransit.md) and [NServiceBus](./migration/from-nservicebus.md).
- **Results reach the HTTP edge without boilerplate.** A handler returns an `IMessageResult`;
  `.ToHttpResult()` maps it to 403 for an authorization failure, 400 for validation,
  `ProblemDetails` for structured errors and 500 for the rest. You write that mapping once, not in
  every endpoint.
- **Low allocation on the hot path.** 96 B allocated per command and roughly 60 ns of dispatch
  overhead, each measured on a dated, reproducible benchmark — see
  [Performance](./performance/index.md) for the method.
- **Secure by default.** Authorization denies when a decision is missing rather than falling open,
  and audit trails are hash-chained so tampering is detectable rather than merely discouraged.
- **Modular, not monolithic.** Install `Excalibur.Dispatch` alone and nothing else comes with it.
  Every further capability is a separate package you opt into.
- **Built for AOT.** Handler discovery is source-generated, so trimming and Native AOT work without
  runtime reflection.

## Quick start

Two commands and one file. **No database and no message broker** — messages dispatch in-process by
default, so nothing below needs infrastructure to run.

### 1. Install

```bash
dotnet add package Excalibur.Dispatch.Hosting.AspNetCore
```

`Excalibur.Dispatch` and `Excalibur.Dispatch.Abstractions` come in transitively. Building a console
app or worker with no HTTP edge? Install `Excalibur.Dispatch` on its own instead.

### 2. Write `Program.cs`

This is the entire application. Copy it as-is.

```csharp runnable title="Program.cs"
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Hosting.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Auto-discovers handlers in this assembly.
// Handlers live in another project? Name it: AddDispatch(typeof(MyHandler).Assembly)
builder.Services.AddDispatch();

var app = builder.Build();

app.MapPost("/orders", async (CreateOrderRequest req, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(new CreateOrderAction(req.CustomerId, req.Items), ct);

    // Maps the result to the correct status: 202 on success,
    // 403 / 400 / ProblemDetails / 500 on failure. No hand-written mapping.
    return result.ToHttpResult();
});

app.Run();

// --- The action, its handler, and the request body ---

public record CreateOrderAction(string CustomerId, List<string> Items) : IDispatchAction;

public record CreateOrderRequest(string CustomerId, List<string> Items);

public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    public Task HandleAsync(CreateOrderAction action, CancellationToken cancellationToken)
    {
        // Your business logic here.
        return Task.CompletedTask;
    }
}
```

### 3. Run it

```bash
dotnet run
```

```bash
curl -X POST http://localhost:5000/orders \
  -H 'Content-Type: application/json' \
  -d '{"customerId":"acme","items":["widget"]}'
```

That is the whole loop — an action, a handler, and a result mapped to the right HTTP status with one
call. Everything else in Excalibur is optional and additive.

## Next steps

| If you are… | Start here |
|---|---|
| New to Excalibur | **[Dispatch Only](./getting-started/dispatch-only.md)** — the same file above, extended with a query, an event and middleware. Still one package, still no infrastructure. |
| Working through a tutorial | **[Getting Started](./getting-started/index.md)**, then the [order system](./getting-started/order-system-tutorial.md) and [event sourcing](./getting-started/event-sourcing-tutorial.md) tutorials |
| Coming from another library | **[MediatR](./migration/from-mediatr.md)** · **[MassTransit](./migration/from-masstransit.md)** · **[NServiceBus](./migration/from-nservicebus.md)** |
| Deciding what to install | **[Pick Your Stack](./pick-your-stack.md)** for a guided choice, **[Package Guide](./package-guide.md)** for the reasoning, **[Package Matrix](./reference/package-matrix.md)** for all 194 packages |
| Learning the fundamentals | **[Core Concepts](./core-concepts/index.md)** — actions, handlers, results, context and configuration |
| Evaluating Excalibur | **[Performance](./performance/index.md)**, **[Architecture](./architecture/index.md)** and **[Known issues](./known-issues.md)** |
| Looking for help | **[Support](./support.md)** — how to get help, report a bug, or raise a security issue |
