-- SQL Server MIGRATION for Excalibur.EventSourcing.SqlServer — SINGLE-TENANT -> MULTI-TENANT
-- Version: 3.0
--
-- Run this ONCE to grow an event store created before tenancy existed into the current
-- schema. A database created by 001_CreateEventStoreSchema.sql and 002_CreateSnapshotSchema.sql
-- at their present versions already has everything below and does not need this script; it is
-- a no-op there, by design, so it is safe to run unconditionally as part of a deployment.
--
-- Without it, an existing deployment fails on the first append with
-- Invalid column name 'TenantId'.
--
-- Table and schema names use the defaults (dbo.EventStoreEvents, dbo.EventStoreSnapshots);
-- rename the objects below if you overrode either.
--
--
-- THE TWO TABLES ARE MIGRATED DIFFERENTLY, AND THE DIFFERENCE IS DELIBERATE
-- -------------------------------------------------------------------------
-- EventStoreEvents.TenantId is added NULLABLE by THIS script, and is made total by the
-- NEXT one. Every read is written as COALESCE(TenantId, '__untenanted__') = @TenantId,
-- which exists precisely so that rows appended before tenancy remain reachable while
-- holding NULL. That fold is what lets this script add the column without a backfill, so
-- the store is usable the moment this script finishes.
--
-- It is not the end state. 004_MakeEventTenantTotal.sql then backfills the sentinel and
-- applies NOT NULL, so an upgraded database ends up in the SAME shape as one created fresh
-- by 001 rather than carrying a second, permanent representation of "untenanted". Run 004
-- after this script. Splitting the two is deliberate: this script must stay safe to run
-- unconditionally as part of a deployment, and the backfill in 004 is sized by table and
-- wants its own maintenance window.
--
-- EventStoreSnapshots.TenantId is NOT NULL and is part of the primary key. A key column
-- cannot be added to a populated table without a value, so that table does need a
-- temporary default and a backfill, after which the default is dropped — a default on a
-- key column would let a save that omitted the tenant land silently in the untenanted
-- partition instead of failing.
--
-- Snapshots are a rebuildable cache, not the system of record. If any step against the
-- snapshot table is inconvenient, deleting its rows and letting them regenerate is a
-- legitimate alternative; the same is never true of the events table.
--
--
-- COLLATION IS THE POINT OF THIS SCRIPT, NOT AN ORNAMENT
-- ------------------------------------------------------
-- TenantId is pinned to Latin1_General_BIN2. SQL Server's server default is typically
-- case-INSENSITIVE, under which 'Acme' = 'acme'. On the events table that means one tenant
-- reads another's rows — the comparison fails OPEN. On the snapshot table TenantId is part
-- of the MERGE key, so it is worse than a leak: one tenant's save MATCHES and overwrites
-- another tenant's snapshot.
--
-- Adding the column WITHOUT the explicit COLLATE clause therefore produces a database that
-- looks migrated, passes a column-existence check, and silently merges tenants that differ
-- only in case. Step 5 exists for installs that already did exactly that.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run during a maintenance window with the store stopped: this rebuilds a unique
-- constraint and a primary key. Take a backup you have restored at least once — this
-- touches the system of record.
--
-- TESTING THIS SCRIPT: 001_CreateEventStoreSchema.sql now creates the TARGET shape directly
-- (TenantId already NOT NULL, already in UQ_EventStoreEvents_Stream) -- this script is a no-op
-- against a fresh install and exists only for a database created by an OLDER 001. To exercise it
-- you must deliberately revert a fresh 001+002 schema first: drop TenantId, its default, and any
-- index/constraint referencing it, and rebuild the narrower pre-tenancy constraints. Building a
-- synthetic table from memory instead of the real 001 is the trap here -- it is easy to omit a
-- real column (e.g. EventStoreEvents.Position, the IDENTITY row-ordering key step 6 indexes) that
-- this script's later steps depend on, and the resulting failure looks like a script defect.

