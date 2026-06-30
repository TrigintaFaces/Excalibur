# Excalibur.Security.AuditLogging

Bridges `ISecurityEventStore` onto the existing `IAuditStore` (e.g. `Excalibur.AuditLogging.SqlServer`),
giving security events durable, tamper-evident SQL persistence through the audit-store abstraction —
**without** duplicating any SQL/Dapper/hash-chain machinery (ADR-336: wire the advertised seam by
composing, never fork a parallel store).

## Usage

```csharp
// Register a durable IAuditStore (e.g. the SQL Server audit store) ...
services.AddSqlServerAuditStore(/* ... */);

// ... then bridge ISecurityEventStore onto it:
services.AddSqlSecurityEventStore();
```

## Round-trip fidelity (documented boundary)

The audit-relevant who/what/when/where of a `SecurityEvent` round-trips losslessly:

| `SecurityEvent` | `AuditEvent` |
| --- | --- |
| `Id` | `EventId` |
| `EventType` | `Action` (enum name, parsed back exactly) |
| `Timestamp` | `Timestamp` |
| `UserId` | `ActorId` |
| `CorrelationId` | `CorrelationId` |
| `Description` | `Reason` (dedicated column) |
| `SourceIp` | `IpAddress` (dedicated column, PII-masked by the audit pipeline) |
| `UserAgent` | `UserAgent` (dedicated column) |
| `Severity`, `MessageType` | `Metadata` (non-sensitive reference values) |

**Intentionally not carried:** `SecurityEvent.AdditionalData` (arbitrary `object?` forensic payload)
has no compliant home — the free-form `AuditEvent.Metadata` is contractually "references/identifiers
only, no sensitive values." Full forensic persistence (encryption-at-rest + retention) is a dedicated
security-event store, tracked as `bd-8f1l09`.
