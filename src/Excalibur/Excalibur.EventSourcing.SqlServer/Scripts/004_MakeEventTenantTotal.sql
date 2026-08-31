-- SQL Server MIGRATION for Excalibur.EventSourcing.SqlServer — EVENT STORE TENANT TOTALITY
-- Version: 1.0
--
-- Converges EventStoreEvents.TenantId onto a TOTAL representation: NOT NULL, defaulting to the
-- reserved '__untenanted__' sentinel. After this script there is exactly ONE way to say "this
-- event has no tenant", and it is a value rather than the absence of one.
--
-- BEFORE: TenantId is nullable. An event appended before tenancy existed holds NULL, an event
--         appended since holds '__untenanted__', and the read path folds the two together with
--         COALESCE(TenantId, '__untenanted__'). Two spellings, one meaning.
-- AFTER:  TenantId is NOT NULL and every untenanted row holds '__untenanted__'. The COALESCE
--         still runs and is now a no-op, so no query changes and nothing breaks.
--
-- RUN ORDER
-- ---------
--   Fresh install .......... 001 and 002 already produce this shape. This script is a no-op.
--   Pre-tenancy install .... 003 first (it ADDS the column and rebuilds stream identity),
--                            then this script.
--
-- It is guarded and re-runnable: every step tests for the state it is about to create, so
-- running it twice, or running it against a database that is already converged, changes nothing.
--
--
-- WHY THIS IS WORTH A MIGRATION AND NOT JUST A FRESH-SCHEMA CHANGE
-- ----------------------------------------------------------------
-- If only the fresh-install schema (001) were changed, a database created before tenancy and one
-- created today would hold the SAME data in two different shapes forever, and every consumer of
-- both would have to keep handling both. That divergence is the thing being removed here.
--
-- There is also a correctness gain that is easy to miss. TenantId participates in
-- UQ_EventStoreEvents_Stream, which is what makes optimistic concurrency per-tenant rather than
-- global. SQL Server compares NULLs in a UNIQUE constraint as equal to each other, so the
-- constraint does hold for untenanted rows today -- but it holds by a DIFFERENT rule than the one
-- that governs tenanted rows. Making the column total puts both under one rule.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run during a maintenance window with the store stopped. Step 3 rebuilds a unique constraint and
-- an index on the system of record. Take a backup you have restored at least once.
--
-- The backfill in step 2 writes every row whose TenantId IS NULL. On a large event store that is
-- the dominant cost of this script; it is a single set-based UPDATE and is not resumable, so size
-- the maintenance window against the count reported by step 0.

--
-- IF THE TABLE IS NOT PRESENT UNDER THE NAME THIS SCRIPT ADDRESSES, THIS SCRIPT FAILS. LOUDLY.
-- ------------------------------------------------------------------------------------------
-- This script addresses its table as [dbo].[EventStoreEvents] -- a hardcoded name. The store that owns
-- it accepts a table-name override (SqlServerEventStore takes a table parameter), so a deployment that used
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
IF OBJECT_ID(N'[dbo].[EventStoreEvents]', N'U') IS NULL
BEGIN
    THROW 51004, N'EventStoreEvents is not present under that name. Two different deployments reach this line and they need different fixes, so establish which one you are before editing anything -- SELECT name FROM sys.tables ORDER BY name; will show what this database actually holds. (1) The event store schema was never created here: 001_CreateEventStoreSchema.sql has not been run against this database. Run it, then 003_MigrateToMultiTenant.sql, then re-run this script -- editing the literals in this script would not help you, because there is nothing to rename to. (2) The table exists under another name: SqlServerEventStore accepts schema and table overrides (schema, table), so a deployment that used them holds its data elsewhere. Edit the table literals in this script to match your deployment, then re-run. Refusing rather than completing silently having done nothing: without this the first write after the upgrade fails on a missing column, with no signal in between.', 1;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 0) PRE-FLIGHT. RUN THIS FIRST AND READ THE RESULT. DO NOT SKIP.
