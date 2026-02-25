---
sidebar_position: 4
title: Source Generators
description: AOT-compatible source generators for Dispatch
---

# Source Generators

Dispatch includes Roslyn source generators that enable ahead-of-time (AOT) compilation and Native AOT support by generating explicit code at compile time instead of using runtime reflection.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.SourceGenerators.Analyzers
  ```
- Familiarity with [dependency injection](../core-concepts/dependency-injection.md) and [actions and handlers](../core-concepts/actions-and-handlers.md)

## Overview

### What Are Source Generators?

Source generators are Roslyn compiler extensions that analyze your code during compilation and generate additional C# source files. Unlike runtime reflection, all code generation happens at compile time, making your application:

- **AOT-compatible** - Works with Native AOT (`PublishAot=true`)
- **Trimming-safe** - No types unexpectedly removed by IL Linker
- **Faster at startup** - No reflection-based discovery
- **Debuggable** - Generated code is visible and can be inspected

### Why Dispatch Uses Source Generators

| Benefit | Description |
|---------|-------------|
| **Zero-reflection dispatch** | Handler resolution at compile time, not runtime |
| **Faster handler activation** | Pre-compiled property setters, no `Expression.Compile()` |
| **Static pipelines** | Middleware chains inlined for deterministic messages |
| **AOT deployment** | Native executables without JIT compilation |

### Generator Inventory

The `Excalibur.Dispatch.SourceGenerators` package includes:

| Generator | Purpose | Output File |
|-----------|---------|-------------|
| [`HandlerRegistrySourceGenerator`](#handlerregistrysourcegenerator) | Discovers, registers, and resolves handlers (AOT factory) | `PrecompiledHandlerRegistry.g.cs` |
| [`HandlerActivationGenerator`](#handleractivationgenerator) | AOT-compatible handler activation | `SourceGeneratedHandlerActivator.g.cs` |
| [`HandlerInvocationGenerator`](#handlerinvocationgenerator) | Zero-reflection handler invocation | `SourceGeneratedHandlerInvoker.g.cs` |
| [`MessageTypeSourceGenerator`](#messagetypesourcegenerator) | Message type metadata | `PrecompiledHandlerMetadata.g.cs` |
| [`StaticPipelineGenerator`](#staticpipelinegenerator) | Static middleware pipelines | `StaticPipelines.g.cs` |
| [`DispatchInterceptorGenerator`](#dispatchinterceptorgenerator) | C# 12 dispatch interceptors | `DispatchInterceptors.g.cs` |
| [`MiddlewareDecompositionAnalyzer`](#middlewaredecompositionanalyzer) | Middleware analysis | `MiddlewareDecomposition.g.cs` |
| [`CachePolicySourceGenerator`](#cachepolicysourcegenerator) | Cache policy registration | `CacheInfoRegistry.g.cs` |
| [`JsonSerializationSourceGenerator`](#jsonserializationsourcegenerator) | Message type metadata for AOT serialization | `DiscoveredMessageTypeMetadata.g.cs` |
| [`MessageResultExtractorGenerator`](#messageresultextractorgenerator) | AOT result factory (no reflection) | `ResultFactoryRegistry.g.cs` |
| [`ServiceRegistrationSourceGenerator`](#serviceregistrationsourcegenerator) | DI service registration | `GeneratedServiceCollectionExtensions.g.cs` |

## Installation

```bash
dotnet add package Excalibur.Dispatch.SourceGenerators
```

The generators run automatically during compilation - no additional configuration required.

---

## Handler Generators

### HandlerRegistrySourceGenerator

Discovers all handler implementations at compile time and generates registration code.

**Discovers:**
- `IActionHandler<TMessage>` implementations
- `IActionHandler<TMessage, TResponse>` implementations
- `IEventHandler<TEvent>` implementations
- `IDocumentHandler<TDocument>` implementations

**Generated Output:**

```csharp
// PrecompiledHandlerRegistry.g.cs
public static class PrecompiledHandlerRegistry
{
    public static void RegisterAll(IHandlerRegistry registry)
    {
        registry.Register(typeof(CreateOrderCommand), typeof(CreateOrderHandler), false);
        registry.Register(typeof(GetOrderQuery), typeof(GetOrderHandler), true);
    }

