# Event ID Strategy

This document defines the authoritative Event ID allocation strategy for the Excalibur.Dispatch framework. Event IDs are used with .NET's `[LoggerMessage]` source generator for structured logging.

**Authoritative Documentation:** [Event ID Strategy Consolidation](../../management/architecture/adr-095-event-id-strategy-consolidation.md)

---

## Overview

Each package in the solution is assigned a dedicated Event ID range. This enables:

- **Log Filtering** - Filter logs by Event ID range to isolate package-specific messages
- **Conflict Prevention** - No two packages share the same Event ID
- **Centralized Tracking** - Single reference for all allocations

---

## Event ID Allocation

### Dispatch Packages (1-2999)

| Range | Package | Description |
|-------|---------|-------------|
| 1-99 | **CONFLICT ZONE** | Legacy conflicts - see below |
| 100-199 | **CONFLICT ZONE** | Legacy conflicts - see below |
| 300-344 | Excalibur.Dispatch.Resilience.Polly | Resilience policies, circuit breakers |
| 400-499 | Excalibur.Dispatch.Observability | Context flow tracking, telemetry |
| 520-563 | Excalibur.Security | Security middleware, authorization |
| 600-642 | Excalibur.Dispatch.Transport.RabbitMQ | RabbitMQ transport implementation |
| 700-799 | Excalibur.Dispatch.Transport.Kafka | Kafka transport implementation |
| 800-899 | Excalibur.Dispatch.Transport.GooglePubSub | Google Pub/Sub transport |
| 920-1099 | Excalibur.Dispatch.Transport.Abstractions | Transport abstraction layer |
| 1100-1299 | Excalibur.Dispatch.Transport.AzureServiceBus | Azure Service Bus transport |
| 1300-1499 | Excalibur.Dispatch.Transport.AwsSqs | AWS SQS/SNS transport |
| 2000-2199 | Excalibur.Dispatch.Middleware | Pipeline middleware components |
| 2100-2117 | Excalibur.Dispatch.BackgroundServices.Outbox | Outbox background service |
| 2200-2599 | Excalibur.Dispatch (Core) | Core dispatcher, handlers, subsystems |
| 2600-2699 | Excalibur.Dispatch.Hosting.* | Hosting providers + ASP.NET Core authorization bridge (2600-2606) |

### Compliance & Caching Packages (2500-2799)

| Range | Package | Description |
|-------|---------|-------------|
| 2500-2599 | Excalibur.Dispatch.Caching | Cache middleware, key builder |
| 2700-2799 | Excalibur.Compliance | Processing restriction, rectification |

### Reserved Ranges (2800-2999) - Now Available

These ranges were originally reserved for remapping packages with duplicate Event IDs.
**All conflicts have been resolved** - packages were remapped to dedicated 50000+ and 70000+ ranges.

| Range | Status |
|-------|--------|
| 2800-2999 | Available for future use |

**Resolved Remappings:**

| Package | Original IDs | New Range | Status |
|---------|--------------|-----------|--------|
| Excalibur.Dispatch.Hosting.GoogleCloudFunctions | 1-11 | 50400-50499 | Resolved |
| Excalibur.Dispatch.Hosting.AzureFunctions | 1-10 | 50200-50399 | Resolved |
| Excalibur.Security (all) | 1-10, 100-105 | 70000-70999 | Resolved |
| Excalibur.Dispatch.Patterns (all) | 66-70, 94-105 | 90000-91299 | Resolved |

### Excalibur Packages (3000-4999)

Reserved for Excalibur package LoggerMessage migrations:

| Range | Package | Status |
|-------|---------|--------|
| 3000-3099 | Excalibur.EventSourcing | Reserved |
| 3100-3199 | Excalibur.Data.SqlServer | Reserved |
| 3200-3299 | Excalibur.Data.Postgres | Reserved |
| 3300-3399 | Excalibur.Data.CosmosDb | Reserved |
| 3400-3499 | Excalibur.Data.DynamoDb | Reserved |
| 3500-3599 | Excalibur.A3.Governance | **Active** -- SoD detective scan (3500-3504), SoD preventive (3510-3511), Access review expiry (3520-3530), Orphaned access (3540-3549) |
| 3600-3699 | Excalibur.Caching | Reserved |
| 3700-3799 | Excalibur.Data.IdentityMap.SqlServer | **Active** -- BindingStored (3700), BindingConflict (3701) |
| 3800-4999 | Other Excalibur.* packages | Reserved |

---

## Known Conflicts - RESOLVED

**Status:** All conflicts resolved.

The following Event ID ranges had duplicate assignments that have now been resolved:

### Range 1-11 (Historical - Resolved)

Multiple packages previously used Event IDs 1-11. All have been remapped:

