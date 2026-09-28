-- SQL Server SCHEMA for Excalibur.EventSourcing.SqlServer — PROJECTION CURSOR MAPS
-- Version: 1.0
--
-- Creates the per-stream cursor table used by SqlServerCursorMapStore. Until now this table had no
-- CREATE TABLE anywhere in the package: the only statement of its shape was the XML doc comment on
-- the store class, which a consumer had to transcribe by hand from IntelliSense.
--
-- THERE IS NO AUTO-CREATE PATH FOR THIS TABLE. The store issues no DDL at runtime — it only reads,
-- merges and deletes. Without this script, the first projection checkpoint fails with
-- `Msg 208, Invalid object name 'ProjectionCursorMaps'`.
--
-- WHEN YOU NEED IT: whenever a projection resumes from per-stream positions rather than replaying
-- from zero. If nothing in your host resolves ICursorMapStore, you do not need this table.
--
-- HOW TO RUN IT. Plain T-SQL with GO batch separators; any client that understands GO applies it
-- unchanged (sqlcmd, SSMS, Azure Data Studio, DbUp, Flyway, or your own loop splitting on GO).
--
-- DERIVED FROM the store's own SQL, column for column, rather than from any prior copy of this
-- schema:
--   read     SqlServerCursorMapStore.cs:106-107   SELECT StreamId, Position ... WHERE TenantId = ... AND ProjectionName = ...
--   merge    SqlServerCursorMapStore.cs:144-151   MERGE ... ON TenantId/ProjectionName/StreamId ... INSERT (TenantId, ProjectionName, StreamId, Position)
--   reset    SqlServerCursorMapStore.cs:179-181   DELETE ... WHERE TenantId = ... AND ProjectionName = ...
--
-- OBJECT NAMES: unlike the event store and snapshot tables, this one is NOT configurable. The store
-- names `ProjectionCursorMaps` as a literal in every statement, unqualified, so it resolves under
-- the connection's default schema. This script creates it in [dbo], which is that schema for a
-- typical login. If your login defaults to another schema, create it there instead.
--
-- RE-RUNNABLE: every statement is guarded on whether its object already exists. It does not alter an
-- existing table.
--
-- IF YOU ALREADY CREATED THIS TABLE BY HAND from an earlier version of the store's doc comment,
-- READ THE INDEX KEY WIDTH SECTION BELOW BEFORE ASSUMING YOU ARE CURRENT. The guard skips an
-- existing table, so this script will not repair it, and the shape that comment described has a
-- clustered key over SQL Server's limit — a fault whose only symptom is a run-time refusal of long
-- rows.
--
--
-- INDEX KEY WIDTH — STATED, BECAUSE IT IS WHAT DECIDES THE SHAPE BELOW
-- ---------------------------------------------------------------------
-- SQL Server caps a CLUSTERED index key at 900 bytes and a NONCLUSTERED one at 1700. At 2 bytes per
-- NVARCHAR character the natural key is:
--
--     TenantId       NVARCHAR(64)   ->   128 bytes
--     ProjectionName NVARCHAR(256)  ->   512 bytes
--     StreamId       NVARCHAR(256)  ->   512 bytes
--                                       ----------
--                                        1152 bytes   (over 900 clustered, under 1700 nonclustered)
--
-- So the natural key CANNOT be the clustered key, and a plain PRIMARY KEY defaults to CLUSTERED.
-- That failure is quiet in the worst way: CREATE TABLE succeeds with only warning Msg 1946, and the
-- table then REFUSES oversized inserts at run time — a projection whose name and stream id together
-- run past ~450 characters stops checkpointing, and the next restart replays from the beginning.
--
-- This script therefore declares the natural key NONCLUSTERED, where the 1700-byte cap gives it
-- room, and clusters the table on (TenantId, ProjectionName) instead. That pair is exactly what the
-- read and reset paths filter on, so the clustered index IS the covering structure for both: one
-- range scan returns StreamId and Position with no key lookup. The sibling materialized-view tables
-- solve the same cap with a surrogate identity key; they are migrating tables that already had a
-- clustered natural key, and their access pattern is a point lookup rather than a prefix range.

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dbo')
BEGIN
    EXEC('CREATE SCHEMA [dbo]');
