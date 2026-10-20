---
sidebar_position: 5
title: Native AOT
description: Native AOT compilation guide for Excalibur
---

# Native AOT Support

Dispatch provides Native AOT paths through generated handler registration and invocation, together with explicit serialization metadata. Compatibility depends on the handlers, middleware, serializers, and integrations your application selects. Publish and execute your application to verify those paths.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [source generators](../source-generators/getting-started.md) and [deployment patterns](../deployment/aspnet-core.md)

## Overview

Native AOT compiles your application directly to machine code, eliminating the JIT compiler at runtime. This provides:

| Benefit | Description |
|---------|-------------|
| **Startup** | No JIT warm-up; measure total application startup on your deployment target |
| **Smaller binaries** | IL trimming removes unused code |
| **Lower memory** | No JIT compiler loaded in memory |
| **Predictable perf** | No JIT-related latency spikes |

Dispatch's generators cover supported handler shapes and generate direct invocation paths. Inaccessible or open generic shapes require a supported registration strategy; generated coverage is not a guarantee that every application dependency is AOT compatible. See [source generator coverage](./source-generators.md).

For pipeline validation, run `samples/10-aot/Excalibur.Dispatch.Aot.Sample` as a published native executable. It asserts middleware execution, explicit handler-interface invocation, scoped asynchronous disposal, and background execution ownership. Middleware backed by asynchronous-only disposable services should use [the deferred factory registration](../pipeline/index.md); ordinary factory registration probes middleware synchronously during composition.

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
using Excalibur.Dispatch;

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

// AOT-safe: source-generated handler registration (zero reflection)
services.AddDispatch(dispatch => dispatch.AddDiscoveredHandlers());

// Register source-generated DI services
services.AddGeneratedServices();
```

### 4. Handler Activation and Context Binding

`AddDispatch` selects `AotHandlerActivator` when the runtime does not support dynamic code.
It respects an existing activator registration. To select the same container-based strategy
explicitly, including when testing in a managed process, register it before `AddDispatch`:

```csharp
using Excalibur.Dispatch.Delivery.Handlers;

// Before AddDispatch: Dispatch registers its default activator only if none is present.
services.AddSingleton<IHandlerActivator, AotHandlerActivator>();

