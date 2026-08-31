-- SQLite SCHEMA for Excalibur.EventSourcing.Sqlite — EVENT STORE + SNAPSHOT STORE
-- Version: 1.0
--
-- Creates the two tables this package writes to: the event store ("Events") and the snapshot
-- store ("Snapshots").
--
-- WHY THIS FILE EXISTS, GIVEN THE STORES AUTO-CREATE
-- -------------------------------------------------
-- SqliteTableInitializer issues CREATE TABLE IF NOT EXISTS on first use, so a process that may
-- create tables never needs this script. It exists for the deployment that MAY NOT: a database
-- file provisioned ahead of time, shipped read-only, built by a migration tool that owns schema
-- centrally, or reviewed by someone who has to read the schema before agreeing to run it.
-- Auto-creation is a convenience. It is not a substitute for schema a consumer can obtain,
-- inspect and apply themselves.
--
-- This script is DERIVED FROM THAT AUTO-CREATE PATH and is intended to be indistinguishable from
-- it, so that a database provisioned either way has the same shape:
--   Events    — SqliteTableInitializer.cs (EventsTableDdl)
--   Snapshots — SqliteTableInitializer.cs (SnapshotsTableDdl)
-- If you change one, change the other. The store's own comments on EventsTableDdl and
-- SnapshotsTableDdl make the same point about their create and rebuild paths, for the same reason:
-- a schema reproduced in two places drifts silently, and the drift only surfaces as a query that
-- worked yesterday.
--
-- OBJECT NAMES
-- ------------
-- Both table names are constructor parameters — SqliteEventStore's constructor defaults to
-- "Events", SqliteSnapshotStore's constructor defaults to "Snapshots". This script targets those
-- DEFAULTS. A
-- deployment that passes different names should rename the objects below to match; nothing else
-- changes. SQLite has no schema namespace, so there is no schema to qualify.
--
-- RE-RUNNABLE. Every statement is IF NOT EXISTS, so running this twice, or against a database the
-- stores already created, changes nothing.
--
-- WHAT THIS SCRIPT CANNOT DO
-- --------------------------
-- CREATE TABLE IF NOT EXISTS does not touch an EXISTING table. Against a database created by an
-- older version of this package, this script is a no-op and will NOT bring the table up to the
-- current shape — it will run clean, report nothing, and leave the table exactly as it was. So a
-- database provisioned before the tenant column existed still fails on the first append with "no
-- such column: TenantId" after re-running this file.
--
-- There are two upgrade paths for that database, and which one applies is a deployment question:
--
--   * Running the package with table-creation rights. SqliteTableInitializer reconciles an existing
--     events table and an existing snapshots table at startup, in both cases rebuilding onto the
--     tenant-scoped shape and converging untenanted rows.
--
--   * 002_MakeEventAndSnapshotIdentityTenantScoped.sql, for the deployment that provisions its
--     schema separately — a migration tool that owns schema centrally, or a database reviewed and
--     built before the application touches it. That is the deployment this file exists for, so
--     "run the package once" is not an answer for it.
--
-- This script deliberately does not try to reproduce that migration itself: it is the CREATE, and
-- mixing an upgrade into it would make the two indistinguishable to a reader deciding which to run.

