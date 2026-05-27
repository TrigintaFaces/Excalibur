<p align="center">
  <img src="images/Combined/readme-banner.svg" alt="Excalibur + Dispatch" width="900">
</p>

# Excalibur
<!-- badges -->
[![Build Status](https://img.shields.io/github/actions/workflow/status/TrigintaFaces/Excalibur/ci.yml?branch=main)](https://github.com/TrigintaFaces/Excalibur/actions)
[![Tests](https://img.shields.io/github/actions/workflow/status/TrigintaFaces/Excalibur/ci.yml?label=tests)](https://github.com/TrigintaFaces/Excalibur/actions/workflows/ci.yml)
[![Documentation](https://img.shields.io/badge/docs-excalibur--dispatch.dev-blue.svg)](https://docs.excalibur-dispatch.dev)

[![Release Date](https://img.shields.io/github/release-date/TrigintaFaces/Excalibur?style=flat-square)](https://github.com/TrigintaFaces/Excalibur/releases/latest)
[![Latest Release](https://img.shields.io/github/v/release/TrigintaFaces/Excalibur?sort=semver)](https://github.com/TrigintaFaces/Excalibur/releases/latest)
![Total Downloads](https://img.shields.io/github/downloads/TrigintaFaces/Excalibur/total?logo=github&style=flat-square)
[![Latest Release](https://img.shields.io/github/downloads/TrigintaFaces/Excalibur/latest/total?logo=github&style=flat-square)](https://github.com/TrigintaFaces/Excalibur/releases/latest)
<!-- badges -->

**High-performance .NET messaging framework with CQRS, event sourcing, and production hosting — 170 packages, Native AOT ready**

**[Read the full documentation](https://docs.excalibur-dispatch.dev/)**

---

## Overview

This repository ships **two cooperating frameworks**:

| Layer | Responsibilities | Primary Packages |
|-------|------------------|------------------|
| **Dispatch (Messaging Core)** | Message contracts, handlers, middleware pipeline, transports, diagnostics hooks, thin ASP.NET Core bridge | `Dispatch`, `Excalibur.Dispatch.Abstractions`, `Excalibur.Dispatch.Hosting.AspNetCore`, `Excalibur.Dispatch.Transport.*`, `Excalibur.Dispatch.Observability` |
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
dotnet add package Excalibur.Dispatch.Abstractions
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
dotnet add package Excalibur.Hosting.Web
```

```csharp
builder.Services
    .AddDispatch(typeof(Program).Assembly)
    .AddInMemoryEventStore()
    .AddSqlServerOutboxStore(builder.Configuration.GetConnectionString("Default")!);
```

You continue to dispatch messages through `IDispatcher`; Excalibur layers domain modeling, persistence, and compliance features on top.

### 3. Run the Samples

| Sample | Purpose |
|--------|---------|
| [`DispatchMinimal`](samples/01-getting-started/DispatchMinimal/README.md) | Pure Dispatch usage with no Excalibur dependencies |
| [`ExcaliburCqrs`](samples/01-getting-started/ExcaliburCqrs/README.md) | Full CQRS/Event Sourcing stack built on Dispatch + Excalibur |

---

## Package Families

| Family | Packages | Notes |
|--------|----------|-------|
| **Dispatch Core** | `Excalibur.Dispatch`, `Excalibur.Dispatch.Abstractions`, `Excalibur.Dispatch.Hosting.AspNetCore`, `Excalibur.Dispatch.Middleware.*`, `Excalibur.Dispatch.Observability` | Messaging primitives, pipeline, analytics, and the thin hosting bridge. |
| **Dispatch Transports** | `Excalibur.Dispatch.Transport.AzureServiceBus`, `Excalibur.Dispatch.Transport.AwsSqs`, `Excalibur.Dispatch.Transport.Kafka`, `Excalibur.Dispatch.Transport.RabbitMQ`, `Excalibur.Dispatch.Transport.GooglePubSub`, `Excalibur.Dispatch.Transport.Grpc`, `Excalibur.Dispatch.Transport.InMemory` | Bring only the transports you need; no domain logic included. |
| **Excalibur Domain/CQRS** | `Excalibur.Domain`, `Excalibur.EventSourcing`, `Excalibur.EventSourcing.*`, `Excalibur.Saga.*` | Aggregates, repositories, snapshots, sagas, and serialization helpers (`EventTypeNameHelper`). |
| **Excalibur Hosting** | `Excalibur.Hosting.Web`, `Excalibur.Hosting.AzureFunctions`, `Excalibur.Hosting.AwsLambda`, `Excalibur.Hosting.GoogleCloudFunctions` | Opinionated hosting templates that compose Dispatch + Excalibur. |
| **Compliance & Coordination** | `Excalibur.Compliance.*`, `Excalibur.AuditLogging.*`, `Excalibur.LeaderElection.*` | Audit logging, masking, key escrow, leader election, and cross-cutting governance. |

The [`Directory.Packages.props`](Directory.Packages.props) file lists every published package and version.

---

## Performance

Dispatch is optimized for high-throughput, low-latency messaging with lean local hot paths and transport-aware pipeline profiles.

### Key Metrics

From the 20260420 epoch baseline (`benchmarks/baselines/net10.0/dispatch-comparative-20260420/`, BenchmarkDotNet 0.15.8, .NET 10.0.6 / SDK 10.0.202, i9-14900K):

| Metric | Value | Notes |
|--------|-------|-------|
| **Standard dispatch** | 70.87 ns / 240 B | Full pipeline with context, routing, and correlation (`MediatRWarmPathComparisonBenchmarks`) |
| **Ultra-local dispatch** | 34.56 ns / 24 B | Lowest-overhead path, near-zero allocation |
| **Singleton-promoted** | 33.67 ns / 24 B | Cached direct handler path |
| **Handler invocation** | 6.0 ns / 0 B | Direct delegate, zero allocation (from `DispatchHotPathBreakdownBenchmarks`, last refreshed 2026-04-13) |
| **Handler activation** | 24.4 ns / 0 B | Pre-created context, zero allocation (from `DispatchHotPathBreakdownBenchmarks`, last refreshed 2026-04-13) |
| **100 concurrent commands** | 7,293.79 ns / 19,360 B | Scales linearly (WarmPath — one row under investigation pending methodology-matched rerun) |

**Competitor comparisons** (ns, WarmPath): Dispatch ultra-local **1.28× faster than MediatR** with 6.3× less memory; Dispatch **2.64× faster than Wolverine** on InvokeAsync; Dispatch **leads MassTransit Mediator** on every in-process tier.

### Optimizations Included

- **C# 12 Interceptors** - Compile-time dispatch resolution
- **FrozenDictionary Caches** - Lock-free handler and middleware lookup
- **Static Pipelines** - Zero-allocation execution for known message types
- **Auto-Freeze on Startup** - Zero-configuration production optimization
- **LightMode** - Opt-in minimal overhead (disables AsyncLocal context flow + correlation)

### Quick Configuration

```csharp
// Default: Optimized automatically
services.AddDispatch();

// Opt-out for development (if needed)
services.Configure<PerformanceOptions>(o => o.AutoFreezeOnStart = false);
```

For detailed benchmarks, methodology caveats, and raw reports, see:
- [Competitor comparison](docs-site/docs/performance/competitor-comparison.md)
- `benchmarks/baselines/` (published baselines)
- `benchmarks/runs/BenchmarkDotNet.Artifacts/results/` (latest local run outputs)
- `benchmarks/experiments/` (auto-optimize experiment logs)

---

## Status & Testing

- **170 NuGet packages** across Dispatch, Excalibur, and hosting families (334 total projects in solution)
- **Supported frameworks:** .NET 8.0 LTS, .NET 9.0, .NET 10.0
- **150 of 170 packages** are Native AOT compatible (`IsAotCompatible=true`)
- **112,000+ automated tests** across 10 CI shards (unit, integration, functional, conformance, performance)
- **21 Roslyn source generators** for AOT-safe handler registration, serialization, and saga coordination

Run the full suite locally:

```bash
dotnet build Excalibur.sln
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
