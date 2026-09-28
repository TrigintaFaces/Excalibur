-- Oracle Schema for Excalibur.EventSourcing.Oracle — EVENT STORE
-- Version: 1.0
--
-- Creates the table required by the Oracle event store. The store never creates it at runtime:
-- run this script before the first append. Without it, every append fails with
-- ORA-00942 (table or view does not exist).
--
-- This is the event store's primary table. The package's other scripts create the SNAPSHOT
-- table, which is an optimisation; this one holds the events themselves, and without it the
-- package cannot do the thing it exists to do.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     OracleEventStoreOptions.Schema = "EXCALIBUR"
--     OracleEventStoreOptions.Table  = "EVENTSTOREEVENTS"
--
-- The store qualifies and upper-cases both names, so it addresses this table as
-- "EXCALIBUR"."EVENTSTOREEVENTS". In Oracle a schema is a user, so run this script while
-- connected AS that user (or as a user with quota on that schema, having first switched
-- with ALTER SESSION SET CURRENT_SCHEMA = EXCALIBUR). The objects are created unqualified,
-- matching the snapshot script shipped alongside this one; if you connect as a different user
-- and do not switch schema, the table is created in the wrong place and the store will not
-- find it.
--
-- Oracle has no "CREATE TABLE IF NOT EXISTS"; re-running this script against an existing table
-- raises ORA-00955 (name is already used by an existing object), which is safe to ignore.

-- ---------------------------------------------------------------------------
-- Events
-- ---------------------------------------------------------------------------
-- Every column below is written or read by the store's SQL.
-- This script creates schema. Without the directive below SQL*Plus exits 0 even when a statement
-- fails -- ORA-00955 on an object that already exists, or an insufficient-privilege error -- so an
-- unattended runner records the schema as created and proceeds to the next script against a database
-- that does not have it. This is the FIRST script a consumer runs, so nothing downstream is safe if
-- it is wrong. The refusal-exit-code gate does not cover this script: its predicate is a raised
-- refusal, and there is none here -- the exposure is ordinary statement failure.
-- An operator running this inside an interactive session is ended by that non-zero exit;
-- to keep the session, issue WHENEVER SQLERROR CONTINUE before @-ing the file.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

CREATE TABLE EVENTSTOREEVENTS (
    -- The global append position. NOT an identity column, and the difference is the entire
    -- ordering guarantee. An Oracle identity column is a sequence; a sequence hands its number
    -- out at INSERT and lets it escape the transaction, so an aborted append burns a value.
    -- Worse, a sequence defaults to CACHE 20, so each SESSION draws a block and issues from it:
    -- a pooled session can commit a LOW position long after another session committed a HIGHER
    -- one. A subscriber that already read the higher position never sees the lower one, and the
    -- event is committed, durable and permanently invisible to every projection.
    --
    -- The store supplies this value explicitly, allocated from EVENTSTOREEVENTSPOSITION inside
    -- the appending transaction. GENERATED ALWAYS would REJECT that insert outright.
    POSITION        NUMBER(19)                     NOT NULL,
    EVENTID         VARCHAR2(255)                  NOT NULL,
    AGGREGATEID     VARCHAR2(255)                  NOT NULL,
    AGGREGATETYPE   VARCHAR2(255)                  NOT NULL,
    -- Rewritten to an erasure marker when a stream is erased, so this column is not immutable.
    EVENTTYPE       VARCHAR2(255)                  NOT NULL,
    -- Nullable despite being NOT NULL on append: erasure sets it to NULL, which is what makes
    -- the event unrecoverable while leaving the stream's shape intact.
    EVENTDATA       BLOB,
    METADATA        BLOB,
    VERSION         NUMBER(19)                     NOT NULL,
    EVENTTIMESTAMP  TIMESTAMP(7) WITH TIME ZONE    NOT NULL,
    -- TOTAL: every row carries a tenant term, and an untenanted event is the reserved
    -- '__untenanted__' sentinel rather than the absence of a value.
    --
    -- NOT NULL rejects nothing the store can produce. The store binds the term through
    -- KeyedTenantPartition, which has no empty inhabitant and yields the sentinel for an
    -- unscoped host, so an untenanted append supplies the sentinel and is accepted.
    --
    -- Totality is load-bearing HERE in a way it is not on a non-key column, because TENANTID
    -- is part of UQ_EVENTSTOREEVENTS_STREAM below. Oracle treats NULLs as DISTINCT in a unique
    -- index, so while this column was nullable that constraint did not constrain untenanted
    -- rows at all: two appends at the same version of the same untenanted stream both
    -- succeeded, and optimistic concurrency silently did not hold for them. The snapshot
    -- store hit this first and worked around it with a function-based index over
    -- NVL(TENANTID, CHR(1)); 002 removed that workaround by making its column total. This is
    -- the same fix applied to the event store, which is why the constraint below can stay a
    -- plain UNIQUE.
    --
    -- The read path's COALESCE(TENANTID, <sentinel>) stays and is now a no-op over this
    -- column. It is left in place deliberately: removing it is a separate, behaviour-visible
    -- change, and it costs nothing here.
    --
    -- An existing database created before tenancy is converged by
    -- 004_MakeEventTenantTotal.sql, which backfills the sentinel and then applies this
    -- constraint, so an upgraded database ends up in the same shape as a fresh one.
    TENANTID        VARCHAR2(64)  DEFAULT '__untenanted__'  NOT NULL,
    CONSTRAINT PK_EVENTSTOREEVENTS PRIMARY KEY (POSITION),
    -- The optimistic-concurrency guarantee: one row per version per stream, per tenant. Without
    -- TENANTID in the key, two tenants appending the same aggregate at the same version collide
    -- and one tenant's append is rejected as another tenant's conflict.
    CONSTRAINT UQ_EVENTSTOREEVENTS_STREAM UNIQUE (AGGREGATEID, AGGREGATETYPE, VERSION, TENANTID)
);

