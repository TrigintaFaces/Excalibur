---
sidebar_position: 1
title: Introduction to Dispatch
description: Dispatch is a high-performance .NET messaging framework for building scalable applications with type-safe message dispatching, pipeline behaviors, and multi-transport support.
---

# Dispatch

**Dispatch** is a modern, high-performance .NET messaging framework that provides clean, type-safe message dispatching for your applications. Whether you're building a simple CRUD API or a complex distributed system, Dispatch handles the messaging infrastructure so you can focus on business logic.

## What Dispatch Does

Dispatch handles **how messages flow through your system**:

- **Message Dispatching** — Send actions to handlers with full type safety
- **Pipeline Behaviors** — Add cross-cutting concerns like validation, logging, and transactions
- **Multi-Transport Support** — Route messages to Kafka, RabbitMQ, Azure Service Bus, and more
- **Result Handling** — Clean success/failure patterns without exceptions
- **Context Propagation** — Automatic correlation ID and metadata tracking

## Dispatch vs Excalibur

Dispatch provides the messaging pipeline, security, compliance, and transport infrastructure. When you need aggregates, event sourcing, sagas, or persistence, layer **Excalibur** on top of Excalibur.Dispatch. Excalibur packages (e.g., `Excalibur.Domain`, `Excalibur.EventSourcing`) call `AddDispatch()` internally so you keep the same handler code.

See the **[Dispatch vs Excalibur Decision Guide](./dispatch-vs-excalibur.md)** for package selection, migration paths, and code examples.

## Dispatch vs MediatR

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

**Key improvements over MediatR:**

- Built-in result types with error handling
- Automatic context propagation for distributed tracing
- Multi-transport routing support
- Performance optimizations for high-throughput scenarios

## Quick Start

### 1. Install the Package

```bash
dotnet add package Excalibur.Dispatch
dotnet add package Excalibur.Dispatch.Abstractions
```

### 2. Define an Action

```csharp
using Excalibur.Dispatch.Abstractions;

// Action without return value
public record CreateOrderAction(string CustomerId, List<string> Items) : IDispatchAction;

// Action with return value
public record GetOrderAction(Guid OrderId) : IDispatchAction<Order>;
```

### 3. Create a Handler

```csharp
using Excalibur.Dispatch.Abstractions.Delivery;

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

        return BadRequest(result.ErrorMessage);
    }
}
```

## Dispatch vs Excalibur

This framework is actually two complementary frameworks in one repository:

| Framework | Responsibility | When to Use |
|-----------|----------------|-------------|
| **Dispatch** | Message routing, pipelines, transports | You need to send messages between components |
| **Excalibur** | Domain modeling, persistence, event sourcing | You need to model domain logic and persist state |

**Dispatch handles HOW messages flow.** It doesn't care about what's in them or how they're stored.

**Excalibur handles WHAT gets persisted.** It provides domain building blocks like aggregates, event sourcing, and repositories.

Most applications use both:
1. Dispatch routes commands/queries to handlers
2. Excalibur provides the domain model and persistence layer

For domain modeling and event sourcing, see [Excalibur Documentation](/docs/intro).

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
| `Excalibur.Dispatch.Serialization.MemoryPack` | High-performance binary serialization (default) |
| `Excalibur.Dispatch.Serialization.MessagePack` | MessagePack serialization |
| `Excalibur.Dispatch.Serialization.Protobuf` | Protocol Buffers serialization |

### Security & Compliance Packages

| Package | Description |
|---------|-------------|
| `Excalibur.Dispatch.Security` | Core security infrastructure |
| `Excalibur.Dispatch.AuditLogging` | Comprehensive audit logging |
| `Excalibur.Dispatch.AuditLogging.Datadog` | Datadog audit export |
| `Excalibur.Dispatch.AuditLogging.Sentinel` | Azure Sentinel integration |
| `Excalibur.Dispatch.AuditLogging.Splunk` | Splunk audit export |
| `Excalibur.Dispatch.AuditLogging.SqlServer` | SQL Server audit store |
| `Excalibur.Dispatch.Compliance` | Regulatory compliance framework |
| `Excalibur.Dispatch.Compliance.Abstractions` | Compliance abstractions |
| `Excalibur.Dispatch.Compliance.Aws` | AWS compliance integration |
| `Excalibur.Dispatch.Compliance.Azure` | Azure compliance integration |
| `Excalibur.Dispatch.Compliance.Vault` | HashiCorp Vault integration |

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

## Next Steps

- [Getting Started](./getting-started/) — Full tutorial with working code
- [Handlers](handlers/) — Learn about action and event handlers
- [Pipeline](pipeline/) — Understand middleware and behaviors
- [Configuration](core-concepts/configuration.md) — Configure Dispatch for your needs
- [Transports](transports/) — Multi-transport routing
- [Excalibur](/docs/intro) — Domain modeling and event sourcing
- [Support](support.md) — Get help, report bugs, security policy

## See Also

- [Dispatch vs Excalibur](./dispatch-vs-excalibur.md) - Decision guide for choosing between Dispatch alone and the full framework
- [Core Concepts](./core-concepts/index.md) - Actions, handlers, results, and message context
- [Performance Overview](./performance/index.md) - Benchmarks and optimization strategies


