-- Postgres Schema for Excalibur.Outbox.Postgres
-- Version: 1.0
--
-- Creates the three tables required by the Postgres outbox store: the outbox itself, the
-- dead letter table, and the durable leadership-fence control table. The store never
-- creates these at runtime: run this script against the target database before the first
-- message is staged.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     PostgresOutboxStoreOptions.SchemaName           = "public"
--     PostgresOutboxStoreOptions.OutboxTableName      = "outbox"
--     PostgresOutboxStoreOptions.DeadLetterTableName  = "outbox_dead_letters"
--     PostgresOutboxStoreOptions.FenceTableName       = "outbox_fence"
--
-- If you override any of those, rename the corresponding object below to match. The store
-- quotes both identifiers when it builds a qualified name, so an override that differs only
-- by case must be created quoted here too.
--
-- Every statement is guarded with IF NOT EXISTS, so the script is safe to re-run and safe to
-- apply to a database whose outbox table was created by an earlier version.

-- ---------------------------------------------------------------------------
-- Outbox messages
-- ---------------------------------------------------------------------------
-- Every column below is written or read by the store's SQL. The columns carrying ordering
-- and routing (priority, scheduled_at, partition_key, group_key, sequence_number,
-- target_transports, is_multi_transport) and failure state (error_message, next_attempt_at)
-- are not optional: the drain path names them explicitly, so a table provisioned without
-- them fails with 42703 (column does not exist) rather than silently degrading.
CREATE TABLE IF NOT EXISTS public.outbox (
    -- The message id is the key every read, claim, delete and dead-letter path uses; there is
    -- no surrogate key because no query selects one.
    message_id          VARCHAR(100)                    NOT NULL PRIMARY KEY,
    message_type        VARCHAR(500)                    NOT NULL,
    message_metadata    TEXT,
    message_body        BYTEA                           NOT NULL,
    -- TOTAL: every row carries a tenant term, and "no tenant" is the reserved
    -- '__untenanted__' value rather than the absence of one. There is exactly ONE way to say a
    -- message has no tenant, so a scoped predicate and an unscoped one compare the same kind of
    -- thing. The staging path binds this term explicitly (via KeyedTenantPartition, which has no
    -- empty inhabitant), so the DEFAULT is a backstop for hand-written INSERTs rather than
    -- something the store relies on.
    --
    -- This column was nullable until the tenant term was made total, and the comment here used to
    -- argue for that: a NOT NULL column would reject every untenanted stage. That was true only
    -- while the staging path bound the caller's raw value. It now binds the partition's term,
    -- which is never null, so the constraint rejects nothing the store writes. Databases created
    -- under the old shape are converged by 002_MakeOutboxTenantTotal.sql.
    tenant_id           VARCHAR(64)                    NOT NULL DEFAULT '__untenanted__',
    destination         VARCHAR(500),
    correlation_id      VARCHAR(255),
    causation_id        VARCHAR(255),
    priority            INT                             NOT NULL DEFAULT 0,
    scheduled_at        TIMESTAMPTZ,
    partition_key       VARCHAR(255),
    group_key           VARCHAR(255),
    sequence_number     BIGINT                          NOT NULL DEFAULT 0,
    target_transports   VARCHAR(500),
    is_multi_transport  BOOLEAN                         NOT NULL DEFAULT FALSE,
    -- The caller's created-at, not the server clock: the staging path binds it explicitly so
    -- the value survives the stage-then-reload round trip.
    occurred_on         TIMESTAMPTZ                     NOT NULL DEFAULT NOW(),
    attempts            INT                             NOT NULL DEFAULT 0,
    error_message       TEXT,
    -- The claim token written by a drain that has reserved the row, and the instant that
    -- reservation lapses. A row is eligible for claim when dispatcher_id IS NULL or the
    -- timeout has passed, so the pair together form the reservation lease.
    dispatcher_id       VARCHAR(100),
    dispatcher_timeout  TIMESTAMPTZ,
    -- The failure-anchored visibility floor. A message reported failed below the retry
    -- ceiling is deferred until this instant, so a failure cannot hot-loop the drain.
    next_attempt_at     TIMESTAMPTZ
);

-- All four timestamp columns are TIMESTAMPTZ, not TIMESTAMP. The store binds them as true
-- timestamptz values; a column created WITHOUT time zone is silently shifted by the session
-- timezone on reload, so a scheduled message becomes due at the wrong instant.

