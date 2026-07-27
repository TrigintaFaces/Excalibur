-- SQL Server Schema for Excalibur.EventSourcing.SqlServer — EVENT STORE
-- Version: 1.0
--
-- Creates the table required by the SQL Server event store. The store never creates this
-- table at runtime: run this script against the target database before the first append.
-- Without it, every append and load fails with Invalid object name.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     schema = "dbo"
--     table  = "EventStoreEvents"
--
-- If you override either, rename the object below to match.
--
--
-- TENANT COLLATION
-- ----------------
-- TenantId is pinned to a binary collation. SQL Server's server default is typically
-- case-INSENSITIVE, under which 'Acme' = 'acme' — a tenant would read another tenant's
-- rows, and the comparison fails OPEN. The store compares tenant terms with ordinal
-- semantics, so the column must agree or the guarantee is lost in storage.

CREATE TABLE [dbo].[EventStoreEvents] (
    -- Global append position. The store returns this from its INSERT to order the stream.
    [Position]       BIGINT IDENTITY(1,1) NOT NULL,
    [EventId]        NVARCHAR(255)  NOT NULL,
    [AggregateId]    NVARCHAR(255)  NOT NULL,
    [AggregateType]  NVARCHAR(255)  NOT NULL,
    [EventType]      NVARCHAR(255)  NOT NULL,
    [EventData]      VARBINARY(MAX) NOT NULL,
    -- Nullable: an event may be appended without metadata, and erasure overwrites this
    -- column with a tombstone payload rather than deleting the row.
    [Metadata]       VARBINARY(MAX) NULL,
    [Version]        BIGINT         NOT NULL,
    [Timestamp]      DATETIMEOFFSET NOT NULL,

    -- NULLABLE, deliberately, and unlike the snapshot table.
    --
    -- The read path is written as COALESCE(TenantId, '__untenanted__') = @TenantId, which
    -- exists to keep rows appended BEFORE tenancy was introduced reachable. Those legacy
    -- rows hold NULL. Declaring this column NOT NULL here would be correct for new data
    -- and would break the migration path this store deliberately supports.
    --
    -- New writes never store NULL: the store binds the reserved '__untenanted__' sentinel
    -- for an unscoped host. Untenanted is a named partition, not an absent tenant.
    [TenantId]       NVARCHAR(255) COLLATE Latin1_General_BIN2 NULL,

    CONSTRAINT [PK_EventStoreEvents] PRIMARY KEY CLUSTERED ([Position]),

    -- The tenant participates in stream IDENTITY, not merely in read filters. Keying on
    -- (AggregateId, AggregateType, Version) alone lets one tenant's append collide with
    -- another tenant's stream at the same version. This quad is what makes optimistic
    -- concurrency per-tenant rather than global.
    CONSTRAINT [UQ_EventStoreEvents_Stream] UNIQUE ([AggregateId], [AggregateType], [Version], [TenantId])
);
GO

-- Stream load: the store reads by aggregate, ordered by version, scoped to a tenant.
CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Stream]
    ON [dbo].[EventStoreEvents] ([AggregateId], [AggregateType], [TenantId], [Version]);
GO

-- Archive/projection catch-up reads by global position.
CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Position]
    ON [dbo].[EventStoreEvents] ([Position]);
GO