-- Explicit transaction wrapper -- see 006_ConvergeUntenantedToDefaultTenant.sql's header for why:
-- without it, a guard's THROW partway through this script does not roll back steps that already
-- ran (measured live against this package's Postgres twin; the same defect, same mechanism).
--
-- The transaction is one half. The other is the one-line guard at the top of every batch below,
-- and neither replaces the other:
--
--   XACT_ABORT ON rolls the transaction back the moment a guard's THROW fires. But GO is a CLIENT
--   batch separator, not a statement -- whatever applies this script sends each batch to the
--   server as a SEPARATE unit, and once the transaction is gone the batches AFTER the refusal
--   would run unprotected in autocommit and do the work anyway. The rollback makes what already
--   ran reversible; something else has to stop what has not run yet.
--
--   IF @@TRANCOUNT = 0 SET NOEXEC ON; is that something. A rolled-back transaction leaves
--   @@TRANCOUNT at zero, so the first batch after a refusal turns execution off for the rest of
--   the session and every later batch is compiled but never run. SET NOEXEC OFF at the very end
--   restores the session on the path where nothing refused.
--
--
-- BOTH OF THOSE ASSUME THE WHOLE SCRIPT IS APPLIED ON ONE CONNECTION. A transaction belongs to a
-- session, so a client that reconnects between batches loses it at the first GO -- and the guard
-- would then quietly switch the rest of the script off, which is the "completed having done
-- nothing" outcome this migration exists to refuse. The batch immediately below the transaction
-- therefore checks for that case explicitly and REFUSES, naming it. It is the one batch that can:
-- nothing has run yet that could have refused, so @@TRANCOUNT = 0 there means the session was lost
-- and nothing else. sqlcmd keeps one connection by default; a migration runner may need telling.
-- That guard is deliberately plain T-SQL rather than sqlcmd's :on error exit. The directive does
-- the same job, but it is a CLIENT command rather than a statement, so any tool that is not
-- sqlcmd sends it to the server and the whole script dies on its first line with
-- "Incorrect syntax near ':'" -- having done nothing at all. This script is meant to be applied
-- by whatever your deployment already uses, not only by sqlcmd.
--
-- ONE THING THE GUARD CANNOT DO IS SET THE PROCESS EXIT CODE. On a refusal sqlcmd still exits 0
-- unless you pass -b. If a pipeline branches on this script's exit code, run it as
--     sqlcmd -b -i <this script>
-- or the pipeline will read a refused, no-op migration as a success.

SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

-- The transaction opened in the batch above must still be here. If it is not, the client did not
-- keep one session across this script's batches -- see the header. This is the only batch that can
-- tell that apart from a deliberate refusal, because nothing has run yet that could have refused.
IF @@TRANCOUNT = 0
BEGIN
    THROW 51006, N'This migration opens a transaction in its first batch and commits it in its last, so the whole script must be applied on a SINGLE connection. @@TRANCOUNT is 0 here, which means the client reconnected after the opening batch and the transaction is already gone: every remaining batch would run unprotected, and a refusal partway through would leave the database half-migrated with no way back. Refusing rather than converting anything. Apply the whole script on one connection (sqlcmd does this by default), then re-run.', 1;
END

-- ---------------------------------------------------------------------------------------
-- 1) EventStoreEvents: add the nullable tenant column. No backfill here -- 004 does it (see header).
-- ---------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]') AND name = N'TenantId')
BEGIN
    ALTER TABLE [dbo].[EventStoreEvents]
        ADD [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NULL;
END
--
-- IF THE TABLE IS NOT PRESENT UNDER THE NAME THIS SCRIPT ADDRESSES, THIS SCRIPT FAILS. LOUDLY.
-- ------------------------------------------------------------------------------------------
-- This script addresses its tables as [dbo].[EventStoreEvents] and [dbo].[EventStoreSnapshots] -- a hardcoded name. The store that owns
-- them accepts a table-name override (SqlServerEventStore takes a table parameter), so a deployment that used
-- it holds its data under a different name, and every guard below would evaluate false.
--
-- The pre-flight at the top of the script stops that. Without it the script completes SILENTLY
-- having done nothing and reports SUCCESS, and the failure surfaces later, somewhere else, as a
-- missing column on the first write after the upgrade. The operator gets no signal in between,
-- and debugs it from the wrong end.
--
-- FATAL rather than a warning, deliberately. A halted deploy is recoverable in minutes; a green
-- run into a store binding a column that does not exist is a production incident. And the check
-- can only fire for a deployment this script was ALREADY doing nothing for -- so nobody who
-- currently succeeds starts failing. If you overrode the table name, edit the literals below to
-- match your deployment before running it.
--
-- A RENAMED TABLE IS NOT THE ONLY WAY TO REACH THE REFUSAL, AND THE TWO NEED OPPOSITE FIXES.
-- A table that was never created is absent for the same reason a renamed one is, and the check
-- cannot tell them apart -- it sees an absent name either way. EventStoreSnapshots is the one this
-- happens to: 001 creates the events table and 002 creates the snapshot table, so a deployment that
-- ran 001 alone, or built its event store from documentation rather than from these scripts, has an
-- events table and no snapshot table. Telling that operator to edit the literals sends them looking
-- for a table that is not there under any name; what they need is to run 002 first. Both refusal
-- messages below therefore name both causes and say how to tell which one you are.

GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- PRE-FLIGHT: THE TABLE MUST EXIST UNDER THE NAME THIS SCRIPT ADDRESSES.
--
-- See the header. This is the check that turns a silent no-op on a renamed deployment into a
-- stopped deploy. It is deliberately the FIRST thing that runs, before any guarded block, so
-- that a wrong-named deployment cannot get partway.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[EventStoreEvents]', N'U') IS NULL
BEGIN
    THROW 51003, N'EventStoreEvents is not present under that name. Two different deployments reach this line and they need different fixes, so establish which one you are before editing anything -- SELECT name FROM sys.tables ORDER BY name; will show what this database actually holds. (1) The event store schema was never created here: 001_CreateEventStoreSchema.sql has not been run against this database. Run it, then run 002_CreateSnapshotSchema.sql, then re-run this script -- editing the literals in this script would not help you, because there is nothing to rename to. (2) The table exists under another name: SqlServerEventStore accepts schema and table overrides (schema, table), so a deployment that used them holds its data elsewhere. Edit the table literals in this script to match your deployment, then re-run. Refusing rather than completing silently having done nothing: without this the first write after the upgrade fails on a missing column, with no signal in between.', 1;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF OBJECT_ID(N'[dbo].[EventStoreSnapshots]', N'U') IS NULL
BEGIN
    THROW 51003, N'EventStoreSnapshots is not present under that name. Two different deployments reach this line and they need different fixes, so establish which one you are before editing anything -- SELECT name FROM sys.tables ORDER BY name; will show what this database actually holds. (1) The snapshot table was never created here: 002_CreateSnapshotSchema.sql has not been run against this database. That is the ordinary case for a deployment whose event store was created by 001 alone, or built from documentation rather than from the shipped scripts. Run 002_CreateSnapshotSchema.sql, then re-run this script -- editing the literals in this script would not help you, because there is nothing to rename to. (2) The table exists under another name: SqlServerSnapshotStore accepts schema and table overrides (schema, table), so a deployment that used them holds its snapshots elsewhere. Edit the snapshot table literals in this script to match your deployment, then re-run. Refusing rather than completing silently having done nothing: without this the first snapshot write after the upgrade fails on a missing column, with no signal in between.', 1;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 2) EventStoreEvents: rebuild stream identity to include the tenant.
