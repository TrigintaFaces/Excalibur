---
sidebar_position: 7
title: AOT Migration Guide
description: Migrate from reflection-based to AOT-safe Dispatch APIs
---

# AOT Migration Guide

This guide walks through migrating an existing Excalibur.Dispatch application from reflection-based APIs to Native AOT-safe alternatives. If you are starting a new project, see the [Native AOT Guide](native-aot.md) instead.

## Prerequisites

- .NET 10.0 and the native build prerequisites for your target platform
- `Excalibur.Dispatch` referenced in your entry-point project (it carries the generators)

```bash
dotnet add package Excalibur.Dispatch
```

## Migration Checklist

| Step | What Changes | Effort |
|------|-------------|--------|
| 1. Enable AOT | Add `<PublishAot>true</PublishAot>` to project | Trivial |
| 2. Mark handlers | Add `[AutoRegister]` to handler classes | Low |
| 3. Register generated services | Call `services.AddGeneratedServices()` | Trivial |
| 4. JSON serialization | Create `JsonSerializerContext` for your DTOs | Medium |
| 5. FluentValidation (if used) | Call `services.AddGeneratedFluentValidationDispatcher()` | Low |
| 6. Check package compatibility | Verify the selected dependencies and execution paths | Varies |
| 7. Publish and verify | Review diagnostics and execute the published native application | Varies |

---

## Step 1: Enable AOT Publishing

Add to your `.csproj`:

```xml
<PropertyGroup>
    <PublishAot>true</PublishAot>
    <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

This enables AOT analysis during build. Review trim and dynamic-code diagnostics for the paths your application uses; reflection does not universally produce a warning, and suppressions are not compatibility evidence. See [Microsoft's Native AOT requirements and limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).

## Step 2: Mark Handlers with `[AutoRegister]`

The source generator discovers handlers at compile time. Mark each handler:

```csharp
using Excalibur.Dispatch;

