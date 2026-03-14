# E-commerce Order Versioning

Demonstrates multi-hop message upcasting for e-commerce order domain events evolving through three versions.

## Scenario

An e-commerce platform's `OrderPlacedEvent` has evolved over time:

| Version | Era | Fields | Change |
|---------|-----|--------|--------|
| V1 | Launch | `OrderId`, `Total` | Original schema |
| V2 | Loyalty program | + `CustomerId` | Customer attribution |
| V3 | Tax compliance | Split `Total` into `Subtotal` + `Tax` | Tax reporting |

## What This Sample Shows

1. **Multi-hop upcasting** -- V1 events are automatically transformed through V2 to V3
2. **BFS path finding** -- The pipeline finds the shortest transformation path
3. **Safe defaults** -- Legacy orders get `CustomerId = Guid.Empty` and `Tax = 0`
4. **No-op passthrough** -- V3 events pass through unchanged

## Running the Sample

```bash
cd samples/09-advanced/Versioning.Examples/EcommerceOrderVersioning
dotnet run
```

## Registration

```csharp
services.AddMessageUpcasting(builder =>
{
    builder.RegisterUpcaster(new OrderPlacedV1ToV2Upcaster());
    builder.RegisterUpcaster(new OrderPlacedV2ToV3Upcaster());
    builder.EnableAutoUpcastOnReplay();
});
```

## Upcaster Implementations

### V1 to V2 (Add CustomerId)

```csharp
public sealed class OrderPlacedV1ToV2Upcaster : IMessageUpcaster<OrderPlacedEventV1, OrderPlacedEventV2>
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public OrderPlacedEventV2 Upcast(OrderPlacedEventV1 oldMessage) =>
        new() { OrderId = oldMessage.OrderId, CustomerId = Guid.Empty, Total = oldMessage.Total };
}
```

### V2 to V3 (Split Total into Subtotal + Tax)

```csharp
public sealed class OrderPlacedV2ToV3Upcaster : IMessageUpcaster<OrderPlacedEventV2, OrderPlacedEventV3>
{
    public int FromVersion => 2;
    public int ToVersion => 3;

    public OrderPlacedEventV3 Upcast(OrderPlacedEventV2 oldMessage) =>
        new() { OrderId = oldMessage.OrderId, CustomerId = oldMessage.CustomerId,
                Subtotal = oldMessage.Total, Tax = 0m };
}
```

## Project Structure

```
EcommerceOrderVersioning/
├── EcommerceOrderVersioning.csproj
├── Program.cs                         # Demo with V1→V3 upcasting scenarios
├── Events/
│   └── OrderPlacedEvents.cs           # V1, V2, V3 event definitions
├── Upcasters/
│   └── OrderPlacedUpcasters.cs        # V1→V2 and V2→V3 upcasters
└── README.md
```

## Related Samples

- [IntegrationEventVersioning](../IntegrationEventVersioning/) -- Cross-service integration event versioning
- [UserProfileVersioning](../UserProfileVersioning/) -- GDPR-aware profile event versioning
- [EventUpcasting](../../EventUpcasting/) -- Full event sourcing upcasting with aggregates
