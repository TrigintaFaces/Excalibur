---
sidebar_position: 5
title: Native AOT
description: Native AOT compilation guide for Excalibur
---

# Native AOT Support

Excalibur provides first-class Native AOT support through source generators that eliminate all reflection in handler resolution, JSON serialization, and result creation. This guide explains how to enable AOT compilation and what the framework generates for you.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.SourceGenerators.Analyzers
  ```
- Familiarity with [source generators](../source-generators/getting-started.md) and [deployment patterns](../deployment/aspnet-core.md)

## Overview

Native AOT compiles your application directly to machine code, eliminating the JIT compiler at runtime. This provides:

| Benefit | Description |
|---------|-------------|
| **Instant startup** | No JIT warm-up, sub-millisecond startup times |
| **Smaller binaries** | IL trimming removes unused code |
| **Lower memory** | No JIT compiler loaded in memory |
| **Predictable perf** | No JIT-related latency spikes |

Dispatch achieves AOT compatibility by generating all handler discovery, invocation, and serialization code at compile time via Roslyn source generators.

## Quick Start

### 1. Enable AOT in Your Project

```xml
<PropertyGroup>
    <PublishAot>true</PublishAot>
    <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

### 2. Mark Handlers with `[AutoRegister]`

```csharp
using Excalibur.Dispatch.Abstractions;

[AutoRegister]
public class CreateOrderHandler : IActionHandler<CreateOrderCommand>
{
    public Task HandleAsync(CreateOrderCommand message, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

### 3. Register Generated Services

```csharp
var services = new ServiceCollection();

services.AddDispatch(dispatch =>
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly));

// Register source-generated DI services
services.AddGeneratedServices();
```

### 4. Publish

```bash
dotnet publish -c Release
```

Expected: zero IL2XXX/IL3XXX trimming or AOT warnings.

---

## What Gets Generated

When you build with `Excalibur.Dispatch.SourceGenerators` referenced, the following compile-time code is produced:

### Handler Factory (`PrecompiledHandlerRegistry.g.cs`)

The `HandlerRegistrySourceGenerator` discovers all handler implementations and generates AOT-safe resolution:

```csharp
// Generated — switch expression, zero reflection
public static class PrecompiledHandlerRegistry
{
    public static void RegisterAll(IHandlerRegistry registry)
    {
        registry.Register(typeof(CreateOrderCommand), typeof(CreateOrderHandler), false);
    }

    public static Type? ResolveHandlerType(Type messageType)
    {
        return messageType switch
        {
            Type t when t == typeof(global::MyApp.CreateOrderCommand)
                => typeof(global::MyApp.CreateOrderHandler),
            _ => null
        };
    }

    public static object? CreateHandler(Type messageType, IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var handlerType = ResolveHandlerType(messageType);
        return handlerType is not null ? provider.GetRequiredService(handlerType) : null;
    }

