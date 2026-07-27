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

    -- NOT NULL and no DEFAULT, deliberately — and unlike the events table, which stays
    -- nullable to keep pre-tenancy rows reachable. There are no legacy snapshot rows to
    -- preserve here, so this column can carry the stronger contract.
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
    [TenantId]       NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL,

    -- The store's MERGE matches on exactly this triple. Declaring anything narrower
    -- produces a silent cross-tenant overwrite rather than an error: an unscoped save
    -- against a table holding tenant rows for the same aggregate would match a row it
    -- does not own.
    CONSTRAINT [PK_EventStoreSnapshots] PRIMARY KEY CLUSTERED ([AggregateId], [AggregateType], [TenantId])
);
GO

-- Snapshot pruning reads by age.
CREATE NONCLUSTERED INDEX [IX_EventStoreSnapshots_CreatedAt]
    ON [dbo].[EventStoreSnapshots] ([CreatedAt]);
GO
