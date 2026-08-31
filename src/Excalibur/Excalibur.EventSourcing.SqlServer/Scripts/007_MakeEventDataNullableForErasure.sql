-- SQL Server MIGRATION for Excalibur.EventSourcing.SqlServer -- ERASURE TOMBSTONE COLUMN
-- Version: 1.0
--
-- Makes [EventData] nullable on an existing EventStoreEvents table so that GDPR Article 17
-- erasure can tombstone an event in place.
--
--
-- WHY THIS EXISTS
-- ----------------
-- Erasure does not delete an event's row. It TOMBSTONES the event: EventData is set to NULL,
-- EventType is overwritten with the reserved '$erased' marker, and Metadata is replaced with an
-- erasure stamp -- while the row, its Position and its Version stay exactly where they were.
-- Keeping the row is what preserves the stream: versions stay contiguous, so replay of a
-- partially-erased aggregate does not encounter a hole and every reader's sequence arithmetic
-- still holds. The payload -- the only column that carries the data subject's personal data --
-- is the part that goes.
--
-- That requires EventData to permit NULL. Earlier releases of 001_CreateEventStoreSchema.sql
-- declared it VARBINARY(MAX) NOT NULL, so on a database created from one of those scripts the
-- tombstone UPDATE is rejected by the engine:
--
--     Cannot insert the value NULL into column 'EventData', table '<db>.dbo.EventStoreEvents';
--     column does not allow nulls. UPDATE fails.
--
-- The erasure call therefore fails and NO payload is destroyed. This is not a degraded or
-- partial erase -- it is no erase at all, on the path a consumer uses to satisfy a data
-- subject's right to erasure. If your deployment was created from a script declaring EventData
-- NOT NULL, erasure has never worked against it, and running this migration is what makes the
-- capability available. 001 now ships the column nullable, so a FRESH install does not need
-- this script; it is here for databases that already exist.
--
-- Nullability is the shape the other stores already use -- Postgres (event_data BYTEA NULL),
-- Oracle (EVENTDATA BLOB) and MongoDB all tombstone by nulling the payload -- so this converges
-- SQL Server onto the behaviour the rest of the framework, its documentation and its samples
-- already describe, rather than introducing a SQL-Server-specific representation.
--
--
-- WHAT THIS SCRIPT DOES NOT DO
-- ------------------------------
-- It does not erase anything, and it does not alter a single row. Widening a column from
-- NOT NULL to NULL neither writes nor rewrites data -- it removes a constraint. Events already
-- in the table are untouched and keep their payloads; erasure remains something the application
-- requests per aggregate, never a side effect of migrating.
--
-- It does not touch any index. EventData participates in no index, no key and no constraint in
-- this schema (the primary key is Position; the unique constraint is
-- AggregateId + AggregateType + Version + TenantId), so the column can be altered directly
-- without dropping and rebuilding anything.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Run after 001. It is independent of 002-006 and can be applied in any order relative to them.
--
-- Unlike the tenancy backfills, this is a metadata-only change on a column that carries no
-- index, so it does not scan or rewrite the table and does not need a maintenance window sized
-- to your data. It does briefly take a schema-modification lock on EventStoreEvents, which
-- waits for in-flight readers and writers of that table and blocks new ones until it is granted;
-- apply it when the store is quiet, or expect a short stall on a busy system.
--
-- The script is guarded against the state it creates, so it is safe to re-run: a database whose
-- EventData column is already nullable is a no-op, and so is one that has no EventStoreEvents
-- table (a consumer who never registered the event store).
--
-- The guards are plain T-SQL rather than sqlcmd's :on error exit / :setvar deliberately. Those
-- directives are CLIENT commands rather than statements, so any tool that is not sqlcmd sends
-- them to the server and the whole script dies on its first line with "Incorrect syntax near
-- ':'" -- having done nothing at all.
--
-- ONE THING THE GUARDS CANNOT DO IS SET THE PROCESS EXIT CODE. On an error sqlcmd still exits 0
-- unless you pass -b. If a pipeline branches on this script's exit code, run it as
--     sqlcmd -b -i <this script>
-- or the pipeline will read a failed migration as a success.
--
-- Table and schema names use the defaults (dbo.EventStoreEvents); edit the literals below if you
-- overrode either (SqlServerEventStore accepts a schema and table override).

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- This migration is a SINGLE statement against a SINGLE table, so it needs none of the
-- cross-batch machinery 006 carries: there is no sequence of tables that could be left
-- half-converted, and DDL is transactional on this engine, so the ALTER either takes effect
-- completely or not at all. There is deliberately no GO in the body below -- the whole script is
-- one batch, which also means it cannot be split across a reconnecting client.

IF OBJECT_ID(N'[dbo].[EventStoreEvents]', N'U') IS NULL
BEGIN
    PRINT N'007: dbo.EventStoreEvents does not exist -- nothing to migrate. If this deployment '
        + N'uses the event store, run 001_CreateEventStoreSchema.sql first.';
END
ELSE IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[dbo].[EventStoreEvents]', N'U')
      AND [name] = N'EventData')
BEGIN
    -- The table exists but does not have the column this script is about to alter. Something
    -- other than this package's 001 created it, so REFUSE rather than guess at the shape.
    THROW 51007, N'007 REFUSED: dbo.EventStoreEvents exists but has no EventData column, so this table was not created by this package''s 001_CreateEventStoreSchema.sql. Nothing has been changed. Verify the schema and table names (SqlServerEventStore accepts overrides for both) and edit the literals in this script if you overrode them.', 1;
END
ELSE IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE [object_id] = OBJECT_ID(N'[dbo].[EventStoreEvents]', N'U')
      AND [name] = N'EventData'
      AND [is_nullable] = 1)
BEGIN
    -- Already nullable: either 001 shipped it that way or this script has already run.
    PRINT N'007: dbo.EventStoreEvents.EventData is already nullable -- no change required.';
END
ELSE
BEGIN
    -- VARBINARY(MAX) is restated because ALTER COLUMN replaces the whole column definition: the
    -- type must be repeated exactly, or the column would be silently redefined to whatever is
    -- named here. Only the nullability changes.
    ALTER TABLE [dbo].[EventStoreEvents]
        ALTER COLUMN [EventData] VARBINARY(MAX) NULL;

    PRINT N'007: dbo.EventStoreEvents.EventData is now nullable -- erasure can tombstone events.';
END
