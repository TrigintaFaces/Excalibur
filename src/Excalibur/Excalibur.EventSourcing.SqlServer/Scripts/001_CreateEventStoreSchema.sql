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
    -- Global append position. NOT an IDENTITY -- see EventStoreEventsPosition below for why that
    -- distinction is the entire ordering guarantee. The store allocates this value from the
    -- counter row inside the appending transaction and writes it explicitly.
    [Position]       BIGINT NOT NULL,
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

    -- Set when this event's PAYLOAD has been moved to cold storage. The ROW stays, with its Version
    -- and its Position, so the global stream keeps no holes and a projection rebuild still replays
    -- every event in order (reading archived payloads back through cold storage). Deleting the row
    -- instead would make archived events invisible to every global-stream consumer while remaining
    -- perfectly readable per-aggregate -- a partial disappearance nothing downstream can detect.
    --
    -- It is also what separates an ARCHIVED entry from an ERASED one. Both have EventData NULL, but
    -- an archived payload is retrievable and an erased payload is gone. A reader that conflates them
    -- either drops data that still exists or resurrects data a data-subject request removed.
    [ArchivedAt]     DATETIMEOFFSET NULL,

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

-- =============================================================================
-- GLOBAL POSITION COUNTER -- the seam that makes the stream gapless.
-- =============================================================================
-- Position is allocated from THIS ROW inside the appending transaction, never from an
-- IDENTITY column or a sequence. The difference is not a detail; it is the ordering
-- guarantee itself.
--
-- An IDENTITY (and every sequence) hands its number out at INSERT and lets that number
-- escape the transaction: it is allocated before COMMIT and burned if the transaction
-- aborts. Two concurrent appends can therefore COMMIT in the opposite order to their
-- positions, and a tailing reader that has already passed the higher position will never
-- see the lower one. That is silent, permanent event loss for every projection built on
-- the stream -- silent because nothing downstream can detect it from the outside.
--
-- An UPDATE of this row takes an exclusive row lock released ONLY at COMMIT, and the
-- increment ROLLS BACK with the transaction, so no value is ever burned. Therefore:
--
--   INVARIANT J: at every instant, the set of committed global positions is a
--                contiguous prefix {1..k}. There are no gaps, ever.
--
-- WITHDRAWN. The sentence that stood here was a NON-SEQUITUR and it is corrected below.
-- It read: "A subscriber may consequently advance its high-water mark to any position it
-- has observed committed, and a hole in the sequence is not a case to be handled -- it is
-- a bug to be reported loudly."
--
-- INVARIANT J IS TRUE. The subscriber rule DOES NOT FOLLOW FROM IT. J is a predicate on a
-- STATE; a subscriber scan SPANS states. A SELECT ... WHERE Position > @cp ORDER BY Position
-- examines each slot at a different instant and never returns to one it has passed, and J
-- relates none of that. ONE WRITER IS ENOUGH to lose an event: the scan passes slot 1 while
-- it is uncommitted, slot 1 commits, slot 2 commits, the scan reaches slot 2 and delivers
-- it. The high-water mark now sits above a committed event that was never delivered, and no
-- later scan from that checkpoint revisits it.
--
-- The counter row does not save this. It totally orders COMMITS -- position 2 cannot commit
-- before position 1 -- but the scan READS are interleaved with those commits rather than
-- ordered against them.
--
-- WHAT DECIDES IT IS WHETHER THE SCAN IS ATOMIC w.r.t. CONCURRENT COMMITS.
--   statement-level snapshot (SQL Server with READ_COMMITTED_SNAPSHOT ON; PostgreSQL and
--   Oracle READ COMMITTED, which are MVCC) -- one SELECT reads as of statement start, so it
--   cannot see a later position that committed mid-scan while missing an earlier one. SAFE.
--   locking READ COMMITTED (the default for a SELF-HOSTED SQL Server) -- no statement
--   snapshot; shared locks are taken and released row by row, so a row inserted and
--   committed AHEAD of the scan's position after the scan began IS visible. THE SKIP HAPPENS.
--
-- CORRECTION: an earlier revision of this comment had that backwards -- it named RCSI ON as the
-- exposed configuration. It is the safe one. The error came from naming a configuration rather
-- than the property, so the property is stated first here.
--
-- THE READER NOW DEFENDS AGAINST BOTH, in one place: ContiguousGlobalStreamQuery, a decorator
-- over IGlobalStreamQuery that this provider's registration applies. It delivers only the
-- CONTIGUOUS run from the caller's position and stops at the first gap. By J a missing position
-- is in flight rather than absent, so stopping DEFERS events instead of dropping them.
--
-- IF YOUR DATABASE WAS ARCHIVED BY A VERSION THAT DELETED EVENT ROWS, it carries permanent gaps
-- and the reader will wait at the first one. Script 012_BackfillGapsLeftByLegacyArchival.sql
-- reports them and can restore contiguity by inserting a tombstone at each missing position.
-- What J DOES still give you: the relative allocation order of two counters and the lifetime
-- of any single transaction do not enter the correctness argument for the STORED sequence.
--
-- COST, stated here because it is consumer-visible: appends serialize on this row, so
-- sustained append throughput is bounded by roughly one commit's log flush (order 10^3
-- to 10^4 appends/sec). That is the intrinsic price of a single global total order over
-- concurrent writers, not an artifact of this implementation -- an IDENTITY column only
-- appears to avoid the cost because it does not actually produce a total order. Consumers
-- who need more throughput than one ordered stream can carry should shard, and accept
-- that there is then no cross-shard global order to read.
CREATE TABLE [dbo].[EventStoreEventsPosition] (
    -- Singleton by construction: the CHECK makes a second counter row unrepresentable
    -- rather than merely discouraged. Two counters would silently reintroduce gaps.
    [Id]    TINYINT NOT NULL CONSTRAINT [PK_EventStoreEventsPosition] PRIMARY KEY,
    [Value] BIGINT  NOT NULL,
    CONSTRAINT [CK_EventStoreEventsPosition_Singleton] CHECK ([Id] = 1)
);
GO

-- The counter is named after the events table it orders ({table}Position), so two event stores
-- sharing one schema get one counter each rather than silently sharing a sequence.
-- Seeded from the table's own high-water mark rather than 0: Position is the PRIMARY KEY, so a
-- counter seeded at 0 against a table that already holds events would reissue existing values
-- and every append would fail on the key. On a fresh install the MAX is NULL and this is 0,
-- making the first allocated position 1.
INSERT INTO [dbo].[EventStoreEventsPosition] ([Id], [Value])
SELECT 1, ISNULL((SELECT MAX([Position]) FROM [dbo].[EventStoreEvents]), 0);
GO

-- Stream load: the store reads by aggregate, ordered by version, scoped to a tenant.
CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Stream]
    ON [dbo].[EventStoreEvents] ([AggregateId], [AggregateType], [TenantId], [Version]);
GO

-- Archive/projection catch-up reads by global position.
CREATE NONCLUSTERED INDEX [IX_EventStoreEvents_Position]
    ON [dbo].[EventStoreEvents] ([Position]);
GO
