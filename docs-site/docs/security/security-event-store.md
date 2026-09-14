---
sidebar_position: 5
title: Security Event Store
description: Persist and query authentication, authorization and threat-detection events through ISecurityEventStore
---

# Security Event Store

`ISecurityEventStore` records security-relevant events — failed sign-ins, authorization denials,
injection attempts, rate-limit breaches — and queries them back. `Excalibur.Security.AuditLogging`
supplies a SQL-backed implementation that writes through the audit store you already configured, so
security events land in the same tamper-evident audit trail as everything else.

## Registration

```csharp
// The security event store writes through IAuditStore, so register an audit store first.
services.AddAuditLogging();
services.AddSqlServerAuditStore(connectionString);

// Then the security event store itself.
services.AddSqlSecurityEventStore();
```

:::caution `AddSqlSecurityEventStore()` does not bring its own storage
It registers `ISecurityEventStore` only. The implementation takes an `IAuditStore` constructor
dependency and nothing in this call supplies one — a host that calls it without registering an audit
store resolves `ISecurityEventStore` and fails at that point, not at startup. Register the audit
store in the same composition.

The registration uses `TryAdd`, so a consumer-supplied `ISecurityEventStore` registered earlier wins
and this call becomes a no-op.
:::

## Recording events

```csharp
public sealed class SignInAuditor(ISecurityEventStore store)
{
    public Task RecordFailureAsync(string userId, string sourceIp, CancellationToken ct) =>
        store.StoreEventsAsync(
            [
                new SecurityEvent
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTimeOffset.UtcNow,
                    EventType = SecurityEventType.AuthenticationFailure,
                    Severity = SecuritySeverity.Medium,
                    Description = "Password sign-in rejected",
                    UserId = userId,
                    SourceIp = sourceIp,
                },
            ],
            ct);
}
```

`StoreEventsAsync` takes a sequence, so a request that produces several events is one round trip.

## Querying events

```csharp
var suspicious = await store.QueryEventsAsync(
    new SecurityEventQuery
    {
        StartTime = DateTimeOffset.UtcNow.AddHours(-24),
        MinimumSeverity = SecuritySeverity.High,
        MaxResults = 500,
    },
    cancellationToken);
```

Every `SecurityEventQuery` filter is optional and they combine with AND. `MaxResults` defaults to
**1000** — an unbounded query is not available, so a caller paging through a large window narrows by
time rather than by asking for everything.

## `SecurityEvent`

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | Caller-assigned; the store does not generate one |
| `Timestamp` | `DateTimeOffset` | When the event occurred |
| `EventType` | `SecurityEventType` | See the values below |
| `Description` | `string` | Free text; defaults to empty rather than null |
| `Severity` | `SecuritySeverity` | `Low`, `Medium`, `High`, `Critical` |
| `CorrelationId` | `Guid?` | Ties the event to a request or message flow |
| `UserId` | `string?` | Subject, where one is known |
| `SourceIp` | `string?` | Client address |
| `UserAgent` | `string?` | Client application identifier |
| `MessageType` | `string?` | The message being handled, for dispatch-originated events |
| `AdditionalData` | `IDictionary<string, object?>` | Defaults to an empty dictionary |

Properties are `init`-only: an event is a record of something that happened and is not edited after
construction.

### `SecurityEventType`

`AuthenticationSuccess`, `AuthenticationFailure`, `AuthorizationSuccess`, `AuthorizationFailure`,
`ValidationFailure`, `ValidationError`, `InjectionAttempt`, `RateLimitExceeded`,
`SuspiciousActivity`, `DataExfiltrationAttempt`, `ConfigurationChange`, `CredentialRotation`,
`AuditLogAccess`, `SecurityPolicyViolation`, `EncryptionFailure`, `DecryptionFailure`.

## `SecurityEventQuery`

| Property | Type | Default |
|----------|------|---------|
| `StartTime` / `EndTime` | `DateTimeOffset?` | unbounded |
| `EventType` | `SecurityEventType?` | any |
| `MinimumSeverity` | `SecuritySeverity?` | any |
| `UserId` / `SourceIp` | `string?` | any |
| `CorrelationId` | `Guid?` | any |
| `MaxResults` | `int` | `1000` |

## See Also

- [Audit Logging](./audit-logging.md) — the store these events are written through
- [Authorization & Audit (A3)](./authorization.md)
