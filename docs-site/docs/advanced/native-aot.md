---
sidebar_position: 5
title: Native AOT
description: Native AOT compilation guide for Excalibur
---

# Native AOT Support

Dispatch provides Native AOT paths through generated handler registration and invocation, together with explicit serialization metadata. Compatibility depends on the handlers, middleware, serializers, and integrations your application selects. Publish and execute your application to verify those paths.

The compiler-checked examples on this page use the optional dependency/compiler profile of the existing `samples/10-aot/Excalibur.Dispatch.Aot.Sample/Excalibur.Dispatch.Aot.Sample.csproj` project, together with the normal Dispatch runtime references. They do not borrow its application types or demonstrate a deployed cloud host or successful NativeAOT publication. The real Dispatch analyzers and SDK JSON generator must run. Generated implementation classes are not application source to copy; application messages and service implementations remain your responsibility.

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
| **Binary size** | Trimming removes unused code; measure the published artifact |
| **Memory** | No runtime JIT compiler; measure total application memory |
| **Latency** | Removes JIT compilation, not other sources of latency variation |

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
using Excalibur.Dispatch.Delivery;

public sealed record CreateOrderCommand(Guid OrderId) : IDispatchAction;
public sealed record GetOrderQuery(Guid OrderId) : IDispatchAction<OrderDto>;
public sealed record OrderCreatedEvent(Guid OrderId) : IDispatchEvent;
public sealed record OrderDto(Guid OrderId);

// The application implements and registers this contract; this example does not simulate an order write.
public interface IOrderApplication
{
    Task CreateAsync(Guid orderId, CancellationToken cancellationToken);
}