services.AddDispatch(dispatch => dispatch.AddDiscoveredHandlers());
services.AddGeneratedServices();
```

`AotHandlerActivator` resolves the handler from the service provider, falling back to constructor
injection when the handler is not registered. Context binding uses this precedence:

1. `IMessageContextAware.SetContext`, including explicit and inherited implementations.
2. A typed setter registered through `HandlerActivatorRegistry`, normally by generated code.
3. A cached property plan for the requested handler type's first writable instance property
   whose type is exactly `IMessageContext`. Static properties, indexers, read-only properties
   and properties of other types are excluded. Inherited public properties and private setters
   on public properties retain the same eligibility as the expression activator.

Prefer the interface or a registered setter for frequently activated handlers. The property
fallback uses metadata preserved by the `IHandlerActivator` type annotation; it does not compile
expressions or generate code at runtime. Native AOT permits analyzable reflection with the
required preservation annotations; it does not impose a blanket reflection ban.
See [Microsoft's AOT library guidance](https://devblogs.microsoft.com/dotnet/creating-aot-compatible-libraries/).

A later registered setter takes precedence in direct `AotHandlerActivator` calls even when a
property or empty fallback plan is already cached. Cached dispatcher plans do not yet preserve
that late-registration behavior; register setters during composition until that gap is repaired.
A setter exception propagates to the caller; activation does not return an
incompletely configured handler. Reflection's invocation wrapper is removed to preserve the
original setter exception. Handlers still need container registration or a public constructor
whose dependencies the container can supply.

For repeatable native verification of these binding paths, publish and run the
`tests/harnesses/HandlerActivationAotProbe` project. Its executable requires
`RuntimeFeature.IsDynamicCodeSupported == false` and checks actual context identities, including
private setters, inherited properties, registry precedence and concurrent transient activation.
Its full-dispatch check selects the native default activator and uses typed handler registration.
The typed registration helpers preserve constructors and context properties through the DI
descriptor boundary. For manual composition, use the helper matching the handler shape:

```csharp
services.AddActionHandler<CreateOrder, CreateOrderHandler>();
services.AddActionHandler<GetOrder, OrderDto, GetOrderHandler>();
services.AddEventHandler<OrderCreated, OrderCreatedHandler>();
services.AddDocumentHandler<Invoice, InvoiceHandler>();
```

These helpers register the concrete handler and its interface. Dispatch resolves the concrete
registration; an existing concrete registration remains authoritative. Interface and concrete
resolutions are independent DI registrations, even for scoped or singleton lifetimes. The helpers
may be called before or after `AddDispatch`, before building the provider. For NativeAOT invocation,
also use source-generated invokers or explicitly register closed pairs through
`HandlerInvokerRegistry.RegisterInvoker`; preserving activation metadata does not generate invokers.

Ordinary `AddTransient<IActionHandler<T>, THandler>()` descriptors alone do not guarantee preservation of context
properties under trimming. The failed ordinary-descriptor native check and the low-level annotated
`IHandlerRegistry.Register` control are retained. The current probe exercises all four typed helper
shapes through full dispatch without generated context setters or an extra registry call. These
checks establish functional behavior, not NativeAOT performance.

### 5. Publish

```bash
dotnet publish -c Release
```

Expected: zero IL2XXX/IL3XXX trimming or AOT warnings.

---

## What Gets Generated

When you build with `Excalibur.Dispatch` referenced, its bundled generators produce the following compile-time code:

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
            global::Excalibur.Dispatch.IMessageResult<Guid> r => r.ReturnValue,
            global::Excalibur.Dispatch.IMessageResult<OrderDto> r => r.ReturnValue,
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
        services.AddScoped<global::Excalibur.Dispatch.Delivery.IActionHandler<global::MyApp.CreateOrderCommand>,
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

SQL Server outbox header reads use package-generated metadata for the persisted JSON dictionary.
Non-null header values reload as `JsonElement`; null values remain null, and header keys remain case-sensitive.
This reader has been verified with JSON reflection disabled in a native executable and through a fresh-store
round trip against SQL Server. That verification covers header decoding; publish and execute your application's
database and payload serialization paths separately. Generated dictionary metadata does not supply metadata
for arbitrary runtime objects written into headers or message payloads.

### Consumer JSON Context

For your own DTOs, create a `JsonSerializerContext`:

```csharp
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CreateOrderCommand))]
[JsonSerializable(typeof(GetOrderQuery))]
[JsonSerializable(typeof(OrderDto))]
public partial class AppJsonSerializerContext : JsonSerializerContext { }
```

:::warning Wire-shape parity is your responsibility
The three options above (`PropertyNamingPolicy = CamelCase`, `UseStringEnumConverter = true`,
`DefaultIgnoreCondition = WhenWritingNull`) are **required** to match the reflection-based
`JsonEventSerializer`'s wire shape — camelCase property names, enums serialized as strings, and
nulls omitted. The `[JsonSourceGenerationOptions]` attribute is plain source-generated JSON config;
nothing in the framework validates or enforces your choices here. If your `JsonSerializerContext`
omits or diverges from these options (e.g. leaves the default PascalCase naming, or serializes
enums as numbers), events written under one serialization path (source-generated, AOT) and read
under the other (reflection-based) will silently mis-read — property names won't match and enum
values may not round-trip. Always copy the options block above verbatim unless you have
deliberately standardized on a different wire shape across *all* readers and writers.
:::

Register it:

```csharp
services.AddSingleton(AppJsonSerializerContext.Default.Options);
```

### Direct JSON serializers

`AotJsonSerializer` and `CompositeAotJsonSerializer` expose byte-array and span
APIs backed by explicit source-generated metadata. Both byte-array APIs can be
called from a NativeAOT application without dynamic-code or trimming-warning
suppression. Register every payload type in the supplied context; missing metadata
produces `NotSupportedException` rather than reflection fallback.

```csharp
using var serializer = new AotJsonSerializer(AppJsonSerializerContext.Default);
byte[] json = serializer.Serialize(order);
OrderDto restored = serializer.Deserialize<OrderDto>(json);
```

Both deserialization overloads consume one complete JSON document. Trailing
whitespace is accepted; a second value or malformed suffix is rejected. Parsing
uses the selected context's options, including `AllowTrailingCommas`,
`ReadCommentHandling` and `MaxDepth`. Composite serialization selects the first
supporting context and keeps its options after cache warmup. A parsing or converter
failure does not retry a later, more permissive context.

This corrects earlier behavior that ignored context reader options and could
accept the first value while ignoring a suffix. Custom converters now follow the
standard System.Text.Json span-reader position and validation order. Conversion
can invoke a converter, constructor or setter before later malformed input fails;
deserialization does not roll those side effects back. JSON `null` that produces
a null result still raises `InvalidOperationException`.

The runnable `tests/harnesses/AotJsonContractProbe` sample publishes and executes
both serializers with reflection-based JSON disabled under NativeAOT. It covers
byte-array/span round trips, writer parity, complete-input checks, context
selection, configured parsing, selected depth cases and a custom converter. These
are functional checks, separate from managed or native performance measurements.

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
- **Annotation propagation**: the core dispatch *implementations* (`Dispatcher`, including its local fast path, and `FinalDispatchHandler`) carry `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` attributes so the trimmer accurately reports any reflection-dependent call sites. The `IDispatcher` abstraction itself carries no such attributes. Source-generated alternatives eliminate these warnings when the generators package is referenced.

All of this is transparent to consumers. The same `AddDispatch()`, `AddSaga()`, and `AddProjection()` DI entry points work identically under both JIT and AOT.

### How this is kept true

Every change to the framework is published ahead-of-time with `PublishAot=true` and rejected if it
introduces a new IL2XXX or IL3XXX trim or AOT warning. The only warnings tolerated are a fixed,
recorded set originating in third-party dependencies we cannot change; nothing new joins that set
silently. **So the AOT compatibility described on this page is enforced on every change, not asserted
once.**

---

## Trimmer Configuration

`TrimmerRoots.xml` preserves critical types during IL trimming:

```xml
<linker>
  <assembly fullname="Excalibur.Dispatch.Abstractions">
    <type fullname="Excalibur.Dispatch.IDispatchMessage" preserve="all" />
    <type fullname="Excalibur.Dispatch.Delivery.IActionHandler`1" preserve="all" />
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

