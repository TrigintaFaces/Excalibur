# IMessageUpcaster<TOld, TNew> Interface

**Namespace:** `Excalibur.Dispatch.Abstractions.Versioning`
**Assembly:** `Excalibur.Dispatch.Abstractions`

Generic interface for type-safe message transformations between versions.

## Definition

```csharp
public interface IMessageUpcaster<in TOld, out TNew>
    where TOld : IDispatchMessage, IVersionedMessage
    where TNew : IDispatchMessage, IVersionedMessage
{
    int FromVersion { get; }
    int ToVersion { get; }
    TNew Upcast(TOld oldMessage);
}
```

## Type Parameters

### TOld (contravariant)
The source message type (older version). Must implement both `IDispatchMessage` and `IVersionedMessage`.

### TNew (covariant)
The target message type (newer version). Must implement both `IDispatchMessage` and `IVersionedMessage`.

## Properties

### FromVersion

Gets the source version this upcaster transforms from.

```csharp
int FromVersion { get; }
```

### ToVersion

Gets the target version this upcaster transforms to.

```csharp
int ToVersion { get; }
```

**Remarks:**
- `ToVersion` must be greater than `FromVersion`
- Upcasters can skip versions (e.g., V1 → V3) if intermediate versions are obsolete

## Methods

### Upcast

Transforms an old message version to a new version.

```csharp
TNew Upcast(TOld oldMessage);
```

**Parameters:**
- `oldMessage`: The source message to transform

**Returns:**
A new message instance with the target version

**Remarks:**
This method must be:
- **Pure** - Same input always produces same output
- **Immutable** - Does not modify `oldMessage`
- **Deterministic** - No random values or current time
- **Fast** - Called in hot path (event replay, command handling)

## Usage

### Basic Upcaster Implementation

```csharp
public class UserCreatedEventV1ToV2 : IMessageUpcaster<UserCreatedEventV1, UserCreatedEventV2>
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public UserCreatedEventV2 Upcast(UserCreatedEventV1 old)
    {
        // Split "Name" into "FirstName" and "LastName"
        var nameParts = old.Name.Split(' ', 2);

        return new UserCreatedEventV2
        {
            // Preserve identity
            EventId = old.EventId,
            AggregateId = old.AggregateId,
            AggregateVersion = old.AggregateVersion,
            OccurredAt = old.OccurredAt,

            // Transform data
            FirstName = nameParts.Length > 0 ? nameParts[0] : string.Empty,
            LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty,
            Email = old.Email
        };
    }
}
```

### Upcaster with Default Values

```csharp
public class OrderEventV2ToV3 : IMessageUpcaster<OrderEventV2, OrderEventV3>
{
    public int FromVersion => 2;
    public int ToVersion => 3;

    public OrderEventV3 Upcast(OrderEventV2 old)
    {
        return new OrderEventV3
        {
            // Preserve existing fields
            EventId = old.EventId,
            AggregateId = old.AggregateId,
            AggregateVersion = old.AggregateVersion,
            OccurredAt = old.OccurredAt,
            OrderId = old.OrderId,
            Items = old.Items,

            // New field with default value
            Currency = "USD",  // V2 didn't have currency, default to USD
            ShippingMethod = "Standard"  // New in V3
        };
    }
}
```

### Upcaster with Dependency Injection

```csharp
public class AddressEventV1ToV2 : IMessageUpcaster<AddressEventV1, AddressEventV2>
{
    private readonly ICountryCodeMapper _countryMapper;

    public AddressEventV1ToV2(ICountryCodeMapper countryMapper)
    {
        _countryMapper = countryMapper;
    }

    public int FromVersion => 1;
    public int ToVersion => 2;

    public AddressEventV2 Upcast(AddressEventV1 old)
    {
        return new AddressEventV2
        {
            EventId = old.EventId,
            AggregateId = old.AggregateId,
            AggregateVersion = old.AggregateVersion,
            OccurredAt = old.OccurredAt,

            Street = old.Street,
            City = old.City,
            // V1 had country names, V2 uses ISO codes
            CountryCode = _countryMapper.NameToCode(old.Country),
            PostalCode = old.PostalCode
        };
    }
}

// Registration with DI
services.AddMessageUpcasting(builder =>
{
    builder.RegisterUpcaster<AddressEventV1, AddressEventV2>(sp =>
        new AddressEventV1ToV2(sp.GetRequiredService<ICountryCodeMapper>()));
});
```

### Multi-Hop Chain

```csharp
// V1 → V2 → V3 → V4 upcasting chain
public class UserEventV1ToV2 : IMessageUpcaster<UserEventV1, UserEventV2> { ... }
public class UserEventV2ToV3 : IMessageUpcaster<UserEventV2, UserEventV3> { ... }
public class UserEventV3ToV4 : IMessageUpcaster<UserEventV3, UserEventV4> { ... }

// Registration
services.AddMessageUpcasting(builder =>
{
    builder.RegisterUpcaster(new UserEventV1ToV2());
    builder.RegisterUpcaster(new UserEventV2ToV3());
    builder.RegisterUpcaster(new UserEventV3ToV4());
});

// Pipeline automatically finds shortest path: V1 → V2 → V3 → V4
```

## Performance

The interface uses direct delegate invocation for type-safe transformations:
- **Direct delegate invocation**: ~36ns
- **DynamicInvoke (comparison)**: ~107ns
- **Full transformation (including allocation)**: ~90-105ns per hop

The performance characteristics:
1. Compile-time type safety (no runtime type checking)
2. Variance markers enable direct delegate storage
3. Path lookups are cached for O(1) access (~13-18ns)
4. Each transformation creates a new message instance (inherent cost)

## Design Notes

### Variance

The interface uses variance markers for flexibility:
- `in TOld` (contravariant) - Allows accepting base types
- `out TNew` (covariant) - Allows returning derived types

### Why Single Hops?

Each upcaster handles exactly one version transition. This design:
- Keeps upcasters simple and testable
- Allows the pipeline to find optimal paths
- Supports adding/removing versions without cascading changes

## See Also

- [IVersionedMessage](./IVersionedMessage.md) - Message versioning marker
- [IUpcastingPipeline](./IUpcastingPipeline.md) - Orchestrate multi-hop upcasting
- [UpcastingBuilder](./UpcastingBuilder.md) - Fluent registration API