    // AOT handler resolution — switch expression, zero reflection
    public static Type? ResolveHandlerType(Type messageType)
    {
        return messageType switch
        {
            Type t when t == typeof(global::MyApp.CreateOrderCommand)
                => typeof(global::MyApp.CreateOrderHandler),
            Type t when t == typeof(global::MyApp.GetOrderQuery)
                => typeof(global::MyApp.GetOrderHandler),
            _ => null
        };
    }

    // AOT handler creation via DI — no Type.GetType() or assembly scanning
    public static object? CreateHandler(Type messageType, IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var handlerType = ResolveHandlerType(messageType);
        return handlerType is not null ? provider.GetRequiredService(handlerType) : null;
    }

    public static int HandlerCount => 2;
}
```

`ResolveHandlerType` and `CreateHandler` were added in Sprint 521 to support Native AOT compilation. Under `#if AOT_ENABLED`, `HandlerRegistryBootstrapper` uses these methods instead of reflection-based fallbacks. See [Native AOT](./native-aot.md) for details.

**Diagnostic:** `HND001` - Reports handler discovery count during compilation.

---

### HandlerActivationGenerator

Generates AOT-compatible handler activation code that sets `IMessageContext` properties without reflection.

**Input Requirements:**
- Public, non-abstract class implementing a handler interface
- Optional `IMessageContext` property with public/internal setter

**Generated Output:**

```csharp
// SourceGeneratedHandlerActivator.g.cs
public sealed class SourceGeneratedHandlerActivator : IHandlerActivator
{
    public object ActivateHandler(Type handlerType, IMessageContext context, IServiceProvider provider)
    {
        var handler = provider.GetRequiredService(handlerType);
        SetHandlerContext(handler, context);
        return handler;
    }

    private static void SetHandlerContext(object handler, IMessageContext context)
    {
        switch (handler)
        {
            case CreateOrderHandler typedHandler:
                typedHandler.Context = context;
                break;
            // ... other handlers with context properties
        }
    }
}
```

**Use Case:** Handlers that need access to `IMessageContext` without constructor injection.

---

### HandlerInvocationGenerator

Generates type-safe handler invocation code that eliminates virtual dispatch overhead.

**Generated Output:**

```csharp
// SourceGeneratedHandlerInvoker.g.cs
public sealed class SourceGeneratedHandlerInvoker : IHandlerInvoker
{
    public Task<object?> InvokeAsync(object handler, IDispatchMessage message, CancellationToken ct)
    {
        return handler switch
        {
            CreateOrderHandler h when message is CreateOrderCommand m =>
                ConvertTaskOfTToTaskOfObject(h.HandleAsync(m, ct)),
            GetOrderHandler h when message is GetOrderQuery m =>
                ConvertTaskOfTToTaskOfObject(h.HandleAsync(m, ct)),
            _ => throw new InvalidOperationException(...)
        };
    }
}
```

**Benefits:**
- No reflection at runtime
- Direct method calls via pattern matching
- Proper async handling with result type coercion

---

### MessageTypeSourceGenerator

Generates metadata about discovered handlers for runtime queries.

**Generated Output:**

```csharp
// PrecompiledHandlerMetadata.g.cs
public static class PrecompiledHandlerMetadata
{
    public static bool TryGetHandlerForMessage(Type messageType, out HandlerMetadata? metadata);
    public static ImmutableArray<Type> GetMessageTypesForHandler(Type handlerType);

    public sealed class HandlerMetadata
    {
        public required Type HandlerType { get; init; }
        public required Type MessageType { get; init; }
        public required bool HasResponse { get; init; }
        public Type? ResponseType { get; init; }
    }
}
```

**Use Cases:**
- Query handler registrations at runtime
- Validate message-handler bindings
- Build documentation/introspection tools

---

## Pipeline Generators

### StaticPipelineGenerator

Creates fully static middleware pipelines for deterministic message types, eliminating delegate allocation.

