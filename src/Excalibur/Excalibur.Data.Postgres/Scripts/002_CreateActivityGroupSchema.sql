-- PostgreSQL Schema for Excalibur.Data.Postgres — AUTHORIZATION ACTIVITY GROUPS
-- Version: 1.0
--
-- Creates the table PostgresActivityGroupStore reads and writes. This provider never creates the
-- table at runtime: run this script against the target database before registering the store.
-- Without it, every activity-group operation fails with 'relation "authz.activity_group" does not
-- exist'.
--
-- WHAT THIS SCRIPT DOES NOT COVER. It creates the activity-group table only. The grant tables the
-- authorization stores also use (authz.grant, authz.grant_history) are not created here; they are
-- provisioned separately.
--
-- Every statement is guarded, so the script is safe to re-run.

CREATE SCHEMA IF NOT EXISTS "authz";

CREATE TABLE IF NOT EXISTS "authz"."activity_group" (
    -- The tenant the group belongs to. Never null: an untenanted group is stored under the reserved
    -- value below, so "no tenant" is one explicit value rather than a missing one.
    tenant_id      VARCHAR(64)   NOT NULL DEFAULT '__untenanted__',
    -- The group's name. Unique per tenant, not globally: two tenants may each have a group with the
    -- same name, and those are distinct groups.
    name           VARCHAR(128)  NOT NULL,
    -- One activity the group confers. A group is a set of activities, stored one row per member.
    activity_name  VARCHAR(256)  NOT NULL,
    -- One row is one (tenant, group, activity) triple, and the key is that triple. The widths match
    -- the SQL Server script, which is bound by a 900-byte key limit, so both providers accept exactly
    -- the same names. The tenant is part of the key rather than merely a column: leaving it out would
    -- let two tenants' groups of the same name collide, so one tenant could read a row another wrote.
    PRIMARY KEY (tenant_id, name, activity_name)
);
