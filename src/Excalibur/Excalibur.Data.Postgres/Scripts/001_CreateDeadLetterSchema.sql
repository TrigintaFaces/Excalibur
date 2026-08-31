-- PostgreSQL Schema for Excalibur.Data.Postgres — DEAD LETTER MESSAGES
-- Version: 1.0
--
-- Creates the table PostgresDeadLetterStore reads and writes. This provider never creates the table
-- at runtime: run this script against the target database before the first message is dead-lettered.
-- Without it, the first failure that should have been captured is instead lost to an undefined_table
-- error — the one moment the store exists for.
--
-- WHY THIS FILE EXISTS AT ALL, since a reader may believe this schema already shipped: it did not.
-- A PostgreSQL definition for this table was present in the Excalibur.Dispatch package, but the
-- whole of it sat inside a /* ... */ comment, in a file that is otherwise SQL Server dialect and
-- separated into batches by GO. Running that file against PostgreSQL creates nothing and cannot: it
-- fails at the first `GO`, and the PostgreSQL section it carries is inert even if reached. The
-- schema was, in practice, unobtainable. It now ships as executable DDL from the package that owns
-- the store.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     schema = "public"
--     table  = "dead_letter_messages"
--
-- If you override either, rename the object below to match.
--
--
-- WHY properties IS text, AND WHY THE WRITER'S ::jsonb CAST IS HARMLESS
-- --------------------------------------------------------------------
-- StoreAsync inserts this column as `@Properties::jsonb` while the reader binds it back to a plain
-- string, deserialized with JsonSerializer. Either column type can satisfy that pair, so the choice
-- is stated rather than assumed:
--
--   text accepts the cast. MEASURED, not reasoned about — PostgreSQL 16, a text column, the store's
--   own INSERT shape with a ::jsonb-cast value: accepted, one row written. An earlier draft of this
--   comment asserted that text would REJECT it for want of an assignment cast. That is false, and it
--   is recorded here rather than quietly deleted so nobody re-derives it.
--
--   text is what the reader expects. The row type declares `string? properties` and hands the value
--   straight to JsonSerializer.Deserialize, so text needs no provider mapping to get there.
--
-- jsonb is a defensible alternative: it validates the payload on write and permits JSON operators
-- and GIN indexing. It is not used here because nothing in this store reads the column as JSON, so
-- the capability would go unused while adding a provider type mapping between the column and the
-- string the reader wants.
--
-- Note that jsonb would NOT change what a consumer reads back, and a reader who assumes otherwise
-- has the same wrong model an earlier draft of this comment did. The normalisation people associate
-- with jsonb (whitespace collapsed, object keys reordered, duplicates dropped) is performed by the
-- ::jsonb CAST in the writer, before the value ever reaches the column. MEASURED: inserting
-- '{"a":"b"}'::jsonb into this TEXT column and selecting it back yields '{"a": "b"}'. The stored
-- text is already normalised whichever type the column has.

-- tenant_id IS COLLATE "C", FOR THE SAME REASON IT IS NOT NULL
-- ------------------------------------------------------------
-- A dead-letter row holds the failed message BODY, so an unscoped or over-matching read discloses
-- one tenant's message content to another. Two ways that comparison can fail open, both closed here:
--
--   NULL — every read is a scoped `tenant_id = @TenantId`, which never matches NULL. A nullable
--   column would let a row silently leave its own tenant's results while remaining in the table.
--   The column is NOT NULL and defaults to the reserved untenanted sentinel instead; a
--   single-tenant deployment binds the sentinel, which is a real term like any other.
--
--   COLLATION — PostgreSQL 15 and later permit a database created with a nondeterministic ICU
--   collation, under which 'Acme' = 'acme' compares TRUE. On such a database an unpinned tenant
--   column matches tenants that differ only by case, which for this table means reading their
--   message bodies. Nothing errors; the predicate simply matches more than it should. Pinning "C"
--   makes the comparison byte-exact regardless of how the database was created.
--
-- The primary key stays on id ALONE and deliberately does not become (id, tenant_id). A composite
-- key would permit the same id in two tenants, and any lookup written as WHERE id = @Id would then
-- resolve an arbitrary tenant's row. Keeping id globally unique makes that ambiguity
-- unrepresentable rather than requiring every predicate to remember the tenant term for
-- correctness. The tenant term is still bound on every statement, but for ISOLATION, not identity.
--
-- Every statement is guarded, so the script is safe to re-run.

CREATE SCHEMA IF NOT EXISTS "public";

CREATE TABLE IF NOT EXISTS "public"."dead_letter_messages" (
    id                      VARCHAR(32)   NOT NULL PRIMARY KEY,
    tenant_id               VARCHAR(64)  COLLATE "C" NOT NULL DEFAULT '__untenanted__',
    message_id              VARCHAR(128)  NOT NULL,
    message_type            VARCHAR(500)  NOT NULL,
    message_body            TEXT          NOT NULL,
    message_metadata        TEXT          NOT NULL,
    reason                  VARCHAR(1000) NOT NULL,
    exception_details       TEXT          NULL,
    processing_attempts     INTEGER       NOT NULL DEFAULT 0,
    moved_to_dead_letter_at TIMESTAMPTZ   NOT NULL,
    first_attempt_at        TIMESTAMPTZ   NULL,
    last_attempt_at         TIMESTAMPTZ   NULL,
    is_replayed             BOOLEAN       NOT NULL DEFAULT false,
    replayed_at             TIMESTAMPTZ   NULL,
    source_system           VARCHAR(200)  NULL,
    correlation_id          VARCHAR(128)  NULL,
    properties              TEXT          NULL
);

-- Additive upgrade for a database created before the tenant column existed. Existing rows predate
-- multi-tenancy and are therefore untenanted: they bind the sentinel rather than NULL, so they stay
-- readable by an untenanted scope instead of vanishing from every tenant's results.
ALTER TABLE "public"."dead_letter_messages"
    ADD COLUMN IF NOT EXISTS tenant_id VARCHAR(64) COLLATE "C" NOT NULL DEFAULT '__untenanted__';

-- GetByIdAsync, ReplayAsync and DeleteAsync all match (message_id, tenant_id). The tenant term
-- leads because every read is tenant-scoped.
CREATE INDEX IF NOT EXISTS idx_dead_letter_messages_tenant_id_message_id
    ON "public"."dead_letter_messages" (tenant_id, message_id);

-- The listing query filters by tenant and orders by moved_to_dead_letter_at DESC; this index
-- serves both halves so the sort does not spill.
CREATE INDEX IF NOT EXISTS idx_dead_letter_messages_tenant_id_moved_at
    ON "public"."dead_letter_messages" (tenant_id, moved_to_dead_letter_at DESC);

-- The retention sweep is `DELETE ... WHERE moved_to_dead_letter_at < @CutoffDate AND tenant_id =
-- @TenantId`; without a tenant-leading index it scans the table on every pass.
CREATE INDEX IF NOT EXISTS idx_dead_letter_messages_tenant_id_type
    ON "public"."dead_letter_messages" (tenant_id, message_type);

CREATE INDEX IF NOT EXISTS idx_dead_letter_messages_correlation_id
    ON "public"."dead_letter_messages" (correlation_id)
    WHERE correlation_id IS NOT NULL;