**Triggers:** `DispatchAsync<TMessage>` calls where:
- Message type is statically known (not interface or type parameter)
- Message implements `IDispatchMessage`
- Pipeline is deterministic (no runtime-conditional middleware)

**Generated Output:**

```csharp
// StaticPipelines.g.cs
file static class StaticPipelines
{
    [InterceptsLocation(1, "...")]
    internal static async Task<IMessageResult> CreateOrder_L42_C12(
        this IDispatcher dispatcher,
        CreateOrderCommand message,
        IMessageContext context,
        CancellationToken cancellationToken)
    {
        if (_isHotReloadEnabled)
        {
            // Fallback to dynamic pipeline
            return await ((Dispatcher)dispatcher).DispatchAsync<CreateOrderCommand>(...);
        }

        // Static pipeline with zero delegate allocation
        try
        {
            return await ((Dispatcher)dispatcher).DispatchAsync<CreateOrderCommand>(...);
        }
        catch (Exception ex)
        {
            return MessageResult.Exception(ex);
        }
    }

    public static int InterceptionCount => 1;
}
```

**Determinism Checks:**
Messages with these attributes are non-deterministic and fallback to runtime pipelines:
- `[PipelineProfile]` with dynamic selection
- `[TenantSpecific]`, `[PerTenant]`, `[MultiTenant]`
- `[ConditionalMiddleware]`, `[FeatureFlagMiddleware]`

**Hot Reload:** Automatically detected via `DOTNET_WATCH` and `DOTNET_MODIFIABLE_ASSEMBLIES` environment variables.

---

### DispatchInterceptorGenerator

Creates C# 12 interceptors that redirect `DispatchAsync` calls to optimized static methods.

**Resolution Hierarchy:**
1. **Intercepted** (this generator) - Direct static dispatch, zero lookups
2. **Precompiled** - FrozenDictionary lookup
3. **Runtime** - Reflection-based fallback

**Generated Output:**

```csharp
// DispatchInterceptors.g.cs
file static class DispatchInterceptors
{
    [InterceptsLocation(1, "...")]
    internal static async Task<IMessageResult<OrderDto>> Intercept_abc123(
        this IDispatcher dispatcher,
        GetOrderQuery message,
        IMessageContext context,
        CancellationToken cancellationToken)
    {
        // PERF-9: Direct dispatch through Dispatcher internals
        return await ((Dispatcher)dispatcher).DispatchAsync<GetOrderQuery, OrderDto>(
            message, context, cancellationToken).ConfigureAwait(false);
    }
}
```

**Requirements:**
- .NET 8+ with C# 12
- `<LangVersion>preview</LangVersion>` or `12.0`

---

### MiddlewareDecompositionAnalyzer

Analyzes middleware implementations to determine if they can be decomposed into Before/After phases for static inlining.

**Analyzes:**
- Statement position relative to `next()` call
- State variables crossing the `next()` boundary
- Control flow patterns (try/catch/finally, using)

**Generated Output:**

```csharp
// MiddlewareDecomposition.g.cs
file static class MiddlewareDecompositionMetadata
{
    private static readonly FrozenDictionary<Type, DecompositionInfo> _decompositions;

    public static bool IsDecomposable<TMiddleware>();
    public static bool IsDecomposable(Type middlewareType);
    public static DecompositionInfo? GetInfo(Type middlewareType);

    public static int TotalCount => 5;
    public static int DecomposableCount => 3;
}

file readonly record struct DecompositionInfo(
    bool IsDecomposable,
    bool HasBeforePhase,
    bool HasAfterPhase,
    bool HasTryCatch,
    bool HasFinally,
    bool HasUsing,
    bool CanShortCircuit,
    int StateVariableCount,
    string? NonDecomposableReason);
```

**Non-Decomposable Patterns:**
- Multiple `next()` calls (retry patterns)
- `next()` inside loops
- `next()` inside runtime-dependent conditionals
- Missing `next()` call

---

## Caching Generator

### CachePolicySourceGenerator

Generates compile-time cache policy registration for types implementing `ICacheable<T>` or decorated with `[CacheResult]`.

**Generated Output:**