--
--    The tenant participates in stream IDENTITY, not merely in read filters. While the
--    unique constraint remains the pre-tenancy triple, one tenant's append collides with
--    another tenant's stream at the same version and optimistic concurrency stays global
--    instead of per-tenant. This is the step that makes concurrency correct.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.key_constraints
           WHERE parent_object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]')
             AND name = N'UQ_EventStoreEvents_Stream' AND type = N'UQ')
   AND NOT EXISTS (SELECT * FROM sys.index_columns ic
                   JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                   JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                   WHERE i.name = N'UQ_EventStoreEvents_Stream'
                     AND i.object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]')
                     AND c.name = N'TenantId')
BEGIN
    ALTER TABLE [dbo].[EventStoreEvents] DROP CONSTRAINT [UQ_EventStoreEvents_Stream];
    ALTER TABLE [dbo].[EventStoreEvents]
        ADD CONSTRAINT [UQ_EventStoreEvents_Stream]
            UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 3) EventStoreEvents: rebuild the stream-load index to match the current schema.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.indexes
           WHERE name = N'IX_EventStoreEvents_Stream'
             AND object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]'))
   AND NOT EXISTS (SELECT * FROM sys.index_columns ic
                   JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                   JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                   WHERE i.name = N'IX_EventStoreEvents_Stream'
                     AND i.object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]')
                     AND c.name = N'TenantId')
BEGIN
    DROP INDEX [IX_EventStoreEvents_Stream] ON [dbo].[EventStoreEvents];
    CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Stream]
        ON [dbo].[EventStoreEvents] ([AggregateId], [AggregateType], [TenantId], [Version]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 4) EventStoreSnapshots: add the tenant column, anchor existing rows, rebuild the key.