[AutoRegister]
public sealed class CreateOrderHandler(IOrderApplication application) : IActionHandler<CreateOrderCommand>
{
    public Task HandleAsync(CreateOrderCommand message, CancellationToken cancellationToken) =>
        application.CreateAsync(message.OrderId, cancellationToken);
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

Review every trimming/AOT diagnostic and execute the published application. This command alone
does not establish compatibility; a clean publish is an acceptance objective, not an assured outcome.

---

## What Gets Generated

When you build with `Excalibur.Dispatch` referenced, its bundled generators produce the following compile-time code:

### Handler Factory (`PrecompiledHandlerRegistry.g.cs`)

The `HandlerRegistrySourceGenerator` discovers supported handler implementations and generates resolution code:

The generator emits its own `PrecompiledHandlerRegistry` implementation from application handlers. Do not paste a handwritten registry into the application: it would compete with generated declarations. See `src/Dispatch/Excalibur.Dispatch.SourceGenerators/` for the implementation; the application-facing registration calls above remain the consumer entry point.

`ResolveHandlerType` uses a compiler-generated switch expression — no `Type.GetType()`, no reflection, no runtime assembly scanning.

### Message Type Metadata (`DiscoveredMessageTypeMetadata.g.cs`)

The `JsonSerializationSourceGenerator` produces a compile-time registry of eligible discovered `IDispatchMessage` types:

The generator also discovers application message types at compilation. Its generated metadata is an implementation output, not an application class to copy; declare the real messages and handlers instead.

This registry does not supply JSON contracts for those types. Define an application
`JsonSerializerContext` with explicit `[JsonSerializable]` declarations and configure serialization
to use that context.

**Type filtering:** The generator automatically skips abstract types, open generic types, and non-public or nested types.

### Result Factory Registration

`MessageResultExtractorGenerator` emits an application module initializer that calls the framework's
public typed registration API. `ResultFactoryRegistry` itself is framework-owned, not a second
consumer-generated registry. The generated initializer body contains calls such as:

```csharp
using Excalibur.Dispatch.Delivery.Handlers;

ResultFactoryRegistry.RegisterFactory<Guid>();
ResultFactoryRegistry.RegisterFactory<OrderDto>();
```

`FinalDispatchHandler` consumes those registered full and lean factories; applications do not call
its internal `GetFactory` methods. Result extraction helpers generated for caching are separate.

### Service Registration (`GeneratedServiceCollectionExtensions.g.cs`)

The `ServiceRegistrationSourceGenerator` generates DI registrations for `[AutoRegister]` types:

`AddGeneratedServices()` is emitted by the service-registration generator from `[AutoRegister]` application types. Do not declare a competing `GeneratedServiceCollectionExtensions` implementation. The real analyzer must be referenced as an analyzer, as in the existing `samples/10-aot/Excalibur.Dispatch.Aot.Sample` project.

**Key enhancement:** Uses `AllInterfaces` to discover handler interfaces from base types, so a handler inheriting from `BaseHandler<T>` that implements `IActionHandler<T>` will have the interface registration generated automatically.

---

## Database request execution

For request-owned fixed SQL, [protected ADO execution](../data-access/data-request.md#explicit-ado-execution)
keeps the DataRequest contract while explicitly binding parameters and projecting row ordinals.
It avoids Dapper's dynamic execution at those migrated calls. This does not establish NativeAOT
support for the selected database driver or the complete SQL profile: publish and execute that exact
application path against real infrastructure. Existing Dapper request execution remains available.

## JSON Serialization

For generated JSON paths, supply explicit metadata instead of reflection fallback. Dispatch has two internal implementation contexts; the factory composes their declared metadata, and consumers do not resolve these types from DI:

| Context | Covers |
|---------|--------|
| `CoreMessageJsonContext` | Declared dictionaries and basic value types; inspect its explicit contracts |
| `CloudEventJsonContext` | Declared CLR metadata, including `Uri`; root metadata is not proof the SDK's whole object graph is generically serializable |

`DiscoveredMessageTypeMetadata` lists eligible message types; it does not generate their JSON
serialization contracts. Supply those contracts in an application `JsonSerializerContext`.

SQL Server outbox header reads use package-generated metadata for the persisted JSON dictionary.
Non-null header values reload as `JsonElement`; null values remain null, and header keys remain case-sensitive.
This reader has been verified with JSON reflection disabled in a native executable and through a fresh-store
round trip against SQL Server. That verification covers header decoding; publish and execute your application's
database and payload serialization paths separately. Generated dictionary metadata does not supply metadata
for arbitrary runtime objects written into headers or message payloads.

### Consumer JSON Context

For your own DTOs, create a `JsonSerializerContext`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CreateOrderCommand))]
[JsonSerializable(typeof(GetOrderQuery))]
[JsonSerializable(typeof(OrderDto))]
public partial class AppJsonSerializerContext : JsonSerializerContext { }
```

:::warning Persisted events have their own wire policy
The generated context above can supply application metadata. Persisted event serialization is a separate
contract: use `AddAotEventSerializer` with its canonical naming/null/enum requirements. That registration
validates its required policy; selecting application JSON does not change the persisted event codec.
Do not change persisted readers and writers by copying an unrelated application options object.
:::

### One explicit application JSON policy

The parallel `AotJsonSerializer`/`CompositeAotJsonSerializer` classes and their four registration helpers
have been removed before RC. Select the existing JSON codec with a complete owned policy, then use the
existing registry/current-serializer path:

```csharp
using System.Text.Json;
using Excalibur.Dispatch.Serialization;
using Microsoft.Extensions.DependencyInjection;

public static class AppSerializationRegistration
{
    public static void Configure(IServiceCollection services)
    {
        var json = SystemTextJsonSerializer.CreateWithMetadata(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = AppJsonSerializerContext.Default,
        });
        services.AddPluggableSerializer(SerializerIds.SystemTextJson, json, setAsCurrent: true);
    }
}
```

The factory snapshots options, converter-list membership and resolver-chain membership, adds generated
Core/CLR CloudEvents metadata fallbacks after your resolvers, and freezes its own snapshot. One supplied
policy applies across the graph: a generated context's defaults are not an additional per-type options
policy. Declare every runtime payload type; a resolver for a nested runtime child can participate in the
same chain. Missing metadata fails rather than adding reflection. Custom resolvers/converters remain
your responsibility for thread safety and AOT safety; their arbitrary mutable internals are not cloned.

This registers the selected codec for `ISerializerRegistry`/`IPayloadSerializer`; it does not implicitly
register that object as a direct `ISerializer` service. Resolve the current codec from the registry, or
explicitly register your codec when direct injection is needed. Duplicate serializer IDs remain an
actionable refusal; repeated registrations are not a last-wins way to change one format's wire policy.

For a deliberately context-owned policy, retain the existing advanced route:

```csharp
using Excalibur.Dispatch.Serialization;