```csharp
// CacheInfoRegistry.g.cs
public static class CacheInfoRegistry
{
    public static CacheableInfo? GetCacheableInfo(IDispatchMessage message);
    public static CacheAttributeInfo? GetCacheAttributeInfo(IDispatchMessage message);
    public static bool IsCacheable(Type messageType);
    public static bool HasCacheAttribute(Type messageType);
    public static bool? InvokeCachePolicy(object policy, IDispatchMessage message, object? result);
    public static object? ExtractReturnValue(IMessageResult result);

    public sealed class CacheableInfo { ... }
    public sealed class CacheAttributeInfo { ... }
}
```

**Input Requirements:**

```csharp
// Via ICacheable<T> interface
public record GetProductQuery(Guid Id) : IDispatchAction<ProductDto>, ICacheable<ProductDto>
{
    public bool ShouldCache(ProductDto result) => result != null;
    public int ExpirationSeconds => 300;
    public string[]? GetCacheTags() => new[] { $"product:{Id}" };
}

// Via [CacheResult] attribute
[CacheResult(ExpirationSeconds = 60, Tags = new[] { "products" })]
public record GetAllProductsQuery : IDispatchAction<ProductDto[]>;
```

---

## Serialization Generators

### JsonSerializationSourceGenerator

Generates compile-time metadata for all discovered `IDispatchMessage` types, enabling AOT-safe serialization.

**Discovers:**
- All concrete (non-abstract, non-generic, public) types implementing `IDispatchMessage`

**Generated Output:**

```csharp
// DiscoveredMessageTypeMetadata.g.cs
public static class DiscoveredMessageTypeMetadata
{
    /// <summary>
    /// Gets all discovered concrete message types (compile-time).
    /// </summary>
    public static IReadOnlyList<Type> MessageTypes { get; } = ImmutableArray.Create(new Type[]
    {
        typeof(CreateOrderCommand),
        typeof(GetOrderQuery),
        typeof(OrderCreatedEvent),
    });

    /// <summary>
    /// Checks whether a given type is a discovered message type.
    /// </summary>
    public static bool IsMessageType(Type type)
    {
        return type switch
        {
            Type t when t == typeof(CreateOrderCommand) => true,
            Type t when t == typeof(GetOrderQuery) => true,
            Type t when t == typeof(OrderCreatedEvent) => true,
            _ => false
        };
    }
}
```

This metadata is consumed by the framework's `CoreMessageJsonContext` and `CompositeAotJsonSerializer` for AOT-safe JSON serialization without runtime type discovery.

**Type Filtering:** Abstract types, open generic types, and non-public/nested types are automatically excluded.

**Diagnostic:** `JSON001` - Reports discovered message type count during compilation.

---

### MessageResultExtractorGenerator

Generates AOT-safe result creation code that replaces the reflection-based `ResultFactoryCache` (which uses `MakeGenericMethod()` and `MethodInfo.Invoke()`).

**Discovers:**
- Concrete result types from `IDispatchAction<TResult>` implementations
- Return types from `IActionHandler<TAction, TResult>` implementations

**Generated Output:**

```csharp
// ResultFactoryRegistry.g.cs
public static partial class ResultFactoryRegistry
{
    // Factory dictionary keyed by result Type
    internal static Func<object?, RoutingDecision?, object?, IAuthorizationResult?, bool, IMessageResult>?
        GetFactory(Type resultType)
    {
        return _factories.TryGetValue(resultType, out var factory) ? factory : null;
    }

    // AOT-safe return value extraction
    public static object? ExtractReturnValue(IMessageResult result)
    {
        return result switch
        {
            global::Excalibur.Dispatch.Messaging.MessageResult<Guid> r => r.ReturnValue,
            global::Excalibur.Dispatch.Messaging.MessageResult<OrderDto> r => r.ReturnValue,
            _ => null
        };
    }
}
```

Under `#if AOT_ENABLED`, `FinalDispatchHandler.CreateTypedResult()` calls `ResultFactoryRegistry.GetFactory()` instead of using reflection. The existing `ResultFactoryCache` remains as the JIT fallback.

**Generated code uses `MessageResult.Success<T>()`** — the `MessageResult<T>` type is constructed via static factory methods in the `Excalibur.Dispatch.Messaging.HighPerformance` namespace.