--
--    The rewrite is safe in the general case: SQL Server already treats all NULL-tenant rows
--    for a given (AggregateId, AggregateType, Version) as ONE uniqueness class, so collapsing
--    them onto the sentinel preserves the identical classes and cannot manufacture a new
--    violation. No real tenant can occupy the sentinel -- a scoped tenant that names it is
--    rejected before it reaches the database.
--
--    There is exactly ONE case where that does not hold: a stream that already holds BOTH a
--    literal sentinel row AND a NULL row at the SAME version. Those are two distinct entries
--    today and collide after the rewrite, so step 3 would fail partway.
--
--    That failure is CORRECT. It means the data already contains two rows each claiming to be
--    version N of the untenanted stream, which is a pre-existing duplicate this script has no
--    authority to resolve -- deciding which append survives is a data question, not a schema one.
--    Resolve those rows first, then re-run.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]') AND name = N'TenantId')
BEGIN
    DECLARE @NullRows BIGINT, @Collisions BIGINT;

    SELECT @NullRows = COUNT_BIG(*)
    FROM [dbo].[EventStoreEvents]
    WHERE [TenantId] IS NULL;

    SELECT @Collisions = COUNT_BIG(*)
    FROM (
        SELECT [AggregateId], [AggregateType], [Version]
        FROM [dbo].[EventStoreEvents]
        WHERE [TenantId] IS NULL OR [TenantId] = N'__untenanted__'
        GROUP BY [AggregateId], [AggregateType], [Version]
        HAVING COUNT(*) > 1
    ) AS c;

    PRINT N'004 pre-flight: ' + CONVERT(NVARCHAR(20), @NullRows) + N' row(s) will be backfilled to the sentinel.';

    IF @Collisions > 0
    BEGIN
        DECLARE @Msg NVARCHAR(400) = N'004 ABORT: ' + CONVERT(NVARCHAR(20), @Collisions)
            + N' untenanted stream version(s) hold BOTH a NULL and a literal sentinel row. '
            + N'Collapsing them would violate UQ_EventStoreEvents_Stream. Resolve the duplicate '
            + N'appends first, then re-run this script.';
        THROW 50004, @Msg, 1;
    END
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 1) Guard: everything below applies only while the column is still nullable.
-- 2) Backfill NULL -> sentinel, BEFORE the constraint is applied. This order is not optional:
--    ALTER COLUMN ... NOT NULL fails outright if any row still holds NULL.
-- 3) Rebuild stream identity around the now-total column.
--
--    The constraint and index are DROPPED and RECREATED rather than altered in place. SQL Server
--    will not alter a column that an index or key constraint depends on, and 003 established this
--    same drop/alter/recreate shape for the same two objects.
--
--    The COLLATE clause is restated on the ALTER. Omitting it does not preserve the column's
--    collation -- it resets the column to the DATABASE default, which is typically
--    case-INSENSITIVE. That would silently make 'Acme' and 'acme' the same tenant, so a scoped
--    read would return another tenant's events. The tenant predicate would fail OPEN, which is
--    the exact failure this store's binary collation exists to prevent.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]')
             AND name = N'TenantId' AND is_nullable = 1)
BEGIN
    UPDATE [dbo].[EventStoreEvents]
        SET [TenantId] = N'__untenanted__'
        WHERE [TenantId] IS NULL;

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

    ALTER TABLE [dbo].[EventStoreEvents]
        ALTER COLUMN [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

    ALTER TABLE [dbo].[EventStoreEvents]
        ADD CONSTRAINT [UQ_EventStoreEvents_Stream]
            UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId]);

    CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Stream]
        ON [dbo].[EventStoreEvents] ([AggregateId], [AggregateType], [TenantId], [Version]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 4) The default. Guarded separately from the block above on purpose: a database that reached
--    NOT NULL by some other route still needs the default, and once that block runs the column is
--    no longer nullable, so a single combined guard would skip this.
--
--    The default is what makes the column total for a writer that omits it entirely. The store
--    always binds the term explicitly (via KeyedTenantPartition, which has no empty inhabitant),
--    so this is a backstop for hand-written INSERTs rather than something the store relies on.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]') AND name = N'TenantId')
   AND NOT EXISTS (SELECT * FROM sys.default_constraints
                   WHERE parent_object_id = OBJECT_ID(N'[dbo].[EventStoreEvents]')
                     AND name = N'DF_EventStoreEvents_TenantId')
BEGIN
    ALTER TABLE [dbo].[EventStoreEvents]
        ADD CONSTRAINT [DF_EventStoreEvents_TenantId] DEFAULT N'__untenanted__' FOR [TenantId];
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

COMMIT TRANSACTION;
GO

SET NOEXEC OFF;
GO