public static class OrderSerialization
{
    public static OrderDto RoundTrip(OrderDto order)
    {
        var json = SystemTextJsonSerializer.Create(AppJsonSerializerContext.Default);
        byte[] bytes = json.SerializeToBytes(order);
        return json.Deserialize<OrderDto>(bytes);
    }
}
```

The advanced route borrows returned metadata and its actual effective options, without modifying or
disposing the context. It requires that context to cover the complete runtime graph. Both routes use
the same existing codec and consume one complete JSON document; a second value or malformed suffix
is rejected. Reader/converter options and LF/CRLF/tab formatting apply through the selected policy.
The codec reports `SerializationException`, retaining underlying errors where present; a null
result is a serialization failure. Conversion may run before later invalid input is discovered, and
those application-side converter/constructor effects are not rolled back.

Generic CLR `CloudEvent` JSON is not the standard CloudEvents wire representation. The SDK object graph
contains metadata that a generic serializer can refuse (including `System.Type`); a nonnull root
`JsonTypeInfo` does not certify that graph. Use the existing `AddCloudEvents(resolver)` and its formatter
for standard envelope/payload encoding. Neither the new factory nor its finite known-root checks are
full native transport or CloudEvents interoperability certification.

The existing `tests/harnesses/AotJsonContractProbe` exercises the selected owned-policy registry/framing
and advanced context routes, full-input/reader/converter behavior, and M07 formatting. Publish and run
that current source; earlier native receipts do not verify a changed codec. These are functional checks,
not performance measurements or full-framework certification.

---

## Type Resolution

Framework internals can use the internal `TypeResolver` with registered metadata. This is an implementation excerpt, not an application-callable API:

`TypeResolver` is an internal framework helper (`src/Dispatch/Excalibur.Dispatch/TypeResolution/TypeResolver.cs`), not a public application API. Application code should use typed Dispatch registration and generated serialization metadata; this helper is not a consumer recipe for resolving arbitrary configured types.

Internal serialization and poison-message paths use this helper. `EnvelopeCloudEventBridge` instead uses preclosed transport adapters.

---

## How AOT Works Under the Hood

Supported generated paths require the appropriate registrations, payload metadata,
and compatible dependencies. Referencing the generators package alone does not
establish application compatibility: publish with `PublishAot=true` and execute
the application's relevant scenarios under NativeAOT.

Key behaviors:

- **Enum serialization** uses typed `JsonStringEnumConverter<TEnum>` and source-generated JSON contexts -- zero reflection.
- **Event serialization** uses `JsonSerializerContext.GetTypeInfo()` for type-safe serialization without `Type.GetType()`.
- Selected generated paths use precompiled registrations. Compatibility also depends on serializers and integration dependencies; verify each configured path. See the [scoped caching NativeAOT limitations](../performance/caching.md).
- **Runtime branching** via `RuntimeFeature.IsDynamicCodeSupported` selects supported implementations according to dynamic-code availability. Its cost depends on the executed path and configuration.
- **Annotation propagation**: the core dispatch *implementations* (`Dispatcher`, including its local fast path, and `FinalDispatchHandler`) carry `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` attributes so the trimmer reports reflection-dependent call sites. The `IDispatcher` abstraction itself carries no such attributes. Supported generated alternatives can avoid reflection-dependent paths; inspect the application's publish diagnostics.

The `AddDispatch()`, `AddSaga()`, and `AddProjection()` DI entry points support
generated paths where available. Their use does not certify every registered
handler, provider, serializer, or dependency for NativeAOT.

### Verification scope

NativeAOT evidence applies to the source revision, toolchain, publish configuration,
and scenarios actually exercised by a probe. Review IL2XXX and IL3XXX diagnostics
and execute the published native application. A managed test or successful
compilation alone does not verify native behavior. Check the applicable CI run
and its artifacts rather than assuming every change or package has been verified.

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
    <type fullname="Excalibur.Dispatch.Delivery.Handlers.ResultFactoryRegistry" preserve="all" />
    <!-- ... -->
  </assembly>
  <assembly fullname="YourApplication">
    <type fullname="Excalibur.Dispatch.Generated.PrecompiledHandlerRegistry" preserve="all" />
  </assembly>
</linker>
```

