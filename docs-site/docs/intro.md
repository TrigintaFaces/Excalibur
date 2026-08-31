---
sidebar_position: 1
title: Introduction to Excalibur
description: Excalibur is a high-performance .NET framework for messaging, event sourcing, CQRS, and compliance — install only the packages you need.
---

# Excalibur

**Excalibur** is a modern, high-performance .NET framework for building scalable applications. Start with `Excalibur.Dispatch` for type-safe message dispatching, then add domain modeling, event sourcing, and sagas as your architecture requires. Whether you're building a simple CRUD API or a complex distributed system, Excalibur handles the infrastructure so you can focus on business logic.

## At a Glance

| Signal | Value |
|--------|-------|
| **Automated Tests** | 55,000+ (unit, integration, conformance, performance) |
| **Packages** | 197 NuGet packages across 6 families |
| **CI Pipeline** | 10 sharded test stages, governance gates, conformance suites |
| **API Stability** | PublicAPI analyzer tracking on every package |
| **Target Framework** | .NET 10.0 |

## Tamper-Evident Audit Trails

Compliance-grade audit stores (SQL Server, PostgreSQL) **hash-chain** every audit event — each record is cryptographically linked to the one before it — so the trail is *verifiable*, not merely stored. A single altered, inserted, or deleted record breaks the chain and is detected, with the exact offending event pinpointed. One call verifies an entire time range:

```csharp
using Excalibur.Compliance;

// Verify the audit trail for the last 30 days has not been tampered with.
public static async Task<string> DescribeAuditTrailAsync(IAuditQuery audit, CancellationToken ct)
{
    AuditIntegrityResult result = await audit.VerifyChainIntegrityAsync(
        startDate: DateTimeOffset.UtcNow.AddDays(-30),
        endDate: DateTimeOffset.UtcNow,
        cancellationToken: ct);

    return result.Outcome switch
    {
        AuditIntegrityOutcome.Verified
            => $"Intact: {result.EventsVerified} events checked.",

        AuditIntegrityOutcome.ViolationsDetected
            => $"Tampered at {result.FirstViolationEventId}: {result.ViolationDescription}",

        // The window held no audit events, so nothing was checked and nothing is proven.
        // This is deliberately a separate outcome: it must never be reported as a pass.
        _ => "Not exercised: no audit events in this window.",
    };
}
```

Audit events also stream to SIEM and observability sinks — AWS, Datadog, Elasticsearch, Google Cloud, OpenSearch, Microsoft Sentinel, and Splunk — for search and alerting (these are write-only projections, not compliance stores). See **[Audit Logging](compliance/audit-logging)** for the compliance boundary, setup, and the full backend list.

## What Excalibur.Dispatch Does

`Excalibur.Dispatch` handles **how messages flow through your system**:

- **Message Dispatching** — Send actions to handlers with full type safety
- **Pipeline Behaviors** — Add cross-cutting concerns like validation, logging, and transactions
- **Multi-Transport Support** — Route messages to Kafka, RabbitMQ, Azure Service Bus, and more
- **Result Handling** — Clean success/failure patterns without exceptions
- **Context Propagation** — Automatic correlation ID and metadata tracking

## Package Families

Excalibur is one framework with focused package families. Install only what you need:

| Package Family | Purpose |
|----------------|---------|
| `Excalibur.Dispatch.*` | Messaging, pipeline, handlers, transports |
| `Excalibur.Domain` | Domain modeling (aggregates, entities, value objects) |
| `Excalibur.EventSourcing.*` | Event stores, snapshots, persistence |
| `Excalibur.Saga.*` | Sagas and process managers |
| `Excalibur.Hosting.*` | ASP.NET Core, serverless hosting templates |

See the **[Package Guide](package-guide)** for selection help, migration paths, and code examples.

## Excalibur.Dispatch vs MediatR

If you're familiar with MediatR, you'll feel right at home. Here's how concepts map:

| MediatR | Dispatch | Notes |
|---------|----------|-------|
| `IRequest` | `IDispatchAction` | Actions without return value |
| `IRequest<TResponse>` | `IDispatchAction<TResult>` | Actions with return value |
| `IRequestHandler<T>` | `IActionHandler<T>` | Handler without return |
| `IRequestHandler<T, R>` | `IActionHandler<T, R>` | Handler with return |
| `INotification` | `IDispatchEvent` | Events/notifications |
| `INotificationHandler<T>` | `IEventHandler<T>` | Event handlers |
| `IMediator` | `IDispatcher` | Message dispatcher |