-- ---------------------------------------------------------------------------------------
-- 1) Events — the append-only event store.
--
--    GlobalPosition is INTEGER PRIMARY KEY AUTOINCREMENT: in SQLite that aliases the rowid, and
--    AUTOINCREMENT additionally forbids REUSE of a deleted row's value. That is load-bearing for
--    an event store read by global position — a reader that has consumed up to position N must
--    never see a NEW event appear at a position below N, which is exactly what rowid reuse would
--    produce after a delete.
--
--    EventData and Metadata are BLOB because the configured serializer may be binary (MemoryPack
--    is a package dependency, not only JSON). Metadata is nullable: an event carrying no metadata
--    stores NULL rather than an empty payload.
--
--    EventData is nullable too, and that is the erasure tombstone shape rather than an oversight.
--    Erasing an event does not delete its row -- it nulls the payload, overwrites the event type
--    with the reserved erased marker and keeps Position and Version exactly where they were, so
--    versions stay contiguous and replay of a partially-erased aggregate does not hit a hole. A
--    NOT NULL column makes that UPDATE fail at the engine, which is no erase at all rather than a
--    partial one, so the constraint is omitted here for the same reason the SQL Server, PostgreSQL
--    and Oracle schemas omit it. This provider does not implement erasure today; the column shape
--    is what keeps it available without a table rebuild if it ever does.
--
--    TenantId is TEXT NOT NULL and carries the reserved '__untenanted__' sentinel for rows that
--    belong to no tenant, matching Snapshots below. NOT NULL is load-bearing, not stylistic:
--    SQLite treats NULLs as DISTINCT in a UNIQUE constraint, so a nullable tenant column would
--    never conflict and two writers appending the same version for the same untenanted aggregate
--    would both succeed — optimistic concurrency silently gone for exactly the rows a pre-tenancy
--    database is made of. NO DEFAULT, matching the PostgreSQL and Oracle schemas: TenantId
--    participates in the UNIQUE key, and a key column is not defaulted. The store binds the value
--    explicitly on every insert.
--
--    UNIQUE(AggregateId, AggregateType, Version, TenantId) is the optimistic-concurrency control,
--    and the tenant participates in stream IDENTITY, not merely in read filters. Two writers
--    racing to append version N to the same aggregate FOR THE SAME TENANT do not both succeed; the
--    loser gets a constraint violation, which is how the store detects a concurrent modification.
--    Without the tenant term, two tenants sharing a natural aggregate id (an order number, a
--    customer reference — routine, not exotic) collide: tenant B's version probe reports -1 ("does
--    not exist") while an append of version 0 hits tenant A's row and fails as a duplicate — a
--    conflict that never converges on retry, because the probe keeps reporting -1. This is the
--    same shape PostgreSQL converges to in 005_MakeEventStreamIdentityTenantScoped.sql.
-- ---------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS [Events] (
    GlobalPosition INTEGER PRIMARY KEY AUTOINCREMENT,
    EventId TEXT NOT NULL,
    AggregateId TEXT NOT NULL,
    AggregateType TEXT NOT NULL,
    EventType TEXT NOT NULL,
    EventData BLOB,
    Metadata BLOB,
    Version INTEGER NOT NULL,
    Timestamp TEXT NOT NULL,
    TenantId TEXT NOT NULL,
    UNIQUE(AggregateId, AggregateType, Version, TenantId)
);

-- This index covers a strict PREFIX of the UNIQUE constraint above (three of its four columns,
-- in the same order) — which SQLite already backs with an implicit index, so this one is
-- redundant for that prefix. It is reproduced here ANYWAY because the auto-create path creates it
-- (SqliteTableInitializer.EventsTableDdl), matching PostgreSQL's own idx_events_aggregate, which
-- likewise was not widened to include tenant_id when 005_MakeEventStreamIdentityTenantScoped.sql
-- added the tenant term to that provider's key. A script that "improved" on the store by omitting
-- it would make script-provisioned and auto-provisioned databases structurally different, which is
-- the drift this file exists to prevent. Removing it is a change to make in the store first, and
-- then here.
CREATE INDEX IF NOT EXISTS IX_Events_AggregateId
    ON [Events] (AggregateId, AggregateType, Version);

-- ---------------------------------------------------------------------------------------
-- 2) Snapshots — the latest materialized aggregate state, one row per (aggregate, tenant).
--
--    TenantId is TEXT NOT NULL and carries the reserved '__untenanted__' sentinel for rows that
--    belong to no tenant. Both halves of that are load-bearing and neither is stylistic:
--
--    NOT NULL, because SQLite treats NULLs as DISTINCT in a UNIQUE constraint. A nullable tenant
--    column would mean two untenanted rows for the same aggregate never conflict, so every save
--    would INSERT a new row instead of updating the existing snapshot — an unbounded table and a
--    snapshot read that has to guess which row is current.
--
--    A sentinel VALUE rather than the absence of one, because "this row is deliberately global"
--    and "whoever wrote this row forgot the tenant" must not be the same bit pattern. The
--    sentinel is collision-proof: the framework's Scoped() rejects it, so no real tenant can
--    claim the untenanted partition.
--
--    NO DEFAULT, matching the PostgreSQL and Oracle schemas in this framework. TenantId
--    participates in the UNIQUE key, and a key column is not defaulted: with a default, an INSERT
--    that omitted the tenant would land silently in the untenanted partition and re-introduce
--    exactly the ambiguity the sentinel removes. The store binds the value explicitly on every
--    save.
--
--    The sentinel is spelled '__untenanted__' rather than '' because the empty string is not
--    portable — Oracle stores it as NULL, so the identical intent becomes a different value on
--    that provider.
-- ---------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS [Snapshots] (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SnapshotId TEXT NOT NULL,
    AggregateId TEXT NOT NULL,
    AggregateType TEXT NOT NULL,
    Version INTEGER NOT NULL,
    Data BLOB NOT NULL,
    CreatedAt TEXT NOT NULL,
    TenantId TEXT NOT NULL,
    UNIQUE(AggregateId, AggregateType, TenantId)
);
