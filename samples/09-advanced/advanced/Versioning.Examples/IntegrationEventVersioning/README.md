# Integration Event Versioning

Demonstrates message upcasting for cross-service integration events in a multi-tenant e-commerce platform.

## Scenario

A `ProductPriceChanged` integration event consumed across microservices has evolved:

| Version | Era | Fields | Change |
|---------|-----|--------|--------|
| V1 | Single-tenant | `ProductId`, `NewPrice` | Original schema |
| V2 | Multi-tenant | + `TenantId` | Marketplace support |
| V3 | International | + `Currency`, `OldPrice`, `ChangePercentage` | Currency and price tracking |

## What This Sample Shows

1. **Cross-service compatibility** -- Consumer (Cart Service) handles events from producers at any version
2. **Assembly scanning** -- `ScanAssembly()` auto-discovers upcasters
3. **Migration artifacts** -- Identifiable defaults (`TenantId="default"`, `OldPrice==NewPrice`) indicate origin version
4. **UpcastingMessageBusDecorator** -- Transparent upcasting for integration events

## Running the Sample

```bash
cd samples/09-advanced/Versioning.Examples/IntegrationEventVersioning
dotnet run
```

## Registration

```csharp
services.AddMessageUpcasting(builder =>
{
    builder.ScanAssembly(typeof(ProductPriceChangedV1ToV2Upcaster).Assembly);
});
```

## Migration Artifacts

When consuming V3 events upcasted from earlier versions:

| Artifact | Meaning |
|----------|---------|
| `TenantId = "default"` | Originated as V1 (pre-multi-tenant) |
| `OldPrice == NewPrice` | No price history (V1 or V2 origin) |
| `Currency = "USD"` | May be default from V1/V2 upcast |

## Project Structure

```
IntegrationEventVersioning/
├── IntegrationEventVersioning.csproj
├── Program.cs                         # Demo simulating multi-version producers
├── Events/
│   └── ProductCatalogEvents.cs        # V1, V2, V3 event definitions
├── Upcasters/
│   └── ProductCatalogUpcasters.cs     # V1→V2 and V2→V3 upcasters
└── README.md
```

## Related Samples

- [EcommerceOrderVersioning](../EcommerceOrderVersioning/) -- Domain event versioning
- [UserProfileVersioning](../UserProfileVersioning/) -- GDPR-aware profile versioning
- [EventUpcasting](../../EventUpcasting/) -- Full event sourcing upcasting with aggregates
