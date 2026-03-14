---
sidebar_position: 3
title: Project Templates
description: Use dotnet new templates to quickly scaffold Excalibur projects
---

# Project Templates

Get started quickly with `dotnet new` templates for Excalibur projects.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the template pack:
  ```bash
  dotnet new install Excalibur.Dispatch.Templates
  ```

## Available Templates

| Template | Short Name | Description | Key Options |
|----------|------------|-------------|-------------|
| Dispatch API | `dispatch-api` | ASP.NET Core API with Dispatch messaging | `--Transport`, `--IncludeDocker`, `--IncludeTests` |
| Dispatch Minimal API | `dispatch-minimal-api` | Minimal API endpoints with Dispatch messaging | `--Transport`, `--IncludeDocker`, `--IncludeTests` |
| Dispatch Worker | `dispatch-worker` | Background worker with message processing | `--Transport`, `--IncludeDocker`, `--IncludeTests` |
| Dispatch Serverless | `dispatch-serverless` | Serverless function with Dispatch messaging | `--Platform`, `--Transport`, `--IncludeDocker` |
| Excalibur DDD | `excalibur-ddd` | Domain-Driven Design with Event Sourcing | `--Database`, `--IncludeDocker`, `--IncludeTests` |
| Excalibur CQRS | `excalibur-cqrs` | Full CQRS pattern with event sourcing | `--Transport`, `--Database`, `--IncludeDocker`, `--IncludeTests` |
| Excalibur Saga | `excalibur-saga` | Saga / process manager with orchestration | `--Transport`, `--Database`, `--Framework` |
| Excalibur Outbox | `excalibur-outbox` | Reliable outbox messaging pattern | `--Transport`, `--Database`, `--Framework` |

## Installation

Install the templates package:

```bash
dotnet new install Excalibur.Dispatch.Templates
```

Verify installation:

```bash
dotnet new list dispatch
```

## Using Templates

### Dispatch API

Create a new API project with Dispatch messaging:

```bash
dotnet new dispatch-api -n MyApi
cd MyApi
dotnet run
```

**What's included:**
- ASP.NET Core Web API with controllers
- Dispatch registration via `AddDispatch()` unified builder
- Sample `CreateOrderAction` / `GetOrderAction` with working handler implementations
- `InMemoryOrderStore` demonstrating a replaceable persistence pattern
- Transport selection via `--Transport` option
- `appsettings.json` with transport configuration

```bash
# With Kafka transport and tests
dotnet new dispatch-api -n MyApi --Transport kafka --IncludeTests

# With Docker support
dotnet new dispatch-api -n MyApi --Transport rabbitmq --IncludeDocker
```

### Dispatch Minimal API

Create a minimal API project without controllers:

```bash
dotnet new dispatch-minimal-api -n MyMinimalApi
cd MyMinimalApi
dotnet run
```

**What's included:**
- Minimal API endpoints using `app.MapPost`/`app.MapGet` (no controllers)
- Dispatch registration via `AddDispatch()` unified builder
- Sample `CreateOrderAction` / `GetOrderAction` with working handler implementations
- `InMemoryOrderStore` demonstrating a replaceable persistence pattern
- Transport selection via `--Transport` option (all 6 transports supported)

```bash
# With Kafka transport
dotnet new dispatch-minimal-api -n MyMinimalApi --Transport kafka

# With Docker support
dotnet new dispatch-minimal-api -n MyMinimalApi --Transport rabbitmq --IncludeDocker
```

### Dispatch Worker

Create a background worker service:

```bash
dotnet new dispatch-worker -n MyWorker
```

**What's included:**
- .NET Worker Service with `Host.CreateDefaultBuilder`
- `OrderProcessingWorker` background service
- Sample `OrderCreatedEventHandler`
- Transport selection via `--Transport` option

```bash
# With RabbitMQ and tests
dotnet new dispatch-worker -n MyWorker --Transport rabbitmq --IncludeTests
```

### Excalibur DDD

Create a Domain-Driven Design project with Event Sourcing:

```bash
dotnet new excalibur-ddd -n MyDomain
```

**What's included:**
- Domain layer with `Order` aggregate using **pattern matching** (no reflection)
- Domain events: `OrderCreated`, `OrderShipped`
- Value objects: `Money`
- Application layer: `CreateOrderCommand`, `GetOrderQuery` with handlers
- Event sourcing via `AddExcalibur()` unified builder
- Database selection via `--Database` option

