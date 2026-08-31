-- SQLite MIGRATION for Excalibur.EventSourcing.Sqlite — TENANT-SCOPED STREAM AND SNAPSHOT IDENTITY
-- Version: 1.0
--
-- Brings an EXISTING database — one provisioned by an earlier version of
-- 001_CreateEventStoreSchema.sql, or written by an earlier version of this package — onto the shape
-- 001 now creates. After this script a stream is identified by
-- (AggregateId, AggregateType, Version, TenantId) rather than by the first three alone, and a
-- snapshot by (AggregateId, AggregateType, TenantId).
--
-- BEFORE: [Events] has no TenantId, and its uniqueness key is the three-column
--         (AggregateId, AggregateType, Version). [Snapshots] may likewise predate the tenant column.
-- AFTER:  Both tables carry TenantId TEXT NOT NULL, both keys include it, and every pre-existing row
--         holds the reserved '__untenanted__' sentinel.
--
--
-- WHY A SEPARATE SCRIPT IS NEEDED AT ALL
-- --------------------------------------
-- 001 is CREATE TABLE IF NOT EXISTS from top to bottom. Against a database that already has these
-- tables it is a no-op: it will not add the column and cannot alter the key. So a consumer who
-- provisioned from the earlier 001 and re-runs it gets a clean, silent, green run that changes
-- nothing, and then fails on the first append with "no such column: TenantId". This script is the
-- upgrade path for exactly that consumer.
--
-- A consumer whose application runs this package WITH table-creation rights does not need this
-- script — SqliteTableInitializer performs the same reconciliation at startup. This script exists
-- for the deployment where schema is owned centrally by a migration tool, or where the database is
-- provisioned and reviewed ahead of the application ever touching it.
--
--
-- WHY THE TABLES ARE REBUILT RATHER THAN ALTERED
-- ----------------------------------------------
-- SQLite's ALTER TABLE cannot express either half of this change:
--
--   * There is no ADD CONSTRAINT. A table-level UNIQUE can only be declared in CREATE TABLE, so the
--     key cannot be widened in place. A separate CREATE UNIQUE INDEX would enforce the same tuple,
--     but it would leave the table's own declaration disagreeing with the constraint that governs
--     it, and it would not close the NULL hole below.
--
--   * ADD COLUMN cannot add a NOT NULL column without a DEFAULT to a table that already has rows —
--     SQLite rejects it with "Cannot add a NOT NULL column with default value NULL". And TenantId
--     deliberately has NO DEFAULT (001 explains why: it is part of the key, and a key column is not
--     defaulted, or an insert that omitted the tenant would land silently in the untenanted
--     partition). Adding it nullable instead is not a smaller step, it is a WORSE one: SQLite
--     treats NULLs as DISTINCT in a UNIQUE constraint, so a nullable tenant column would never
--     conflict, and two writers appending the same version for the same untenanted aggregate would
--     both succeed — optimistic concurrency silently gone for exactly the rows a pre-tenancy
--     database is made of.
--
-- So each table is rebuilt: rename the existing table aside, create the current shape under the
-- original name, copy every row across stamping the sentinel, and leave the renamed original in
-- place as a verifiable backup. This is the same rebuild SqliteTableInitializer performs at runtime
-- (EventsTableDdl / RebuildEventsTableWithTenantAsync, SnapshotsTableDdl /
-- RebuildSnapshotsTableWithTenantAsync), reproduced here so a database upgraded either way ends in
-- the same shape. If you change one, change the other — a schema reproduced in two places drifts
-- silently, and the drift only surfaces as a query that worked yesterday.
--
--
-- THE SENTINEL, AND WHAT THIS SCRIPT DELIBERATELY DOES NOT DO
-- -----------------------------------------------------------
-- Every carried-over row is stamped '__untenanted__' — the reserved key for rows that belong to no
-- tenant, spelled that way rather than '' because the empty string is not portable (Oracle stores it
-- as NULL, so the identical intent becomes a different value on that provider). Stamping one single
-- value is collision-free by construction: the previous key already constrained
-- (AggregateId, AggregateType, Version), so adding one constant fourth column keeps the resulting
-- 4-tuple unique. The same argument holds for snapshots on the triple.
--
-- This script does NOT move those rows onto a single-tenant host's default tenant identity. Whether
-- that is correct depends on the DEPLOYMENT MODE — in a multi-tenant deployment the untenanted
-- partition is a live partition holding rows that genuinely belong to no tenant, and moving them
-- would put them in the default tenant's data. SQL cannot see how the host is configured, so the
-- store performs that convergence at startup, gated on the mode. This script stops at the shape.
--
--
-- HOW TO RUN IT — AND THE ONE THING THAT WILL BITE YOU
-- ----------------------------------------------------
-- Run it with the application stopped, against a database you have backed up and restored at least
-- once. It rebuilds the system of record.
--
-- APPLY IT WITH A RUNNER THAT STOPS ON THE FIRST ERROR. Every migration tool does, and so does every
-- driver (a failed statement throws). The sqlite3 command-line shell does NOT by default: piping
-- this file into a bare `sqlite3 app.db` prints the refusal below and then keeps going. Use:
--
--     sqlite3 -bail app.db < 002_MakeEventAndSnapshotIdentityTenantScoped.sql
--
-- The whole script is one transaction, and each guard below is written OR ROLLBACK, so a refusal
-- undoes the transaction rather than leaving it half-applied.
--
--
-- RUNNING IT TWICE
-- ----------------
-- Two independent refusals, and it is worth knowing which is which:
--
--   1. A precondition check, first in each section, fails when the table already carries TenantId,
--      and rolls the transaction back. This is the one that produces a readable diagnostic.
--
--   2. The rename target is occupied. Once this script has run, [Events_before_tenant_upgrade] and
--      [Snapshots_before_tenant_upgrade] exist, so the rename that begins each rebuild cannot
--      succeed a second time. This one holds even under a runner that ignored the first.
--
-- The backup tables are LEFT IN PLACE on purpose. Verify the migrated data, then drop them:
--
--     DROP TABLE [Events_before_tenant_upgrade];
--     DROP TABLE [Snapshots_before_tenant_upgrade];
--
-- Dropping them reclaims the space and removes the second refusal above, so do it once you are
-- satisfied and not before.
--
-- If a run under a runner that ignores errors got past BOTH refusals — the backups had already been
-- dropped AND the precondition was skipped — the copy guards below still refuse to stamp the
-- sentinel over live tenant values, so no data is rewritten. What you are left with is a swap that
-- did not finish: an empty [Events] and your rows still in [Events_before_tenant_upgrade]. Recover
-- by putting them back, then re-read the paragraph above:
--
--     DROP TABLE [Events];
--     ALTER TABLE [Events_before_tenant_upgrade] RENAME TO [Events];
--
--
-- OBJECT NAMES
-- ------------
-- This script targets the constructor DEFAULTS, "Events" and "Snapshots", as 001 does. A deployment
-- that passed different table names should rename the objects below to match; nothing else changes.
-- SQLite has no schema namespace, so there is nothing to qualify. A deployment that never had a
-- [Snapshots] table can delete section 2 — the script addresses each table independently.

