<p align="center">
  <img src="images/Combined/readme-banner.svg" alt="Excalibur + Dispatch" width="900">
</p>

# Excalibur
<!-- badges -->
[![Build Status](https://img.shields.io/github/actions/workflow/status/TrigintaFaces/Excalibur/ci.yml?branch=main)](https://github.com/TrigintaFaces/Excalibur/actions)
[![Tests](https://img.shields.io/github/actions/workflow/status/TrigintaFaces/Excalibur/ci.yml?label=tests)](https://github.com/TrigintaFaces/Excalibur/actions/workflows/ci.yml)
[![Documentation](https://img.shields.io/badge/docs-excalibur--dispatch.dev-blue.svg)](https://docs.excalibur-dispatch.dev)

[![NuGet](https://img.shields.io/nuget/vpre/Excalibur.Dispatch?logo=nuget&label=Excalibur.Dispatch)](https://www.nuget.org/packages/Excalibur.Dispatch/)
[![Downloads](https://img.shields.io/nuget/dt/Excalibur.Dispatch?logo=nuget)](https://www.nuget.org/packages/Excalibur.Dispatch/)
[![Release Date](https://img.shields.io/github/release-date/TrigintaFaces/Excalibur?style=flat-square)](https://github.com/TrigintaFaces/Excalibur/releases/latest)
<!-- badges -->

**High-performance .NET messaging framework with CQRS, event sourcing, and production hosting — 194 packages, Native AOT ready**

**[Read the full documentation](https://docs.excalibur-dispatch.dev/)**

---

## Overview

This repository ships **two cooperating frameworks**:

| Layer | Responsibilities | Primary Packages |
|-------|------------------|------------------|
| **Dispatch (Messaging Core)** | Message contracts, handlers, middleware pipeline, transports, diagnostics hooks, thin ASP.NET Core bridge | `Excalibur.Dispatch`, `Excalibur.Dispatch.Abstractions`, `Excalibur.Dispatch.Hosting.AspNetCore`, `Excalibur.Dispatch.Transport.*`, `Excalibur.Dispatch.Observability` |
| **Excalibur (CQRS + Hosting)** | Aggregates, repositories, event stores, sagas, leader election, compliance, ASP.NET Core & serverless hosting templates | `Excalibur.Domain`, `Excalibur.EventSourcing.*`, `Excalibur.Application`, `Excalibur.Hosting.*`, `Excalibur.Compliance.*`, `Excalibur.LeaderElection.*` |

Start with Dispatch when you need a MediatR-class dispatcher. Layer Excalibur packages on later when you need full CQRS, event sourcing, or production hosting.

---

## NuGet Quick Links

| Package | NuGet |
|--------|-------|
| `Excalibur.Dispatch` | https://www.nuget.org/packages/Excalibur.Dispatch/ |
| `Excalibur.Dispatch.Abstractions` | https://www.nuget.org/packages/Excalibur.Dispatch.Abstractions/ |
| `Excalibur.Dispatch.Hosting.AspNetCore` | https://www.nuget.org/packages/Excalibur.Dispatch.Hosting.AspNetCore/ |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | https://www.nuget.org/packages/Excalibur.Dispatch.Transport.AzureServiceBus/ |
| `Excalibur.Dispatch.Transport.AwsSqs` | https://www.nuget.org/packages/Excalibur.Dispatch.Transport.AwsSqs/ |
| `Excalibur.Dispatch.Transport.Kafka` | https://www.nuget.org/packages/Excalibur.Dispatch.Transport.Kafka/ |
| `Excalibur.Dispatch.Transport.RabbitMQ` | https://www.nuget.org/packages/Excalibur.Dispatch.Transport.RabbitMQ/ |
| `Excalibur.Dispatch.Transport.GooglePubSub` | https://www.nuget.org/packages/Excalibur.Dispatch.Transport.GooglePubSub/ |
| `Excalibur.Dispatch.Transport.Grpc` | https://www.nuget.org/packages/Excalibur.Dispatch.Transport.Grpc/ |
| `Excalibur.Dispatch.SourceGenerators` | https://www.nuget.org/packages/Excalibur.Dispatch.SourceGenerators/ |
| `Excalibur.EventSourcing` | https://www.nuget.org/packages/Excalibur.EventSourcing/ |
| `Excalibur.Hosting.Web` | https://www.nuget.org/packages/Excalibur.Hosting.Web/ |

---

## Quick Start

### 1. Dispatch-Only Messaging

Install the core messaging packages and register handlers:

```bash
dotnet add package Excalibur.Dispatch
```

```csharp
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

var builder = WebApplication.CreateBuilder(args);

// Register Dispatch with handler auto-discovery
builder.Services.AddDispatch(typeof(Program).Assembly);

var app = builder.Build();
app.MapPost("/orders", async (CreateOrder command, IDispatcher dispatcher, CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(command, ct);
    return result.Succeeded ? Results.Accepted() : Results.BadRequest(result.ErrorMessage);
});

app.Run();

// Define an action (command)
public record CreateOrder(string CustomerId, List<string> Items) : IDispatchAction;

// Handle it
public class CreateOrderHandler : IActionHandler<CreateOrder>
{
    public Task HandleAsync(CreateOrder action, CancellationToken cancellationToken)
    {
        // Your business logic here
        return Task.CompletedTask;
    }
}
```

The hand-written endpoint above is one of three ways to get from an HTTP request to a handler. They mix
freely in the same application — pick per endpoint.

#### Declarative route registration

`Excalibur.Dispatch.Hosting.AspNetCore` registers the route, binds the wire contract, builds the action and
maps the result, so the endpoint carries no plumbing. The status code comes from the dispatch result rather
than from the registration:

```bash
dotnet add package Excalibur.Dispatch.Hosting.AspNetCore
```

```csharp
using Excalibur.Dispatch.Hosting.AspNetCore;

app.MapGroup("/orders")
   .DispatchPostAction<CreateOrderRequest, CreateOrder>(
       "/",
       static (request, _) => new CreateOrder(request.CustomerId, request.Items));

// The wire contract stays a separate type from the action
public record CreateOrderRequest(string CustomerId, List<string> Items);
```

`DispatchGetAction`, `DispatchPutAction` and `DispatchPostEvent` are the siblings, and a
`<TRequest, TAction, TResponse>` overload returns the handler's value as `200 OK`. Every overload takes an
optional `responseHandler` when you want your own mapping; without one, `ToHttpResult()` maps the result —
the value on success, `403` on an authorization failure, `400` on validation, `ProblemDetails` when the
failure carries them, `500` otherwise.

**These overloads bind parameters reflectively and are not Native AOT or trimming safe** — they declare that
with `[RequiresDynamicCode]` / `[RequiresUnreferencedCode]`. Under AOT, use a hand-written endpoint.

#### Railway-oriented composition

`Map`, `Bind`, `Match` and `Tap` compose over `IMessageResult<T>` — and over `Task<IMessageResult<T>>`, so
there is no intermediate `await`. Each short-circuits on failure and carries the original `ProblemDetails`
through, so the success path reads as one expression with no error branches inside it:

```csharp
app.MapGet("/orders/{id:guid}", async (
    Guid id, IDispatcher dispatcher, ILogger<Program> logger, CancellationToken ct) =>
    await dispatcher.DispatchAsync(new GetOrder(id), ct)
        .Tap(order => logger.LogInformation("Order {OrderId} read", order.Id))
        .Map(order => new OrderResponse(order.Id, order.Total))
        .Match(
            onSuccess: response => Results.Ok(response),
            onFailure: problem => Results.Problem(problem?.Detail, statusCode: problem?.Status)));
```

`MapAsync` / `BindAsync` / `TapAsync` / `MatchAsync` take async callbacks; `GetValueOrDefault` and
`GetValueOrThrow` leave the chain. Full reference:
[Results and errors](docs-site/docs/core-concepts/results-and-errors.md).

### 2. Add Excalibur for CQRS + Hosting

Bring in Excalibur when you need aggregates, event stores, or opinionated hosting:

```bash
dotnet add package Excalibur.Domain
dotnet add package Excalibur.EventSourcing
dotnet add package Excalibur.EventSourcing.SqlServer
dotnet add package Excalibur.Outbox.SqlServer
dotnet add package Excalibur.Hosting.Web
```

```csharp
using Excalibur.EventSourcing.SqlServer;
using Excalibur.Outbox.SqlServer;

var connectionString = builder.Configuration.GetConnectionString("Default")!;

builder.Services.AddDispatch(typeof(Program).Assembly);
builder.Services.AddExcalibur(x => x
    .AddEventSourcing(es => es.UseSqlServer(sql => sql.ConnectionString(connectionString)))
    .AddOutbox(outbox => outbox.UseSqlServer(sql => sql.ConnectionString(connectionString))));
```

Changing provider is one word. Install that provider's package and replace `UseSqlServer` with
`UsePostgres`, `UseOracle`, `UseMongoDB`, `UseCosmosDb`, `UseDynamoDb`, `UseFirestore`, `UseRedis` or
`UseSqlite` — every provider offers the same call, and the outbox builder adds `UseElasticSearch` and
`UseMarten`. Some providers place that extension in their own namespace rather than
`Microsoft.Extensions.DependencyInjection`, so follow the `using` your chosen package declares.

`Excalibur.EventSourcing.InMemory` (`AddInMemoryEventStore()`) exists for tests and local spikes. It keeps
nothing across a restart, so it is not a starting point for a deployed application.

You continue to dispatch messages through `IDispatcher`; Excalibur layers domain modeling, persistence, and
compliance features on top.

### 3. Run the Samples

| Sample | Purpose |
|--------|---------|
| [`HelloDispatch`](samples/01-getting-started/HelloDispatch/README.md) | The smallest thing that dispatches a message |
| [`DispatchOnly`](samples/01-getting-started/DispatchOnly/README.md) | Pure Dispatch usage with no Excalibur dependencies |
| [`WebApiQuickStart`](samples/01-getting-started/WebApiQuickStart/README.md) | Dispatch behind an ASP.NET Core Web API |
| [`EventSourcingIntro`](samples/01-getting-started/EventSourcingIntro/README.md) | Aggregates and an event store, built on Dispatch |

---

## Package Families

| Family | Packages | Notes |
|--------|----------|-------|
| **Dispatch Core** | `Excalibur.Dispatch`, `Excalibur.Dispatch.Abstractions`, `Excalibur.Dispatch.Hosting.AspNetCore`, `Excalibur.Dispatch.Middleware.*`, `Excalibur.Dispatch.Observability` | Messaging primitives, pipeline, analytics, and the thin hosting bridge. |
| **Dispatch Transports** | `Excalibur.Dispatch.Transport.AzureServiceBus`, `Excalibur.Dispatch.Transport.AwsSqs`, `Excalibur.Dispatch.Transport.Kafka`, `Excalibur.Dispatch.Transport.RabbitMQ`, `Excalibur.Dispatch.Transport.GooglePubSub`, `Excalibur.Dispatch.Transport.Grpc`, `Excalibur.Dispatch.Transport.InMemory` | Bring only the transports you need; no domain logic included. |
| **Excalibur Domain/CQRS** | `Excalibur.Domain`, `Excalibur.EventSourcing`, `Excalibur.EventSourcing.*`, `Excalibur.Saga.*` | Aggregates, repositories, snapshots, sagas, and serialization helpers (`EventTypeNameHelper`). |
| **Excalibur Hosting** | `Excalibur.Hosting.Web`, `Excalibur.Hosting.AzureFunctions`, `Excalibur.Hosting.AwsLambda`, `Excalibur.Hosting.GoogleCloudFunctions` | Opinionated hosting templates that compose Dispatch + Excalibur. |
| **Compliance & Coordination** | `Excalibur.Compliance.*`, `Excalibur.AuditLogging.*`, `Excalibur.LeaderElection.*` | Audit logging, masking, key escrow, leader election, and cross-cutting governance. |

The table above names the entry points rather than the whole set; every package is published on NuGet under the `Excalibur.` prefix.

---

## Performance

Dispatch is built for low allocation and predictable latency on the local dispatch path. The
figures below are measurements taken on one machine under stated conditions. They are not
guarantees, and the only number that matters for your service is the one you measure in it.

### Local dispatch overhead

Median of independent processes: commands and queries measured 19 October 2026. BenchmarkDotNet 0.15.8, adaptive
warm-path job, in-process emit toolchain; .NET 10.0.12, SDK 10.0.401, x64 RyuJIT; Intel Core
i9-14900K, Windows 11 build 26300.9711. Rounded to the precision the between-process spread
supports.

Each row comes from three separate original-observation processes. Query rows use
the current selected runtime after the fixture rental-lifecycle repair. They do
not certify the entire rebuilt checkout or actual NativeAOT performance. The
earlier isolated-marker query experiment remains dated history.

| Path | Latency | Allocation |
|---|---:|---:|
| Single command | **~65 ns** | **96 B** |
| Typed query, default pipeline | **~136 ns** | **288 B** |
| Typed query, context-less overload | ~146 ns | 288 B |
| Typed query, singleton promotion configured | ~149 ns | 288 B |

The command handler is a no-op; query handlers double the input ID. These fixtures
measure dispatch and completion overhead rather than application work. Query timing
increased from the earlier isolated experiment on both peers; its cause remains
unresolved. What the allocation buys is described below.

These figures measure Excalibur's overhead and do not establish a competitive ranking.
Comparative fixtures live in `benchmarks/Excalibur.Dispatch.Benchmarks`; their results remain
pending validation of equivalent completed work, matched runtime and job settings, and documented
library configurations. Comparisons must disclose versions, project affiliation, raw results and
failures so others can reproduce and challenge them. Measure your own workload before choosing
a framework.

Full records, with per-process figures and the retained distribution warnings:
[benchmark results](docs-site/docs/performance/competitor-comparison.md).


### What the allocation buys

Every dispatch publishes an ambient message context, so a nested dispatch inherits causation,
correlation, tenant and user instead of starting a fresh root. That costs one
`ExecutionContext` copy-on-write, and the copy covers the whole async-local value map: 72 of
the 96 bytes on a command dispatch, measured with no other async-local values present. With
one other value it measured roughly 160 B, and with fifteen, roughly 992 B. **This figure
scales with your application's async-local density, not ours** -- measure it in your own
process before budgeting for it.

### Earlier measurements, and what has not been re-measured

The command and query figures above replace the corresponding rows of a September 2026
comparison epoch, which measured a single command at 45.6 ns before a correction that restored
default correlation and causation initialization. That epoch is not a baseline for the current
source.

Its notification, multi-middleware pipeline and concurrent-batch comparisons are preserved
in [`benchmarks/BENCHMARKS.md`](benchmarks/BENCHMARKS.md) with their own conditions.
Later, separately scoped October observations do not refresh those September comparisons.
They describe that epoch's code. Do not quote them as current.

### Reading any of these numbers

- **Allocations in the local-overhead table were byte-identical per method across processes.**
  The October 19 command observations ranged from 58.10 to 65.27 ns (about 12.3% maximum/minimum spread). The October 19 query process
  ranges were 134–141 ns (default), 144–147 ns (context-less) and 143–153 ns
  (promotion configured). BenchmarkDotNet's error describes spread within one
  process, not reproducibility between processes; these observations establish no
  stable latency.
- **These fixture calls omit an extra `async` wrapper.** Wrapper allocation depends on the exact
  call shape, completion path and runtime. These observations do not establish a universal byte
  cost for an async frame or your own `await`.
- **No sustained-throughput, percentile or cold-start claim follows from these fixtures.** They are
  warm, in-process, single-operation measurements. The command handler is a no-op;
  query handlers double an input ID.

> MediatR is a trademark of its owner. It is named here only to describe API compatibility; no
> affiliation or endorsement is implied.

### Optimizations included

- **C# 12 interceptors** -- compile-time dispatch resolution
- **FrozenDictionary caches** -- lock-free handler and middleware lookup
- **Static pipelines** -- pre-built execution chains for message types whose route is known
- **Auto-freeze on startup** -- applied by default; no configuration required
- **LightMode** -- opt-in minimal overhead; disables AsyncLocal context flow and correlation-ID generation


### Quick Configuration

```csharp
using Excalibur.Dispatch.Options.Configuration;

// Optimized automatically by default
services.AddDispatch();

// Opt-out for development (if needed)
services.Configure<DispatchOptions>(o => o.CrossCutting.Performance.AutoFreezeOnStart = false);
```

For detailed benchmarks, methodology caveats, and raw reports, see:
- [Benchmark results](docs-site/docs/performance/competitor-comparison.md)
- `benchmarks/baselines/net10.0/dispatch-comparative-20260905/` (historical September 5 comparison epoch)
- `benchmarks/baselines/net10.0/dispatch-warmpath-20260905-lean/` (separate post-correction command measurements)
- `benchmarks/runs/BenchmarkDotNet.Artifacts/results/` (latest local run outputs)
- `benchmarks/experiments/` (auto-optimize experiment logs)

---

## Status & Testing

- **194 NuGet packages** across Dispatch, Excalibur, and hosting families (438 projects in the solution)
- **Supported framework:** .NET 10.0 (LTS)
- **148 of the 194 packages** are Native AOT compatible (`IsAotCompatible=true`)
- **Over 100,000 automated tests** across the CI shards (unit, integration, functional, conformance, performance)
- **14 Roslyn source generators** for AOT-safe handler registration, serialization, and saga coordination

Run the full suite locally:

```bash
dotnet build Excalibur.sln -p:BuildExamplesAndTests=true
dotnet test Excalibur.sln
```

---

## Legal Notice

> **Important**: This framework provides **tools and functionality** to assist with building applications, including compliance-assistance features (audit logging, event sourcing, GDPR helpers). However, use of this framework does **NOT** guarantee compliance with any law or regulation.

**You remain solely responsible for**:
- Ensuring your applications comply with all applicable laws and regulations
- Conducting independent compliance testing and validation
- Obtaining required certifications, audits, and approvals
- Engaging qualified legal and compliance professionals

**The framework is provided "AS IS" without warranty.**

## License

You may use Excalibur under the **Excalibur License 1.1**, **AGPL-3.0-or-later**, or **SSPL-1.0** — your
choice ([LICENSE](LICENSE)). Under the Excalibur License you may build, sell, host and support your own
applications on the framework, including commercially. You may not resell the framework itself or
offer it to others as a hosted service. See the [plain-language summary](docs-site/docs/legal/index.md)
and the full texts in [`licenses/`](licenses/).

There is no licence key, no activation step, and no per-message, per-endpoint or per-seat metering.
Building, shipping and operating your own applications on Excalibur costs nothing — including
commercially, and including when you run those applications as a hosted service for your own
customers. The restrictions are on the framework itself, not on what you build with it.

---

## Support

Need help? See [SUPPORT.md](SUPPORT.md) for:
- Support channels (GitHub Discussions, Issues, Security Advisories)
- Response time expectations
- Supported .NET versions and provider tiers
- Security vulnerability reporting

---

## Contributing

1. Keep documentation in `docs-site/` (consumers) in sync.
2. See [CONTRIBUTING.md](CONTRIBUTING.md) for coding standards, test expectations, and review gates.