```bash
# With PostgreSQL and tests
dotnet new excalibur-ddd -n MyDomain --Database postgresql --IncludeTests
```

### Excalibur CQRS

Create a full CQRS implementation:

```bash
dotnet new excalibur-cqrs -n MyCqrs
```

**What's included:**
- Command and query separation with working handler implementations
- Dispatch for command/query handling via `AddDispatch()`
- Excalibur for event sourcing via `AddExcalibur()`
- Read model projections: `OrderReadModel`, `OrderProjection` with real upsert logic
- `InMemoryProjectionStore<T>` implementing `IProjectionStore<T>` for demonstration
- Combined transport + database selection

```bash
# Full CQRS with Kafka transport and PostgreSQL
dotnet new excalibur-cqrs -n MyCqrs --Transport kafka --Database postgresql --IncludeTests --IncludeDocker
```

### Dispatch Serverless

Create a serverless function project:

```bash
dotnet new dispatch-serverless -n MyFunction
```

**What's included:**
- Platform-specific scaffolding for Azure Functions, AWS Lambda, or Google Cloud Functions
- Dispatch registration via `AddDispatch()` unified builder
- Sample `CreateOrderHandler` with message processing
- Transport selection via `--Transport` option (all 6 transports supported)

```bash
# Azure Functions (default)
dotnet new dispatch-serverless -n MyFunction --Platform azure

# AWS Lambda
dotnet new dispatch-serverless -n MyFunction --Platform aws --Transport awssqs

# Google Cloud Functions
dotnet new dispatch-serverless -n MyFunction --Platform gcp --Transport googlepubsub
```

**Platform details:**

| Platform | Entry Point | Config Files |
|----------|-------------|-------------|
| Azure Functions | `Program.cs` with `HostBuilder.ConfigureFunctionsWebApplication()` | `host.json`, `local.settings.json` |
| AWS Lambda | `Function.cs` with Lambda handler, `Startup.cs` for DI | `aws-lambda-tools-defaults.json` |
| Google Cloud Functions | `Function.cs` with HTTP handler, `Startup.cs` for DI | -- |

### Excalibur Saga

Create a saga / process manager project:

```bash
dotnet new excalibur-saga -n MySaga
cd MySaga
dotnet run
```

**What's included:**
- Saga orchestration via `AddExcaliburSaga()` unified builder
- Sample `OrderSaga` process manager with 3 steps (CollectPayment, ReserveInventory, ShipOrder) including compensation
- `OrderSagaData` implementing `ISagaData`
- Messages: `StartOrderProcessing` action + domain events
- Builder configuration: `UseSqlServer()`, `WithOrchestration()`, `WithCorrelation()`, `WithInstrumentation()`
- Transport selection via `--Transport` option (all 6 transports supported)
- Database selection via `--Database` option (sqlserver, inmemory)

```bash
# With Kafka transport
dotnet new excalibur-saga -n MySaga --Transport kafka

# With in-memory database (for testing)
dotnet new excalibur-saga -n MySaga --Database inmemory
```

### Excalibur Outbox

Create an outbox pattern project for reliable messaging:

```bash
dotnet new excalibur-outbox -n MyOutbox
cd MyOutbox
dotnet run
```

**What's included:**
- Outbox pattern via `AddExcaliburOutbox()` unified builder
- Sample `PlaceOrderHandler` using `IActionHandler<PlaceOrderCommand>` with outbox publishing
- Messages: `PlaceOrderCommand` action + `OrderPlacedEvent`
- Builder configuration: `UseSqlServer()`, `WithProcessing()`, `EnableBackgroundProcessing()`
- Transport selection via `--Transport` option (all 6 transports supported)
- Database selection via `--Database` option (sqlserver, inmemory)

```bash
# With RabbitMQ transport
dotnet new excalibur-outbox -n MyOutbox --Transport rabbitmq

# With in-memory for development
dotnet new excalibur-outbox -n MyOutbox --Database inmemory
```

## Template Options

