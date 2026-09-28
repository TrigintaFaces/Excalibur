-- PostgreSQL SCHEMA for Excalibur.EventSourcing.Postgres — EVENT STORE
-- Version: 1.0
--
-- Creates the append-only event store table. The package already shipped
-- 001_CreateSnapshotSchema.sql for the snapshot store; this is the other half, and until now the
-- core table of the package had no DDL a consumer could obtain.
--
-- THERE IS NO AUTO-CREATE PATH FOR THIS TABLE. Unlike the SQLite provider, this package never
-- issues CREATE TABLE for the event store at runtime, and PostgresMigrator is not a provisioning
-- path — it executes migrations from a CONSUMER-SUPPLIED assembly and embeds no schema of its own.
-- Without this script the first append fails with `42P01: relation "events" does not exist`.
--
-- DERIVED FROM the store's own SQL, column for column, rather than from any prior copy of this
-- schema (see the note on divergence at the end of this header):
--   INSERT + RETURNING   Requests/InsertEventsBatchRequest.cs:129-133
--   read path            Requests/LoadEventsRequest.cs:58-67
--   version probe        Requests/GetCurrentVersionRequest.cs:58-66
--   erasure              Requests/EraseEventsRequest.cs:57-67
--
-- OBJECT NAMES: this script targets the DEFAULTS — schema `public`, table `events`
-- (PostgresEventSourcingOptions.cs:64, settable as EventStoreTable). Rename below to match a
-- deployment that configures other names.
--
-- RE-RUNNABLE: every statement is IF NOT EXISTS. It does not alter an existing table.
--
-- IF YOU HAVE SEEN A DIFFERENT VERSION OF THIS SCHEMA, READ THIS
-- --------------------------------------------------------------
-- Copies of this table's DDL exist in the integration fixtures and in the documentation, and they
-- do not agree with each other. This file is derived from the store's statements and is the
-- canonical one. Two differences are deliberate:
--
--   * `is_dispatched` is NOT declared here. It supported an event-store dispatch-tracking path
--     that has been removed; nothing in this package reads or writes it. A column no query
--     mentions is not schema, it is residue.
--   * `tenant_id` is NOT NULL here, where older copies made it nullable on the grounds that an
--     unscoped write omits the column. That is no longer true of the store — see below.

CREATE SCHEMA IF NOT EXISTS "public";

-- ---------------------------------------------------------------------------------------
-- events — the append-only event stream.
--
--   position is BIGINT PRIMARY KEY and is the global stream order. The store -- not the database --
--   assigns it, from the events_position counter row, inside the appending transaction. It is
--   deliberately NOT a BIGSERIAL: PostgreSQL advances a sequence non-transactionally, so an aborted
--   append would burn a value and two concurrent appends could commit out of position order, which
--   permanently hides the late-committing event from every subscriber. See events_position below.
--
--   event_data is BYTEA and NULLABLE, and the nullability is load-bearing rather than lax.
--   Erasure TOMBSTONES an event by setting event_data to NULL while keeping its position in the
--   stream, so the version sequence is preserved and readers do not see a hole. Declared NOT NULL,
--   every erasure fails with `23502: null value in column "event_data" violates not-null
--   constraint` — which is not a theoretical risk: it is what a fixture carrying that mistake did
--   until the erasure path was first exercised against a real engine.
--
--   metadata is the other nullable column: an event written without metadata stores NULL rather
--   than an empty payload.
--
--   timestamp is TIMESTAMPTZ. Events are ordered and compared across processes that need not share
--   a timezone; a naive timestamp would make that arithmetic depend on where the writer ran.
--
--   tenant_id is NOT NULL, with no DEFAULT, matching event_store_snapshots in 001.
--
--   NOT NULL, because the store now ALWAYS emits this column and its parameter: writes route
--   through KeyedTenantPartition, which binds the resolved tenant when scoped and the reserved
--   '__untenanted__' sentinel when not, so an un-partitioned write is unconstructable
--   (InsertEventsBatchRequest.cs:83-89). "Untenanted" is a value, not a missing one. Older copies
--   of this schema declare the column nullable and justify it with "the unscoped path emits
--   neither this column nor its parameter" — that was true once and is not true now.
--
--   NO DEFAULT, because a defaulted tenant column makes "this row is deliberately untenanted"
--   indistinguishable from "the writer forgot the tenant." Without one, such a statement fails
--   outright instead of silently landing the row in the untenanted partition. The store binds the
--   value explicitly on every insert, so the default would never legitimately fire.
--
--   The reads say `COALESCE(tenant_id, @UntenantedSentinel) = @TenantId` rather than a bare
--   equality. That COALESCE is TRANSITION TOLERANCE for databases provisioned before the column
--   was total — it folds a legacy NULL onto the sentinel so a pre-migration row is still found. It
--   is not a licence to create new nullable rows, and it costs nothing against this table: the
--   COALESCE of a non-null value is that value.
-- ---------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.events (
    -- NOT a BIGSERIAL. A sequence hands its number out at INSERT and lets it escape the
    -- transaction, so an aborted append leaves a permanent hole and two concurrent appends can
    -- COMMIT in the opposite order to the positions they hold -- which silently and permanently
    -- hides the late-committing event from every projection. The store allocates this value from
    -- the events_position counter row inside the appending transaction instead. See below.
    position        BIGINT PRIMARY KEY,
    event_id        VARCHAR(255) NOT NULL,
    aggregate_id    VARCHAR(255) NOT NULL,
    aggregate_type  VARCHAR(255) NOT NULL,
    event_type      VARCHAR(255) NOT NULL,
    event_data      BYTEA NULL,
    metadata        BYTEA,
    version         BIGINT NOT NULL,
    timestamp       TIMESTAMPTZ NOT NULL,

    -- Set when this event's PAYLOAD has been moved to cold storage. The ROW stays, with its version
    -- and its position, so the global stream keeps no holes and a projection rebuild still replays
    -- every event in order (reading archived payloads back through cold storage). Deleting the row
    -- instead would make archived events invisible to every global-stream consumer while remaining
    -- perfectly readable per-aggregate -- a partial disappearance nothing downstream can detect.
    --
    -- It is also what separates an ARCHIVED entry from an ERASED one. Both have event_data NULL, but
    -- an archived payload is retrievable and an erased payload is gone. A reader that conflates them
    -- either drops data that still exists or resurrects data a data-subject request removed.
    archived_at     TIMESTAMPTZ NULL,
    tenant_id       VARCHAR(64) NOT NULL,

    -- Optimistic concurrency. Two writers racing to append version N for the same aggregate do not
    -- both succeed; the loser gets a uniqueness violation, which is how the store detects a
    -- concurrent modification. Dropping this does not make appends faster, it makes them wrong.
    --
    -- The tenant participates in stream IDENTITY, not merely in read filters, which makes that
    -- concurrency check per-tenant rather than global. Without the term this key disagrees with the
    -- version probe, which IS tenant-scoped (GetCurrentVersionRequest): the probe reports -1 for a
    -- tenant that has never used the aggregate id while the insert collides with another tenant's
    -- row, so the caller sees a retryable conflict whose retry re-probes and gets -1 again. It never
    -- converges. Deployments created before this line carried the tenant term are converged by 005.
    CONSTRAINT uq_events_aggregate_version_tenant
        UNIQUE (aggregate_id, aggregate_type, version, tenant_id)
);