    public static int HandlerCount => 1;
}
```

`ResolveHandlerType` uses a compiler-generated switch expression — no `Type.GetType()`, no reflection, no runtime assembly scanning.

### Message Type Metadata (`DiscoveredMessageTypeMetadata.g.cs`)

The `JsonSerializationSourceGenerator` produces compile-time metadata for all discovered `IDispatchMessage` types:

```csharp
// Generated — compile-time type registry
public static class DiscoveredMessageTypeMetadata
{
    public static IReadOnlyList<Type> MessageTypes { get; } = ImmutableArray.Create(new Type[]
    {
        typeof(CreateOrderCommand),
        typeof(GetOrderQuery),
        typeof(OrderCreatedEvent),
    });

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

This metadata is consumed by the framework's `CoreMessageJsonContext` and `CompositeAotJsonSerializer` for AOT-safe JSON serialization.

**Type filtering:** The generator automatically skips abstract types, open generic types, and non-public or nested types.

### Result Factory (`ResultFactoryRegistry.g.cs`)

The `MessageResultExtractorGenerator` generates AOT-safe result creation, replacing the reflection-based `ResultFactoryCache`:

```csharp
// Generated — no MakeGenericMethod(), no MethodInfo.Invoke()
public static partial class ResultFactoryRegistry
{
    internal static Func<object?, RoutingDecision?, object?, IAuthorizationResult?, bool, IMessageResult>?
        GetFactory(Type resultType)
    {
        return _factories.TryGetValue(resultType, out var factory) ? factory : null;
    }

    public static object? ExtractReturnValue(IMessageResult result)
    {
        return result switch
        {
            global::Excalibur.Dispatch.Abstractions.IMessageResult<Guid> r => r.ReturnValue,
            global::Excalibur.Dispatch.Abstractions.IMessageResult<OrderDto> r => r.ReturnValue,
            _ => null
        };
    }
}
```

When publishing with NativeAOT, `FinalDispatchHandler.CreateTypedResult()` automatically uses `ResultFactoryRegistry.GetFactory()` instead of reflection.

### Service Registration (`GeneratedServiceCollectionExtensions.g.cs`)

The `ServiceRegistrationSourceGenerator` generates DI registrations for `[AutoRegister]` types:

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class GeneratedServiceCollectionExtensions
{
    public static IServiceCollection AddGeneratedServices(this IServiceCollection services)
    {
        services.AddScoped<global::MyApp.CreateOrderHandler>();
        services.AddScoped<global::Excalibur.Dispatch.Abstractions.Delivery.IActionHandler<global::MyApp.CreateOrderCommand>,
            global::MyApp.CreateOrderHandler>();
        return services;
    }
}
```

**Key enhancement:** Uses `AllInterfaces` to discover handler interfaces from base types, so a handler inheriting from `BaseHandler<T>` that implements `IActionHandler<T>` will have the interface registration generated automatically.

---

## JSON Serialization

For AOT, JSON serialization requires `JsonSerializerContext` instead of runtime reflection. Dispatch provides two framework-level contexts:

| Context | Covers |
|---------|--------|
| `CoreMessageJsonContext` | Framework message types (`MessageResult`, `MessageContext`, etc.) |
| `CloudEventJsonContext` | CloudEvents envelope types |

The `DiscoveredMessageTypeMetadata` generated class provides the compile-time type registry that these contexts use.

### Consumer JSON Context

For your own DTOs, create a `JsonSerializerContext`:

```csharp
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(CreateOrderCommand))]
[JsonSerializable(typeof(GetOrderQuery))]
[JsonSerializable(typeof(OrderDto))]
public partial class AppJsonSerializerContext : JsonSerializerContext { }
```

Register it:

```csharp
services.AddSingleton(AppJsonSerializerContext.Default.Options);
```

---

## Type Resolution

Dispatch replaces all `Type.GetType()` calls with AOT-safe `TypeResolver.ResolveType()`:

```csharp
// AOT-safe — does not use Type.GetType()
var type = TypeResolver.ResolveType(typeName);     // returns null if not found
var type = TypeResolver.ResolveTypeRequired(typeName); // throws if not found
```

This affects serialization (`SpanEventSerializer`, `SerializerMigrationService`), CloudEvents processing (`EnvelopeCloudEventBridge`), and poison message handling (`PoisonMessageHandler`).

---

## How AOT Works Under the Hood

When you publish with `PublishAot=true`, the framework automatically switches internal code paths from reflection to source-generated registries. No consumer action is needed beyond referencing the source generators package.

Key behaviors:

- **Enum serialization** uses typed `JsonStringEnumConverter<TEnum>` and source-generated JSON contexts -- zero reflection.
- **Event serialization** uses `JsonSerializerContext.GetTypeInfo()` for type-safe serialization without `Type.GetType()`.
- **Handler resolution**, **saga coordination**, **caching**, and **projection invalidation** all use pre-compiled registries instead of `MakeGenericType()`.
- **Runtime branching** via `RuntimeFeature.IsDynamicCodeSupported` ensures JIT builds keep their existing behavior with zero overhead, while AOT builds use the source-generated path.
- **Annotation propagation**: Core dispatch paths (`Dispatcher`, `FinalDispatchHandler`, `IDirectLocalDispatcher`, etc.) carry `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` attributes so the trimmer accurately reports any reflection-dependent call sites. Source-generated alternatives eliminate these warnings when the generators package is referenced.

All of this is transparent to consumers. The same `AddDispatch()`, `AddSaga()`, and `AddProjection()` DI entry points work identically under both JIT and AOT.

### CI Enforcement

The AOT validation CI gate (`aot-validation.yml`) runs on every push and pull request. It publishes the AOT sample with `PublishAot=true` and fails the build if any new IL2XXX/IL3XXX warnings are introduced. A baseline file (`eng/ci/aot-warning-baseline.json`) tracks known false-positives from third-party dependencies that cannot be resolved upstream.

---

## Trimmer Configuration

`TrimmerRoots.xml` preserves critical types during IL trimming:

```xml
<linker>
  <assembly fullname="Excalibur.Dispatch.Abstractions">
    <type fullname="Excalibur.Dispatch.Abstractions.IDispatchMessage" preserve="all" />
    <type fullname="Excalibur.Dispatch.Abstractions.Delivery.IActionHandler`1" preserve="all" />
    <!-- ... -->
  </assembly>

  <assembly fullname="Excalibur.Dispatch">
    <type fullname="Excalibur.Dispatch.Delivery.Handlers.PrecompiledHandlerRegistry" preserve="all" />
    <type fullname="Excalibur.Dispatch.Delivery.Handlers.ResultFactoryRegistry" preserve="all" />
    <!-- ... -->
  </assembly>