// Before: handler discovered via assembly scanning (reflection)
public class CreateOrderHandler : IActionHandler<CreateOrderCommand>
{
    public Task HandleAsync(CreateOrderCommand message, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

// After: handler discovered at compile time (AOT-safe)
[AutoRegister]
public class CreateOrderHandler : IActionHandler<CreateOrderCommand>
{
    public Task HandleAsync(CreateOrderCommand message, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

The source generator produces `PrecompiledHandlerRegistry` with switch-based resolution -- no `Type.GetType()`, no assembly scanning.

## Step 3: Register Generated Services

```csharp
var services = new ServiceCollection();

// AOT-safe: use AddDiscoveredHandlers() instead of AddHandlersFromAssembly()
services.AddDispatch(dispatch => dispatch.AddDiscoveredHandlers());

// Add this line -- registers source-generated DI services
services.AddGeneratedServices();
```

`AddDiscoveredHandlers()` emits explicit registrations for discovered supported handlers, avoiding assembly scanning in that registration method. `AddGeneratedServices()` emits explicit registrations using each attribute's configured lifetime. Inspect generated output and verify the application's complete native execution path.

## Step 4: AOT-Safe JSON Serialization

Reflection-based `JsonSerializer.Serialize(obj)` is not AOT-compatible. Create a `JsonSerializerContext` for your message types:

```csharp
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CreateOrderCommand))]
[JsonSerializable(typeof(GetOrderQuery))]
[JsonSerializable(typeof(OrderDto))]
[JsonSerializable(typeof(OrderCreatedEvent))]
public partial class AppJsonSerializerContext : JsonSerializerContext { }
```

Register it, and select the reflection-free event serializer that reads and writes through it:

```csharp
services.AddSingleton(AppJsonSerializerContext.Default.Options);

services.AddEventTypes<OrderCreatedEvent>();          // the names events resolve by
services.AddAotEventSerializer(AppJsonSerializerContext.Default);
```

`AddAotEventSerializer` replaces the default reflection-based event serializer, so call order relative to `AddDispatch()` does not matter. Event types resolve through the allow-list `AddEventTypes` populates — there is no assembly-scan fallback — and payloads are read and written entirely through your context.

Each registered event type must declare its stable name with `[MessageName]`; registering one that does not throws `InvalidOperationException` naming the type. Nothing is derived from the CLR type, so trimming and single-file publishing cannot change the name an event is stored under:

```csharp
[MessageName("Contoso.Orders.OrderCreated")]
public sealed record OrderCreatedEvent(Guid OrderId, string CustomerId) : DomainEvent;
```

See [Stable Message Names](../event-sourcing/domain-events.md#stable-message-names).

Pass the generated `Default` instance, not `new AppJsonSerializerContext()`: the `[JsonSourceGenerationOptions]` settings are applied to `Default` only, and a freshly constructed context carries default PascalCase, null-writing options.

:::warning `OrderCreatedEvent` crosses the wire — options must match the reflection path
`OrderCreatedEvent` is an event type, so it round-trips through your event store. The
`PropertyNamingPolicy`, `UseStringEnumConverter`, and `DefaultIgnoreCondition` settings above must
match the reflection-based `JsonEventSerializer`'s defaults (camelCase, string enums, omitted
nulls) or events written via the AOT path will silently mis-read when loaded via the reflection
path (or vice versa). The naming-policy and null-handling settings are checked when the serializer is
constructed and a divergent context is rejected with an error naming the missing attribute; the
string-enum setting is not visible at run time, so copy the options block verbatim.
:::

Event metadata is an `IDictionary<string, object>`, so each value is written as its runtime type.
Declare the closed value types your application actually stores — `[JsonSerializable(typeof(string))]`,
`[JsonSerializable(typeof(int))]`, and so on. An undeclared value type throws `NotSupportedException`
on serialize. Do not declare `Dictionary<string, object>` itself as a shortcut: it compiles and then
throws at run time on the same values.

### Event Type Resolution

For event sourcing scenarios, the framework replaces `Type.GetType()` with AOT-safe `TypeResolver`:

```csharp
// Reflection-based (NOT AOT-safe):
var eventType = Type.GetType(eventTypeName);

// AOT-safe (framework handles this internally):
// TypeResolver uses compile-time type registries from source generators.
// No consumer code change needed -- the framework switches automatically
// when RuntimeFeature.IsDynamicCodeSupported is false.
```

## Step 5: FluentValidation (if used)

If you use `Excalibur.Dispatch.Validation.FluentValidation`, the source generator produces AOT-safe validator dispatch:

```csharp
// Register the source-generated FluentValidation dispatcher
services.AddGeneratedFluentValidationDispatcher();
```

The generator emits dispatch for supported validator types discovered at compile time. Each validation call resolves `IValidator<TMessage>` services from the supplied provider, materializes the collection and invokes the validators. It does not cache validator instances across calls. Generated dispatch alone does not establish native compatibility for every validator or its dependencies.

## Step 6: Check Package Compatibility

Not all packages are AOT-compatible. Check the [AOT Compatibility Matrix](aot-compatibility.md) for the full list.

### AOT-Safe Alternatives

| Reflection-Based | AOT-Safe Alternative |
|------------------|---------------------|
| `JsonEventSerializer` (default) | `AotJsonEventSerializer` (opt in with `AddAotEventSerializer(...)`) |
| Assembly-scanned handler discovery | `[AutoRegister]` + `PrecompiledHandlerRegistry` |
| `ResultFactoryCache` (reflection) | `ResultFactoryRegistry` (source-generated) |
| `Type.GetType()` for deserialization | `TypeResolver` + `EventStoreTypeMap` (source-generated) |
| Runtime `Expression.Lambda` invokers | Source-generated `HandlerInvoker` dispatch |

### Dependency Paths Requiring Verification

Verify selected package versions, configuration and execution paths against the [AOT Compatibility Matrix](aot-compatibility.md), then publish and run the application. Reflection usage does not by itself establish that every configuration is incompatible. Replacing a transport can change ordering, delivery and recovery contracts.

For example, `Excalibur.Dispatch.Serialization.Protobuf` uses `Google.Protobuf` generated messages and annotated reflection-based access to parser/descriptor properties. Verify that path and its required metadata; it is not a protobuf-net implementation. For FluentValidation, verify both the generated dispatcher and the validators it invokes. Transport verification must include the selected encoding, provider and recovery paths.

### Annotated Methods

Some methods in AOT-compatible packages carry `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]` attributes. These attributes describe trimming or dynamic-code requirements; they do not certify all JIT calls as safe. Do not suppress a diagnostic merely to obtain a clean build:

```csharp
// Only after verifying this exact path and recording the suppression justification:
#pragma warning disable IL2026, IL3050
services.AddKafkaTransport("kafka", builder => { ... });
#pragma warning restore IL2026, IL3050
```

Any suppression needs evidence that the selected path is supported, required metadata is preserved and the published native binary behaves correctly. A suppression cannot make runtime code generation available.

## Step 7: Publish and Verify

```bash
dotnet publish -c Release
```

Required verification:
- Review all trim/AOT diagnostics and any justified suppressions.
- Execute the published native binary through the application's required scenarios.

Run the published native executable and verify its behavior. Measure process startup separately in the intended deployment; successful publication, warning review and functional execution do not establish a startup-latency target.

If you see warnings, check:
1. `Excalibur.Dispatch` is referenced in the entry-point project
2. All handlers have `[AutoRegister]`
3. All custom DTOs are included in your `JsonSerializerContext`
4. You are not using incompatible packages (see Step 6)

---

## Runtime Behavior

Selected framework paths, including typed result construction, use `RuntimeFeature.IsDynamicCodeSupported` to choose an implementation:

- **Dynamic code supported:** the selected path can use its dynamic implementation. This does not establish zero overhead.
- **Dynamic code unsupported:** the selected path requires its generated or statically registered alternative. This does not establish that the entire application is reflection-free or Native AOT-compatible.

The shared `AddDispatch()` entry point does not prove equivalent behavior across deployment modes. Verify generated registrations, serialization contracts and dependency paths by publishing and executing the native application, including cancellation and error cases. Measure managed hot-path execution, native execution and process startup separately.

---

## See Also

- [Native AOT Guide](native-aot.md) -- Full AOT setup from scratch
- [AOT Compatibility Matrix](aot-compatibility.md) -- Per-package status
- [Source Generators](source-generators.md) -- What gets generated and how
- [AOT Samples](https://github.com/TrigintaFaces/Excalibur/tree/main/samples/10-aot) -- Consult each README for verification scope and limitations
