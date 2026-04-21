# AddMessageUpcasting Extension Method

**Namespace:** `Dispatch.DependencyInjection`
**Assembly:** `Dispatch`

Extension methods for configuring message upcasting services in dependency injection.

## Definition

```csharp
public static class UpcastingServiceCollectionExtensions
{
    public static IServiceCollection AddMessageUpcasting(this IServiceCollection services);
    public static IServiceCollection AddMessageUpcasting(this IServiceCollection services, Action<UpcastingBuilder> configure);
    public static bool HasMessageUpcasting(this IServiceCollection services);
}
```

## Methods

### AddMessageUpcasting (Basic)

Adds message upcasting services to the service collection with no configuration.

```csharp
public static IServiceCollection AddMessageUpcasting(this IServiceCollection services);
```

**Parameters:**
- `services`: The service collection

**Returns:**
The service collection for method chaining.

**Registered Services:**
- `IUpcastingPipeline` - Singleton pipeline for message upcasting

**Example:**
```csharp
// Basic registration (no upcasters configured)
services.AddMessageUpcasting();
```

### AddMessageUpcasting (With Configuration)

Adds message upcasting services with configuration.

```csharp
public static IServiceCollection AddMessageUpcasting(
    this IServiceCollection services,
    Action<UpcastingBuilder> configure);
```

**Parameters:**
- `services`: The service collection
- `configure`: Configuration action for the upcasting builder

**Returns:**
The service collection for method chaining.

**Example:**
```csharp
services.AddMessageUpcasting(builder =>
{
    // Register individual upcasters
    builder.RegisterUpcaster(new UserEventV1ToV2());

    // Or scan assemblies for auto-discovery
    builder.ScanAssembly(typeof(Program).Assembly);

    // Enable auto-upcasting during event store replay
    builder.EnableAutoUpcastOnReplay();
});
```

### HasMessageUpcasting

Checks if message upcasting services have been registered.

```csharp
public static bool HasMessageUpcasting(this IServiceCollection services);
```

**Parameters:**
- `services`: The service collection

**Returns:**
`true` if upcasting services are registered; otherwise `false`.

**Example:**
```csharp
if (!services.HasMessageUpcasting())
{
    services.AddMessageUpcasting();
}
```

## Usage Examples

### Minimal Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMessageUpcasting();

var app = builder.Build();
```

### With Manual Registration

```csharp
builder.Services.AddMessageUpcasting(upcasting =>
{
    // Register upcasters explicitly
    upcasting.RegisterUpcaster(new UserCreatedEventV1ToV2());
    upcasting.RegisterUpcaster(new UserCreatedEventV2ToV3());
    upcasting.RegisterUpcaster(new OrderPlacedEventV1ToV2());
});
```

### With Assembly Scanning

```csharp
builder.Services.AddMessageUpcasting(upcasting =>
{
    // Auto-discover all upcasters in the application
    upcasting.ScanAssembly(typeof(Program).Assembly);
});
```

### With Event Sourcing

```csharp
builder.Services.AddMessageUpcasting(upcasting =>
{
    upcasting.ScanAssembly(typeof(Program).Assembly);
    upcasting.EnableAutoUpcastOnReplay();  // Auto-upcast on aggregate replay
});

builder.Services.AddEventSourcing(eventsourcing =>
{
    eventsourcing.UseInMemoryEventStore();
});
```

### Multi-Assembly Configuration

```csharp
builder.Services.AddMessageUpcasting(upcasting =>
{
    // Scan domain layer
    upcasting.ScanAssembly(typeof(UserAggregate).Assembly);

    // Scan integration events
    upcasting.ScanAssembly(typeof(OrderShippedEvent).Assembly);

    // Scan shared kernel
    upcasting.ScanAssembly(typeof(BaseEvent).Assembly);
});
```

### With Dependency Injection

```csharp
builder.Services.AddMessageUpcasting(upcasting =>
{
    // Some upcasters via scanning
    upcasting.ScanAssembly(typeof(Program).Assembly);

    // Upcaster with DI dependencies
    upcasting.RegisterUpcaster<AddressEventV1, AddressEventV2>(sp =>
        new AddressEventV1ToV2(
            sp.GetRequiredService<ICountryCodeMapper>(),
            sp.GetRequiredService<ILogger<AddressEventV1ToV2>>()));
});
```

### Conditional Registration

```csharp
// Register only if not already registered
if (!builder.Services.HasMessageUpcasting())
{
    builder.Services.AddMessageUpcasting(upcasting =>
    {
        upcasting.ScanAssembly(typeof(Program).Assembly);
    });
}
```

## Integration Points

### With Event Sourcing Repository

When `EnableAutoUpcastOnReplay()` is configured, the `EventSourcedRepository` automatically upcasts events when loading aggregates:

```csharp
// Configuration
builder.Services.AddMessageUpcasting(upcasting =>
{
    upcasting.ScanAssembly(typeof(Program).Assembly);
    upcasting.EnableAutoUpcastOnReplay();
});

builder.Services.AddEventSourcing();

// Usage - events are auto-upcasted
var repo = serviceProvider.GetRequiredService<IEventSourcedRepository<User>>();
var user = await repo.GetAsync(userId);  // V1 events upcasted to latest
```

### With Integration Event Dispatch

Upcasting on the publish path is handled automatically by the dispatch pipeline when
`AddMessageUpcasting(...)` is registered: an upcasting middleware sits in front of the
handler resolution and rewrites the inbound message to its latest version before the
handler runs. Producers therefore should always emit the **current** event version —
they own the schema and have no reason to publish stale shapes.

For consumer-side projections that read historical events from the store, upcasting is
applied automatically on `LoadAsync` (see "Auto-upcast on replay" above).

For ad-hoc upcasting outside the dispatch pipeline (e.g. one-off migration tooling),
inject `IUpcastingPipeline` directly:

```csharp
public sealed class EventMigrationTool
{
    private readonly IUpcastingPipeline _pipeline;
    private readonly IDispatcher _dispatcher;

    public EventMigrationTool(IUpcastingPipeline pipeline, IDispatcher dispatcher)
    {
        _pipeline = pipeline;
        _dispatcher = dispatcher;
    }

    public async Task RepublishAsync<T>(T legacyEvent, IMessageContext context, CancellationToken ct)
        where T : IDispatchMessage
    {
        var upcasted = _pipeline.Upcast(legacyEvent);
        await _dispatcher.DispatchAsync(upcasted, context, ct);
    }
}
```

## Service Lifetime

| Service | Lifetime |
|---------|----------|
| `IUpcastingPipeline` | Singleton |
| `UpcastingOptions` | Singleton (via Options pattern) |

The pipeline is a singleton because:
1. Path cache should be shared across requests
2. Registration happens once at startup
3. Thread-safe implementation supports concurrent reads

## See Also

- [UpcastingBuilder](./UpcastingBuilder.md) - Fluent builder API
- [IUpcastingPipeline](./IUpcastingPipeline.md) - Pipeline interface
- [Developer Guide](../../versioning/universal-upcasting-guide.md) - Step-by-step tutorial
