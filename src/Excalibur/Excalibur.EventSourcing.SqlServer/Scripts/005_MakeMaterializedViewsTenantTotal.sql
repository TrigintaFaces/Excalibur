-- SQL Server MIGRATION for Excalibur.EventSourcing.SqlServer — MATERIALIZED-VIEW TENANT PARTITIONING
-- Version: 1.0
--
-- Adds the tenant term to MaterializedViews and MaterializedViewPositions and makes it part of each
-- table's identity, so two tenants projecting the same named view no longer share a row.
--
-- BEFORE: MaterializedViews is keyed on (ViewName, ViewId) and MaterializedViewPositions on (ViewName)
--         alone. Both are caller-supplied strings with no tenant discriminator, so two tenants
--         projecting the same named view write and read ONE row. The view upsert has no guard, so the
--         later writer's data silently wins and a read returns whichever tenant wrote last.
-- AFTER:  Both tables carry a NOT NULL TenantId that participates in uniqueness. Each tenant holds its
--         own view rows and its own checkpoint.
--
-- The checkpoint half is the worse of the two and is the reason this is not merely a disclosure fix.
-- One checkpoint per view name across all tenants means tenant A's progress advances tenant B's
-- checkpoint, so B's projector skips every event in between — silently, with no error raised. The
-- monotonic guard makes that permanent rather than transient: it exists to stop the checkpoint moving
-- backwards, so the skipped range can never be re-read.
--
--
-- WHAT THIS MIGRATION PRODUCES FOR EXISTING ROWS, AND WHY THAT DIRECTION IS THE SAFE ONE
-- --------------------------------------------------------------------------------------
-- Step 2 backfills every existing row to the reserved '__untenanted__' sentinel. Two consequences,
-- and the distinction between them is the whole point:
--
--   A host that was single-tenant resolves the SAME sentinel at run time (the store binds it through
--   KeyedTenantPartition, which has no empty inhabitant). Its rows are found exactly as before. No
--   replay, no skip, no change in behaviour.
--
--   A host that was already multi-tenant, writing unscoped rows, leaves those rows in a partition that
--   NO REAL TENANT CAN EVER RESOLVE TO — a scoped tenant is rejected outright if it names the sentinel.
--   So a scoped tenant reads its checkpoint, finds nothing, and REPLAYS FROM THE BEGINNING, which
--   re-derives its view from its own events.
--
-- Replay is the failure this migration chooses. The alternative — distributing the legacy rows among
-- real tenants by some guess — would hand a tenant a checkpoint written by another, and that tenant
-- would SKIP the events in between with no error and no way to detect it afterwards. Replay costs
-- time. A skip costs data. The sentinel is chosen precisely because it is unreachable from a scoped
-- read, which makes the skip outcome unconstructable rather than merely unlikely.
--
-- The two tables are backfilled to the SAME sentinel in the same script, so a view and the checkpoint
-- recording how far that view was built stay consistent. Backfilling one and not the other would leave
-- a tenant reading a stale view behind an advanced checkpoint.
--
--
-- WHY A BACKFILL INTO A KEY IS SAFE HERE, WHICH IS NOT SOMETHING TO ASSUME
-- ------------------------------------------------------------------------
-- A value being promoted into a key must not be load-bearing for anything else, or the backfill is
-- itself the data-loss event. Checked, for these two tables specifically:
--
--   * TenantId is a NEW column. Nothing reads it, so nothing can be invalidated by its value.
--   * Data is an opaque serialized read model. It is not encrypted, not signed, and carries no
--     authenticated associated data derived from the row key, so re-keying the row cannot render it
--     unreadable.
--   * ViewName and ViewId are pure identity. The only other consumer of a view name is a telemetry
--     tag, which is unaffected by a schema change.
--
--
-- WHY THERE IS NO COLLISION PRE-FLIGHT, WHICH IS DELIBERATE
-- ----------------------------------------------------------
-- The sibling event-store migration opens with a collision check because collapsing NULL onto a
-- sentinel can merge two rows that were previously distinct. That cannot happen here and a check for
-- it would be one that can never fire — worse than absent, because it would read as protection.
--
-- (ViewName, ViewId) is already unique, enforced by the existing primary key. Adding a column whose
-- value is the SAME CONSTANT for every row is an injective transformation: distinct rows stay
-- distinct. There is no pre-existing state in which the new constraint can be violated.
--
--
-- INDEX KEY WIDTH — STATED, BECAUSE IT IS WHAT DECIDES THE SHAPE BELOW
-- ---------------------------------------------------------------------
-- SQL Server caps a CLUSTERED index key at 900 bytes and a NONCLUSTERED one at 1700. At 2 bytes per
-- NVARCHAR character the tenant-qualified natural keys are:
--
--     MaterializedViews          TenantId NVARCHAR(255)  ->  510 bytes
--                                ViewName NVARCHAR(256)  ->  512 bytes
--                                ViewId   NVARCHAR(256)  ->  512 bytes
--                                                           ----------
--                                                            1534 bytes  (of 1700 nonclustered)
--
--     MaterializedViewPositions  TenantId NVARCHAR(255)  ->  510 bytes
--                                ViewName NVARCHAR(256)  ->  512 bytes
--                                                           ----------
--                                                            1022 bytes  (of 1700 nonclustered)
--
-- Both exceed 900, so neither natural key can remain the clustered key. Each table therefore takes a
-- surrogate identity as its clustered key and enforces its natural key with a UNIQUE constraint, which
-- carries the same guarantee under the 1700-byte cap. This is the shape the CDC state store uses, for
-- this same reason.
--
-- THE VIEW TABLE WAS ALREADY OVER THE LIMIT BEFORE TENANCY. (ViewName, ViewId) alone is 1024 bytes as
-- a clustered key. That failure is quiet in the worst way: CREATE TABLE succeeds with only a warning,
-- and the table then REFUSES oversized inserts at run time with Msg 1946. A deployment whose view
-- names and ids together exceeded 450 characters has been failing to save views since it was created.
-- This script therefore also repairs a pre-existing fault rather than only adding a constraint.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run during a maintenance window with projections stopped. Steps 3 and 4 add an IDENTITY column and
-- rebuild the clustered index on both tables, which are size-of-data operations holding a
-- schema-modification lock. Take a backup you have restored at least once.
--
-- Every statement is guarded against the state it is about to create, so the script is safe to re-run.

