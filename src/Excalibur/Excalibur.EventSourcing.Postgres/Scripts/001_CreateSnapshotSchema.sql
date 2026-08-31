-- Postgres Schema for Excalibur.EventSourcing.Postgres — SNAPSHOT STORE
-- Version: 1.0
--
-- Creates the table required by the Postgres snapshot store. The store never creates
-- this table at runtime: run this script against the target database before the first
-- snapshot is saved. Without it, every snapshot operation fails.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     schema = "public"
--     table  = "event_store_snapshots"
--
-- If you override either, rename the object below to match.
--
--
-- ONE SCHEMA FORM, NOT TWO
-- ------------------------
-- Unlike the inbox store, this schema does NOT split by deployment mode. The snapshot
-- store's INSERT names tenant_id unconditionally, so a single-tenant deployment writes
-- the column too — it stores the reserved empty-string ('') tenant key. A "pair form"
-- schema without tenant_id would fail on the first save regardless of whether
-- multi-tenancy is registered.
--
-- Untenanted is a distinct partition, not an absent tenant.

CREATE TABLE IF NOT EXISTS public.event_store_snapshots (
    snapshot_id     TEXT NOT NULL,
    aggregate_id    VARCHAR(255) NOT NULL,
    aggregate_type  VARCHAR(255) NOT NULL,
    version         BIGINT NOT NULL,
    data            BYTEA NOT NULL,
    -- Version metadata the snapshot carries (serializer version, last-applied event id/timestamp).
    -- Nullable: a snapshot may be written without metadata.
    metadata        BYTEA,
    created_at      TIMESTAMPTZ NOT NULL,
    -- No DEFAULT, deliberately. tenant_id is a component of IDENTITY, not an optional
    -- filter -- it is part of the primary key below, and you do not default a key column.
    -- With a default, an INSERT that omitted the tenant would silently land the row in the
    -- untenanted partition, making "I forgot to supply the tenant" indistinguishable from
    -- "this row is deliberately untenanted." Without one, that statement fails outright.
    tenant_id       VARCHAR(64) NOT NULL,

    -- The tenant participates in the record's IDENTITY, not merely in read filters.
    -- Keying on (aggregate_id, aggregate_type) alone lets one tenant's save overwrite
    -- another tenant's snapshot for the same aggregate — the upsert matches a row it
    -- does not own. This triple is the store's ON CONFLICT target; declaring anything
    -- narrower produces a silent cross-tenant overwrite rather than an error.
    PRIMARY KEY (aggregate_id, aggregate_type, tenant_id)
);

-- Reads locate the latest snapshot for an aggregate by version.
CREATE INDEX IF NOT EXISTS idx_event_store_snapshots_version
    ON public.event_store_snapshots (aggregate_id, aggregate_type, version);


-- WHY tenant_id IS NOT NULL RATHER THAN NULLABLE
-- ----------------------------------------------
-- PostgreSQL before version 15 treats NULL as DISTINCT in a unique index, so a nullable
-- tenant column cannot serve as an upsert key: each untenanted save would INSERT a new
-- row instead of updating the existing one, accumulating duplicate snapshots that the
-- read path never reconciles.
--
-- An untenanted deployment therefore stores the reserved '__untenanted__' tenant key,
-- written explicitly by the store on every save — never supplied by a column default. It is
-- collision-proof (a scoped tenant is rejected if it names the reserved sentinel, so no real
-- tenant can claim the untenanted partition) and the read and write paths agree on it.
--
-- WHY THE SENTINEL AND NOT THE EMPTY STRING
-- -----------------------------------------
-- An earlier revision stored '' here. That value is not portable: Oracle cannot store a
-- zero-length string ('' IS NULL there), so the same intent became a NULL on that provider
-- and needed a function-based index to stay unique. The empty string was never a second
-- choice alongside NULL — it BECOMES NULL on contact with Oracle. A reserved, non-empty
-- sentinel expresses identically on every provider, which is why it, and not '', is the
-- canonical untenanted representation across the keyed store family.