BEGIN IMMEDIATE;

-- ---------------------------------------------------------------------------------------
-- 1) Events
-- ---------------------------------------------------------------------------------------

-- PRECONDITION. Refuses when [Events] already carries TenantId, rather than rebuilding a
-- tenant-scoped table and re-stamping every row back to the untenanted sentinel — which would
-- discard real tenant assignments silently. A CHECK constraint carries the branch because SQLite's
-- SQL has no procedural IF: the count of matching columns is inserted, and a non-zero count
-- violates the constraint. OR ROLLBACK rather than the default ABORT so the refusal undoes the
-- transaction instead of merely failing the statement.
DROP TABLE IF EXISTS [Events_tenant_upgrade_precondition];
CREATE TEMP TABLE [Events_tenant_upgrade_precondition] (
    already_tenant_scoped INTEGER NOT NULL CHECK (already_tenant_scoped = 0),
    rows_carried_over     INTEGER
);
INSERT OR ROLLBACK INTO [Events_tenant_upgrade_precondition] (already_tenant_scoped)
    SELECT COUNT(*) FROM pragma_table_info('Events') WHERE name = 'TenantId';

-- Move the existing table aside. This is what makes a second run impossible once the script has
-- been applied and the backup has not yet been dropped.
ALTER TABLE [Events] RENAME TO [Events_before_tenant_upgrade];

-- The index follows the table across a rename, keeping its original NAME, so it now belongs to the
-- backup table and would make the CREATE INDEX at the end of this section a no-op — leaving the
-- rebuilt table with no index and nothing reporting it. Dropped here rather than left in place: the
-- backup is read by a human verifying a migration, not by the store.
DROP INDEX IF EXISTS IX_Events_AggregateId;