</linker>
```

You typically do not need to modify this file. If you have custom types that must survive trimming, add them to your own `TrimmerRoots.xml` or use `[DynamicDependency]` attributes.

---

## Diagnostics

Source generators report diagnostic messages during compilation:

| ID | Severity | Description |
|----|----------|-------------|
| `HND001` | Info | Handler discovery count |
| `SRG001` | Info | Service registration count |
| `SRG002` | Warning | `[AutoRegister(AsInterfaces=true)]` with no discoverable interfaces |
| `JSON001` | Info | JSON message type count |

---

## AOT Sample

The `samples/11-aot/Excalibur.Dispatch.Aot.Sample` project demonstrates full AOT compilation:

```bash
cd samples/11-aot/Excalibur.Dispatch.Aot.Sample
dotnet publish -c Release
```

The sample showcases:
- `[AutoRegister]` on handlers for compile-time DI registration
- Source-generated `AppJsonSerializerContext` for AOT JSON serialization
- `PrecompiledHandlerRegistry` with `ResolveHandlerType` and `CreateHandler`
- `ResultFactoryRegistry` for AOT result creation
- `DiscoveredMessageTypeMetadata` for compile-time type registry
- Full pipeline dispatch via `IDispatcher.DispatchAsync()` (AOT-safe via source-generated interceptors)

---

## Transport AOT Compatibility

All 7 transport packages have been classified for AOT compatibility. The builder pattern (`Action<IXxxTransportBuilder>`) used by all transports avoids generic type parameters on public DI methods, making some transports inherently AOT-safe. Others have third-party SDK dependencies that use reflection.

### Transport Status

| Transport | AOT Status | Notes |
|-----------|-----------|-------|
| RabbitMQ | **AOT-safe** | Builder pattern only, no reflection |
| AWS SQS | **AOT-safe** | Builder pattern only, no reflection |
| Azure Service Bus | **Not compatible** | Azure SDK dependency uses reflection internally |
| Google Pub/Sub | **Not compatible** | Google Cloud SDK dependency uses reflection internally |
| Kafka | **Not compatible** | Confluent.Kafka SchemaRegistry uses `Activator.CreateInstance` |
| gRPC | **Not compatible** | gRPC code generation not AOT-safe |

See the [AOT Compatibility Matrix](aot-compatibility.md) for the full per-package table.

### Kafka SchemaRegistry Warning

When using Kafka with custom subject name strategies, consumers will see AOT warnings because `CreateSubjectNameStrategy()` uses `Activator.CreateInstance`:

```csharp
// Both AddKafkaTransport() overloads carry these attributes:
[RequiresUnreferencedCode("Schema Registry uses Activator.CreateInstance for custom subject name strategy types.")]
[RequiresDynamicCode("Schema Registry uses Activator.CreateInstance for custom subject name strategy types.")]
public static IServiceCollection AddKafkaTransport(
    this IServiceCollection services,
    string name,
    Action<IKafkaTransportBuilder> configure)
