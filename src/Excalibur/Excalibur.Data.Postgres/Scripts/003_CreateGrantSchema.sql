-- PostgreSQL Schema for Excalibur.Data.Postgres — AUTHORIZATION GRANTS
-- Version: 1.0
--
-- Creates the two tables PostgresGrantStore reads and writes: authz."grant", the grants in force, and
-- authz.grant_history, the record of every grant that was revoked. This provider never creates them at
-- runtime: run this script against the target database before registering the store. Without it, every
-- grant operation fails with 'relation "authz.grant" does not exist'.
--
-- "grant" is a reserved word in PostgreSQL, so the table name is quoted wherever it is declared.
--
-- Every statement is guarded, so the script is safe to re-run.

CREATE SCHEMA IF NOT EXISTS "authz";

CREATE TABLE IF NOT EXISTS "authz"."grant" (
    -- The widths match the SQL Server script, which is bound by a 900-byte key limit, so both providers
    -- accept exactly the same values. The store compares these four columns byte-wise (COLLATE "C"), so
    -- 'Admin' and 'admin' are different grants whatever the database's default collation is.
    user_id      VARCHAR(128)  NOT NULL,
    -- Never null: an untenanted grant is stored under the reserved value below, so "no tenant" is one
    -- explicit value rather than a missing one.
    tenant_id    VARCHAR(64)   NOT NULL DEFAULT '__untenanted__',
    grant_type   VARCHAR(64)   NOT NULL,
    qualifier    VARCHAR(192)  NOT NULL,
    full_name    VARCHAR(256)  NULL,
    -- timestamptz, not timestamp: expiry is compared with now(), and a timestamp without a zone would be
    -- read in the session's time zone, putting every expiry off by the session's offset from UTC.
    expires_on   TIMESTAMPTZ   NULL,
    granted_by   VARCHAR(128)  NOT NULL,
    granted_on   TIMESTAMPTZ   NOT NULL,
    -- One row is one grant, identified by (user, tenant, type, qualifier). Saving a grant that already
    -- exists replaces it. The user comes first because the authorization check reads a user's grants by
    -- user alone; tenant isolation is a property of each statement's predicate, not of key order.
    PRIMARY KEY (user_id, tenant_id, grant_type, qualifier)
);

CREATE TABLE IF NOT EXISTS "authz"."grant_history" (
    -- A surrogate key: history is append-only, and one grant may be revoked, re-granted and revoked again,
    -- so the four identity columns repeat here and cannot be the key.
    history_id   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id      VARCHAR(128)  NOT NULL,
    tenant_id    VARCHAR(64)   NOT NULL,
    grant_type   VARCHAR(64)   NOT NULL,
    qualifier    VARCHAR(192)  NOT NULL,
    full_name    VARCHAR(256)  NULL,
    expires_on   TIMESTAMPTZ   NULL,
    granted_by   VARCHAR(128)  NOT NULL,
    granted_on   TIMESTAMPTZ   NOT NULL,
    revoked_by   VARCHAR(128)  NULL,
    revoked_on   TIMESTAMPTZ   NULL
);

CREATE INDEX IF NOT EXISTS ix_grant_history_grant
    ON "authz"."grant_history" (user_id, tenant_id, grant_type, qualifier);
