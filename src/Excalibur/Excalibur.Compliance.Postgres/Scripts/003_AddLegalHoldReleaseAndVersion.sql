-- PostgreSQL migration for Excalibur.Compliance.Postgres -- legal-hold optimistic concurrency
-- Version: 3.0
--
-- WHY THIS SCRIPT EXISTS AT ALL, which is the part worth reading.
--
-- The legal-hold store gained a `version` column so that releasing a hold can refuse to write over a hold
-- that moved under the writer: the release is a conditional UPDATE whose predicate names the version it
-- read, so two concurrent releases cannot both succeed and the loser is told rather than silently winning.
--
-- That column was added to the CREATE TABLE in 001 and to the store's own provisioning DDL. For a NEW
-- database that is sufficient. For a database that was already provisioned by an earlier version of this
-- package it is not, and the failure mode is the one that matters: the provisioning code is guarded by
-- `CREATE TABLE IF NOT EXISTS`, so on an existing database it sees the table, skips, and never adds the
-- column. The first release then fails on the CONSUMER'S database and never on ours -- the shipped tests
-- all run against a freshly created schema, where the column is always present.
--
-- So an upgrading consumer needs an ALTER, and that is this file.
--
-- RUN THIS ONCE, AFTER UPGRADING THE PACKAGE AND BEFORE STARTING THE APPLICATION.
--
-- RE-RUNNING IS SAFE, and it is safe by construction rather than by bookkeeping. `ADD COLUMN IF NOT EXISTS`
-- is idempotent in PostgreSQL, so a second run is a no-op. There is no flag to remember and no migration
-- table that can fall out of step with the schema it describes.
--
-- A NOTE ON THE DEFAULT. `NOT NULL DEFAULT 0` matches the CREATE TABLE in 001 exactly, and 0 is the right
-- value for a pre-existing row: it means "never released, never contended", which is true of every hold
-- that existed before this column did. Back-filling anything else would invent a history.
--
-- TABLE NAME. This script uses the DEFAULT schema and table name. A deployment that configured a different
-- table name through options must apply the same ALTER to that table instead -- the store reads the name
-- from configuration, and this script cannot.

ALTER TABLE "compliance"."legal_holds"
    ADD COLUMN IF NOT EXISTS version INT NOT NULL DEFAULT 0;

-- release_reason is included for completeness even though 001 already declares it: a database provisioned
-- by a build between the two changes can have the version column's sibling missing as well, and an
-- IF NOT EXISTS add costs nothing when it is already there.
ALTER TABLE "compliance"."legal_holds"
    ADD COLUMN IF NOT EXISTS release_reason VARCHAR(1000) NULL;