-- Serves the aggregate read path, which selects by (aggregate_id, aggregate_type) and orders by
-- version (LoadEventsRequest.cs:66-67), and the version probe's MAX(version) over the same
-- columns. The primary key cannot serve either: it is keyed on global position.
-- =============================================================================
-- GLOBAL POSITION COUNTER -- the seam that makes the stream gapless.
-- =============================================================================
-- Positions are allocated by UPDATEing this row inside the appending transaction, never from a
-- sequence. PostgreSQL advances a sequence non-transactionally by design, so nextval() is not
-- rolled back by an aborted transaction and two concurrent appends can commit out of position
-- order. Both of those are silent, permanent event loss for any subscriber tailing the stream.
--
-- The row lock taken by the UPDATE is released only at COMMIT, and the increment rolls back with
-- the transaction, so no value is ever burned. Therefore:
--
--   INVARIANT J: at every instant, the set of committed global positions is a contiguous
--                prefix {1..k}. There are no gaps, ever.
--
-- A subscriber may consequently advance its high-water mark to any position it has observed
-- committed, and a hole in the sequence is not a case to be handled -- it is a bug to be reported
-- loudly.
--
-- COST, stated because it is consumer-visible: appends serialize on this row, so sustained append
-- throughput is bounded by roughly one commit. That is the intrinsic price of a single global
-- total order over concurrent writers, not an artifact of this implementation -- a sequence only
-- appears to avoid the cost because it does not actually produce a total order.
--
-- The counter is named after the events table it orders ({table}_position), so two event stores
-- sharing one schema get one counter each rather than silently sharing a sequence.
CREATE TABLE IF NOT EXISTS public.events_position (
    -- Singleton by construction: the CHECK makes a second counter row unrepresentable rather than
    -- merely discouraged. Two counters would silently reintroduce gaps.
    id    SMALLINT PRIMARY KEY CHECK (id = 1),
    value BIGINT NOT NULL
);

-- Seeded from the table's own high-water mark rather than 0: position is the PRIMARY KEY, so a
-- counter seeded at 0 against a table that already holds events would reissue existing values
-- and every append would fail on the key. On a fresh install the MAX is NULL and this is 0,
-- making the first allocated position 1.
INSERT INTO public.events_position (id, value)
SELECT 1, COALESCE((SELECT MAX(position) FROM public.events), 0)
ON CONFLICT (id) DO NOTHING;

CREATE INDEX IF NOT EXISTS idx_events_aggregate
    ON public.events (aggregate_id, aggregate_type, version);
