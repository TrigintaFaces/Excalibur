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

Median of the WarmPath comparison epoch of 2026-09-05 (BenchmarkDotNet 0.15.8, .NET 10.0.11 / SDK 10.0.400, i9-14900K, `InProcessEmitToolchain`). Allocation was byte-identical across runs; latency varied about 4% run to run on our arms and about 8.6% on MediatR's, which is several times BenchmarkDotNet's reported error — that error describes spread within a single process, not reproducibility between processes. Read the byte figures as exact and the nanosecond figures as indicative, and do not read a ratio inside that band as a finding.

| Metric | Value | Notes |
|--------|-------|-------|
| **Standard dispatch** | 45.6 ns / 96 B | `DispatchAsync` with a caller-supplied context (`MediatRWarmPathComparisonBenchmarks`) |
| **Context-less 2-arg overload** | 53.0 ns / 96 B | `DispatchAsync(message, ct)` — the framework creates the context for you |
| **Singleton-promoted handler** | 53.9 ns / 96 B | Context-less overload against a promoted singleton handler |
| **Query with return value** | 63.7 ns / 192 B | Typed result materialised |
| **Three-middleware pipeline** | 71.7 ns / 240 B | Logging + validation + timing (`PipelineWarmPathComparisonBenchmarks`) |
| **100 concurrent commands** | 5,584 ns / 12,160 B | Scales linearly |

**Read the 96 B as a floor, not a fixed cost.** A dispatch publishes an ambient message context so a nested dispatch inherits causation, correlation, tenant and user instead of silently starting a fresh root. That costs one `ExecutionContext` copy-on-write, and the copy is of the whole async-local value map — so what you actually pay scales with how many `AsyncLocal` values *your* application has live: 72 of the 96 bytes when there are none, roughly 160 B with one other, roughly 992 B with fifteen. A real host carries several before it reaches us, and this framework itself declares two.

**Competitor comparisons** (same epoch). Against **Wolverine** in-process we are **3.8× faster** on `InvokeAsync` (47.0 ns vs 179.1 ns) at 6.1× less memory, and against **MassTransit**'s in-memory bus **370× faster** on a single command (46.3 ns vs 17,118 ns) at 230× less memory. On a three-middleware pipeline we lead every framework measured: **71.7 ns / 240 B** against MediatR's 124.9 ns / 680 B, Wolverine's 236.3 ns / 680 B and MassTransit's 2,128.0 ns / 4,568 B.

Against **MediatR** the answer splits, and the half that goes against us belongs first:

| | Dispatch | MediatR | |
|---|---|---|---|
| Single command | 45.6 ns / **96 B** | 41.3 ns / 152 B | MediatR is 1.10× faster; we allocate 1.58× less |
| Notification → 3 handlers | 135.0 ns / **96 B** | 95.0 ns / 616 B | MediatR is 1.42× faster; we allocate 6.4× less |
| 10 concurrent commands | 596.1 ns / **1,360 B** | 541.7 ns / 1,856 B | MediatR is 1.10× faster; we allocate 1.36× less |
| 100 concurrent commands | 5,584 ns / **12,160 B** | 5,146 ns / 17,064 B | MediatR is 1.09× faster; we allocate 1.40× less |
| Three-middleware pipeline | **71.7 ns / 240 B** | 124.9 ns / 680 B | we are 1.74× faster; we allocate 2.83× less |

So: **MediatR is a few nanoseconds ahead on the bare paths, we allocate less on every scenario, and we lead once middleware is in the pipeline** — which is the shape most applications actually run. The two concurrency gaps sit inside the run-to-run band described above; the single-command and notification gaps do not, and are stated as measured.

The query comparison is deliberately absent. MediatR's own query row moved about 21% between epochs for reasons that have nothing to do with this framework and were consistent across every run of the new one; until that is explained the ratio means nothing in either direction. Dispatch's own query figures are in the table above.

> **Reading these numbers.** They come from BenchmarkDotNet's warm job (`WarmPathBenchmarkConfig`), which is the configuration used for published comparisons; the cold job is a CI latency gate and does not report allocation. Every arm calls its library directly from the benchmark method with no intermediate `async` frame, so the allocation column compares libraries rather than harness — an extra `async` frame returning a reference costs ~72 bytes on x64 and would silently charge one side for the measurement itself. Your own call site adds whatever your `await` costs on top of the figures above.

### Optimizations Included
- **C# 12 Interceptors** - Compile-time dispatch resolution
- **FrozenDictionary Caches** - Lock-free handler and middleware lookup
- **Static Pipelines** - Pre-built execution chains for message types whose route is known
- **Auto-Freeze on Startup** - Zero-configuration production optimization
- **LightMode** - Opt-in minimal overhead (skips correlation-ID generation)
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
- `benchmarks/baselines/net10.0/dispatch-comparative-20260905/` (the epoch quoted above)
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
