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
// register the durability capability marker.
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
    InstanceId  NVARCHAR(200)        NOT NULL,
    SignalId    NVARCHAR(200)        NOT NULL,
    SignalName  NVARCHAR(200)        NOT NULL,
    PayloadJson NVARCHAR(MAX)        NULL,
    CONSTRAINT UQ_workflow_signal_inbox UNIQUE (InstanceId, SignalId)
);
```

The `UNIQUE (InstanceId, SignalId)` constraint is required for correctness, not just hygiene: it is what
makes a producer's redelivery of an already-admitted signal a no-op. Omit it and every redelivery is
admitted a second time, silently breaking exactly-once signal delivery. `Sequence` is an `IDENTITY` arrival
column: drain order is the monotonic append sequence, never a wall-clock timestamp, so consumption is
deterministic and reproducible.