END
GO

-- ---------------------------------------------------------------------------------------
-- ProjectionCursorMaps — one row per (tenant, projection, stream) recording how far that
-- projection has consumed that stream.
--
--   TenantId is NOT NULL, with no DEFAULT, matching EventStoreEvents and EventStoreSnapshots.
--
--   NOT NULL, because the store ALWAYS binds this column: every statement routes through
--   KeyedTenantPartition, which yields the resolved tenant when scoped and the reserved
--   '__untenanted__' sentinel when not, so a statement carrying no tenant term is unconstructable.
--   "Untenanted" is a value here, not a missing one.
--
--   NO DEFAULT, because TenantId is a component of IDENTITY and you do not default a key column.
--   With a DEFAULT, a save that omitted the tenant would silently land in the untenanted partition,
--   making "I forgot to supply the tenant" indistinguishable from "this row is deliberately
--   untenanted." Without one, that statement fails outright. The store binds the value explicitly on
--   every path, so the default would never legitimately fire.
--
--   NVARCHAR(64) is the narrowest tenant column across every shipped provider, and the framework
--   refuses a longer identifier where it is constructed. Fixing every provider at that width is the
--   only choice that cannot truncate.
--
--   COLLATE Latin1_General_BIN2 ON ALL THREE KEY COLUMNS, and it is not decoration.
--
--   For TenantId it is the tenant boundary: the server default is typically case-INSENSITIVE, under
--   which 'Acme' = 'acme' and a tenant-scoped read reaches another tenant's rows with no error.
--
--   For ProjectionName and StreamId it is agreement with the store, which is ORDINAL about both —
--   it builds its result map with StringComparer.Ordinal (SqlServerCursorMapStore.cs:110). Under a
--   case-insensitive collation the database disagrees: two stream ids the framework treats as
--   distinct collapse onto ONE row, so one stream's cursor overwrites the other's and that
--   projector resumes past events it never processed. That is the same silent read-model gap the
--   tenant term exists to prevent, arriving through the collation instead.
--
--   Position is BIGINT and NOT NULL. It is a stream position, not a timestamp: the store reads it
--   straight into a long and hands it to the projector as the resume point.
-- ---------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('[dbo].[ProjectionCursorMaps]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[ProjectionCursorMaps] (
        [TenantId]       NVARCHAR(64)  COLLATE Latin1_General_BIN2 NOT NULL,
        [ProjectionName] NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
        [StreamId]       NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,
        [Position]       BIGINT        NOT NULL,

        -- All three columns, and the membership matters more than the order.
        --
        -- THE TENANT TERM IS PART OF CURSOR IDENTITY, not merely a read filter. Keyed on
        -- (ProjectionName, StreamId) alone, two tenants running the same projection over the same
        -- stream share ONE row: a position advanced by the first makes the second's projector resume
        -- past events it never processed. The result is data permanently missing from a read model,
        -- with no error and nothing to alert on.
        --
        -- Declaring anything narrower than the triple also breaks the save path in the direction
        -- that does not report itself: the store's MERGE matches on exactly these three columns, so
        -- a narrower key lets an unscoped save MATCH — and overwrite — a row it does not own,
        -- instead of raising a duplicate key.
        --
        -- NONCLUSTERED for the key-width reason given in the header. Do not change it to CLUSTERED.
        CONSTRAINT [PK_ProjectionCursorMaps] PRIMARY KEY NONCLUSTERED ([TenantId], [ProjectionName], [StreamId])
    );
END
GO

-- The clustered index, and therefore the physical order of the table. (TenantId, ProjectionName) is
-- exactly the predicate of both the read and the reset, so this one structure serves both as a range
-- scan and carries StreamId and Position with it — no key lookup on either path. It is deliberately
-- NOT unique: many streams share one (tenant, projection), and uniqueness lives on the primary key.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProjectionCursorMaps_Tenant_Projection' AND object_id = OBJECT_ID('[dbo].[ProjectionCursorMaps]'))
BEGIN
    CREATE CLUSTERED INDEX [IX_ProjectionCursorMaps_Tenant_Projection]
        ON [dbo].[ProjectionCursorMaps] ([TenantId], [ProjectionName]);
END
GO
