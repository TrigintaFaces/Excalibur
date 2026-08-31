-- PostgreSQL SCHEMA for Excalibur.EventSourcing.Postgres — PROJECTION CURSOR MAPS
-- Version: 1.0
--
-- Creates the per-stream cursor table used by PostgresCursorMapStore. Until now this table had no
-- CREATE TABLE anywhere in the package: the only statement of its shape was the XML doc comment on
-- the store class, which a consumer had to transcribe by hand from IntelliSense.
--
-- THERE IS NO AUTO-CREATE PATH FOR THIS TABLE. The store issues no DDL at runtime — it only reads,
-- upserts and deletes — and PostgresMigrator is not a provisioning path (it executes migrations
-- from a CONSUMER-SUPPLIED assembly and embeds no schema of its own). Without this script, the
-- first projection checkpoint fails with `42P01: relation "projection_cursor_maps" does not exist`.
--
-- WHEN YOU NEED IT: whenever a projection resumes from per-stream positions rather than replaying
-- from zero. If nothing in your host resolves ICursorMapStore, you do not need this table.
--
-- DERIVED FROM the store's own SQL, column for column, rather than from any prior copy of this
-- schema:
--   read     PostgresCursorMapStore.cs:111-113   SELECT stream_id, position ... WHERE tenant_id = ... AND projection_name = ...
--   upsert   PostgresCursorMapStore.cs:148-153   INSERT (tenant_id, projection_name, stream_id, position) ... ON CONFLICT (tenant_id, projection_name, stream_id) DO UPDATE
--   reset    PostgresCursorMapStore.cs:176-178   DELETE ... WHERE tenant_id = ... AND projection_name = ...
--
-- OBJECT NAMES: unlike the event store and snapshot tables, this one is NOT configurable. The store
-- names `projection_cursor_maps` as a literal in every statement, unqualified, so it resolves
-- through the connection's search_path. This script creates it in `public`, which is where a
-- default search_path finds it. If your deployment uses a different search_path, create it in the
-- schema that path resolves first.
--
-- RE-RUNNABLE: guarded with IF NOT EXISTS. It does not alter an existing table.
--
-- IF YOU ALREADY CREATED THIS TABLE BY HAND from the store's doc comment, the guard below sees it
-- and skips the definition — this script is not an upgrade path. Compare your columns against the
-- ones below before assuming you are current; in particular confirm the primary key carries all
-- three of (tenant_id, projection_name, stream_id), for the reason given under the constraint.

CREATE SCHEMA IF NOT EXISTS "public";

-- ---------------------------------------------------------------------------------------
-- projection_cursor_maps — one row per (tenant, projection, stream) recording how far that
-- projection has consumed that stream.
--
--   tenant_id is NOT NULL, with no DEFAULT, matching events and event_store_snapshots.
--
--   NOT NULL, because the store ALWAYS binds this column: every statement routes through
--   KeyedTenantPartition, which yields the resolved tenant when scoped and the reserved
--   '__untenanted__' sentinel when not, so a statement carrying no tenant term is unconstructable
--   (PostgresCursorMapStore.cs:46-47, :101-102). "Untenanted" is a value here, not a missing one.
--
--   NO DEFAULT, because a defaulted tenant column makes "this row is deliberately untenanted"
--   indistinguishable from "the writer forgot the tenant." Without one, such a statement fails
--   outright instead of silently landing the row in the untenanted partition. The store binds the
--   value explicitly on every path, so the default would never legitimately fire.
--
--   VARCHAR(64) is the narrowest tenant column across every shipped provider, and the framework
--   refuses a longer identifier where it is constructed. Fixing every provider at that width is the
--   only choice that cannot truncate — and truncation here would be a KEY MERGE, not a lossy label,
--   for the reason given under the constraint.
--
--   position is BIGINT and NOT NULL. It is a stream position, not a timestamp: the store reads it
--   straight into a long and hands it to the projector as the resume point.
-- ---------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.projection_cursor_maps (
    tenant_id       VARCHAR(64)  NOT NULL,
    projection_name VARCHAR(256) NOT NULL,
    stream_id       VARCHAR(256) NOT NULL,
    position        BIGINT       NOT NULL,

    -- All three columns, and the order matters less than the membership.
    --
    -- THE TENANT TERM IS PART OF CURSOR IDENTITY, not merely a read filter. Keyed on
    -- (projection_name, stream_id) alone, two tenants running the same projection over the same
    -- stream share ONE row: a position advanced by the first makes the second's projector resume
    -- past events it never processed. The result is data permanently missing from a read model,
    -- with no error and nothing to alert on — the store's own remarks call this out at
    -- PostgresCursorMapStore.cs:93-98.
    --
    -- IT IS ALSO WHAT MAKES THE UPSERT LEGAL. The save path says
    -- ON CONFLICT (tenant_id, projection_name, stream_id), and PostgreSQL requires a unique or
    -- exclusion constraint matching that exact column set. Drop a column from this key, or replace
    -- it with a non-unique index, and every save fails with `42P10: there is no unique or exclusion
    -- constraint matching the ON CONFLICT specification`. This constraint is load-bearing twice.
    CONSTRAINT pk_projection_cursor_maps PRIMARY KEY (tenant_id, projection_name, stream_id)
);

-- No secondary index. The read path selects on (tenant_id, projection_name) and the reset path
-- deletes on the same pair — both are a leading-column prefix of the primary key, which serves them
-- directly. An extra index here would be write cost for a lookup the key already covers.
