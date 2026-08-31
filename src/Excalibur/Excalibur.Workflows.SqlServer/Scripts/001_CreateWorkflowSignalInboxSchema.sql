-- SQL Server Schema for Excalibur.Workflows.SqlServer
-- Version: 1.0
--
-- Creates the table required by the durable workflow signal inbox. The store never
-- creates this table at runtime: run this script against the target database before
-- the first signal is admitted.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     SqlServerWorkflowSignalInboxOptions.SchemaName = "dbo"
--     SqlServerWorkflowSignalInboxOptions.TableName  = "workflow_signal_inbox"
--
-- If you override either of those, rename the corresponding object below to match.
--
-- The UNIQUE (TenantId, InstanceId, SignalId) constraint is load-bearing, not decorative: it is
-- what makes a producer's redelivery of an already-admitted signal a no-op. The store's
-- conditional insert relies on the server rejecting the duplicate — omit the constraint
-- and every redelivery is admitted a second time, silently breaking exactly-once signal
-- delivery. `Sequence` is an IDENTITY arrival column so drain order is the monotonic
-- append sequence, never a wall-clock timestamp (deterministic, reproducible drain).
--
-- The script is idempotent: the CREATE is guarded, so it is safe to re-run.

-- ---------------------------------------------------------------------------
-- Workflow signal inbox
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[workflow_signal_inbox]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[workflow_signal_inbox] (
        Sequence    BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,

        -- The owning tenant, and a COMPONENT OF SIGNAL IDENTITY rather than a filter over it. NOT NULL with
        -- no DEFAULT: you do not default a key column, because with one a write that omitted the tenant
        -- would land silently in the untenanted partition, making "I forgot to supply the tenant" and "this
        -- signal is deliberately untenanted" the same row. The store always binds the term explicitly
        -- through KeyedTenantPartition, which has no empty inhabitant, so no fallback is needed.
        --
        -- Latin1_General_BIN2 because the database default is typically case-INSENSITIVE, under which
        -- 'Acme' and 'acme' would be one tenant: a scoped drain would return another tenant's signals and
        -- the admission check would treat their signals as duplicates of each other.
        TenantId    NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,

        InstanceId  NVARCHAR(200)        NOT NULL,
        SignalId    NVARCHAR(200)        NOT NULL,
        SignalName  NVARCHAR(200)        NOT NULL,
        PayloadJson NVARCHAR(MAX)        NULL,
        CONSTRAINT UQ_workflow_signal_inbox UNIQUE (TenantId, InstanceId, SignalId)
    );
END
GO
