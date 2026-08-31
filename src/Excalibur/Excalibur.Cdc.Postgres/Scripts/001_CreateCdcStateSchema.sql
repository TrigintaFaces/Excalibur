-- PostgreSQL SCHEMA for Excalibur.Cdc.Postgres — CDC PROCESSOR STATE
-- Version: 1.0
--
-- Creates the table that records how far each CDC processor has read, so a processor restarting
-- resumes from its last committed position instead of replaying from the start of the log.
--
-- DERIVED FROM the auto-create path this script mirrors:
--   PostgresCdcStateStore.cs:400-414 (schema, table and index)
-- The store issues that DDL on first use. This file exists for the deployment that runs without
-- table-creation rights, or that provisions schema centrally. Keep the two in step.
--
-- WHY OBTAINABLE DDL MATTERS PARTICULARLY HERE
-- --------------------------------------------
-- This is recovery-shaped state. A processor that cannot persist its position does not fail at
-- startup with a clear complaint about a missing table — it fails at the moment it first tries to
-- record progress, which is the moment you most need it to work, and the visible symptom is
-- replay from the start of the retained WAL rather than an obvious error.
--
-- OBJECT NAMES: this script targets the DEFAULTS —
--   schema "excalibur"  (PostgresCdcStateStoreOptions.cs:17)
--   table  "cdc_state"  (PostgresCdcStateStoreOptions.cs:21)
-- A deployment that configures different names should rename below to match — INCLUDING the
-- constraint and index names, which the store derives from the table name (pk_<table>,
-- ix_<table>_updated_at) rather than fixing.
--
-- RE-RUNNABLE: every statement is IF NOT EXISTS. Running it twice changes nothing.
--
-- CONCURRENCY, worth knowing before scripting this into a parallel deploy: PostgreSQL DDL of this
-- shape is NOT concurrency-safe. Racing CREATE TABLE IF NOT EXISTS statements collide on internal
-- catalog inserts (23505 on pg_type_typname_nsp_index) rather than one of them quietly winning.
-- The store serializes its own first-time provisioning with a lock for exactly this reason
-- (PostgresCdcStateStore.cs:385). Run this script ONCE, not from every node at once.
--
-- WHAT THIS SCRIPT CANNOT DO: it does not alter an EXISTING table, so against a database
-- provisioned by an older version it is a no-op rather than an upgrade.

CREATE SCHEMA IF NOT EXISTS "excalibur";

-- ---------------------------------------------------------------------------------------
-- cdc_state — one row per (processor, replication slot, table).
--
--   The PRIMARY KEY is the COMPOSITE (processor_id, slot_name, table_name). All three parts are
--   needed: one processor may follow several tables through one slot, and one slot may be read by
--   distinct processors. This key is what makes a position write idempotent — a processor
--   checkpointing repeatedly updates its own row rather than appending history nobody reads.
--
--   table_name is NOT NULL DEFAULT '' rather than nullable, and that is load-bearing: it is part
--   of the primary key, and PostgreSQL treats NULLs as distinct in a unique index. A nullable
--   member would mean a slot-wide row — one not scoped to a single table — never conflicts with
--   itself, so every checkpoint would INSERT instead of UPDATE and the resume position would be
--   ambiguous. The empty string gives "not scoped to one table" a single, comparable spelling.
--
--   position is VARCHAR(32), holding a PostgreSQL LSN in its TEXT form ('16/B374D848'), not a
--   number. An LSN is a two-part segment/offset value; text is what the store reads and writes,
--   and narrowing it to an integer type would lose the form the replication protocol speaks.
--
--   last_event_time is the one nullable column — a processor that has committed a position but
--   not yet seen a timestamped event has no honest value to record, and NULL says so.
--
--   NO tenant column, deliberately: CDC state describes the PROCESSOR's progress through a
--   replication slot, not tenant-owned data. The auto-create path declares none and no query
--   references one.
-- ---------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "excalibur"."cdc_state" (
    processor_id VARCHAR(255) NOT NULL,
    slot_name VARCHAR(255) NOT NULL,
    table_name VARCHAR(255) NOT NULL DEFAULT '',
    position VARCHAR(32) NOT NULL,
    last_event_time TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    event_count BIGINT NOT NULL DEFAULT 0,
    CONSTRAINT pk_cdc_state PRIMARY KEY (processor_id, slot_name, table_name)
);

-- Supports the "most recently updated state for this processor" read. It leads with processor_id
-- and descends on updated_at, so the newest row for a processor is the first the index yields.
-- The primary key cannot serve this: it leads with processor_id too, but then orders by slot_name
-- and table_name, so answering the same question from it means scanning every row the processor
-- owns and sorting them.
CREATE INDEX IF NOT EXISTS ix_cdc_state_updated_at
    ON "excalibur"."cdc_state" (processor_id, updated_at DESC);
