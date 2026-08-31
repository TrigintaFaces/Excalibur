# UpcastingBuilder Class

**Namespace:** `Dispatch.DependencyInjection`
**Assembly:** `Dispatch`

Fluent builder for configuring message upcasting services.

## Definition

```csharp
public sealed class UpcastingBuilder
{
    public UpcastingBuilder RegisterUpcaster<TOld, TNew>(IMessageUpcaster<TOld, TNew> upcaster);
    public UpcastingBuilder RegisterUpcaster<TOld, TNew>(Func<IServiceProvider, IMessageUpcaster<TOld, TNew>> factory);
    public UpcastingBuilder ScanAssembly(Assembly assembly, Func<Type, bool>? filter = null);
    public UpcastingBuilder ScanAssemblies(IEnumerable<Assembly> assemblies, Func<Type, bool>? filter = null);
    public UpcastingBuilder EnableAutoUpcastOnReplay(bool enable = true);
}
```

## Methods

### RegisterUpcaster (Instance)

Registers an upcaster instance for a specific version transition.

```csharp
public UpcastingBuilder RegisterUpcaster<TOld, TNew>(IMessageUpcaster<TOld, TNew> upcaster)
    where TOld : IDispatchMessage, IVersionedMessage
    where TNew : IDispatchMessage, IVersionedMessage;
```

**Parameters:**
- `upcaster`: The upcaster instance

**Returns:**
The builder for method chaining.

**Example:**
```csharp
services.AddMessageUpcasting(builder =>
{
    builder.RegisterUpcaster(new UserEventV1ToV2());
    builder.RegisterUpcaster(new UserEventV2ToV3());
});
```

### RegisterUpcaster (Factory)

Registers an upcaster using a factory function that receives the service provider.

```csharp
public UpcastingBuilder RegisterUpcaster<TOld, TNew>(
    Func<IServiceProvider, IMessageUpcaster<TOld, TNew>> factory)
    where TOld : IDispatchMessage, IVersionedMessage
    where TNew : IDispatchMessage, IVersionedMessage;
```

**Parameters:**
- `factory`: Factory function that creates the upcaster using DI services

**Returns:**
The builder for method chaining.

**Example:**
```csharp
services.AddMessageUpcasting(builder =>
{
    // Upcaster with dependencies
    builder.RegisterUpcaster<AddressEventV1, AddressEventV2>(sp =>
        new AddressEventV1ToV2(sp.GetRequiredService<ICountryCodeMapper>()));

    // Upcaster with logging
    builder.RegisterUpcaster<OrderEventV1, OrderEventV2>(sp =>
        new OrderEventV1ToV2(sp.GetRequiredService<ILogger<OrderEventV1ToV2>>()));
});
```

### ScanAssembly

Scans an assembly for all types implementing `IMessageUpcaster<TOld, TNew>` and registers them.

```csharp
[RequiresUnreferencedCode("Assembly scanning uses reflection to discover upcaster types.")]
public UpcastingBuilder ScanAssembly(Assembly assembly, Func<Type, bool>? filter = null);
```

**Parameters:**
- `assembly`: The assembly to scan
- `filter`: Optional filter to exclude certain types. Return `true` to include, `false` to exclude.

**Returns:**
The builder for method chaining.

**Example:**
```csharp
services.AddMessageUpcasting(builder =>
{
    // Scan all upcasters in the current assembly
    builder.ScanAssembly(typeof(Program).Assembly);

    // Scan with filter to exclude test upcasters
    builder.ScanAssembly(
        typeof(Program).Assembly,
        type => !type.Name.Contains("Test"));

    // Scan only specific namespace
    builder.ScanAssembly(
        typeof(Program).Assembly,
        type => type.Namespace?.StartsWith("MyApp.Events.Upcasters") == true);
});
```

### ScanAssemblies

Scans multiple assemblies for upcaster types.

```csharp
[RequiresUnreferencedCode("Assembly scanning uses reflection to discover upcaster types.")]
public UpcastingBuilder ScanAssemblies(IEnumerable<Assembly> assemblies, Func<Type, bool>? filter = null);
```

**Parameters:**
- `assemblies`: The assemblies to scan
- `filter`: Optional filter to exclude certain types

**Returns:**
The builder for method chaining.

**Example:**
```csharp
services.AddMessageUpcasting(builder =>
{
    builder.ScanAssemblies(new[]
    {
        typeof(UserEvents).Assembly,
        typeof(OrderEvents).Assembly,
        typeof(ProductEvents).Assembly
    });
});
```

### EnableAutoUpcastOnReplay

Enables or disables automatic upcasting during event store replay.

```csharp
public UpcastingBuilder EnableAutoUpcastOnReplay(bool enable = true);
```

**Parameters:**
- `enable`: `true` to enable, `false` to disable

**Returns:**
The builder for method chaining.

**Example:**
```csharp
services.AddMessageUpcasting(builder =>
{
    builder.ScanAssembly(typeof(Program).Assembly);
    builder.EnableAutoUpcastOnReplay();  // Events auto-upcasted on aggregate replay
});
```

## Usage Patterns

### Simple Registration

```csharp
services.AddMessageUpcasting(builder =>
{
    builder.RegisterUpcaster(new UserCreatedEventV1ToV2());
    builder.RegisterUpcaster(new UserCreatedEventV2ToV3());
    builder.RegisterUpcaster(new OrderPlacedEventV1ToV2());
});
```

### Assembly Scanning

```csharp
services.AddMessageUpcasting(builder =>
{
    // Auto-discover all upcasters
    builder.ScanAssembly(typeof(Program).Assembly);
});
```

### Mixed Registration

```csharp
services.AddMessageUpcasting(builder =>
{
    // Simple upcasters via scanning
    builder.ScanAssembly(typeof(Program).Assembly);

    // Override specific upcaster with DI-aware version
    builder.RegisterUpcaster<AddressEventV1, AddressEventV2>(sp =>
        new AddressEventV1ToV2(sp.GetRequiredService<ICountryCodeMapper>()));
});
```

### Production Configuration

```csharp
services.AddMessageUpcasting(builder =>
{
    // Scan domain events assembly
    builder.ScanAssembly(typeof(UserCreatedEvent).Assembly);

    // Scan integration events assembly
    builder.ScanAssembly(typeof(OrderShippedIntegrationEvent).Assembly);

    // Enable auto-upcasting for event sourcing
    builder.EnableAutoUpcastOnReplay();
});
```

## Design Notes

### Deferred Execution

The builder collects registration actions during configuration. These actions are executed when the `IUpcastingPipeline` singleton is created, avoiding the anti-pattern of calling `BuildServiceProvider()` during configuration.

### Instance Resolution

When scanning assemblies, upcasters are created using:
1. **DI first** - `ActivatorUtilities.CreateInstance()` for constructor injection
2. **Fallback** - Parameterless constructor if DI fails

### Thread Safety

All builder methods are designed to be called during startup (single-threaded configuration phase). The resulting `IUpcastingPipeline` is thread-safe for runtime use.

## See Also

- [AddMessageUpcasting](./AddMessageUpcasting.md) - DI extension method
- [IMessageUpcaster](./IMessageUpcaster.md) - Upcaster interface
- [IUpcastingPipeline](./IUpcastingPipeline.md) - Pipeline interface
