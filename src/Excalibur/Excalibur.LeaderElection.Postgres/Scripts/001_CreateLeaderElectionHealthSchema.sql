-- PostgreSQL SCHEMA for Excalibur.LeaderElection.Postgres — HEALTH-BASED LEADER ELECTION
-- Version: 1.0
--
-- Creates the candidate-health table used by health-based leader election, so a leader is chosen
-- among candidates that are actually healthy rather than merely first to the lock.
--
-- SCOPE — READ THIS BEFORE ASSUMING YOU NEED IT
-- ---------------------------------------------
-- This table is used ONLY by the health-based election path (PostgresHealthBasedLeaderElection).
-- The package's other leader election path uses PostgreSQL ADVISORY LOCKS, which live in the
-- server's lock manager and require NO table at all. If you are not using the health-based
-- variant, you do not need to run this script and this table will stay empty.
--
-- DERIVED FROM the auto-create path this script mirrors:
--   PostgresHealthBasedLeaderElection.cs:264-272 (EnsureTableCreatedAsync)
-- The store issues that DDL on first use. This file exists for the deployment that runs without
-- table-creation rights, or that provisions schema centrally ahead of the application. Keep the
-- two in step; a schema written down twice drifts silently.
--
-- OBJECT NAMES: this script targets the DEFAULTS —
--   schema "public"                  (PostgresHealthBasedLeaderElectionOptions.cs:18)
--   table  "leader_election_health"  (PostgresHealthBasedLeaderElectionOptions.cs:25)
-- A deployment that configures different names should rename below to match.
--
-- RE-RUNNABLE: every statement is IF NOT EXISTS. Running it twice changes nothing.
--
-- WHAT THIS SCRIPT CANNOT DO: CREATE TABLE IF NOT EXISTS does not alter an EXISTING table, so
-- against a database provisioned by an older version this is a no-op rather than an upgrade.

CREATE SCHEMA IF NOT EXISTS "public";

-- ---------------------------------------------------------------------------------------
-- leader_election_health — one row per candidate process.
--
--   candidate_id is the PRIMARY KEY. The store's write path is a single INSERT ... ON CONFLICT
--   (candidate_id) DO UPDATE (PostgresHealthBasedLeaderElection.cs:292-296), so this key is what
--   makes a heartbeat idempotent: a candidate reporting health repeatedly updates its own row
--   instead of accumulating one row per heartbeat. Without the key the upsert has no conflict
--   target, and the statement does not merely slow down — it fails.
--
--   The DEFAULTs are meaningful here, unlike on a key column: they define what a row means before
--   the first health report lands. A candidate defaults to HEALTHY (is_healthy TRUE, health_score
--   1.0) and NOT leader. Defaulting is_healthy to FALSE would be the more cautious-looking choice
--   and the wrong one — it would make a freshly-inserted candidate ineligible until its second
--   write, which on a cold start is every candidate at once.
--
--   last_updated is TIMESTAMPTZ, not TIMESTAMP. This is a liveness signal compared across
--   processes that need not share a timezone, and a naive timestamp would make staleness
--   arithmetic depend on where each candidate happens to run.
--
--   metadata_json is the one nullable column: a candidate reporting no metadata stores NULL
--   rather than an empty document.
--
--   NO tenant column, deliberately. Leader election coordinates PROCESSES, not tenant-owned data
--   — there is no per-tenant leader here, the auto-create path declares no such column, and no
--   query references one. Adding one to match other tables in this framework would produce a
--   column nothing reads and nothing writes.
-- ---------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "public"."leader_election_health" (
    candidate_id VARCHAR(256) NOT NULL PRIMARY KEY,
    is_healthy BOOLEAN NOT NULL DEFAULT TRUE,
    health_score DOUBLE PRECISION NOT NULL DEFAULT 1.0,
    last_updated TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_leader BOOLEAN NOT NULL DEFAULT FALSE,
    metadata_json TEXT NULL
);
