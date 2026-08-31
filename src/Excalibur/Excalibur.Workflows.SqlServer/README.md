# Excalibur.Workflows.SqlServer

SQL Server implementation of the durable workflow signal inbox for Excalibur.

## Part Of

The [Excalibur](https://github.com/Excalibur-Framework) durable-execution engine (`Excalibur.Workflows`).

## What It Provides

A restart-durable `IWorkflowSignalInbox`. Admitted signals and their deduplication keys are persisted to
SQL Server, so a signal admitted but not yet drained survives a process restart, and a producer's
redelivery of the same `(instanceId, signalId)` after a restart is still deduplicated — the guarantee the
in-process default cannot make.

## Usage

```csharp
services.AddWorkflows();

// Wire the durable SQL Server signal inbox (overrides the in-process default) and, in the same call,
// register the durability and tenant-scoping capability markers.
services.AddSqlServerWorkflowSignalInbox(options =>
{
    options.ConnectionString = configuration.GetConnectionString("Workflows")!;
    options.SchemaName = "dbo";
    options.TableName = "workflow_signal_inbox";
});

// Optional: fail host start if a durable signal inbox is NOT wired (a deployment that requires
// restart-durable signals opts in to this guard).
services.RequireDurableSignalInbox();
```

### Multi-tenancy

The signal mailbox is tenant-owned: a signal belongs to the tenant whose workflow instance it addresses,
and the durable table carries the tenant in its unique key (`UNIQUE (TenantId, InstanceId, SignalId)`), so
two tenants using the same signal id are kept distinct rather than the second being treated as a
duplicate. This registration attests that scoping as part of wiring the store.

A host that supplies its **own** `IWorkflowSignalInbox` — for example to run signal admission across
processes — must register it through `AddTenantAwareStore<IWorkflowSignalInbox, TInbox>()` (or emit
`ITenantScopingCapability<IWorkflowSignalInbox>` some other way) when multi-tenancy is enabled. A plainly
registered inbox attests nothing and the host is refused at start, rather than silently deduplicating one
tenant's signal against another tenant's key.

### Schema

The store never creates its table at runtime. Run the shipped, idempotent DDL script against the target
database before the first signal is admitted:

```
Scripts/001_CreateWorkflowSignalInboxSchema.sql
```

It creates the table below (shown here for reference; the script is the source of truth and is safe to
re-run):

```sql
CREATE TABLE [dbo].[workflow_signal_inbox] (
    Sequence    BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    TenantId    NVARCHAR(64)  COLLATE Latin1_General_BIN2 NOT NULL,
    InstanceId  NVARCHAR(200)        NOT NULL,
    SignalId    NVARCHAR(200)        NOT NULL,
    SignalName  NVARCHAR(200)        NOT NULL,
    PayloadJson NVARCHAR(MAX)        NULL,
    CONSTRAINT UQ_workflow_signal_inbox UNIQUE (TenantId, InstanceId, SignalId)
);
```

The `UNIQUE (TenantId, InstanceId, SignalId)` constraint is required for correctness, not just hygiene: it
is what makes a producer's redelivery of an already-admitted signal a no-op. Omit it and every redelivery is
admitted a second time, silently breaking exactly-once signal delivery.

`TenantId` is part of that constraint rather than a filter applied over it, and the distinction matters in
both directions. A redelivery *within* one tenant still collides and is still refused. But two tenants
presenting the same `(InstanceId, SignalId)` are two different signals, and under a constraint that omitted
the tenant the second was refused admission and silently discarded — its workflow then waited for a signal
the system had received and thrown away, with no error and no row left behind. The host verifies this exact
three-column constraint at startup and refuses to start without it; if your table predates the tenant
column, apply `Scripts/002_MakeWorkflowSignalInboxTenantTotal.sql` before deploying. `Sequence` is an `IDENTITY` arrival
column: drain order is the monotonic append sequence, never a wall-clock timestamp, so consumption is
deterministic and reproducible.