| Package | Original IDs | New Range |
|---------|--------------|-----------|
| GoogleCloudFunctionsHostProvider | 1-11 | 50400-50410 |
| AzureFunctionsHostProvider | 1-10 | 50200-50211 |
| SqlSecurityEventStore | 1-11 | 70700-70711 |
| FileSecurityEventStore | 1-11 | 70730-70745 |
| MessageSigningMiddleware | 1-6 | 70200-70229 |
| HmacMessageSigningService | 1-6 | 70218-70222 |
| DataProtectionMessageEncryptionService | 1-10 | 70300-70318 |
| EncryptionMigrationService | 1-5 | 70400-70413 |
| LazyReEncryptionMiddleware | 1-5 | 70403-70413 |

### Range 100-105 (Historical - Resolved)

| Package | Original IDs | New Range |
|---------|--------------|-----------|
| InMemoryClaimCheckCleanupService | 100-105 | 90100-90109 |
| RoutingPolicyEvaluator | 100-105 | 90200-90216 |
| TimeZoneAwareRouter | 100-104 | 90203-90216 |

**Verification:** Run the commands in the Verification section to confirm no duplicates exist.

---

## Guidelines for New Event IDs

### When Adding LoggerMessage Methods

1. **Find your package's assigned range** in the tables above
2. **Use the next available ID** within your range
3. **Document the assignment** in your package's logger partial class

### Example

```csharp
// In Excalibur.Dispatch.Transport.RabbitMQ (range 600-642)
internal static partial class RabbitMQLogger
{
    // Next available ID: 643 (if migrating new method)
    [LoggerMessage(
        EventId = 643,
        Level = LogLevel.Information,
        Message = "New logging message here")]
    public static partial void NewLoggingMethod(this ILogger logger);
}
```

### Range Exhaustion

If your package's range is exhausted:

1. Contact **SoftwareArchitect** via Agent Mail
2. Request a range extension or secondary range
3. Update this document with the new allocation

---

## Verification

Use these commands to verify Event ID consistency:

```bash
# Find all Event IDs in use
grep -rE "EventId\s*=\s*\d+" src/Dispatch --include="*.cs" | grep -oE "EventId\s*=\s*\d+" | sort -t= -k2 -n | uniq

# Find duplicate Event IDs (should return empty for clean codebase)
grep -rE "EventId\s*=\s*\d+" src/Dispatch --include="*.cs" | grep -oE "\d+$" | sort -n | uniq -d

# Count Event IDs per range
grep -rE "EventId\s*=\s*\d+" src/Dispatch --include="*.cs" | grep -oE "\d+$" | awk '{
    if ($1 < 100) print "1-99: " $1
    else if ($1 < 200) print "100-199: " $1
    else if ($1 < 1000) print "200-999: " $1
    else if ($1 < 2000) print "1000-1999: " $1
    else print "2000+: " $1
}' | cut -d: -f1 | sort | uniq -c
```

---

## History

| Phase | Action |
|-------|--------|
| Phase 1 | LoggerMessage migration (Observability, IDs 400-499) |
| Phase 2 | Resilience migration (IDs 300-344) |
| Phase 3 | Security migration (IDs 520-563) |
| Phase 4 | Transport.RabbitMQ migration (IDs 600-642) |
| Phase 5 | Transport.Kafka migration (IDs 700-799) |
| Phase 6 | Transport.GooglePubSub migration (IDs 800-899) |
| Phase 7 | Transport.Abstractions migration (IDs 920-1099) |
| Phase 8 | Transport.AzureServiceBus migration (IDs 1100-1299) |
| Phase 9 | Transport.AwsSqs migration (IDs 1300-1499) |
| Phase 10 | Middleware migration (IDs 2000-2199) |
| Phase 11 | Dispatch Core migration (IDs 2200-2599) |
| Phase 12 | Hosting migration (IDs 2600-2699) - **Migration Complete** |
| Documentation | Central documentation established |
| Conflict Resolution | Event ID duplicate resolution - packages remapped to 50000+, 70000+, 90000+ ranges |
| Verification | All Conflicts Resolved |
| Authorization Bridge | ASP.NET Core Authorization Bridge - IDs 2600-2606 allocated in `Excalibur.Dispatch.Hosting.*` range |
| IdentityMap | Excalibur.Data.IdentityMap.SqlServer allocation (3700-3799) formalized |
| Outbox Publisher | MessageBusOutboxPublisher EventIds 2400-2408 allocated within Dispatch Core (2200-2599) range |

---

## See Also

- [Event ID Strategy Consolidation](../../management/architecture/adr-095-event-id-strategy-consolidation.md)
- [.NET LoggerMessage Source Generator](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator)

---

*Document created by DocumentationWriter*
*Updated by BackendDeveloper - All Event ID conflicts resolved*
*Updated by BackendDeveloper - Formalized Excalibur.Data.IdentityMap.SqlServer allocation (3700-3799)*
