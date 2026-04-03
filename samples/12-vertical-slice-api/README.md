# 12 - Vertical Slice API

Demonstrates the vertical slice architecture pattern using Excalibur.Dispatch with ASP.NET Core, where each feature (endpoint + handler + validation) is self-contained in a single folder.

## Projects

| Project | Description |
|---------|-------------|
| [HealthcareApi](HealthcareApi/) | ASP.NET Core Web API organized by feature slices (patients, appointments, prescriptions) |

## What You'll Learn

- Organizing code by feature instead of by layer
- Mapping ASP.NET Core endpoints to Dispatch handlers
- Using `AddHandlersFromAssembly()` for automatic handler discovery
- Keeping each feature self-contained with its own request/response types

## Prerequisites

- .NET 9.0+ SDK
- No external infrastructure required (uses in-memory stores)

## Quick Start

```bash
cd HealthcareApi
dotnet run
```

## Related Docs

- [Vertical Slice Architecture](../../docs-site/docs/architecture/vertical-slice-architecture.md)
- [Actions and Handlers](../../docs-site/docs/core-concepts/actions-and-handlers.md)
- [ASP.NET Core Hosting](../../docs-site/docs/deployment/minimal-api-bridge.md)
