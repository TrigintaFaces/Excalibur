---
sidebar_position: 1
title: Getting Started with Source Generators
description: Quick start guide for using AutoRegister and Dispatch source generators
---

# Getting Started with Source Generators

The Dispatch source generators enable **compile-time service registration** with full AOT (Ahead-of-Time) compatibility. Instead of relying on runtime reflection to discover and register services, the source generator analyzes your code at compile time and generates explicit registration code.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the source generator package:
  ```bash
  dotnet add package Excalibur.Dispatch.SourceGenerators
  ```
- Familiarity with [dependency injection](../core-concepts/dependency-injection.md) and [actions and handlers](../core-concepts/actions-and-handlers.md)

## Why Use Source Generators?

| Benefit | Description |
|---------|-------------|
| **No Runtime Reflection** | All service discovery happens at compile time |
| **Faster Startup** | No assembly scanning required at application start |
| **Native AOT Support** | Compatible with `PublishAot=true` for .NET 8+ |
| **Trimming Safe** | No types unexpectedly removed by IL trimmer |
| **Explicit Control** | Opt-in registration prevents surprises |

## Quick Start

### 1. Install the Package

```bash
dotnet add package Excalibur.Dispatch.SourceGenerators
```

The `[AutoRegister]` attribute is included in `Excalibur.Dispatch.Abstractions`, which is automatically referenced.

### 2. Mark Your Services

Add the `[AutoRegister]` attribute to classes you want automatically registered:

```csharp
using Excalibur.Dispatch.Abstractions;
using Microsoft.Extensions.DependencyInjection;

// Basic usage - registers as Scoped by default
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
```

### 3. Call the Generated Extension

In your `Program.cs` or startup configuration, call the generated extension method:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register all services marked with [AutoRegister]
builder.Services.AddGeneratedServices();

// Your other service registrations...
builder.Services.AddDispatch();

var app = builder.Build();
```

That's it! The source generator discovers all `[AutoRegister]` types at compile time and generates the `AddGeneratedServices()` extension method with explicit registrations.

## Attribute Options

The `[AutoRegister]` attribute provides full control over how services are registered:

### Service Lifetime

```csharp
// Default: Scoped (recommended for request-scoped services)
[AutoRegister]
public class ScopedService : IScopedService { }

// Singleton: One instance for the entire application
[AutoRegister(Lifetime = ServiceLifetime.Singleton)]
public class CacheService : ICacheService { }

// Transient: New instance every time resolved
[AutoRegister(Lifetime = ServiceLifetime.Transient)]
public class HelperService : IHelperService { }
```

### Registration Mode

Control whether the service is registered by its concrete type, interfaces, or both:

```csharp
// Default: Register as both concrete type AND all interfaces
[AutoRegister]
public class MyService : IFirst, ISecond { }
// Generates:
//   services.AddScoped<MyService>();
//   services.AddScoped<IFirst, MyService>();
//   services.AddScoped<ISecond, MyService>();

// Concrete type only (no interface registration)
[AutoRegister(AsSelf = true, AsInterfaces = false)]
public class InternalHelper { }
// Generates:
//   services.AddScoped<InternalHelper>();

// Interfaces only (no concrete type registration)
[AutoRegister(AsSelf = false, AsInterfaces = true)]
public class MultiImplementation : IReader, IWriter { }
// Generates:
//   services.AddScoped<IReader, MultiImplementation>();
//   services.AddScoped<IWriter, MultiImplementation>();
```

## Common Patterns

### Handlers

```csharp
// Command handler (Scoped is appropriate for request-scoped handlers)
[AutoRegister]
public class CreateOrderHandler : IDispatchHandler<CreateOrderCommand>
{
    private readonly IOrderRepository _repository;

