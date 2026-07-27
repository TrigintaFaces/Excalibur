# Dispatch ↔ Excalibur Boundary Rules

**Status**: Authoritative
**Last Updated**: 2025-11-10
**Related**: [Boundary Audit Results](../../management/reports/2025-11-10_boundary-audit-results_v1.0.0.md)

## Table of Contents

1. [Overview](#overview)
2. [The Boundary Definition](#the-boundary-definition)
3. [What's Allowed](#whats-allowed)
4. [What's Not Allowed](#whats-not-allowed)
5. [Examples](#examples)
6. [Decision Tree](#decision-tree)
7. [Validation](#validation)

## Overview

The Excalibur.Dispatch framework maintains a strict architectural boundary between:

- **Dispatch**: The core messaging framework (abstractions + infrastructure)
- **Excalibur**: CQRS/Event Sourcing helpers built on Dispatch

This document defines the boundary rules that **ALL code must follow**.

## The Boundary Definition

### Correct Understanding ✅

**The boundary violation is NOT about dependencies** - it's about **public API exposure**.

```
Excalibur projects CAN:
✅ Reference Excalibur.Dispatch internally
✅ Use Core implementations in private fields
✅ Use Core types in DI registration
✅ Use Core services for infrastructure

Excalibur projects CANNOT:
❌ Expose Core types in public APIs
❌ Return Core types from public methods
❌ Accept Core types in public method parameters
❌ Expose Core types in public properties
```

### Dependency Graph

```
┌─────────────────────────────────────┐
│         Excalibur.Dispatch.Abstractions       │  ← Pure interfaces
│  (IEventStore, IOutboxStore, etc)  │
└─────────────────────────────────────┘
           ▲           ▲
           │           │
           │           │
   ┌───────┴───────┐   │
   │ Excalibur.Dispatch │   │
   │ (Impl + Infra)│   │
   └───────────────┘   │
           ▲           │
           │           │
           │           │
   ┌───────┴───────────┴───────┐
   │  Excalibur Projects       │
   │  (CQRS/ES Helpers)        │
   └───────────────────────────┘
```

**Rules**:

1. Excalibur can reference Core (internal usage)
2. Excalibur can reference Abstractions (public API)
3. Excalibur **MUST NOT** expose Core in public APIs

### Why This Matters

**Loose Coupling**: Consumers of Excalibur depend on abstractions, not implementations
**Testability**: Public APIs can be mocked via interfaces
**Flexibility**: Implementations can change without breaking consumers
**Clean Architecture**: Domain layers can avoid infrastructure dependencies entirely

## What's Allowed

### ✅ Internal Core Usage

Excalibur projects can use Core types **internally** for:

1. **Private Fields**

   ```csharp
   public class OrderService
   {
       private readonly GlobalStreamProjectionHost _dispatcher; // ✅ Core type - private

       public OrderService(GlobalStreamProjectionHost dispatcher)
       {
           _dispatcher = dispatcher; // ✅ Internal DI
       }
   }
   ```

2. **DI Registration**

   ```csharp
   public static class ServiceCollectionExtensions
   {
       public static IServiceCollection AddOrderProcessing(this IServiceCollection services)
       {
           // ✅ Register Core implementations internally
           services.AddSingleton<GlobalStreamProjectionHost>();
           services.AddSingleton<OutboxProcessor>();
           return services;
       }
   }
   ```

3. **Internal Method Implementation**

   ```csharp
   public class MyEventStore : IEventStore
   {
       // ✅ Public surface uses only Abstractions types (IDomainEvent, StoredEvent, AppendResult)
       public async ValueTask<AppendResult> AppendAsync(
           string aggregateId,
           string aggregateType,
           IEnumerable<IDomainEvent> events,
           long expectedVersion,
           CancellationToken cancellationToken)
       {
           // ✅ Concrete infrastructure (Dapper commands, connections) stays internal
           await _internalWriter.WriteAsync(aggregateId, aggregateType, events, cancellationToken);
           return AppendResult.Success(...);
       }
   }
   ```

4. **Configuration and Setup**

   ```csharp
   public class Startup
   {
       public void ConfigureServices(IServiceCollection services)
       {
           // ✅ Use Core for infrastructure setup
           services.AddDispatch();
           services.AddExcalibur(x => x.AddEventSourcing(es => es.UseInMemory()));
       }
   }
   ```

## What's Not Allowed

### ❌ Public Core Exposure

Excalibur projects **MUST NOT** expose Core types in:

1. **Public Properties**

   ```csharp
   public class OrderService
   {
       // ❌ VIOLATION: Core type in public property
       public GlobalStreamProjectionHost Dispatcher { get; }

       // ✅ CORRECT: Abstraction instead
       public IEventStore EventStore { get; }
   }
   ```

2. **Public Method Return Types**

   ```csharp
   public class OrderService
   {
       // ❌ VIOLATION: concrete infrastructure type returned
       public SqlServerEventStore GetStore() { }

       // ✅ CORRECT: abstraction returned
       public IEventStore GetStore() { }
   }
   ```

3. **Public Method Parameters**

   ```csharp
   public class OrderService
   {
       // ❌ VIOLATION: concrete infrastructure type parameter
       public Task ProcessEvents(SqlServerEventStore store) { }

       // ✅ CORRECT: abstraction parameter
       public Task ProcessEvents(IEventStore store) { }
   }
   ```

4. **Public Base Class Members**

   ```csharp
   public abstract class ServiceBase
   {
       // ❌ VIOLATION: Protected Core member visible to consumers
       protected GlobalStreamProjectionHost Dispatcher { get; }

       // ✅ CORRECT: Protected abstraction
       protected IEventStore EventStore { get; }
   }
   ```

5. **Public Extension Method Signatures**

   ```csharp
   public static class Extensions
   {
       // ❌ VIOLATION: Core type in public extension
       public static IServiceCollection AddOrders(
           this IServiceCollection services,
           EventStoreOptions options) // Core type
       { }

       // ✅ CORRECT: Abstraction in extension
       public static IServiceCollection AddOrders(
           this IServiceCollection services,
           IEventStoreOptions options) // Abstraction
       { }
   }
   ```

## Examples

### Example 1: Event Store Implementation

**❌ INCORRECT - Exposes Core**:

```csharp
public class SqlServerEventStore
{
    // VIOLATION: public property exposes an internal infrastructure type
    public OutboxProcessor Processor { get; }

    // VIOLATION: public method returns a concrete implementation type
    public SqlServerEventStore CreateChild() { ... }
}
```

**✅ CORRECT - Uses Abstractions Publicly**:

```csharp
using Excalibur.Dispatch;        // IDomainEvent
using Excalibur.EventSourcing;   // IEventStore, StoredEvent, AppendResult

public class SqlServerEventStore : IEventStore
{
    private readonly SqlConnectionFactory _connections; // Private infrastructure type - OK

    public SqlServerEventStore(SqlConnectionFactory connections)
    {
        _connections = connections; // Internal DI - OK
    }

    // Public surface uses only Abstractions types (StoredEvent, AppendResult, IDomainEvent)
    public async ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
        string aggregateId,
        string aggregateType,
        CancellationToken cancellationToken)
    {
        // Internal infrastructure usage (Dapper, connections) is fine
        return await _internalReader.ReadAsync(aggregateId, aggregateType, cancellationToken);
    }

    public async ValueTask<AppendResult> AppendAsync(
        string aggregateId,
        string aggregateType,
        IEnumerable<IDomainEvent> events,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        // Concrete write path (Dapper commands) stays internal
        return await _internalWriter.WriteAsync(
            aggregateId, aggregateType, events, expectedVersion, cancellationToken);
    }
}
```

### Example 2: Service Registration

**❌ INCORRECT - Exposes Core in Parameters**:

```csharp
public static class ServiceCollectionExtensions
{
    // VIOLATION: Core type in public parameter
    public static IServiceCollection AddEventStore(
        this IServiceCollection services,
        EventStoreOptions options) // Core type exposed
    {
        services.AddSingleton(options);
        return services;
    }
}
```

**✅ CORRECT - Uses Abstractions in Signature**:

```csharp
using Excalibur.EventSourcing;   // IEventStore

public static class ServiceCollectionExtensions
{
    // Public method uses abstraction or framework types only
    public static IServiceCollection AddEventStore(
        this IServiceCollection services,
        Action<EventStoreOptions>? configure = null)
    {
        // Internal infrastructure registration is fine
        services.AddSingleton<GlobalStreamProjectionHost>();
        services.AddSingleton<IEventStore, SqlServerEventStore>();

        if (configure != null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
```

### Example 3: Domain Service

**✅ BEST PRACTICE - Zero Core Dependency**:

```csharp
// Excalibur.Domain project
using Excalibur.Dispatch;        // IDomainEvent
using Excalibur.EventSourcing;   // IEventStore

public class OrderService
{
    private readonly IEventStore _eventStore; // Abstraction only

    public OrderService(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task PlaceOrder(string orderId, ...)
    {
        // Domain events implement the IDomainEvent abstraction — no Core/infrastructure type
        var events = new List<IDomainEvent> { new OrderPlaced(orderId, ...) };

        await _eventStore.AppendAsync(
            orderId,
            nameof(OrderAggregate),
            events,
            expectedVersion: -1,
            cancellationToken);
    }
}
```

## Decision Tree

### Should I Use Core or Abstractions?

```
Is this code...
├─ Part of a PUBLIC API (public class, method, property)?
│  ├─ YES → Use Excalibur.Dispatch.Abstractions ✅
│  └─ NO → Continue...
│
├─ A private/internal implementation detail?
│  ├─ YES → Can use Excalibur.Dispatch ✅
│  └─ NO → Continue...
│
├─ In a pure domain/application layer?
│  ├─ YES → Prefer Excalibur.Dispatch.Abstractions (or remove Dispatch entirely) ✅
│  └─ NO → Continue...
│
├─ Infrastructure implementation (stores, dispatchers)?
│  ├─ YES → Use Excalibur.Dispatch internally ✅
│  └─ NO → Continue...
│
└─ Not sure?
   └─ Default to Excalibur.Dispatch.Abstractions ✅
```

### Should I Remove Core Dependency Entirely?

```
Is my project...
├─ Pure domain logic (no infrastructure)?
│  └─ YES → Remove Core dependency (use only Abstractions) ✅
│      Example: Excalibur.Domain
│
├─ Pure abstractions (interfaces only)?
│  └─ YES → Remove Core dependency ✅
│      Example: Excalibur.*.Abstractions projects
│
├─ Infrastructure implementation (stores, dispatchers)?
│  └─ YES → Keep Core dependency (needed for implementations) ✅
│      Example: Excalibur.Data.SqlServer
│
├─ Application/Hosting layer (DI setup, configuration)?
│  └─ YES → Keep Core dependency (needed for bootstrapping) ✅
│      Example: Excalibur.Hosting
│
└─ Provider implementation (database, cache, etc)?
   └─ YES → Keep Core dependency (needed for concrete types) ✅
      Example: Excalibur.Data.Providers.Redis
```

## Validation

### Manual Verification

**Check your code**:

1. Find all `public class`, `public interface`, `public method`
2. Check return types, parameters, properties
3. Ensure all public types use `Excalibur.Dispatch.Abstractions` (or framework types)
4. Core types should ONLY appear in:
   - Private fields
   - Internal methods
   - Constructor parameters (for DI)

### Automated Validation Plan

**NetArchTest Rules** (TASK-0006):

```csharp
[Fact]
public void Excalibur_PublicApis_ShouldNotExposeCoreTypes()
{
    var result = Types.InAssembly(typeof(OrderService).Assembly)
        .That().ResideInNamespace("Excalibur")
        .And().ArePublic()
        .ShouldNot().HaveDependencyOn("Excalibur.Dispatch")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

### CI/CD Gates

**Build will fail if**:

- Public API exposes `Excalibur.Dispatch` types
- Violations detected by NetArchTest
- Architecture boundaries broken

## Common Questions

### Q: Can Excalibur reference Excalibur.Dispatch at all?

**A**: YES! Excalibur can reference Core and use it **internally**. The rule is about PUBLIC API exposure, not internal usage.

### Q: Why is internal Core usage OK?

**A**: Internal Core usage is implementation detail. Consumers don't see it. The boundary violation is when Core types leak into public contracts that consumers depend on.

### Q: What about transitive dependencies?

**A**: If your project references Core, consumers get it transitively. This is OK as long as they don't need to USE Core types directly (because your public API uses abstractions).

### Q: Should I always use Abstractions?

**A**: For **public APIs**, YES. For **internal implementation**, you can use Core when needed (DI registration, concrete implementations, infrastructure).

### Q: Can I expose Core types via `internal` members?

**A**: YES. `internal` members are not part of the public API. Core types in `internal` methods/properties are fine.

### Q: What about `protected` members in public classes?

**A**: **CAUTION**. Protected members are visible to consumers who inherit your class. Prefer abstractions unless you're certain the class won't be inherited outside your assembly.

## Enforcement

**Current Status** (2025-11-10):

- ✅ Manual audit completed - ZERO violations found
- ⏳ Automated enforcement (NetArchTest) - PENDING (TASK-0006)
- ⏳ CI/CD gates - PENDING (TASK-0006)

**Audit Results**: See [Boundary Audit Results](../../management/reports/2025-11-10_boundary-audit-results_v1.0.0.md)

## Related Documents

- [Boundary Audit Results](../../management/reports/2025-11-10_boundary-audit-results_v1.0.0.md)
- [Abstraction Migration Report](../../management/reports/2025-11-10_abstraction-migration_v1.0.0.md)
- [Architecture Decision Record: Boundary Rules](./adr/0001-boundary-rules.md) *(to be created)*

---

**Remember**: The boundary is about **public API exposure**, not internal usage. Use Abstractions publicly, Core internally!