-- Supports the stream read, which selects by aggregate and type above a version and orders by
-- version ascending.
-- =============================================================================
-- GLOBAL POSITION COUNTER -- the seam that makes the stream gapless.
-- =============================================================================
-- Positions are allocated by UPDATEing this row inside the appending transaction, never from a
-- sequence. The row lock is released only at COMMIT and the increment rolls back with the
-- transaction, so no value is ever burned and no session can hold a block of unissued values.
-- Therefore:
--
--   INVARIANT J: at every instant, the set of committed global positions is a contiguous
--                prefix {1..k}. There are no gaps, ever.
--
-- A subscriber may consequently advance its high-water mark to any position it has observed
-- committed, and a hole is not a case to handle -- it is a bug to report loudly.
--
-- NOCACHE on a sequence is NOT a cheaper alternative: it pays a comparable serialization cost on
-- SEQ$ and still burns values on rollback, so it costs the same and fixes only half the problem.
--
-- COST, stated because it is consumer-visible: appends serialize on this row, so sustained
-- append throughput is bounded by roughly one commit. That is the intrinsic price of a single
-- global total order over concurrent writers.
CREATE TABLE EVENTSTOREEVENTSPOSITION (
    -- Singleton by construction: the CHECK makes a second counter row unrepresentable rather
    -- than merely discouraged. Two counters would silently reintroduce gaps.
    ID    NUMBER(1)  NOT NULL,
    VALUE NUMBER(19) NOT NULL,
    CONSTRAINT PK_EVENTSTOREEVENTSPOSITION PRIMARY KEY (ID),
    CONSTRAINT CK_EVENTSTOREEVENTSPOSITION_ONE CHECK (ID = 1)
);

-- Seeded from the table's own high-water mark rather than 0: POSITION is the PRIMARY KEY, so a
-- counter seeded at 0 against a table that already holds events would reissue existing values
-- and every append would fail on the key. On a fresh install the MAX is NULL and this is 0,
-- making the first allocated position 1.
INSERT INTO EVENTSTOREEVENTSPOSITION (ID, VALUE)
SELECT 1, NVL((SELECT MAX(POSITION) FROM EVENTSTOREEVENTS), 0) FROM DUAL;

CREATE INDEX IX_EVENTSTOREEVENTS_STREAM
    ON EVENTSTOREEVENTS (AGGREGATEID, AGGREGATETYPE, VERSION);