--
-- IF THE TABLE IS NOT PRESENT UNDER THE NAME THIS SCRIPT ADDRESSES, THIS SCRIPT FAILS. LOUDLY.
-- ------------------------------------------------------------------------------------------
-- This script addresses its tables as [dbo].[MaterializedViews] and [dbo].[MaterializedViewPositions] -- a hardcoded name. The store that owns
-- them accepts a table-name override (viewTableName / positionTableName on both constructors and on AddSqlServerMaterializedViewStore), so a deployment that used
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

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

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
-- PRE-FLIGHT: THE TABLE MUST EXIST UNDER THE NAME THIS SCRIPT ADDRESSES.
--
-- See the header. This is the check that turns a silent no-op on a renamed deployment into a
-- stopped deploy. It is deliberately the FIRST thing that runs, before any guarded block, so
-- that a wrong-named deployment cannot get partway.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[MaterializedViews]', N'U') IS NULL
BEGIN
    THROW 51005, N'MaterializedViews is not present under that name. Two different deployments reach this line and they need different fixes, so establish which one you are before editing anything -- SELECT name FROM sys.tables ORDER BY name; will show what this database actually holds. (1) The materialized-view tables were never created here: SqlServerMaterializedViewStore issues their CREATE TABLE when it initialises, so a database that has never run a host with that store registered does not have them. A deployment that does not use materialized views is in this case, and this script simply does not apply to it -- skip it rather than editing anything. (2) The tables exist under other names: the store accepts table-name overrides (viewTableName / positionTableName on both constructors and on AddSqlServerMaterializedViewStore), so a deployment that used them holds its data elsewhere. Edit the table literals in this script to match your deployment, then re-run. Refusing rather than completing silently having done nothing: without this the first write after the upgrade fails on a missing column, with no signal in between.', 1;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF OBJECT_ID(N'[dbo].[MaterializedViewPositions]', N'U') IS NULL
BEGIN
    THROW 51005, N'MaterializedViewPositions is not present under that name. Two different deployments reach this line and they need different fixes, so establish which one you are before editing anything -- SELECT name FROM sys.tables ORDER BY name; will show what this database actually holds. (1) The materialized-view tables were never created here: SqlServerMaterializedViewStore issues their CREATE TABLE when it initialises, so a database that has never run a host with that store registered does not have them. A deployment that does not use materialized views is in this case, and this script simply does not apply to it -- skip it rather than editing anything. (2) The tables exist under other names: the store accepts table-name overrides (viewTableName / positionTableName on both constructors and on AddSqlServerMaterializedViewStore), so a deployment that used them holds its data elsewhere. Edit the table literals in this script to match your deployment, then re-run. Refusing rather than completing silently having done nothing: without this the first write after the upgrade fails on a missing column, with no signal in between.', 1;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 0) PRE-FLIGHT. RUN THIS FIRST AND READ THE RESULT.