---

## DI Registration Generator

### ServiceRegistrationSourceGenerator

Generates DI registration code for types marked with `[AutoRegister]` attribute.

**Quick Start:**

```csharp
using Excalibur.Dispatch.Abstractions;

// 1. Mark services for auto-registration
[AutoRegister]
public class OrderHandler : IActionHandler<CreateOrderCommand>
{
    // IActionHandler<T> returns Task (void) - framework handles result wrapping
    public Task HandleAsync(CreateOrderCommand message, CancellationToken ct)
        => Task.CompletedTask;
}

// 2. Call the generated extension
builder.Services.AddGeneratedServices();
```

**The `[AutoRegister]` Attribute:**

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AutoRegisterAttribute : Attribute
{
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;
    public bool AsSelf { get; set; } = true;
    public bool AsInterfaces { get; set; } = true;
}
```

**Usage Examples:**

```csharp
// Default: Scoped, registered as self AND all interfaces
[AutoRegister]
public class MyService : IFirst, ISecond { }
// Generates:
//   services.AddScoped<MyService>();
//   services.AddScoped<IFirst, MyService>();
//   services.AddScoped<ISecond, MyService>();

// Singleton lifetime
[AutoRegister(Lifetime = ServiceLifetime.Singleton)]
public class CacheService : ICacheService { }

// Interfaces only (no concrete type registration)
[AutoRegister(AsSelf = false)]
public class MultiImpl : IReader, IWriter { }
```

**Generated Output:**

```csharp
// GeneratedServiceCollectionExtensions.g.cs
namespace Microsoft.Extensions.DependencyInjection;

public static class GeneratedServiceCollectionExtensions
{
    public static IServiceCollection AddGeneratedServices(this IServiceCollection services)
    {
        services.AddScoped<global::MyApp.MyService>();
        services.AddScoped<global::MyApp.IFirst, global::MyApp.MyService>();
        services.AddScoped<global::MyApp.ISecond, global::MyApp.MyService>();
        return services;
    }

    public static int GeneratedServiceCount => 1;
}
```

**Interface Discovery:** Uses `AllInterfaces` (not just `Interfaces`) to discover handler interfaces from base types. A handler inheriting from `BaseHandler<T>` that implements `IActionHandler<T>` will have the interface registration generated automatically. System namespaces (`System`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`) are excluded from interface discovery.

**Diagnostics:**
- `SRG001` - Reports discovered services during compilation
- `SRG002` - Warning when `[AutoRegister(AsInterfaces=true)]` is used but no discoverable interfaces are found

---

## Viewing Generated Code

See [Viewing Generated Code](./viewing-generated-code.md) for IDE-specific instructions.

**Quick MSBuild Configuration:**

