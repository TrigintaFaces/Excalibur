# Excalibur.EventSourcing.Oracle

Oracle Database implementations of the Excalibur event-sourcing stores.

Provides:

- `OracleEventStore` — `IEventStore` with optimistic concurrency (read-current-version-then-compare inside a serializable transaction), atomic append, and GDPR erasure.
- `OracleSnapshotStore` — `ISnapshotStore` with `MERGE`-based upsert semantics.

Data access uses Dapper over `Oracle.ManagedDataAccess.Core` (ODP.NET). No EntityFramework.

## Registration

```csharp
services.AddOracleEventStore(o =>
{
    o.ConnectionString = "User Id=excalibur;Password=...;Data Source=localhost:1521/FREEPDB1";
    o.Schema = "EXCALIBUR";
});
services.AddOracleSnapshotStore(o => o.ConnectionString = "...");
```

Options are validated at startup (`ValidateOnStart`).