-- Supports the claim cursor's ordering. The drain selects eligible rows ordered by
-- partition_key, sequence_number, occurred_on, which is what preserves per-partition
-- ordering across concurrent drains.
CREATE INDEX IF NOT EXISTS ix_outbox_claim_order ON public.outbox (partition_key, sequence_number, occurred_on);

-- Supports releasing a reservation by dispatcher, which matches rows on dispatcher_id alone.
CREATE INDEX IF NOT EXISTS ix_outbox_dispatcher ON public.outbox (dispatcher_id);

-- Supports the scheduled-message sweep, which selects rows whose scheduled_at has come due.
CREATE INDEX IF NOT EXISTS ix_outbox_scheduled_at ON public.outbox (scheduled_at);

-- ---------------------------------------------------------------------------
-- Dead letter messages
-- ---------------------------------------------------------------------------
-- A message moved here has exhausted its retries. The move is a single statement that
-- inserts from the outbox and deletes the source row, so this table's columns are a subset
-- of the outbox's. moved_on is not named by that INSERT and is populated by its default,
-- which records when the move happened.
--
-- The move DELETES the source row, so this table is the ONLY remaining record of the
-- message. Anything the move does not copy is destroyed, not merely unindexed -- which is
-- why the tenant term is carried below.
CREATE TABLE IF NOT EXISTS public.outbox_dead_letters (
    message_id          VARCHAR(100)                    NOT NULL,
    -- Originating tenant, carried as provenance so a replay re-enters the SAME tenant. NOT NULL
    -- and a component of the primary key: an untenanted entry stores the reserved
    -- '__untenanted__' value, never NULL, so the key stays intact and the untenanted partition
    -- never collides with a real tenant.
    --
    -- No DEFAULT, deliberately, and this is the one place it differs from the outbox table. The
    -- move copies this value from the outbox row, whose own column is already total, so the
    -- value is always supplied. A default here would let a hand-written INSERT that forgot the
    -- column record a message as UNTENANTED when its tenant was simply never named -- a
    -- provenance column that quietly invents provenance. Without one, that INSERT fails loudly.
    tenant_id           VARCHAR(64)                    NOT NULL,
    message_type        VARCHAR(500)                    NOT NULL,
    message_metadata    TEXT,
    message_body        BYTEA                           NOT NULL,
    occurred_on         TIMESTAMPTZ                     NOT NULL DEFAULT NOW(),
    attempts            INT                             NOT NULL DEFAULT 0,
    error_message       TEXT,
    moved_on            TIMESTAMPTZ                     NOT NULL DEFAULT NOW(),
    -- Composite on purpose, and NOT a pattern to copy onto the outbox table. The outbox is
    -- drained by a claim-then-mark protocol that addresses a row by its id alone, so widening
    -- that key there is a correctness defect. Nothing addresses a dead letter by id: the read
    -- path pages by age and attempts, and the statistics path counts. The tenant is in the key
    -- here for the same reason it is in the SQL Server dead-letter table's key -- so a tenant
    -- can never be silently dropped from an entry and still satisfy the constraint.
    CONSTRAINT pk_outbox_dead_letters PRIMARY KEY (message_id, tenant_id)
);

-- Supports the dead-letter read path, which pages by occurred_on ascending.
CREATE INDEX IF NOT EXISTS ix_outbox_dead_letters_occurred_on ON public.outbox_dead_letters (occurred_on);

-- ---------------------------------------------------------------------------
-- Durable leadership-fence control table
-- ---------------------------------------------------------------------------
-- Holds one durable row per fencing scope, recording the highest leadership token ever
-- accepted. It is deliberately SEPARATE from the outbox table: a successful drain DELETEs
-- the message rows it sent, so a high-water mark stored on those rows would be lowered by
-- the very act of draining, and a superseded leader's stale token would be accepted again
-- afterwards. Keeping the mark in its own table means routine drain and cleanup can never
-- lower it. Cleanup must not reference this table.
--
-- The fenced claim and fenced delete upsert into this table with ON CONFLICT (scope_key),
-- so scope_key must carry a primary key or unique constraint or those statements fail with
-- 42P10 (there is no unique or exclusion constraint matching the ON CONFLICT specification).
CREATE TABLE IF NOT EXISTS public.outbox_fence (
    scope_key           VARCHAR(600)                    NOT NULL PRIMARY KEY,
    high_water_token    BIGINT                          NOT NULL
);