The generated registry is in the application assembly; replace `YourApplication` with its actual assembly name. You typically do not need to modify this file. If you have custom types that must survive trimming, add them to your own `TrimmerRoots.xml` or use `[DynamicDependency]` attributes.

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
- Configured dispatch scenarios through `IDispatcher.DispatchAsync()`; publish and run the current sample and inspect its diagnostics

---

## Transport AOT Compatibility

The classifications below are historical investigation leads. Builder-shaped registration does
not establish native transport compatibility, and a reflection-dependent integration does not
establish that every configuration of its transport is unsupported. Verify the selected application
configuration through native publish and execution.

### Transport Status

| Transport | AOT Status | Notes |
|-----------|-----------|-------|
| RabbitMQ | Current execution unverified | Builder registration alone is not proof |
| AWS SQS | Current execution unverified | Verify selected SDK and serialization paths |
| Azure Service Bus | Current execution unverified | Revalidate historical SDK reflection concerns |
| Google Pub/Sub | Current execution unverified | Revalidate historical SDK reflection concerns |
| Kafka | Configuration-dependent; execution unverified | Inspect the selected SchemaRegistry path |
| gRPC | Current execution unverified | Verify generated code and dependencies |

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

| Package | Compatibility declaration or known constraint (not execution proof) |
|---------|-----------|
| `Excalibur.Dispatch` | Annotated (`[RequiresUnreferencedCode]` on reflection paths, source-gen alternative) |
| `Excalibur.Dispatch.Abstractions` | Included in the targeted native scenarios below; not every consumer path is verified |
| `Excalibur.Dispatch.Transport.RabbitMQ` | Native transport execution not verified by this campaign |
| `Excalibur.Dispatch.Transport.AwsSqs` | Native cloud transport execution not verified by this campaign |
| `Excalibur.Dispatch.Transport.Kafka` | Selected SchemaRegistry path requires verification; no whole-transport verdict |
| `Excalibur.Dispatch.Transport.AzureServiceBus` | Native SDK/transport execution not verified by this campaign |
| `Excalibur.Dispatch.Transport.GooglePubSub` | Native SDK/transport execution not verified by this campaign |
| `Excalibur.Dispatch.Transport.Grpc` | Native generated-code/transport execution not verified by this campaign |

On November 1, 2026, the local win-x64 campaign published and executed a focused
upcasting/snapshot probe on .NET 10.0.14 with SDK 10.0.403. All 18 checks passed
with dynamic code disabled, including typed multihop serialization order and bytes.
Evidence: `artifacts/benchmarks/current-refresh-20261101/upcasting-native-publish-r4`
and `upcasting-native-execution-r2`. This is scenario-specific compatibility evidence,
not a package-wide guarantee, cloud verification or NativeAOT benchmark result.
Consumer serializers and transport dependencies require their own native verification.

### Known Constraints

| Constraint | Detail |
|------------|--------|
| Source generators target `netstandard2.0` | Limited API surface (Roslyn requirement) |
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

- [AOT Compatibility Matrix](./aot-compatibility.md) - Per-package declarations and verification scope
- [Source Generators](./source-generators.md) - Full generator reference
- [Viewing Generated Code](./viewing-generated-code.md) - Inspect generated output
- [Deployment](./deployment.md) - Deployment patterns

## See Also

- [Source Generators Getting Started](../source-generators/getting-started.md) — Step-by-step guide to enabling AOT-compatible source generators
- [Auto-Freeze](../performance/auto-freeze.md) — Automatic FrozenDictionary optimization for AOT-generated registrations
- [ASP.NET Core Deployment](../deployment/aspnet-core.md) — Deploying AOT-compiled applications in ASP.NET Core
- [Viewing Generated Code](./viewing-generated-code.md) — Inspecting source generator output in your IDE