```

If you use only the built-in `TopicName` or `RecordName` strategies (not custom types), the reflection path is not taken at runtime.

To suppress the warning when you know your strategy is safe:

```csharp
#pragma warning disable IL2026, IL3050
services.AddKafkaTransport("kafka", builder => { ... });
#pragma warning restore IL2026, IL3050
```

---

## Scope and Limitations

### AOT Coverage

| Package | AOT Status |
|---------|-----------|
| `Excalibur.Dispatch` | Annotated (`[RequiresUnreferencedCode]` on reflection paths, source-gen alternative) |
| `Excalibur.Dispatch.Abstractions` | Fully AOT-compatible |
| `Excalibur.Dispatch.Transport.RabbitMQ` | AOT-safe |
| `Excalibur.Dispatch.Transport.AwsSqs` | AOT-safe |
| `Excalibur.Dispatch.Transport.Kafka` | Not compatible (SchemaRegistry reflection) |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | Not compatible (Azure SDK reflection) |
| `Excalibur.Dispatch.Transport.GooglePubSub` | Not compatible (Google Cloud SDK reflection) |
| `Excalibur.Dispatch.Transport.Grpc` | Not compatible (gRPC code generation) |

### Known Constraints

| Constraint | Detail |
|------------|--------|
| Source generators target `netstandard2.0` | Limited API surface (Roslyn requirement) |
| `IValidationResult` static abstract | Worked around with `object?` + `as` cast |
| Consumer JSON DTOs | Must create own `JsonSerializerContext` for custom types |
| Generated code always emits | Even with 0 discovered types (ensures compile safety) |
| Kafka SchemaRegistry | Custom strategies require `Activator.CreateInstance` — AOT warning |

---

## Troubleshooting

### IL2XXX/IL3XXX Warnings

If you see trimming warnings:

1. Ensure `Excalibur.Dispatch.SourceGenerators` is referenced
2. Check that handlers have `[AutoRegister]` attribute
3. Add `[DynamicallyAccessedMembers]` to custom reflection-heavy code
4. Verify `TrimmerRoots.xml` is included in your project

### Handlers Not Discovered

1. Handlers must be `public`, non-abstract classes
2. Handlers must implement `IActionHandler<T>`, `IEventHandler<T>`, or `IDocumentHandler<T>`
3. Handler interfaces must be from `Excalibur.Dispatch.Abstractions.Delivery` namespace
4. Clean and rebuild after adding new handlers

### Result Factory Not Working Under AOT

The `ResultFactoryRegistry` is generated from `IDispatchAction<T>` and `IActionHandler<TAction, TResult>` discovery. If your result types aren't discovered:

1. Ensure your action/query types implement `IDispatchAction<TResult>` with concrete result types
2. Verify the generator is active: check for `ResultFactoryRegistry.g.cs` in generated output
3. The `RuntimeFeature.IsDynamicCodeSupported` branch in `FinalDispatchHandler` automatically selects the AOT path when publishing with NativeAOT

---

## Related Documentation

- [AOT Compatibility Matrix](./aot-compatibility.md) - Per-package AOT status for all 173 packages
- [Source Generators](./source-generators.md) - Full generator reference
- [Viewing Generated Code](./viewing-generated-code.md) - Inspect generated output
- [Deployment](./deployment.md) - Deployment patterns

## See Also

- [Source Generators Getting Started](../source-generators/getting-started.md) — Step-by-step guide to enabling AOT-compatible source generators
- [Auto-Freeze](../performance/auto-freeze.md) — Automatic FrozenDictionary optimization for AOT-generated registrations
- [ASP.NET Core Deployment](../deployment/aspnet-core.md) — Deploying AOT-compiled applications in ASP.NET Core
- [Viewing Generated Code](./viewing-generated-code.md) — Inspecting source generator output in your IDE