**Key improvements over MediatR (all included in `Excalibur.Dispatch`):**

- **Results carry to the HTTP edge, no boilerplate** — a handler returns a railway-style `IMessageResult`, and `.ToHttpResult()` maps it to the correct response automatically: **403** when authorization failed, **400** when validation failed, `ProblemDetails` (with its status) for structured errors, **500** for unstructured failures, and **200 / 202 / 201 / 204** on success. With MediatR you hand-write that mapping in every endpoint.
- **Pluggable AuthN/AuthZ/Audit, fail-closed by default** — authorization speaks native ASP.NET Core `[Authorize]` / `AuthorizationHandler`, and denies when the decision is missing rather than falling open.
- Automatic context propagation for distributed tracing
- Multi-transport routing support
- Performance optimizations for high-throughput scenarios

```csharp
// A handler returns a result; the endpoint maps it to the right HTTP status with one call.
app.MapPost("/orders", async (CreateOrderRequest req, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(new CreateOrderAction(req.CustomerId, req.Items), ct);
    return result.ToHttpResult(); // 202 on success; 403/400/ProblemDetails/500 on failure — no manual mapping
});
```

## Quick Start

### 1. Install the Package

```bash
dotnet add package Excalibur.Dispatch
```

### 2. Define an Action

```csharp
using Excalibur.Dispatch;

// Action without return value
public record CreateOrderAction(string CustomerId, List<string> Items) : IDispatchAction;

// Action with return value
public record GetOrderAction(Guid OrderId) : IDispatchAction<Order>;
```

### 3. Create a Handler

```csharp
using Excalibur.Dispatch.Delivery;

public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IOrderRepository _repository;

    public CreateOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(
        CreateOrderAction action,
        CancellationToken cancellationToken)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = action.CustomerId,
            Items = action.Items
        };

        await _repository.SaveAsync(order, cancellationToken);
    }
}
```

### 4. Register and Dispatch

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

var app = builder.Build();

