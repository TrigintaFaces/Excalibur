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

**High-performance .NET messaging framework with CQRS, event sourcing, and production hosting — 195 packages, Native AOT ready**

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
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
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

### 2. Add Excalibur for CQRS + Hosting

Bring in Excalibur when you need aggregates, event stores, or opinionated hosting:

```bash
dotnet add package Excalibur.Domain
dotnet add package Excalibur.EventSourcing
dotnet add package Excalibur.EventSourcing.InMemory
dotnet add package Excalibur.Outbox.SqlServer
dotnet add package Excalibur.Hosting.Web
```

```csharp
builder.Services
    .AddDispatch(typeof(Program).Assembly)
    .AddInMemoryEventStore()
    .AddSqlServerOutboxStore(o => o.ConnectionString = builder.Configuration.GetConnectionString("Default")!);
```

You continue to dispatch messages through `IDispatcher`; Excalibur layers domain modeling, persistence, and compliance features on top.

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

Dispatch is optimized for high-throughput, low-latency messaging with lean local hot paths and transport-aware pipeline profiles.

### Key Metrics

Median of 7 WarmPath runs on one idle machine (BenchmarkDotNet 0.15.8, .NET 10.0.6 / SDK 10.0.202, i9-14900K). Allocation was byte-identical across all 7 runs; latency varied 6-10% between runs, which is several times BenchmarkDotNet's reported error — that error describes spread within a single process, not reproducibility between processes. Read the byte figures as exact and the nanosecond figures as indicative.

| Metric | Value | Notes |
|--------|-------|-------|
| **Standard dispatch** | 30.5 ns / 24 B | Default path for a handler that takes no message context (`MediatRWarmPathComparisonBenchmarks`) |
| **Ultra-local dispatch** | 33.2 ns / 24 B | Explicit lowest-overhead API |
| **Singleton-promoted** | 33.2 ns / 24 B | Cached direct handler path |
| **Handler invocation** | 6.0 ns / 0 B | Direct delegate, zero allocation (from `DispatchHotPathBreakdownBenchmarks`, last refreshed 2026-04-13) |
| **Handler activation** | 24.4 ns / 0 B | Pre-created context, zero allocation (from `DispatchHotPathBreakdownBenchmarks`, last refreshed 2026-04-13) |
| **100 concurrent commands** | 4,484.1 ns / 4,960 B | Scales linearly (WarmPath) |

**Competitor comparisons** (WarmPath, 2026-09-03). Against **Wolverine** we are **5.6× faster** on `InvokeAsync` (33.1 ns vs 186.5 ns) at 24× less memory, and against **MassTransit Mediator** **42× faster** (28.6 ns vs 1,208 ns) at 148× less memory. On a three-middleware pipeline we are **3.2× faster than MediatR** (49.9 ns vs 161.0 ns) at 4.4× less memory.

Against **MediatR** the answer splits, and both halves are worth knowing:

| | Dispatch | MediatR | |
|---|---|---|---|
| Standard dispatch | **30.5 ns / 24 B** | 43.4 ns / 152 B | we are 1.42× faster, 6.3× less memory |
| Ultra-local dispatch | **33.2 ns / 24 B** | 43.4 ns / 152 B | we are 1.31× faster, 6.3× less memory |
| Query with return | 43.0 ns / **120 B** | 39.3 ns / 224 B | parity on time; we allocate 1.9× less |
| Notification → 3 handlers | 140.8 ns / **96 B** | 97.6 ns / 616 B | MediatR is 1.44× faster; we allocate 6.4× less |
| 100 concurrent commands | **4,484 ns / 4.9 KB** | 5,203 ns / 17.1 KB | we are 1.16× faster; we allocate 3.4× less |

So: **we allocate less than MediatR on every scenario, and the standard path is now the fast one.**
It used to cost 66.8 ns and 240 B because it published a message context to every handler, including
the ones that never read it. It no longer does that — a handler receives a context when it declares
it wants one, and pays for it only then. Notification fan-out is the one tier where MediatR is still
ahead on time, and it allocates 6.4× more to get there.

The standard path measuring slightly *faster* than the explicit ultra-local API is not a mistake:
the standard path uses a per-type cached invoker, while the ultra-local entry point re-resolves its
dispatch plan on every call. Reach for the ultra-local API when you want the guarantee, not because
it is quicker.

> **Reading these numbers.** They come from BenchmarkDotNet's warm job (`WarmPathBenchmarkConfig`), which is the configuration used for published comparisons; the cold job is a CI latency gate and does not report allocation. Every arm calls its library directly from the benchmark method with no intermediate `async` frame, so the allocation column compares libraries rather than harness — an extra `async` frame returning a reference costs ~72 bytes on x64 and would silently charge one side for the measurement itself. Your own call site adds whatever your `await` costs on top of the figures above.

### Optimizations Included

- **C# 12 Interceptors** - Compile-time dispatch resolution
- **FrozenDictionary Caches** - Lock-free handler and middleware lookup
- **Static Pipelines** - Zero-allocation execution for known message types
- **Auto-Freeze on Startup** - Zero-configuration production optimization
- **LightMode** - Opt-in minimal overhead (disables AsyncLocal context flow + correlation)

### Quick Configuration

```csharp
using Excalibur.Dispatch.Options.Configuration;

// Optimized automatically by default
services.AddDispatch();

// Opt-out for development (if needed)
services.Configure<DispatchOptions>(o => o.CrossCutting.Performance.AutoFreezeOnStart = false);
```

For detailed benchmarks, methodology caveats, and raw reports, see:
- [Competitor comparison](docs-site/docs/performance/competitor-comparison.md)
- `benchmarks/baselines/` (published baselines)
- `benchmarks/runs/BenchmarkDotNet.Artifacts/results/` (latest local run outputs)
- `benchmarks/experiments/` (auto-optimize experiment logs)

---

## Status & Testing

- **195 NuGet packages** across Dispatch, Excalibur, and hosting families (390 projects in the solution)
- **Supported framework:** .NET 10.0 (LTS)
- **160 of 195 packages** are Native AOT compatible (`IsAotCompatible=true`)
- **112,000+ automated tests** across 10 CI shards (unit, integration, functional, conformance, performance)
- **20 Roslyn source generators** for AOT-safe handler registration, serialization, and saga coordination

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
