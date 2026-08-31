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
CREATE TABLE EVENTSTOREEVENTS (
    -- The global append position, read back by the INSERT itself via RETURNING POSITION INTO.
    -- It MUST be an identity column: the store does not supply a value, and it raises an
    -- invariant-breach error if no position comes back for the first event of an append.
    POSITION        NUMBER(19)     GENERATED ALWAYS AS IDENTITY,
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
CREATE INDEX IX_EVENTSTOREEVENTS_STREAM
    ON EVENTSTOREEVENTS (AGGREGATEID, AGGREGATETYPE, VERSION);
