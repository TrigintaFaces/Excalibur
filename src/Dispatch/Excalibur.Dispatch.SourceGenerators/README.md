# Excalibur.Dispatch.SourceGenerators

Source generators for the Dispatch messaging framework, enabling AOT-compatible code generation at compile time.

## Quick Start

### 1. Install

```bash
dotnet add package Excalibur.Dispatch.SourceGenerators
```

### 2. Mark Services with `[AutoRegister]`

```csharp
using Excalibur.Dispatch.Abstractions;
using Microsoft.Extensions.DependencyInjection;

// Basic usage - Scoped lifetime, registers as self and interfaces
[AutoRegister]
public class OrderHandler : IDispatchHandler<CreateOrderCommand>
{
    public Task<IMessageResult> HandleAsync(
        CreateOrderCommand message,
        IMessageContext context,
        CancellationToken ct)
    {
        // Handle the command
        return Task.FromResult(MessageResult.Success());
    }
}

// Explicit Singleton lifetime
[AutoRegister(Lifetime = ServiceLifetime.Singleton)]
public class CacheService : ICacheService { }

// Interfaces only (no concrete type registration)
[AutoRegister(AsSelf = false, AsInterfaces = true)]
public class MultiService : IFirst, ISecond { }
```

### 3. Call the Generated Extension

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register all [AutoRegister] services
builder.Services.AddGeneratedServices();

// Add Dispatch framework
builder.Services.AddDispatch();

var app = builder.Build();
```

## Features

| Feature | Description |
|---------|-------------|
| **[AutoRegister] Attribute** | Opt-in service registration at compile time |
| **AOT Compatibility** | Full Native AOT and trimming support |
| **Lifetime Control** | Singleton, Scoped, or Transient per service |
| **Interface Registration** | Control self vs interface registration |
| **Zero Reflection** | All discovery happens at compile time |

## Attribute Options

```csharp
[AutoRegister(
    Lifetime = ServiceLifetime.Scoped,  // Default: Scoped
    AsSelf = true,                       // Default: true - register as concrete type
    AsInterfaces = true                  // Default: true - register for interfaces
)]
public class MyService : IMyService { }
```

## Generated Output

The generator creates `AddGeneratedServices()` in `Microsoft.Extensions.DependencyInjection`:

```csharp
public static class GeneratedServiceCollectionExtensions
{
    public static IServiceCollection AddGeneratedServices(this IServiceCollection services)
    {
        services.AddScoped<MyService>();
        services.AddScoped<IMyService, MyService>();
        return services;
    }
}
```

## All Generators

| Generator | Status | Purpose |
|-----------|--------|---------|
| `ServiceRegistrationSourceGenerator` | **Active** | `[AutoRegister]` service registration |
| `HandlerRegistrySourceGenerator` | Active | Handler discovery at compile time |
| `HandlerInvokerSourceGenerator` | Active | Zero-reflection handler invocation |
| `JsonSerializationSourceGenerator` | Active | AOT-compatible JSON serialization |
| `MessageTypeSourceGenerator` | Active | Message type registration |
| `MessageTypeRegistrySourceGenerator` | Active | Aggregate message type registry |
| `RoutingRuleSourceGenerator` | Active | Compile-time routing rules |
| `MessageFactorySourceGenerator` | Active | Message instantiation |
| `CachePolicySourceGenerator` | Active | Caching policy generation |
| `CacheInfoSourceGenerator` | Active | Cache key generation |

## Documentation

- [Getting Started Guide](https://docs.excalibur-dispatch.dev/docs/source-generators/getting-started)
- [Source Generator Architecture](https://docs.excalibur-dispatch.dev/docs/advanced/source-generators)

## License

This project is multi-licensed under:

- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.