```xml
<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\GeneratedFiles</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated files appear in `obj/GeneratedFiles/Excalibur.Dispatch.SourceGenerators/`.

---

## Integration Testing with CSharpGeneratorDriver

All source generators are verified through CSharpGeneratorDriver integration tests that compile real C# source code through the Roslyn pipeline and assert on the generated output.

### Test Pattern

```csharp
[Fact]
public void HandlerRegistry_WithActionHandler_GeneratesPrecompiledHandlerRegistry()
{
    const string source = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Excalibur.Dispatch.Abstractions;
        using Excalibur.Dispatch.Abstractions.Delivery;

        namespace TestApp
        {
            public class CreateOrderCommand : IDispatchAction<Guid> { }
            public class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
            {
                public Task<Guid> HandleAsync(CreateOrderCommand message, CancellationToken cancellationToken)
                    => Task.FromResult(Guid.Empty);
            }
        }
        """;

    var result = RunGenerator<HandlerRegistrySourceGenerator>(source);

    result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    result.GeneratedTrees.ShouldNotBeEmpty();

    var generatedFiles = result.GeneratedTrees
        .Select(t => Path.GetFileName(t.FilePath)).ToList();
    generatedFiles.ShouldContain("PrecompiledHandlerRegistry.g.cs");
}
```

Tests use **semantic assertions** (verify generated files exist, output contains expected signatures, code compiles without errors) rather than snapshot testing of exact text, which is brittle against whitespace and ordering changes.

### Coverage

| Generator | Integration Tests | Focus |
|-----------|------------------|-------|
| HandlerRegistrySourceGenerator | 8 | ResolveHandlerType, CreateHandler, multiple handlers, empty set |
| JsonSerializationSourceGenerator | 8 | MessageTypeMetadata, type filtering, abstract/generic exclusion |
| MessageResultExtractorGenerator | 7 | ResultFactoryRegistry, factory methods, ExtractReturnValue |
| ServiceRegistrationSourceGenerator | 7 | AutoRegister, AllInterfaces, SRG002 diagnostic |

See `tests/unit/Excalibur.Dispatch.SourceGenerators.Tests/Generators/Integration/CSharpGeneratorDriverIntegrationShould.cs` for the full test suite.

---

## Troubleshooting

### Generator Not Running

**Symptoms:** No generated files, handlers not discovered

**Solutions:**
1. Ensure `Excalibur.Dispatch.SourceGenerators` package is referenced
2. Clean and rebuild the solution
3. Check for analyzer errors in build output
4. Verify handler interfaces are from `Excalibur.Dispatch.Abstractions`

### Duplicate Registration Errors

**Symptoms:** `InvalidOperationException: Service already registered`

**Solutions:**
1. Check for manual registration of the same type
2. Remove `[AutoRegister]` if manually registering
3. Use `services.TryAdd*` for conditional registration

### AOT/Trimming Warnings

**Symptoms:** `IL2026` or `IL2104` warnings with Native AOT

**Solutions:**
1. All Dispatch generators include `[UnconditionalSuppressMessage]` for known-safe patterns
2. For custom code, add `[DynamicallyAccessedMembers]` to reflection-heavy methods
3. Test with `PublishAot=true` during development

### Interceptor Issues

**Symptoms:** Interceptors not activating

**Solutions:**
1. Verify C# 12+ language version
2. Check call site is not within `Excalibur.Dispatch.*` namespaces (excluded to avoid conflicts)
3. Ensure message type is concrete (not interface or type parameter)
4. Check for hot reload mode (interceptors fall back to dynamic dispatch)

---

## Best Practices

### 1. Keep Messages in Separate Assemblies

For optimal generator performance, keep message types in dedicated assemblies:

```
MyApp.Messages/          # Message types
MyApp.Handlers/          # Handler implementations
MyApp.Api/               # Application host
```

### 2. Use Deterministic Pipelines

For maximum performance, design message types that qualify for static pipelines:

```csharp
// Good - deterministic, gets static pipeline
public record CreateOrderCommand(Guid OrderId) : IDispatchAction;

// Avoid - non-deterministic, falls back to runtime
[TenantSpecific]
public record CreateTenantOrderCommand(Guid OrderId) : IDispatchAction;
```

### 3. Verify Generated Output

During development, enable `EmitCompilerGeneratedFiles` and inspect the output to understand what's being generated.

### 4. Combine with Auto-Freeze

Source generators work best with [Auto-Freeze](../performance/auto-freeze.md) - the generated registrations are frozen into `FrozenDictionary` at startup for optimal runtime performance.

---

## Related Documentation

- [Native AOT](./native-aot.md) - Complete Native AOT compilation guide
- [Viewing Generated Code](./viewing-generated-code.md) - IDE setup and file locations
- [Performance Overview](../performance/index.md) - Performance optimization guide
- [Auto-Freeze](../performance/auto-freeze.md) - Automatic cache optimization
- [Handlers](../handlers.md) - Handler implementation patterns
- [Deployment](./deployment.md) - AOT deployment considerations

## See Also

- [Source Generators Getting Started](../source-generators/getting-started.md) — Step-by-step setup guide for enabling source generators in your project
- [Native AOT](./native-aot.md) — Complete Native AOT compilation guide using source generators
- [Dependency Injection](../core-concepts/dependency-injection.md) — DI registration patterns including AutoRegister integration
- [Viewing Generated Code](./viewing-generated-code.md) — How to inspect generated output in Visual Studio, Rider, and VS Code