### Common Options (All Templates)

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--Framework` | choice | `net10.0` | Target framework (`net8.0`, `net9.0`, `net10.0`) |
| `--IncludeDocker` | bool | `false` | Include Dockerfile and .dockerignore |
| `--IncludeTests` | bool | `false` | Include test project (xUnit + Shouldly + FakeItEasy) |

### Transport Options (dispatch-api, dispatch-minimal-api, dispatch-worker, dispatch-serverless, excalibur-cqrs, excalibur-saga, excalibur-outbox)

| Option | Description |
|--------|-------------|
| `inmemory` | In-memory transport (default, for development) |
| `kafka` | Apache Kafka transport |
| `rabbitmq` | RabbitMQ transport |
| `azureservicebus` | Azure Service Bus transport |
| `awssqs` | AWS SQS transport |
| `googlepubsub` | Google Cloud Pub/Sub transport |

```bash
dotnet new dispatch-api -n MyApi --Transport kafka
```

### Platform Options (dispatch-serverless)

| Option | Description |
|--------|-------------|
| `azure` | Azure Functions isolated worker (default) |
| `aws` | AWS Lambda |
| `gcp` | Google Cloud Functions |

```bash
dotnet new dispatch-serverless -n MyFunction --Platform aws
```

### Database Options (excalibur-ddd, excalibur-cqrs, excalibur-saga, excalibur-outbox)

| Option | Description |
|--------|-------------|
| `sqlserver` | SQL Server (default) |
| `postgresql` | PostgreSQL |
| `inmemory` | In-memory (for testing) |

```bash
dotnet new excalibur-ddd -n MyDomain --Database postgresql
```

### NuGet Package Versions

Templates reference Excalibur packages via a configurable MSBuild property:

```xml
<PropertyGroup>
  <ExcaliburDispatchVersion>0.1.0-*</ExcaliburDispatchVersion>
</PropertyGroup>
```

Override at build time or in `Directory.Build.props`:

```bash
dotnet build -p:ExcaliburDispatchVersion=1.0.0
```

## Template Structure

### dispatch-api

```
MyApi/
├── Controllers/
│   └── OrdersController.cs
├── Actions/
│   ├── CreateOrderAction.cs
│   └── GetOrderAction.cs
├── Handlers/
│   ├── CreateOrderHandler.cs
│   └── GetOrderHandler.cs
├── Infrastructure/
│   └── InMemoryOrderStore.cs
├── Program.cs
├── appsettings.json
├── MyApi.csproj
├── Dockerfile              (--IncludeDocker)
├── .dockerignore            (--IncludeDocker)
└── MyApi.Tests/             (--IncludeTests)
    ├── Handlers/
    │   └── CreateOrderHandlerShould.cs
    └── MyApi.Tests.csproj
```

### dispatch-minimal-api

```
MyMinimalApi/
├── Actions/
│   ├── CreateOrderAction.cs
│   └── GetOrderAction.cs
├── Handlers/
│   ├── CreateOrderHandler.cs
│   └── GetOrderHandler.cs
├── Infrastructure/
│   └── InMemoryOrderStore.cs
├── Program.cs
├── appsettings.json
├── MyMinimalApi.csproj
├── Dockerfile              (--IncludeDocker)
└── .dockerignore            (--IncludeDocker)
```

### dispatch-serverless

```
MyFunction/
├── Handlers/
│   └── CreateOrderHandler.cs
├── Messages/
│   └── OrderMessages.cs
├── Program.cs               (Azure Functions)
├── Function.cs              (AWS Lambda / GCF)
├── Startup.cs               (AWS Lambda / GCF)
├── MyFunction.csproj
├── appsettings.json
├── host.json                (Azure Functions)
├── local.settings.json      (Azure Functions)
└── aws-lambda-tools-defaults.json (AWS Lambda)
```

### dispatch-worker

```
MyWorker/
├── Workers/
│   └── OrderProcessingWorker.cs
├── Handlers/
│   └── OrderCreatedEventHandler.cs
├── Program.cs
├── appsettings.json
├── MyWorker.csproj
├── Dockerfile              (--IncludeDocker)
├── .dockerignore            (--IncludeDocker)
└── MyWorker.Tests/          (--IncludeTests)
    ├── Handlers/
    │   └── OrderCreatedEventHandlerShould.cs
    └── MyWorker.Tests.csproj