--
--    This reports what the migration is about to do rather than guarding against a collision,
--    because there is no collision to guard against (see the header). The count that matters is the
--    number of checkpoints that will move into the untenanted partition: on a multi-tenant host,
--    that is the number of projections that will replay.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[MaterializedViewPositions]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[MaterializedViewPositions]') AND name = N'TenantId')
BEGIN
    DECLARE @Views BIGINT, @Checkpoints BIGINT;

    SELECT @Views = COUNT_BIG(*) FROM [dbo].[MaterializedViews];
    SELECT @Checkpoints = COUNT_BIG(*) FROM [dbo].[MaterializedViewPositions];

    PRINT N'005 pre-flight: ' + CONVERT(NVARCHAR(20), @Views) + N' view row(s) and '
        + CONVERT(NVARCHAR(20), @Checkpoints) + N' checkpoint(s) will move to the untenanted partition.';
    PRINT N'005 pre-flight: on a single-tenant host this is a no-op at run time. On a multi-tenant host '
        + N'each scoped tenant will find no checkpoint and replay its projections from the beginning.';
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 1-4) MaterializedViews.
--
--    Order is not optional: the column is ADDED with the sentinel as its default so that every
--    existing row is populated in the same statement, and only then does the key change. Applying a
--    NOT NULL column to a populated table without a default fails outright.
--
--    The DEFAULT is dropped again at the end. It exists to make ADD COLUMN total over the existing
--    rows, not to be a standing behaviour: TenantId is a component of identity, and you do not
--    default a key column. Leaving it would make a write that omitted the tenant land silently in the
--    untenanted partition, so "I forgot to supply the tenant" and "this row is deliberately
--    untenanted" would become the same row. The store always binds the term explicitly through
--    KeyedTenantPartition, which has no empty inhabitant.
--
--    The COLLATE clause is not decoration. The database default is typically case-INSENSITIVE, under
--    which 'Acme' and 'acme' would be the same tenant and a scoped read would return another tenant's
--    view. The tenant predicate would fail OPEN, which is the exact failure this column exists to
--    prevent.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[MaterializedViews]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[MaterializedViews]') AND name = N'TenantId')
BEGIN
    ALTER TABLE [dbo].[MaterializedViews]
        ADD [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT [DF_MaterializedViews_TenantId_Backfill] DEFAULT N'__untenanted__';
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF OBJECT_ID(N'[dbo].[MaterializedViews]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[MaterializedViews]') AND name = N'Id')
BEGIN
    -- The surrogate clustered key. See the header for why the natural key cannot serve as one.
    IF EXISTS (SELECT * FROM sys.key_constraints
               WHERE parent_object_id = OBJECT_ID(N'[dbo].[MaterializedViews]')
                 AND name = N'PK_MaterializedViews' AND type = N'PK')
    BEGIN
        ALTER TABLE [dbo].[MaterializedViews] DROP CONSTRAINT [PK_MaterializedViews];
    END

    ALTER TABLE [dbo].[MaterializedViews] ADD [Id] BIGINT IDENTITY(1,1) NOT NULL;

    ALTER TABLE [dbo].[MaterializedViews]
        ADD CONSTRAINT [PK_MaterializedViews] PRIMARY KEY CLUSTERED ([Id]);

    ALTER TABLE [dbo].[MaterializedViews]
        ADD CONSTRAINT [UQ_MaterializedViews_Key] UNIQUE ([TenantId], [ViewName], [ViewId]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF EXISTS (SELECT * FROM sys.default_constraints
           WHERE parent_object_id = OBJECT_ID(N'[dbo].[MaterializedViews]')
             AND name = N'DF_MaterializedViews_TenantId_Backfill')
BEGIN
    ALTER TABLE [dbo].[MaterializedViews] DROP CONSTRAINT [DF_MaterializedViews_TenantId_Backfill];
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 5-8) MaterializedViewPositions. Same shape, same reasons.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[MaterializedViewPositions]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[MaterializedViewPositions]') AND name = N'TenantId')
BEGIN
    ALTER TABLE [dbo].[MaterializedViewPositions]
        ADD [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT [DF_MaterializedViewPositions_TenantId_Backfill] DEFAULT N'__untenanted__';
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF OBJECT_ID(N'[dbo].[MaterializedViewPositions]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[MaterializedViewPositions]') AND name = N'Id')
BEGIN
    IF EXISTS (SELECT * FROM sys.key_constraints
               WHERE parent_object_id = OBJECT_ID(N'[dbo].[MaterializedViewPositions]')
                 AND name = N'PK_MaterializedViewPositions' AND type = N'PK')
    BEGIN
        ALTER TABLE [dbo].[MaterializedViewPositions] DROP CONSTRAINT [PK_MaterializedViewPositions];
    END

    ALTER TABLE [dbo].[MaterializedViewPositions] ADD [Id] BIGINT IDENTITY(1,1) NOT NULL;

    ALTER TABLE [dbo].[MaterializedViewPositions]
        ADD CONSTRAINT [PK_MaterializedViewPositions] PRIMARY KEY CLUSTERED ([Id]);

    ALTER TABLE [dbo].[MaterializedViewPositions]
        ADD CONSTRAINT [UQ_MaterializedViewPositions_Key] UNIQUE ([TenantId], [ViewName]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF EXISTS (SELECT * FROM sys.default_constraints
           WHERE parent_object_id = OBJECT_ID(N'[dbo].[MaterializedViewPositions]')
             AND name = N'DF_MaterializedViewPositions_TenantId_Backfill')
BEGIN
    ALTER TABLE [dbo].[MaterializedViewPositions] DROP CONSTRAINT [DF_MaterializedViewPositions_TenantId_Backfill];
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

COMMIT TRANSACTION;
GO

SET NOEXEC OFF;
GO
