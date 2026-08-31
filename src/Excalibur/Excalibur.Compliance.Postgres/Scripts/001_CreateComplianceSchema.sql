-- PostgreSQL Schema for Excalibur.Compliance.Postgres
-- Version: 1.0
--
-- Creates the five tables required by the PostgreSQL erasure, data-inventory and legal-hold
-- stores. Run this script against the target database before the first request is recorded.
--
-- These stores verify their schema on startup and throw if it is absent, directing you to
-- create it out of band. This script is what that instruction refers to.
--
-- Setting AutoCreateSchema = true on the corresponding options type makes a store create its
-- own tables on first use instead. That is a convenience for development: it requires the
-- application's own role to hold DDL rights, which a production deployment usually withholds
-- deliberately, and it puts schema changes outside whatever change control governs this
-- database. These are the erasure and legal-hold surfaces, so that is rarely the right trade.
-- Prefer running this script.
--
-- Schema and table names are configurable. This script uses the defaults, which use the same
-- schema on all three stores:
--
--     SchemaName                    = "compliance"
--     RequestsTableName             = "erasure_requests"
--     CertificatesTableName         = "erasure_certificates"
--     RegistrationsTableName        = "data_inventory_registrations"
--     DiscoveredLocationsTableName  = "discovered_data_locations"
--     TableName (legal holds)       = "legal_holds"
--
-- If you override any of those, rename the corresponding object below to match. The stores
-- quote both identifiers when building a qualified name, so an override differing only by case
-- must be created quoted here too.
--
-- Every statement is guarded with IF NOT EXISTS, so the script is safe to re-run and safe to
-- apply to a database that already holds some of these tables.

CREATE SCHEMA IF NOT EXISTS "compliance";

