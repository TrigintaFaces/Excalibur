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
-- EventStoreEvents.TenantId is NULLABLE. Every read is written as
-- COALESCE(TenantId, '__untenanted__') = @TenantId, which exists precisely so that rows
-- appended before tenancy remain reachable while holding NULL. So the events table needs
-- NO BACKFILL: adding the column is sufficient, and a backfill would only rewrite every
-- row of the system of record to reach the same result the read path already produces.
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

-- ---------------------------------------------------------------------------------------
-- 1) EventStoreEvents: add the nullable tenant column. No backfill (see header).
-- ---------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]') AND name = N'TenantId')
BEGIN
    ALTER TABLE [dbo].[EventStoreEvents]
        ADD [TenantId] NVARCHAR(255) COLLATE Latin1_General_BIN2 NULL;
END
GO

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
        ADD [TenantId] NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
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

    -- Stays NULLABLE: pre-tenancy rows hold NULL and the read path folds them to the
    -- sentinel. Making this NOT NULL here would break that path.
    ALTER TABLE [dbo].[EventStoreEvents]
        ALTER COLUMN [TenantId] NVARCHAR(255) COLLATE Latin1_General_BIN2 NULL;

    ALTER TABLE [dbo].[EventStoreEvents]
        ADD CONSTRAINT [UQ_EventStoreEvents_Stream]
            UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId]);

    CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Stream]
        ON [dbo].[EventStoreEvents] ([AggregateId], [AggregateType], [TenantId], [Version]);
END
GO

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
        ALTER COLUMN [TenantId] NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL;

    ALTER TABLE [dbo].[EventStoreSnapshots]
        ADD CONSTRAINT [PK_EventStoreSnapshots]
            PRIMARY KEY CLUSTERED ([AggregateId], [AggregateType], [TenantId]);
END
GO

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

IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = N'IX_EventStoreEvents_Stream'
                 AND object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Stream]
        ON [dbo].[EventStoreEvents] ([AggregateId], [AggregateType], [TenantId], [Version]);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = N'IX_EventStoreSnapshots_CreatedAt'
                 AND object_id = OBJECT_ID(N'[dbo].[EventStoreSnapshots]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_EventStoreSnapshots_CreatedAt]
        ON [dbo].[EventStoreSnapshots] ([CreatedAt]);
END
GO
