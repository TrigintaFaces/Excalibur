# Metapackage vs Granular Composition

**Location:** `samples/01-getting-started/MetapackageQuickStart/`

Side-by-side reference showing two equivalent ways to wire the Excalibur
SQL Server stack:

| Path | Code | Best for |
|------|------|----------|
| **Metapackage** | `services.AddExcaliburSqlServer(sql => ...)` | Sensible defaults, full production stack, fastest path to a working app |
| **Granular** | `services.AddExcalibur(excalibur => excalibur.AddEventSourcing(...).AddOutbox(...).Add...())` | Trimming scope, mixing providers, hand-tuning every subsystem |

## Running both modes

```bash
# A) Metapackage (default)
dotnet run

# B) Granular
SAMPLE_MODE=granular dotnet run    # bash
$env:SAMPLE_MODE='granular'; dotnet run    # PowerShell
```

Both modes register `IDispatcher` + the event store. The metapackage mode
additionally registers inbox, saga, leader election, audit, compliance, and
data-access executors; the granular mode only registers what you ask for.

## When to pick the metapackage

- Your app uses most of the subsystems the metapackage wires
- You want dependencies updated together (one version number)
- You are new to Excalibur and want the happy path

## When to pick granular composition

- You want only event sourcing + outbox (no saga/inbox/leader election)
- You want to mix providers (SqlServer ES, Postgres outbox, Azure Blob snapshots)
- You're embedding Excalibur in a framework or plugin and want to keep
  dependencies minimal
- You need to intercept or replace a specific registration (e.g. custom
  `IEventStore`) that the metapackage doesn't expose as a hook

## Metapackage reference

```csharp
services.AddExcaliburSqlServer(sql =>
{
    sql.ConnectionString  = connection;
    sql.UseInbox          = true;          // toggle off to drop inbox
    sql.UseSaga           = true;
    sql.UseLeaderElection = true;
    sql.UseAuditLogging   = true;
    sql.UseCompliance     = true;

    // Per-subsystem fine-tuning hooks
    sql.ConfigureSaga(saga => saga.SchemaName("sagas"));
    sql.ConfigureAuditLogging(audit => audit.SchemaName = "audit");
});
```

## Granular reference

```csharp
services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es =>
        {
            es.UseSqlServer(sql => sql.ConnectionString(connection));
            es.UseIntervalSnapshots(100);
        })
        .AddOutbox(outbox =>
        {
            outbox.UseSqlServer(sql => sql.ConnectionString(connection));
        });
});
```

## Related

- `samples/11-real-world/FullStackAddExcalibur/` -- larger granular composition with CDC, ES projections, and DataProcessing
- `src/metapackages/Excalibur.SqlServer/` -- source of the metapackage