-- ---------------------------------------------------------------------------
-- Erasure requests
-- ---------------------------------------------------------------------------
-- One row per erasure request, from submission through execution to completion or
-- cancellation. The data subject is stored only as a hash: the request record itself must not
-- become a new copy of the identity it exists to erase.
CREATE TABLE IF NOT EXISTS "compliance"."erasure_requests" (
    request_id              UUID           NOT NULL PRIMARY KEY,
    data_subject_id_hash    VARCHAR(128)   NOT NULL,
    id_type                 INT            NOT NULL,
    -- TOTAL, not nullable: an untenanted request holds the reserved sentinel rather than the
    -- absence of a value, so there is exactly one way to say "no tenant".
    tenant_id               VARCHAR(64)   NOT NULL DEFAULT '__untenanted__',
    scope                   INT            NOT NULL,
    legal_basis             INT            NOT NULL,
    external_reference      VARCHAR(256)   NULL,
    requested_by            VARCHAR(256)   NOT NULL,
    requested_at            TIMESTAMPTZ    NOT NULL,
    scheduled_execution_at  TIMESTAMPTZ    NULL,
    executed_at             TIMESTAMPTZ    NULL,
    completed_at            TIMESTAMPTZ    NULL,
    cancelled_at            TIMESTAMPTZ    NULL,
    cancellation_reason     VARCHAR(1000)  NULL,
    cancelled_by            VARCHAR(256)   NULL,
    status                  INT            NOT NULL,
    keys_deleted            INT            NULL,
    records_affected        INT            NULL,
    certificate_id          UUID           NULL,
    error_message           VARCHAR(2000)  NULL,
    -- JSONB, not TEXT: the store writes and reads this as a JSON document.
    data_categories         JSONB          NULL,
    created_at              TIMESTAMPTZ    NOT NULL,
    updated_at              TIMESTAMPTZ    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_erasure_requests_status
    ON "compliance"."erasure_requests" (status, scheduled_execution_at);
CREATE INDEX IF NOT EXISTS ix_erasure_requests_tenant
    ON "compliance"."erasure_requests" (tenant_id, requested_at);
CREATE INDEX IF NOT EXISTS ix_erasure_requests_subject
    ON "compliance"."erasure_requests" (data_subject_id_hash);

-- ---------------------------------------------------------------------------
-- Erasure certificates
-- ---------------------------------------------------------------------------
-- The signed evidence that an erasure was carried out, retained until retain_until. This is
-- the record produced for an auditor, so it outlives the request it certifies.
CREATE TABLE IF NOT EXISTS "compliance"."erasure_certificates" (
    certificate_id          UUID          NOT NULL PRIMARY KEY,
    request_id              UUID          NOT NULL,
    data_subject_reference  VARCHAR(256)  NOT NULL,
    request_received_at     TIMESTAMPTZ   NOT NULL,
    completed_at            TIMESTAMPTZ   NOT NULL,
    method                  INT           NOT NULL,
    summary                 JSONB         NOT NULL,
    verification            JSONB         NOT NULL,
    legal_basis             INT           NOT NULL,
    signature               VARCHAR(512)  NOT NULL,
    retain_until            TIMESTAMPTZ   NOT NULL,
    created_at              TIMESTAMPTZ   NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_erasure_certificates_request
    ON "compliance"."erasure_certificates" (request_id);
CREATE INDEX IF NOT EXISTS ix_erasure_certificates_retain
    ON "compliance"."erasure_certificates" (retain_until);

-- ---------------------------------------------------------------------------
-- Data inventory registrations
-- ---------------------------------------------------------------------------
-- Declares which of YOUR tables and fields hold personal data, so the erasure path knows where
-- to look. A field that is not registered here is a field erasure will not reach.
CREATE TABLE IF NOT EXISTS "compliance"."data_inventory_registrations" (
    table_name              VARCHAR(256)   NOT NULL,
    field_name              VARCHAR(256)   NOT NULL,
    data_category           VARCHAR(256)   NOT NULL,
    data_subject_id_column  VARCHAR(256)   NOT NULL,
    id_type                 INT            NOT NULL,
    key_id_column           VARCHAR(256)   NOT NULL,
    -- The NAME of a tenant column in your own table. Nullable because your table may genuinely
    -- have none. This is not a tenant identity -- see tenant_id below.
    tenant_id_column        VARCHAR(256)   NULL,
    description             VARCHAR(1000)  NULL,
    created_at              TIMESTAMPTZ    NOT NULL,
    updated_at              TIMESTAMPTZ    NOT NULL,
    -- The tenant this registration BELONGS to. NOT NULL with an explicit sentinel default: a
    -- nullable tenant makes "global" and "forgot to set it" indistinguishable, and the store
    -- cannot tell which one it is holding.
    tenant_id               VARCHAR(64)   NOT NULL DEFAULT '__untenanted__',
    -- tenant_id is part of the KEY, not merely a column: without it two tenants registering the
    -- same table and field are ONE row, and the second write silently destroys the first --
    -- taking with it the erasure path's only record that the field exists.
    PRIMARY KEY (table_name, field_name, tenant_id)
);

CREATE INDEX IF NOT EXISTS ix_data_inventory_registrations_category
    ON "compliance"."data_inventory_registrations" (data_category);

-- ---------------------------------------------------------------------------
-- Discovered data locations
-- ---------------------------------------------------------------------------
-- Where a specific data subject's data was actually found, resolved from the registrations
-- above. This is the working set an erasure run acts on.
CREATE TABLE IF NOT EXISTS "compliance"."discovered_data_locations" (
    data_subject_id_hash  VARCHAR(128)  NOT NULL,
    table_name            VARCHAR(256)  NOT NULL,
    field_name            VARCHAR(256)  NOT NULL,
    record_id             VARCHAR(256)  NOT NULL,
    data_category         VARCHAR(256)  NOT NULL,
    key_id                VARCHAR(256)  NOT NULL,
    is_auto_discovered    BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at            TIMESTAMPTZ   NOT NULL,
    updated_at            TIMESTAMPTZ   NOT NULL,
    -- The tenant this discovered location belongs to, on the same terms as the registrations
    -- table above.
    tenant_id             VARCHAR(64)  NOT NULL DEFAULT '__untenanted__',
    -- tenant_id is in the KEY: two tenants discovering the same record for the same data
    -- subject are two distinct findings, not one overwriting the other.
    PRIMARY KEY (data_subject_id_hash, table_name, field_name, record_id, tenant_id)
);

CREATE INDEX IF NOT EXISTS ix_discovered_data_locations_subject
    ON "compliance"."discovered_data_locations" (data_subject_id_hash);
CREATE INDEX IF NOT EXISTS ix_discovered_data_locations_table
    ON "compliance"."discovered_data_locations" (table_name, field_name);

-- ---------------------------------------------------------------------------
-- Legal holds
-- ---------------------------------------------------------------------------
-- A hold suspends erasure for the subject it names. The erasure path consults this table
-- before acting, so it must exist wherever erasure runs.
CREATE TABLE IF NOT EXISTS "compliance"."legal_holds" (
    hold_id               UUID           NOT NULL PRIMARY KEY,
    data_subject_id_hash  VARCHAR(128)   NULL,
    id_type               INT            NULL,
    -- TOTAL, not nullable. An untenanted hold is a GLOBAL hold — it blocks erasure for every
    -- tenant — and it now says so with the reserved sentinel rather than with a NULL. The read
    -- predicate matches that sentinel explicitly; a scoped tenant must still SEE a global hold,
    -- because losing one does not fail safe, it erases data a court order says to keep.
    tenant_id             VARCHAR(64)   NOT NULL DEFAULT '__untenanted__',
    basis                 INT            NOT NULL,
    case_reference        VARCHAR(256)   NOT NULL,
    description           VARCHAR(2000)  NOT NULL,
    is_active             BOOLEAN        NOT NULL DEFAULT TRUE,
    expires_at            TIMESTAMPTZ    NULL,
    created_by            VARCHAR(256)   NOT NULL,
    created_at            TIMESTAMPTZ    NOT NULL,
    released_by           VARCHAR(256)   NULL,
    released_at           TIMESTAMPTZ    NULL,
    release_reason        VARCHAR(1000)  NULL
);

CREATE INDEX IF NOT EXISTS ix_legal_holds_subject
    ON "compliance"."legal_holds" (data_subject_id_hash, is_active);
CREATE INDEX IF NOT EXISTS ix_legal_holds_tenant
    ON "compliance"."legal_holds" (tenant_id, is_active);
CREATE INDEX IF NOT EXISTS ix_legal_holds_expires
    ON "compliance"."legal_holds" (is_active, expires_at)
    WHERE is_active = TRUE AND expires_at IS NOT NULL;