// In your controller or service
public class OrderController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public OrderController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var action = new CreateOrderAction(request.CustomerId, request.Items);
        var result = await _dispatcher.DispatchAsync(action, cancellationToken);

        if (result.IsSuccess)
            return Ok();

        return Problem(result.ErrorMessage, statusCode: result.ProblemDetails?.Status);
    }
}
```

:::caution Do not map a failed result to 400

A failed `IMessageResult` means your handler **ran** and reported a failure — not, by default, that
the caller sent a bad request. Hard-coding `BadRequest(...)` reports a server-side fault to the
caller as their own mistake, so they retry a request that can never succeed, or abandon one that
would have.

`result.ProblemDetails.Status` carries the status the framework determined for that failure. With
the pipeline's exception mapping configured (`UseExceptionMapping()`), a validation failure arrives
as **400** and an authorization failure as **403**; a handler that threw with nothing mapping it
arrives as **500**. When no status was determined, `ProblemDetails` is `null` and `Problem(...)`
falls back to 500 — the safe direction, and never the caller's fault by accident.
:::

:::tip Production Ready

The setup above is a complete, production-ready Dispatch application.
Everything below describes **optional capabilities** you can add later.
For a laser-focused guide, see **[Dispatch Only](./getting-started/dispatch-only.md)**.
:::

## Optional Capabilities

Add these **if and when** you need them — each is independent and opt-in:

- **Domain modeling** — Add `Excalibur.Domain` if you need aggregates, entities, and value objects
- **Event sourcing** — Add `Excalibur.EventSourcing.*` if you need event replay, snapshots, and audit trails
- **Workflows** — Add `Excalibur.Saga.*` if you need multi-step orchestrated processes
- **Unified builder** — Add `Excalibur.Hosting` if you want a single `AddExcalibur()` entry point for all subsystems

Because all packages share the same `Excalibur.*` namespace, you never rewrite existing code when adding new capabilities.

<details>
<summary>Complete Package Reference (70+ packages)</summary>

## Package Overview

### Core Packages

| Package | Description |
|---------|-------------|
| `Excalibur.Dispatch` | Core dispatcher, pipelines, middleware |
| `Excalibur.Dispatch.Abstractions` | Public interfaces (`IDispatchAction`, `IDispatcher`) |

### Transport Packages

| Package | Description |
|---------|-------------|
| `Excalibur.Dispatch.Transport.Kafka` | Apache Kafka transport |
| `Excalibur.Dispatch.Transport.RabbitMQ` | RabbitMQ transport |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | Azure Service Bus transport |
| `Excalibur.Dispatch.Transport.AwsSqs` | AWS SQS transport |
| `Excalibur.Dispatch.Transport.GooglePubSub` | Google Pub/Sub transport |

### Hosting Packages

| Package | Description |
|---------|-------------|
| `Excalibur.Dispatch.Hosting.AspNetCore` | ASP.NET Core integration |
| `Excalibur.Dispatch.Hosting.AzureFunctions` | Azure Functions hosting |
| `Excalibur.Dispatch.Hosting.AwsLambda` | AWS Lambda hosting |
| `Excalibur.Dispatch.Hosting.GoogleCloudFunctions` | Google Cloud Functions hosting |
| `Excalibur.Dispatch.Hosting.Serverless.Abstractions` | Serverless abstractions |

### Serialization Packages

| Package | Description |
|---------|-------------|
| `Excalibur.Dispatch.Serialization.MemoryPack` | High-performance binary serialization (opt-in) |
| `Excalibur.Dispatch.Serialization.MessagePack` | MessagePack serialization |
| `Excalibur.Dispatch.Serialization.Protobuf` | Protocol Buffers serialization |

### Security & Compliance Packages

| Package | Description |
|---------|-------------|
| `Excalibur.Security` | Core security infrastructure |
| `Excalibur.AuditLogging` | Comprehensive audit logging |
| `Excalibur.AuditLogging.Datadog` | Datadog audit export |
| `Excalibur.AuditLogging.Sentinel` | Azure Sentinel integration |
| `Excalibur.AuditLogging.Splunk` | Splunk audit export |
| `Excalibur.AuditLogging.SqlServer` | SQL Server audit store |
| `Excalibur.Compliance` | Regulatory compliance framework |
| `Excalibur.Compliance.Abstractions` | Compliance abstractions |
| `Excalibur.Compliance.Aws` | AWS compliance integration |
| `Excalibur.Compliance.Azure` | Azure compliance integration |
| `Excalibur.Compliance.Vault` | HashiCorp Vault integration |

### Operations Packages

| Package | Description |
|---------|-------------|
| `Excalibur.Dispatch.Observability` | OpenTelemetry integration |
| `Excalibur.Dispatch.Resilience.Polly` | Polly integration for resilience |
| `Excalibur.Dispatch.Caching` | Caching infrastructure |
| `Excalibur.Dispatch.Validation.FluentValidation` | FluentValidation integration |

### Patterns Packages

| Package | Description |
|---------|-------------|
| `Excalibur.Dispatch.Patterns` | Messaging patterns (Outbox, ClaimCheck, etc.) |
| `Excalibur.Dispatch.Patterns.Azure` | Azure-specific patterns |
| `Excalibur.Dispatch.Patterns.ClaimCheck.InMemory` | In-memory claim check store |
| `Excalibur.Dispatch.Patterns.Hosting.Json` | JSON hosting patterns |

### Tooling Packages

| Package | Description |
|---------|-------------|
| `Excalibur.Dispatch.Analyzers` | Roslyn analyzers |
| `Excalibur.Dispatch.SourceGenerators` | Source generators |
| `Excalibur.Dispatch.LeaderElection.Abstractions` | Leader election abstractions |

</details>

## Next Steps

- [Getting Started](./getting-started/) — Full tutorial with working code
- [Handlers](handlers/) — Learn about action and event handlers
- [Pipeline](pipeline/) — Understand middleware and behaviors
- [Configuration](core-concepts/configuration.md) — Configure Dispatch for your needs
- [Transports](transports/) — Multi-transport routing
- [Package Guide](package-guide) — Choose the right packages for your scenario
- [Support](support.md) — Get help, report bugs, security policy

## See Also

- [Package Guide](package-guide) - Decision guide for choosing which Excalibur packages to install
- [Core Concepts](./core-concepts/index.md) - Actions, handlers, results, and message context
- [Performance Overview](./performance/index.md) - Benchmarks and optimization strategies


