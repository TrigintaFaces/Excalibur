-- PostgreSQL Audit Store Provisioning Script
-- Creates the audit schema and tables for Excalibur.AuditLogging.Postgres
-- Provides tamper-evident hash-chain audit logging
--
-- Idempotent: every statement guards with IF NOT EXISTS, so the script may be re-run.
--
-- The identifiers below are the provider defaults (PostgresAuditOptions: schema "audit",
-- table "audit_events"). If you override SchemaName or TableName, apply the same overrides here.

CREATE SCHEMA IF NOT EXISTS "audit";

-- Main audit events table.
-- Column names and types mirror exactly what PostgresAuditStore reads and writes; the store issues
-- snake_case columns and aliases them back to PascalCase on read.
CREATE TABLE IF NOT EXISTS "audit"."audit_events" (
    -- Identity and ordering. sequence_number is generated and returned by the INSERT; it is the
    -- chain's ordering key, which is why verification and paging both sort on it.
    sequence_number         BIGSERIAL   NOT NULL,
    event_id                TEXT        NOT NULL,

    -- Event classification
    event_type              INTEGER     NOT NULL,
    action                  TEXT        NOT NULL,
    outcome                 INTEGER     NOT NULL,
    timestamp               TIMESTAMPTZ NOT NULL,

    -- Actor
    actor_id                TEXT        NULL,
    actor_type              TEXT        NULL,

    -- Resource
    resource_id             TEXT        NULL,
    resource_type           TEXT        NULL,
    resource_classification INTEGER     NULL,

    -- Tenancy. NOT NULL with the untenanted sentinel as the default so the tenant predicate can
    -- never meet a NULL: a nullable tenant column makes `tenant_id = @TenantId` fail OPEN for
    -- legacy rows, because NULL = anything is never true and the row escapes the scope entirely.
    -- The store still reads through COALESCE(tenant_id, @UntenantedSentinel) so it remains correct
    -- against a table provisioned before this default existed.
    --
    -- No COLLATE clause is needed for correctness here: PostgreSQL equality on a deterministic
    -- collation is byte-wise, so 'Acme' and 'acme' are distinct tenants exactly as the .NET side's
    -- Ordinal comparison requires. Do NOT provision this column with a nondeterministic
    -- (case-insensitive) collation -- that would silently merge two tenants into one scope.
    tenant_id               TEXT        NOT NULL DEFAULT '__untenanted__',

    application_name        TEXT        NULL,

    -- Correlation and origin
    correlation_id          TEXT        NULL,
    session_id              TEXT        NULL,
    ip_address              TEXT        NULL,
    user_agent              TEXT        NULL,
    reason                  TEXT        NULL,

    -- Free-form payload. Written with an explicit ::jsonb cast by the store.
    metadata                JSONB       NULL,

    -- Tamper-evident hash chain. previous_event_hash is NULL only for the genesis event of a chain.
    previous_event_hash     TEXT        NULL,
    event_hash              TEXT        NULL,

    CONSTRAINT pk_audit_events PRIMARY KEY (sequence_number),

    -- event_id is the store's public identifier and its duplicate-rejection key: StoreAsync relies
    -- on a unique violation here to refuse a replayed event id.
    CONSTRAINT uq_audit_events_event_id UNIQUE (event_id)
);

-- Indexes for the query patterns the store actually issues.
CREATE INDEX IF NOT EXISTS ix_audit_events_timestamp
    ON "audit"."audit_events" (timestamp DESC);

-- Every read is tenant-scoped first, then time-ordered, so the tenant term leads the composite.
CREATE INDEX IF NOT EXISTS ix_audit_events_tenant_id_timestamp
    ON "audit"."audit_events" (tenant_id, timestamp DESC);

CREATE INDEX IF NOT EXISTS ix_audit_events_application_name_timestamp
    ON "audit"."audit_events" (application_name, timestamp DESC);

CREATE INDEX IF NOT EXISTS ix_audit_events_actor_id_timestamp
    ON "audit"."audit_events" (actor_id, timestamp DESC);

CREATE INDEX IF NOT EXISTS ix_audit_events_resource_id_timestamp
    ON "audit"."audit_events" (resource_id, timestamp DESC);

CREATE INDEX IF NOT EXISTS ix_audit_events_correlation_id
    ON "audit"."audit_events" (correlation_id);

CREATE INDEX IF NOT EXISTS ix_audit_events_event_type_timestamp
    ON "audit"."audit_events" (event_type, timestamp DESC);

CREATE INDEX IF NOT EXISTS ix_audit_events_resource_classification_timestamp
    ON "audit"."audit_events" (resource_classification, timestamp DESC);

-- Chain verification walks in sequence order within a tenant.
CREATE INDEX IF NOT EXISTS ix_audit_events_tenant_id_sequence_number
    ON "audit"."audit_events" (tenant_id, sequence_number);

-- Retention sweeps delete by cutoff.
CREATE INDEX IF NOT EXISTS ix_audit_events_timestamp_cleanup
    ON "audit"."audit_events" (timestamp);
