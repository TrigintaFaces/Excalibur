---
sidebar_position: 12
title: Source Generators
description: AOT-compatible source generators for Dispatch
---

# Source Generators

Dispatch includes Roslyn source generators that enable ahead-of-time (AOT) compilation and Native AOT support. Source generators analyze your code at compile time and produce explicit registrations, eliminating the need for runtime reflection.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.SourceGenerators  # source generators
  ```
- Familiarity with [handlers](../handlers.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Key Features

- **Compile-time service discovery** - No runtime assembly scanning
- **Full AOT support** - Compatible with .NET Native AOT publishing
- **Trimming safe** - Works with IL trimming without losing types
- **Opt-in design** - Only marked types are auto-registered

## Quick Start

```csharp
using Excalibur.Dispatch.Abstractions;
using Microsoft.Extensions.DependencyInjection;

// Mark services for auto-registration
[AutoRegister]
public class OrderHandler : IDispatchHandler<CreateOrderCommand>
{
    // Implementation...
}

// In Program.cs
builder.Services.AddGeneratedServices();
```

## Documentation

| Topic | Description |
|-------|-------------|
| [Getting Started](getting-started.md) | Quick start guide with examples |
| [Architecture](../advanced/source-generators.md) | Deep dive into all generators |

## Available Generators

| Generator | Status | Purpose |
|-----------|--------|---------|
| `ServiceRegistrationSourceGenerator` | **Active** | Auto-registers `[AutoRegister]` types |
| `HandlerRegistrySourceGenerator` | Active | Handler discovery at compile time |
| `HandlerInvokerSourceGenerator` | Active | Zero-reflection handler invocation |
| `JsonSerializationSourceGenerator` | Active | AOT-compatible JSON serialization |
| And 6 more... | Active | See [full list](../advanced/source-generators.md) |

## Installation

```bash
dotnet add package Excalibur.Dispatch.SourceGenerators
```

The `[AutoRegister]` attribute is provided by `Excalibur.Dispatch.Abstractions`.

## See Also

- [Getting Started with Source Generators](./getting-started.md) — Quick start guide with detailed examples for each generator
- [Source Generators Deep Dive](../advanced/source-generators.md) — Architecture and internals of all available source generators
- [Native AOT Support](../advanced/native-aot.md) — Publishing Dispatch applications with Native AOT using source generators