The `samples/10-aot/Excalibur.Dispatch.Aot.Sample` project demonstrates full AOT compilation:

```bash
cd samples/10-aot/Excalibur.Dispatch.Aot.Sample
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

1. Ensure `Excalibur.Dispatch` is referenced
2. Check that handlers have `[AutoRegister]` attribute
3. Add `[DynamicallyAccessedMembers]` to custom reflection-heavy code
4. Verify `TrimmerRoots.xml` is included in your project

### Handlers Not Discovered

1. Handlers must be `public`, non-abstract classes
2. Handlers must implement `IActionHandler<T>`, `IEventHandler<T>`, or `IDocumentHandler<T>`
3. Handler interfaces must be from `Excalibur.Dispatch.Delivery` namespace
4. Clean and rebuild after adding new handlers

### "HandlerActivator cannot activate handler ... does not support runtime code generation"

A message reached the default expression-compiling activator in a build where dynamic code is
unavailable. Register `AotHandlerActivator` before `AddDispatch`, as shown in
[Handler Activation and Context Binding](#4-handler-activation-and-context-binding). If the handler should
have been covered by generated dispatch instead, check that it is discovered — see
[Handlers Not Discovered](#handlers-not-discovered).

### Result Factory Not Working Under AOT

The `ResultFactoryRegistry` is generated from `IDispatchAction<T>` and `IActionHandler<TAction, TResult>` discovery. If your result types aren't discovered:

1. Ensure your action/query types implement `IDispatchAction<TResult>` with concrete result types
2. Verify the generator is active: check for `ResultFactoryRegistry.g.cs` in generated output
3. The `RuntimeFeature.IsDynamicCodeSupported` branch in `FinalDispatchHandler` automatically selects the AOT path when publishing with NativeAOT

---

## Related Documentation

- [AOT Compatibility Matrix](./aot-compatibility.md) - Per-package AOT status for all 194 packages
- [Source Generators](./source-generators.md) - Full generator reference
- [Viewing Generated Code](./viewing-generated-code.md) - Inspect generated output
- [Deployment](./deployment.md) - Deployment patterns

## See Also

- [Source Generators Getting Started](../source-generators/getting-started.md) — Step-by-step guide to enabling AOT-compatible source generators
- [Auto-Freeze](../performance/auto-freeze.md) — Automatic FrozenDictionary optimization for AOT-generated registrations
- [ASP.NET Core Deployment](../deployment/aspnet-core.md) — Deploying AOT-compiled applications in ASP.NET Core
- [Viewing Generated Code](./viewing-generated-code.md) — Inspecting source generator output in your IDE