-- The current shape. Reproduced from 001 and from SqliteTableInitializer.EventsTableDdl; see the
-- header on keeping the three in step.
CREATE TABLE [Events] (
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

-- GlobalPosition is copied EXPLICITLY rather than left for the new table to assign. It is the global
-- stream order handed back to callers and compared across processes, so a reader that has consumed
-- up to position N must never see a new event appear below N. Copying the value also advances
-- SQLite's AUTOINCREMENT high-water mark to the largest position carried over — an explicit rowid
-- insert into an AUTOINCREMENT table advances sqlite_sequence like any other insert — so the next
-- append lands strictly above every preserved row. Re-numbering here would break both properties.
-- The WHERE restates the precondition at the point where it does damage. Stamping the sentinel is
-- only correct for a source that predates the tenant column; against one that already carries it,
-- this statement would overwrite real tenant assignments with the untenanted key and no constraint
-- would object. Guarding here means a stray run that got past the precondition — because the backup
-- tables had already been dropped and the runner did not stop — copies nothing and trips the
-- completeness check below, loudly, instead of quietly flattening the tenants.
INSERT INTO [Events]
    (GlobalPosition, EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp, TenantId)
    SELECT GlobalPosition, EventId, AggregateId, AggregateType, EventType, EventData, Metadata, Version, Timestamp, '__untenanted__'
    FROM [Events_before_tenant_upgrade]
    WHERE (SELECT COUNT(*) FROM pragma_table_info('Events_before_tenant_upgrade')
           WHERE name = 'TenantId') = 0;

-- The copy must be total. A rebuild that carried a subset would leave a database that reads as
-- healthy while missing history, so the row counts are compared and a mismatch rolls the whole
-- transaction back rather than committing a partial event store.
INSERT OR ROLLBACK INTO [Events_tenant_upgrade_precondition] (already_tenant_scoped, rows_carried_over)
    SELECT CASE WHEN (SELECT COUNT(*) FROM [Events])
                   = (SELECT COUNT(*) FROM [Events_before_tenant_upgrade])
                THEN 0 ELSE 1 END,
           (SELECT COUNT(*) FROM [Events]);

CREATE INDEX IF NOT EXISTS IX_Events_AggregateId
    ON [Events] (AggregateId, AggregateType, Version);

-- ---------------------------------------------------------------------------------------
-- 2) Snapshots
--
--    Delete this section if the deployment has no [Snapshots] table.
--
--    Unlike [Events], the shipped 001 has always declared this table with TenantId — so this
--    section is not for a database provisioned from a published script. It is for one written by a
--    version of this package that predates tenant-aware snapshots, whose table the store created
--    itself with the older shape.
-- ---------------------------------------------------------------------------------------

DROP TABLE IF EXISTS [Snapshots_tenant_upgrade_precondition];
CREATE TEMP TABLE [Snapshots_tenant_upgrade_precondition] (
    already_tenant_scoped INTEGER NOT NULL CHECK (already_tenant_scoped = 0),
    rows_carried_over     INTEGER
);
INSERT OR ROLLBACK INTO [Snapshots_tenant_upgrade_precondition] (already_tenant_scoped)
    SELECT COUNT(*) FROM pragma_table_info('Snapshots') WHERE name = 'TenantId';

ALTER TABLE [Snapshots] RENAME TO [Snapshots_before_tenant_upgrade];

-- Reproduced from 001 and from SqliteTableInitializer.SnapshotsTableDdl.
CREATE TABLE [Snapshots] (
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

-- Id is NOT copied, and the asymmetry with Events above is deliberate rather than an oversight: it
-- is a surrogate no query this store issues ever reads, so letting the rebuilt table assign fresh
-- values costs nothing. GlobalPosition is the opposite, and is copied.
-- Guarded for the same reason as the events copy above.
INSERT INTO [Snapshots]
    (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, TenantId)
    SELECT SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, '__untenanted__'
    FROM [Snapshots_before_tenant_upgrade]
    WHERE (SELECT COUNT(*) FROM pragma_table_info('Snapshots_before_tenant_upgrade')
           WHERE name = 'TenantId') = 0;

INSERT OR ROLLBACK INTO [Snapshots_tenant_upgrade_precondition] (already_tenant_scoped, rows_carried_over)
    SELECT CASE WHEN (SELECT COUNT(*) FROM [Snapshots])
                   = (SELECT COUNT(*) FROM [Snapshots_before_tenant_upgrade])
                THEN 0 ELSE 1 END,
           (SELECT COUNT(*) FROM [Snapshots]);

COMMIT;
