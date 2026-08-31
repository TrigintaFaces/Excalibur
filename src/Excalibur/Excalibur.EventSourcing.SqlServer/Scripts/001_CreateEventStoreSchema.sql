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
    -- NULLABLE, and the nullability is load-bearing rather than lax. Erasure TOMBSTONES an
    -- event by setting EventData to NULL while keeping its row and its Version in the stream,
    -- so the sequence stays contiguous and replay does not see a hole. Declared NOT NULL,
    -- every erasure instead fails with "Cannot insert the value NULL into column 'EventData'
    -- ... UPDATE fails" -- which is not a theoretical risk: it is what this script did until
    -- the erasure path was exercised against a real engine, and it meant a consumer's
    -- right-to-erasure request against SQL Server could not succeed at all.
    [EventData]      VARBINARY(MAX) NULL,
    -- Nullable: an event may be appended without metadata, and erasure overwrites this
    -- column with a tombstone payload rather than deleting the row.
    [Metadata]       VARBINARY(MAX) NULL,
    [Version]        BIGINT         NOT NULL,
    [Timestamp]      DATETIMEOFFSET NOT NULL,

    -- TOTAL: every row carries a tenant term, and "untenanted" is the reserved
    -- '__untenanted__' sentinel rather than the absence of a value.
    --
    -- This is a FRESH-install schema, so there are no pre-tenancy rows to preserve and the
    -- column can be total from the start. An existing database created before tenancy is
    -- migrated by 003 (which adds the column) and then 004 (which backfills the sentinel and
    -- applies this constraint), so an upgraded database converges on the same shape rather
    -- than diverging from a fresh one.
    --
    -- The store already binds a non-null term on every write: it goes through
    -- KeyedTenantPartition, which has no empty inhabitant and yields '__untenanted__' for an
    -- unscoped host. So NOT NULL rejects nothing the store can produce -- it only removes the
    -- ability to represent "untenanted" a second way.
    --
    -- Why totality matters beyond tidiness: TenantId is part of UQ_EventStoreEvents_Stream
    -- below. A nullable column in a UNIQUE constraint is compared under three-valued logic,
    -- so the optimistic-concurrency guarantee is weaker for untenanted streams than for
    -- tenanted ones. With the column total, one rule covers both.
    --
    -- The read path's COALESCE(TenantId, '__untenanted__') stays and is now a no-op over this
    -- column. It is left in place deliberately: removing it is a separate, behaviour-visible
    -- change, and it costs nothing here.
    --
    -- The binary collation is load-bearing, not an ornament. Under the server's usual
    -- case-INSENSITIVE default, 'Acme' = 'acme', so a scoped read returns another tenant's
    -- events -- the tenant predicate fails OPEN.
    [TenantId]       NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
        CONSTRAINT [DF_EventStoreEvents_TenantId] DEFAULT '__untenanted__',

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