    public CreateOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<IMessageResult> HandleAsync(
        CreateOrderCommand command,
        IMessageContext context,
        CancellationToken ct)
    {
        var order = Order.Create(command.CustomerId, command.Items);
        await _repository.SaveAsync(order, ct);
        return MessageResult.Success();
    }
}
```

### Repository Services

```csharp
// Repository with explicit Scoped lifetime (matches DbContext scope)
[AutoRegister(Lifetime = ServiceLifetime.Scoped)]
public class OrderRepository : IOrderRepository
{
    private readonly IEventStore _eventStore;

    public OrderRepository(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    // Implementation...
}
```

### Singleton Caches

```csharp
// Singleton for application-wide caching
[AutoRegister(Lifetime = ServiceLifetime.Singleton)]
public class HandlerMetadataCache : IHandlerMetadataCache
{
    private readonly ConcurrentDictionary<Type, HandlerMetadata> _cache = new();

    // Implementation...
}
```

### Middleware

```csharp
// Middleware typically uses Scoped lifetime
[AutoRegister]
public class ValidationMiddleware : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken ct)
    {
        // Validation logic...
        return await nextDelegate(message, context, ct);
    }
}
```

## Generated Code

The source generator produces a single file with an extension method. You can inspect it by enabling generated file output:

```xml
<!-- In your .csproj -->
<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\GeneratedFiles</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

**Example generated output:**

```csharp
// <auto-generated/>
namespace Microsoft.Extensions.DependencyInjection;

public static class GeneratedServiceCollectionExtensions
{
    /// <summary>
    /// Registers all services discovered at compile time via [AutoRegister] attribute.
    /// </summary>
    public static IServiceCollection AddGeneratedServices(this IServiceCollection services)
    {
        services.AddScoped<MyApp.Services.OrderHandler>();
        services.AddScoped<MyApp.Services.IDispatchHandler<CreateOrderCommand>, MyApp.Services.OrderHandler>();
        services.AddSingleton<MyApp.Services.CacheService>();
        services.AddSingleton<MyApp.Services.ICacheService, MyApp.Services.CacheService>();

        return services;
    }

    /// <summary>
    /// Gets the count of services discovered at compile time.
    /// </summary>
    public static int GeneratedServiceCount => 2;
}
```

## Coexistence with Manual Registration

Generated registrations work alongside manual registrations:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Generated registrations from [AutoRegister]
builder.Services.AddGeneratedServices();

// Manual registrations for special cases
builder.Services.AddSingleton<ISpecialService>(sp =>
    new SpecialService(configuration["SpecialKey"]));

// Dispatch framework
builder.Services.AddDispatch();

var app = builder.Build();
```

Services without `[AutoRegister]` must be registered manually. This explicit control prevents unexpected registrations.

## Excluded Types

The generator automatically skips:

- **Abstract classes** - Cannot be instantiated
- **Static classes** - Cannot be registered as services
- **System interfaces** - `IDisposable`, `IAsyncDisposable`, etc. are not registered

## Troubleshooting

### Generated Method Not Found

If `AddGeneratedServices()` is not available:

1. Ensure `Excalibur.Dispatch.SourceGenerators` package is referenced
2. Clean and rebuild the solution
3. Check the build output for analyzer errors

### Build Diagnostic SRG001

When services are discovered, you'll see an informational diagnostic:

```
info SRG001: Discovered 5 type(s) with [AutoRegister] attribute for service registration
```

This confirms the generator is working correctly.

### No Services Discovered

If `GeneratedServiceCount` is 0:

1. Verify classes have `[AutoRegister]` attribute
2. Ensure classes are not abstract or static
3. Check that the attribute namespace `Excalibur.Dispatch.Abstractions` is imported

## Next Steps

- [Source Generator Architecture](../advanced/source-generators.md) - Deep dive into all generators
- [Dependency Injection](../core-concepts/dependency-injection.md) - DI patterns and best practices
- [Handlers](../handlers.md) - Handler implementation patterns

## See Also

- [Source Generators Overview](index.md) - All available source generators
- [Source Generator Architecture](../advanced/source-generators.md) - Deep dive into generator internals
- [Native AOT](../advanced/native-aot.md) - AOT compilation and source generator compatibility

