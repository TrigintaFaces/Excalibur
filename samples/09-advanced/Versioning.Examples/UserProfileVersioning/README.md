# User Profile Versioning

Demonstrates multi-hop message upcasting for user profile events with a focus on GDPR compliance patterns.

## Scenario

A SaaS platform's `UserProfileUpdatedEvent` has evolved through four versions:

| Version | Era | Fields | Change |
|---------|-----|--------|--------|
| V1 | Launch | `UserId`, `Name` | Single name field |
| V2 | UX improvement | Split `Name` into `FirstName` + `LastName` | Proper name handling |
| V3 | GDPR compliance | + `ConsentGiven`, `ConsentDate` | Consent tracking |
| V4 | Privacy enhancement | + `Email`, `IsEmailEncrypted` | Field-level encryption |

## What This Sample Shows

1. **4-version upcasting chain** -- V1 events transform through V2, V3, to V4
2. **GDPR compliance patterns** -- Consent tracking and grandfathering for legacy users
3. **Name splitting** -- Parsing "John Smith" into FirstName/LastName
4. **Assembly scanning** -- Auto-discovery of all upcasters

## Running the Sample

```bash
cd samples/09-advanced/Versioning.Examples/UserProfileVersioning
dotnet run
```

## Registration

```csharp
services.AddMessageUpcasting(builder =>
{
    builder.ScanAssembly(typeof(UserProfileV1ToV2Upcaster).Assembly);
    builder.EnableAutoUpcastOnReplay();
});
```

## GDPR Compliance Patterns

| Pattern | Implementation |
|---------|---------------|
| Consent tracking | `ConsentGiven` + `ConsentDate` fields (V3+) |
| Legacy grandfathering | V1/V2 users get `ConsentGiven = true`, `ConsentDate = null` |
| Email re-verification | V1-V3 users get `Email = ""` (must re-verify) |
| Field-level encryption | `IsEmailEncrypted` flag for PII protection (V4) |

## Project Structure

```
UserProfileVersioning/
├── UserProfileVersioning.csproj
├── Program.cs                         # Demo with V1→V4 upcasting scenarios
├── Events/
│   └── UserProfileEvents.cs           # V1, V2, V3, V4 event definitions
├── Upcasters/
│   └── UserProfileUpcasters.cs        # V1→V2, V2→V3, V3→V4 upcasters
└── README.md
```

## Related Samples

- [EcommerceOrderVersioning](../EcommerceOrderVersioning/) -- Domain event versioning
- [IntegrationEventVersioning](../IntegrationEventVersioning/) -- Cross-service integration event versioning
- [EventUpcasting](../../EventUpcasting/) -- Full event sourcing upcasting with aggregates
