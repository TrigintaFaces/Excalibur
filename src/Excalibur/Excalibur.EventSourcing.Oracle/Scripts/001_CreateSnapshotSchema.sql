-- Oracle Schema for Excalibur.EventSourcing.Oracle — SNAPSHOT STORE
-- Version: 1.0
--
-- Creates the table required by the Oracle snapshot store. The store never creates this
-- table at runtime: run this script against the target schema before the first snapshot
-- is saved. Without it, every snapshot operation fails.
--
-- The table name is configurable. This script uses the default:
--
--     OracleSnapshotStoreOptions.Table = "EVENTSTORESNAPSHOTS"
--
-- If you override it, rename the objects below to match.

CREATE TABLE EVENTSTORESNAPSHOTS (
    SNAPSHOTID     VARCHAR2(255),
    AGGREGATEID    VARCHAR2(255) NOT NULL,
    AGGREGATETYPE  VARCHAR2(255) NOT NULL,
    VERSION        NUMBER(19) NOT NULL,
    -- BLOB, not RAW. RAW is capped at 2000 bytes; a serialized aggregate above that limit
    -- fails to write. BLOB has no such ceiling.
    DATA           BLOB NOT NULL,
    METADATA       BLOB,
    CREATEDAT      TIMESTAMP(7) WITH TIME ZONE NOT NULL,
    -- NOT NULL, carrying the reserved '__untenanted__' sentinel for a genuinely untenanted
    -- row. The tenant is a component of IDENTITY here, not an optional attribute, so it is
    -- never absent.
    --
    -- No DEFAULT, deliberately, matching the PostgreSQL schema. tenant_id is part of the
    -- unique key below, and you do not default a key column: with a default, an INSERT that
    -- omitted the tenant would silently land the row in the untenanted partition, making
    -- "I forgot to supply the tenant" indistinguishable from "this row is deliberately
    -- untenanted." Without one, that statement fails outright. The store writes the
    -- sentinel explicitly on every save.
    --
    -- An earlier revision made this column nullable because Oracle cannot store a
    -- zero-length string ('' IS NULL), and the empty-string sentinel the SQL Server and
    -- PostgreSQL schemas used was therefore inexpressible. That reasoning was sound and its
    -- conclusion was the wrong one: '' can never be the canonical representation precisely
    -- BECAUSE Oracle folds it to NULL. The reserved sentinel is a real, non-empty value, so
    -- it expresses identically on every provider and needs no per-provider translation.
    TENANTID       VARCHAR2(255) NOT NULL
);

-- One row per aggregate PER TENANT.
--
-- A plain unique index over the triple, matching the SQL Server and PostgreSQL schemas.
--
-- This was previously a FUNCTION-BASED index, NVL(TENANTID, CHR(1)), and the reasoning
-- behind it is worth recording because it explains why the column above changed:
--
--   * Oracle treats NULLs as DISTINCT in a unique index, so while the tenant was nullable
--     a plain UNIQUE (AGGREGATEID, AGGREGATETYPE, TENANTID) did not constrain untenanted
--     rows AT ALL — every untenanted save could insert another row for the same aggregate,
--     accumulating duplicate snapshots the read path never reconciles. The read issues a
--     single-row query, so the second row surfaced as a failure at load time, long after
--     the write that caused it.
--
--   * NVL mapped NULL onto CHR(1) — a control character no tenant identifier can contain —
--     to give untenanted rows a single collision-proof key.
--
-- That workaround compensated for the nullable column, not for anything about Oracle. With
-- TENANTID NOT NULL there are no NULLs to collapse, the distinct-NULL problem cannot arise,
-- and the function-based index is dead. It is removed rather than carried forward: a
-- sentinel invented to patch a schema hole must not outlive the hole.
--
-- One caveat from that revision still applies to the CALL SITE and must not be lost: a NVL
-- must never be applied to a bind variable, because NVL(:TenantId, '') returns NULL in
-- Oracle and a read path comparing that with = matches nothing. The requests bind the
-- sentinel as a literal value, so there is no NVL anywhere on the call path.
CREATE UNIQUE INDEX UQ_EVENTSTORESNAPSHOTS_AGG
    ON EVENTSTORESNAPSHOTS (AGGREGATEID, AGGREGATETYPE, TENANTID);

-- Reads locate the latest snapshot for an aggregate by version.
CREATE INDEX IX_EVENTSTORESNAPSHOTS_VERSION
    ON EVENTSTORESNAPSHOTS (AGGREGATEID, AGGREGATETYPE, VERSION);