--
--    The default is temporary. It exists only so the NOT NULL column can be added to a
--    populated table; it is dropped in the same step once the rows carry a value, so a
--    later save that omits the tenant fails outright rather than landing in the
--    untenanted partition.
-- ---------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[EventStoreSnapshots]') AND name = N'TenantId')
BEGIN
    ALTER TABLE [dbo].[EventStoreSnapshots]
        ADD [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT [DF_EventStoreSnapshots_TenantId] DEFAULT N'__untenanted__';

    ALTER TABLE [dbo].[EventStoreSnapshots] DROP CONSTRAINT [DF_EventStoreSnapshots_TenantId];

    IF EXISTS (SELECT * FROM sys.key_constraints
               WHERE parent_object_id = OBJECT_ID(N'[dbo].[EventStoreSnapshots]')
                 AND name = N'PK_EventStoreSnapshots' AND type = N'PK')
    BEGIN
        ALTER TABLE [dbo].[EventStoreSnapshots] DROP CONSTRAINT [PK_EventStoreSnapshots];
    END

    ALTER TABLE [dbo].[EventStoreSnapshots]
        ADD CONSTRAINT [PK_EventStoreSnapshots]
            PRIMARY KEY CLUSTERED ([AggregateId], [AggregateType], [TenantId]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 5) RE-COLLATE an already-migrated database.
--
--    Steps 1 and 4 are guarded on the column being ABSENT, so they do nothing for a
--    consumer who added TenantId by hand, or who ran an earlier revision of this script
--    without the COLLATE clause. Those installs already hold the column in the server's
--    default collation — typically case-insensitive — and they are precisely the installs
--    that have already adopted multi-tenancy, so they are the only ones holding more than
--    one tenant's rows and therefore the only ones that can leak.
--
--    This block is guarded on the COLLATION rather than on the column's existence, so it
--    reaches exactly that population and is a no-op everywhere else, including a fresh
--    install where 001/002 already pinned the column.
--
--    SQL Server will not alter a column that participates in a key, so each key is dropped
--    and rebuilt around its ALTER. Run with the store stopped.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns c
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]')
             AND c.name = N'TenantId'
             AND c.collation_name <> N'Latin1_General_BIN2')
BEGIN
    IF EXISTS (SELECT * FROM sys.indexes
               WHERE name = N'IX_EventStoreEvents_Stream'
                 AND object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]'))
    BEGIN
        DROP INDEX [IX_EventStoreEvents_Stream] ON [dbo].[EventStoreEvents];
    END

    IF EXISTS (SELECT * FROM sys.key_constraints
               WHERE parent_object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]')
                 AND name = N'UQ_EventStoreEvents_Stream' AND type = N'UQ')
    BEGIN
        ALTER TABLE [dbo].[EventStoreEvents] DROP CONSTRAINT [UQ_EventStoreEvents_Stream];
    END

    -- Stays NULLABLE in THIS script: pre-tenancy rows still hold NULL at this point and the
    -- read path folds them to the sentinel, so the store works as soon as this finishes.
    -- Applying NOT NULL here would fail outright, because those rows have not been backfilled
    -- yet. 004_MakeEventTenantTotal.sql does the backfill and then applies the constraint.
    ALTER TABLE [dbo].[EventStoreEvents]
        ALTER COLUMN [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NULL;

    ALTER TABLE [dbo].[EventStoreEvents]
        ADD CONSTRAINT [UQ_EventStoreEvents_Stream]
            UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId]);

    CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Stream]
        ON [dbo].[EventStoreEvents] ([AggregateId], [AggregateType], [TenantId], [Version]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF EXISTS (SELECT * FROM sys.columns c
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EventStoreSnapshots]')
             AND c.name = N'TenantId'
             AND c.collation_name <> N'Latin1_General_BIN2')
BEGIN
    IF EXISTS (SELECT * FROM sys.key_constraints
               WHERE parent_object_id = OBJECT_ID(N'[dbo].[EventStoreSnapshots]')
                 AND name = N'PK_EventStoreSnapshots' AND type = N'PK')
    BEGIN
        ALTER TABLE [dbo].[EventStoreSnapshots] DROP CONSTRAINT [PK_EventStoreSnapshots];
    END

    ALTER TABLE [dbo].[EventStoreSnapshots]
        ALTER COLUMN [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

    ALTER TABLE [dbo].[EventStoreSnapshots]
        ADD CONSTRAINT [PK_EventStoreSnapshots]
            PRIMARY KEY CLUSTERED ([AggregateId], [AggregateType], [TenantId]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 6) CONVERGE on the current schema's supporting indexes.
--
--    A database old enough to predate tenancy may also predate an index the current schema
--    expects, and the steps above only rebuild the indexes they had to touch. Without this,
--    a migrated database differs from a freshly created one — silently, and only in query
--    plans, which is the kind of difference that surfaces as a performance incident months
--    later rather than as an error.
--
--    Each is guarded on existence, so this is a no-op on any database that already has them.
-- ---------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = N'IX_EventStoreEvents_Position'
                 AND object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Position]
        ON [dbo].[EventStoreEvents] ([Position]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = N'IX_EventStoreEvents_Stream'
                 AND object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Stream]
        ON [dbo].[EventStoreEvents] ([AggregateId], [AggregateType], [TenantId], [Version]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = N'IX_EventStoreSnapshots_CreatedAt'
                 AND object_id = OBJECT_ID(N'[dbo].[EventStoreSnapshots]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_EventStoreSnapshots_CreatedAt]
        ON [dbo].[EventStoreSnapshots] ([CreatedAt]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

COMMIT TRANSACTION;
GO

SET NOEXEC OFF;
GO
