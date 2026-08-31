-- SQL Server Schema for Excalibur.EventSourcing.SqlServer — SNAPSHOT STORE
-- Version: 1.0
--
-- Creates the table required by the SQL Server snapshot store. The store never creates
-- this table at runtime: run this script against the target database before the first
-- snapshot is saved. Without it, every snapshot save fails with
-- Invalid object name 'EventStoreSnapshots'.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     schema = "dbo"
--     table  = "EventStoreSnapshots"
--
-- If you override either, rename the object below to match.
--
--
-- ONE SCHEMA FORM, NOT TWO
-- ------------------------
-- This schema does NOT split by deployment mode. The snapshot store's MERGE names
-- TenantId unconditionally, so a single-tenant deployment writes the column too — it
-- stores the reserved '__untenanted__' sentinel. A "pair form" schema without TenantId
-- would fail on the first save regardless of whether multi-tenancy is registered.
--
-- INDEX KEY WIDTH — STATED, BECAUSE IT IS WHAT DECIDES THE KEY'S SHAPE BELOW
-- ---------------------------------------------------------------------------
-- SQL Server caps a CLUSTERED index key at 900 bytes and a NONCLUSTERED one at 1700. At
-- 2 bytes per NVARCHAR character this table's natural key is:
--
--     AggregateId    NVARCHAR(255)  ->   510 bytes
--     AggregateType  NVARCHAR(255)  ->   510 bytes
--     TenantId       NVARCHAR(64)   ->   128 bytes
--                                       ----------
--                                        1148 bytes   (over 900 clustered, under 1700 nonclustered)
--
-- A plain PRIMARY KEY defaults to CLUSTERED, and that fails quietly in the worst way: CREATE
-- TABLE succeeds with only warning Msg 1946, and the table then REFUSES oversized rows at run
-- time with Msg 1946 again. A deployment whose aggregate id, type and tenant together run past
-- ~450 characters cannot save a snapshot, and finds out on the first such write rather than at
-- deployment. So the key is declared NONCLUSTERED, where 1700 bytes gives it room, and the table
-- is clustered on the widest prefix that fits. The uniqueness guarantee is identical either way.
--
-- 009_MakeSnapshotKeyFitTheIndexLimit.sql applies the same change to a table that already exists.
--
-- TENANT COLLATION
-- ----------------
-- TenantId is pinned to a binary collation. SQL Server's server default is typically
-- case-INSENSITIVE, under which 'Acme' = 'acme'. Because TenantId is part of the MERGE
-- key below, a case-insensitive collation does not merely leak reads — it lets one
-- tenant's save MATCH and overwrite another tenant's snapshot.

CREATE TABLE [dbo].[EventStoreSnapshots] (
    [SnapshotId]     NVARCHAR(255)  NOT NULL,
    [AggregateId]    NVARCHAR(255)  NOT NULL,
    [AggregateType]  NVARCHAR(255)  NOT NULL,
    [Version]        BIGINT         NOT NULL,
    [Data]           VARBINARY(MAX) NOT NULL,
    -- Version metadata the snapshot carries (serializer version, last-applied event id
    -- and timestamp). Nullable: a snapshot may be written without metadata.
    [Metadata]       VARBINARY(MAX) NULL,
    [CreatedAt]      DATETIMEOFFSET NOT NULL,

    -- NOT NULL and no DEFAULT, deliberately. The events table in 001 declares its tenant
    -- column NOT NULL too, so the difference between the two tables is the DEFAULT, not
    -- nullability — do not read this comment as a reason to leave an events table nullable
    -- or to skip 004, which is what makes that column total on a database created before
    -- tenancy existed. The events column carries DEFAULT '__untenanted__' because it is not
    -- part of that table's primary key and a backfilled row must land somewhere. This column
    -- sits differently, for the reason that follows.
    --
    -- TenantId is a component of IDENTITY, not an optional filter: it is part of the
    -- primary key below, and you do not default a key column. With a DEFAULT, a save that
    -- omitted the tenant would silently land in the untenanted partition, making "I forgot
    -- to supply the tenant" indistinguishable from "this row is deliberately untenanted."
    -- Without one, that statement fails outright.
    --
    -- The store binds the reserved '__untenanted__' sentinel for an unscoped host — never
    -- NULL, never ''. The empty string cannot be the shared representation: Oracle folds
    -- '' to NULL, so identical intent would become a different value on that provider.
    [TenantId]       NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,

    -- The store's MERGE matches on exactly this triple. Declaring anything narrower
    -- produces a silent cross-tenant overwrite rather than an error: an unscoped save
    -- against a table holding tenant rows for the same aggregate would match a row it
    -- does not own.
    --
    -- NONCLUSTERED, and that is load-bearing rather than a tuning choice. See the header:
    -- the triple is 1148 bytes, past SQL Server's 900-byte CLUSTERED cap and inside the
    -- 1700-byte NONCLUSTERED one. A plain PRIMARY KEY defaults to CLUSTERED, and the
    -- resulting table is created with only warning Msg 1946 and then refuses oversized
    -- rows at run time -- so a deployment whose aggregate id, type and tenant together run
    -- past ~450 characters cannot save a snapshot and discovers that on the first such
    -- write, never at deployment. The uniqueness guarantee is unchanged: the same three
    -- columns are still the key, still enforced, still what the MERGE matches on.
    CONSTRAINT [PK_EventStoreSnapshots] PRIMARY KEY NONCLUSTERED ([AggregateId], [AggregateType], [TenantId])
);
GO

-- The table still gets a clustered index, just not on the full key. Left as a heap, a table
-- whose rows are rewritten in place by MERGE accumulates forwarded records, and every point
-- lookup pays an extra hop through the nonclustered key.
--
-- (AggregateType, TenantId) is 319 characters = 638 bytes, inside the 900-byte clustered cap
-- with room to spare, and it is the widest prefix of the key that fits: adding AggregateId
-- would take it to 1148 and reintroduce the fault this script exists to remove. It groups one
-- aggregate type's snapshots per tenant, which is the range both the pruning sweep and the
-- tenant-scoped delete walk.
CREATE CLUSTERED INDEX [CIX_EventStoreSnapshots_AggregateTypeTenant]
    ON [dbo].[EventStoreSnapshots] ([AggregateType], [TenantId]);
GO

-- Snapshot pruning reads by age.
CREATE NONCLUSTERED INDEX [IX_EventStoreSnapshots_CreatedAt]
    ON [dbo].[EventStoreSnapshots] ([CreatedAt]);
GO