```

### excalibur-ddd

```
MyDomain/
├── Domain/
│   ├── Aggregates/
│   │   └── Order.cs
│   ├── Events/
│   │   ├── OrderCreated.cs
│   │   └── OrderShipped.cs
│   └── ValueObjects/
│       └── Money.cs
├── Application/
│   ├── Commands/
│   │   ├── CreateOrderCommand.cs
│   │   └── CreateOrderCommandHandler.cs
│   └── Queries/
│       ├── GetOrderQuery.cs
│       └── GetOrderQueryHandler.cs
├── Program.cs
├── appsettings.json
├── MyDomain.csproj
├── Dockerfile              (--IncludeDocker)
├── .dockerignore            (--IncludeDocker)
└── MyDomain.Tests/          (--IncludeTests)
    ├── Domain/
    │   └── OrderShould.cs
    └── MyDomain.Tests.csproj
```

### excalibur-cqrs

```
MyCqrs/
├── Domain/
│   ├── Aggregates/
│   │   └── Order.cs
│   └── Events/
│       ├── OrderCreated.cs
│       └── OrderShipped.cs
├── Application/
│   ├── Commands/
│   │   ├── CreateOrderCommand.cs
│   │   └── CreateOrderCommandHandler.cs
│   └── Queries/
│       ├── GetOrderQuery.cs
│       └── GetOrderQueryHandler.cs
├── Infrastructure/
│   └── InMemoryProjectionStore.cs
├── ReadModel/
│   ├── OrderReadModel.cs
│   └── OrderProjection.cs
├── Program.cs
├── appsettings.json
├── MyCqrs.csproj
├── Dockerfile              (--IncludeDocker)
├── .dockerignore            (--IncludeDocker)
└── MyCqrs.Tests/            (--IncludeTests)
    ├── Domain/
    │   └── OrderShould.cs
    └── MyCqrs.Tests.csproj
```

## Generated Code Patterns

### Unified Builder (All Templates)

All templates use the unified builder patterns from Sprints 499-502:

```csharp
// Dispatch API / Worker
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    // Transport added based on --Transport option
});

// Excalibur DDD / CQRS
builder.Services.AddExcalibur(excalibur =>
{
    excalibur.AddEventSourcing(es =>
    {
        // Database configured based on --Database option
    });
});
```

### Pattern Matching in Aggregates

The excalibur-ddd and excalibur-cqrs templates demonstrate the correct event application pattern using **switch expressions** (no reflection):

```csharp
protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
{
    OrderCreated e => Apply(e),
    OrderShipped e => Apply(e),
    _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}")
};
```

### Dockerfile Framework Tags

When using `--IncludeDocker`, the generated Dockerfile automatically uses the correct .NET base image tag matching your `--Framework` selection:

```bash
# net8.0 → Dockerfile uses sdk:8.0 and aspnet:8.0
dotnet new dispatch-api -n MyApi --Framework net8.0 --IncludeDocker

# net9.0 → Dockerfile uses sdk:9.0 and aspnet:9.0
dotnet new dispatch-api -n MyApi --Framework net9.0 --IncludeDocker
```

Worker templates use `runtime:` instead of `aspnet:` for the final stage image.

## Updating Templates

Update to the latest version:

```bash
dotnet new update
```

Or reinstall:

```bash
dotnet new install Excalibur.Dispatch.Templates --force
```

## Uninstalling Templates

Remove the templates:

```bash
dotnet new uninstall Excalibur.Dispatch.Templates
```

## Creating Custom Templates

Create your own templates based on your organization's standards:

1. Create a `template.json` configuration
2. Package as a NuGet package
3. Install locally or publish to a feed

See [Microsoft's template authoring guide](https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates) for details.

## What's Next

- [Samples](samples.md) - Browse working sample applications
- [Core Concepts](../core-concepts/index.md) - Understand actions, handlers, and the pipeline
- [Your First Event](first-event.md) - Event-driven patterns

## See Also

- [Getting Started](./index.md) — Install Dispatch and create your first message handler in 5 minutes
- [Configuration](../core-concepts/configuration.md) — Configure Dispatch options, transports, and middleware via code or appsettings
- [ASP.NET Core Deployment](../deployment/aspnet-core.md) — Deploy Dispatch applications in ASP.NET Core hosting environments
