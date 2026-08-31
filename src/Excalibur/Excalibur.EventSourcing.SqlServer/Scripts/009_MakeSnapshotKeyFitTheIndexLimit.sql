-- SQL Server MIGRATION for Excalibur.EventSourcing.SqlServer -- SNAPSHOT KEY INDEX WIDTH
-- Version: 1.0
--
-- Rebuilds EventStoreSnapshots' primary key as NONCLUSTERED and clusters the table on a
-- narrower prefix, so the key stops exceeding SQL Server's clustered index key limit.
--
--
-- WHY THIS EXISTS
-- ----------------
-- SQL Server caps a CLUSTERED index key at 900 bytes and a NONCLUSTERED one at 1700. At 2 bytes
-- per NVARCHAR character this table's natural key is:
--
--     AggregateId    NVARCHAR(255)  ->   510 bytes
--     AggregateType  NVARCHAR(255)  ->   510 bytes
--     TenantId       NVARCHAR(64)   ->   128 bytes
--                                       ----------
--                                        1148 bytes
--
-- Earlier releases of 002_CreateSnapshotSchema.sql declared that triple PRIMARY KEY CLUSTERED,
-- which is 248 bytes over the cap. CREATE TABLE accepts it with only warning Msg 1946, so the
-- deployment looks clean; the table then REFUSES an oversized row at run time:
--
--     Operation failed. The index entry of length <n> bytes for the index
--     'PK_EventStoreSnapshots' exceeds the maximum length of 900 bytes.
--
-- The failure is per row, not per table, so a deployment whose aggregate ids and types are short
-- never sees it and one that later introduces a long aggregate type starts failing every save for
-- that aggregate -- with the snapshot silently absent and every load falling back to a full event
-- replay. Nothing reports it except the failing save itself.
--
-- The remedy keeps the key EXACTLY as it was and only changes where it lives. Uniqueness is still
-- enforced over (AggregateId, AggregateType, TenantId), which is what the store's MERGE matches on,
-- so a cross-tenant overwrite remains impossible. The 1700-byte nonclustered cap accommodates 1148
-- with room, so no column is narrowed and no value is truncated.
--
--
-- WHAT THIS SCRIPT DOES NOT DO
-- ------------------------------
-- It does not change, move or delete a single snapshot. Rebuilding an index rewrites storage; it
-- does not alter row content, and no column type or nullability changes.
--
-- It does not narrow any column. Narrowing AggregateId or AggregateType would silently truncate
-- consumer values and change which snapshots collide -- the opposite of what a key fix should do.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Run after 002. It is independent of every other script here.
--
-- Unlike a metadata-only widening, this one REBUILDS the table: dropping a clustered index rewrites
-- the rows as a heap and creating one rewrites them again. Expect the work to scale with the number
-- of snapshots and a schema-modification lock to be held for the duration, so apply it in a
-- maintenance window. Snapshots are a cache over the event stream -- a load falls back to replay
-- when one is missing -- so the store stays correct while this runs, only slower.
--
-- The script is guarded against the state it creates, so it is safe to re-run: a database whose
-- primary key is already nonclustered is a no-op, and so is one with no EventStoreSnapshots table.
--
-- The guards are plain T-SQL rather than sqlcmd's :on error exit / :setvar deliberately. Those
-- directives are CLIENT commands rather than statements, so any tool that is not sqlcmd sends them
-- to the server and the whole script dies on its first line with "Incorrect syntax near ':'" --
-- having done nothing at all.
--
-- ONE THING THE GUARDS CANNOT DO IS SET THE PROCESS EXIT CODE. On an error sqlcmd still exits 0
-- unless you pass -b. If a pipeline branches on this script's exit code, run it as
--     sqlcmd -b -i <this script>
-- or the pipeline will read a failed migration as a success.
--
-- Table and schema names use the defaults (dbo.EventStoreSnapshots); edit the literals below if you
-- overrode either.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'[dbo].[EventStoreSnapshots]', N'U') IS NULL
BEGIN
    PRINT N'009: dbo.EventStoreSnapshots does not exist -- nothing to migrate. If this deployment '
        + N'uses snapshots, run 002_CreateSnapshotSchema.sql first.';
END
ELSE IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[EventStoreSnapshots]', N'U')
      AND [name] = N'PK_EventStoreSnapshots')
BEGIN
    -- The table exists but carries no key by that name, so it was not created by this package's
    -- 002. REFUSE rather than guess at the shape and drop something else.
    THROW 51009, N'009 REFUSED: dbo.EventStoreSnapshots exists but has no PK_EventStoreSnapshots constraint, so this table was not created by this package''s 002_CreateSnapshotSchema.sql. Nothing has been changed. Verify the schema and table names and edit the literals in this script if you overrode them.', 1;
END
ELSE IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[EventStoreSnapshots]', N'U')
      AND [name] = N'PK_EventStoreSnapshots'
      AND [type_desc] = N'NONCLUSTERED')
BEGIN
    -- Already nonclustered: either 002 shipped it that way or this script has already run.
    PRINT N'009: PK_EventStoreSnapshots is already NONCLUSTERED -- no change required.';
END
ELSE
BEGIN
    -- Dropping the constraint drops the clustered index with it and leaves a heap. The recreate
    -- below is what makes this a rebuild rather than a permanent heap, so the two must not be
    -- separated by a batch boundary that a reconnecting client could split.
    ALTER TABLE [dbo].[EventStoreSnapshots]
        DROP CONSTRAINT [PK_EventStoreSnapshots];

    -- The same three columns, still unique, now with the 1700-byte cap to live in.
    ALTER TABLE [dbo].[EventStoreSnapshots]
        ADD CONSTRAINT [PK_EventStoreSnapshots]
            PRIMARY KEY NONCLUSTERED ([AggregateId], [AggregateType], [TenantId]);

    -- (AggregateType, TenantId) is 638 bytes, inside the clustered cap, and the widest prefix of
    -- the key that fits -- adding AggregateId returns to 1148 and reintroduces the fault.
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [object_id] = OBJECT_ID(N'[dbo].[EventStoreSnapshots]', N'U')
          AND [name] = N'CIX_EventStoreSnapshots_AggregateTypeTenant')
    BEGIN
        CREATE CLUSTERED INDEX [CIX_EventStoreSnapshots_AggregateTypeTenant]
            ON [dbo].[EventStoreSnapshots] ([AggregateType], [TenantId]);
    END

    PRINT N'009: PK_EventStoreSnapshots is now NONCLUSTERED over the same three columns, and the '
        + N'table is clustered on (AggregateType, TenantId). Snapshot saves no longer fail on long '
        + N'aggregate ids or types.';
END
