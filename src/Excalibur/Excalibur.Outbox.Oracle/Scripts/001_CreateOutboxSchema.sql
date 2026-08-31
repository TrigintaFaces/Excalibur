-- Oracle Schema for Excalibur.Outbox.Oracle
-- Version: 1.0
--
-- Creates the three tables required by the Oracle outbox store: the outbox itself, the
-- dead letter table, and the durable leadership-fence control table. The store never
-- creates these at runtime: run this script against the target database before the first
-- message is staged.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     OracleOutboxStoreOptions.SchemaName           = ""   (no schema qualifier)
--     OracleOutboxStoreOptions.OutboxTableName      = "OUTBOX"
--     OracleOutboxStoreOptions.DeadLetterTableName  = "OUTBOX_DEAD_LETTERS"
--     OracleOutboxStoreOptions.FenceTableName       = "OUTBOX_FENCE"
--
-- If you override any of those (or set SchemaName to qualify the objects), rename the
-- corresponding object below to match.
--
-- Oracle has no "CREATE TABLE IF NOT EXISTS"; re-running this script against an existing
-- table raises ORA-00955 (name is already used by an existing object), which is safe to
-- ignore. Run the statements individually if you are applying only part of the schema.

-- ---------------------------------------------------------------------------
-- Outbox messages
-- ---------------------------------------------------------------------------
-- Every column below is written or read by the store's SQL. In particular the columns
-- carrying ordering and routing (priority, scheduled_at, partition_key, group_key,
-- sequence_number, target_transports, is_multi_transport) and failure state
-- (error_message, next_attempt_at) are not optional: the drain path selects them by name,
-- so a table provisioned without them fails with ORA-00904 (invalid identifier) rather
-- than silently degrading.
CREATE TABLE OUTBOX (
    message_id          VARCHAR2(100)                   NOT NULL,
    message_type        VARCHAR2(500),
    message_metadata    CLOB,
    message_body        BLOB,
    -- TOTAL: every row carries a tenant term, and "no tenant" is the reserved
    -- '__untenanted__' value rather than the absence of one. The staging path binds this term
    -- explicitly (via KeyedTenantPartition, which has no empty inhabitant), so the DEFAULT is a
    -- backstop for hand-written INSERTs rather than something the store relies on.
    --
    -- The sentinel is deliberately NON-EMPTY. Oracle folds the empty string to NULL, so an
    -- empty-string sentinel would collapse straight back into the NULL this column exists to
    -- eliminate -- the constraint would hold while the representation stayed split.
    --
    -- DEFAULT precedes NOT NULL: that is Oracle's required order for an inline column
    -- constraint, and it matches occurred_on and attempts below.
    --
    -- Databases created while this column was nullable are converged by
    -- 002_MakeOutboxTenantTotal.sql.
    tenant_id           VARCHAR2(64)  DEFAULT '__untenanted__' NOT NULL,
    destination         VARCHAR2(500),
    correlation_id      VARCHAR2(255),
    causation_id        VARCHAR2(255),
    occurred_on         TIMESTAMP(7) WITH TIME ZONE     DEFAULT SYSTIMESTAMP NOT NULL,
    attempts            NUMBER(10)     DEFAULT 0        NOT NULL,
    error_message       CLOB,
    priority            NUMBER(10)     DEFAULT 0        NOT NULL,
    -- The claim token written by a drain that has reserved the row, and the instant that
    -- reservation lapses. A row is eligible for claim when dispatcher_id IS NULL or the
    -- timeout has passed, so the pair together form the reservation lease.
    dispatcher_id       VARCHAR2(100),
    dispatcher_timeout  TIMESTAMP(7) WITH TIME ZONE,
    next_attempt_at     TIMESTAMP(7) WITH TIME ZONE,
    scheduled_at        TIMESTAMP(7) WITH TIME ZONE,
    partition_key       VARCHAR2(255),
    group_key           VARCHAR2(255),
    sequence_number     NUMBER(19)     DEFAULT 0        NOT NULL,
    target_transports   VARCHAR2(1000),
    is_multi_transport  NUMBER(1)      DEFAULT 0        NOT NULL,
    CONSTRAINT UQ_OUTBOX_MESSAGE_ID UNIQUE (message_id)
);

-- Supports the claim cursor's ordering. The drain selects eligible rows ordered by
-- partition_key, sequence_number, occurred_on, which is what preserves per-partition
-- ordering across concurrent drains.
CREATE INDEX IX_OUTBOX_CLAIM_ORDER ON OUTBOX (partition_key, sequence_number, occurred_on);

-- Supports the second half of the claim: having reserved rows under its own token, the
-- drain reads them back by dispatcher_id.
CREATE INDEX IX_OUTBOX_DISPATCHER ON OUTBOX (dispatcher_id);

-- Supports the scheduled-message sweep, which selects rows whose scheduled_at has come due.
CREATE INDEX IX_OUTBOX_SCHEDULED_AT ON OUTBOX (scheduled_at);

-- ---------------------------------------------------------------------------
-- Dead letter messages
-- ---------------------------------------------------------------------------
-- A message moved here has exhausted its retries. The move is a single statement that
-- inserts from the outbox and deletes the source row, so this table's columns are a subset
-- of the outbox's, plus moved_on to record when the move happened.
--
-- The move DELETES the source row, so this table is the ONLY remaining record of the
-- message. Anything the move does not copy is destroyed, not merely unindexed -- which is
-- why the tenant term is carried below.
CREATE TABLE OUTBOX_DEAD_LETTERS (
    message_id          VARCHAR2(100)                   NOT NULL,
    -- Originating tenant, carried as provenance so a replay re-enters the SAME tenant. NOT NULL
    -- and a component of the key: an untenanted entry stores the reserved '__untenanted__'
    -- value, never NULL, so the key stays intact and the untenanted partition never collides
    -- with a real tenant. The reserved value is non-empty because Oracle folds '' to NULL, so an
    -- empty sentinel would violate the very constraint that is meant to make the term total.
    --
    -- No DEFAULT, deliberately, and this is the one place it differs from the OUTBOX table. The
    -- move copies this value from the outbox row, whose own column is already total, so the
    -- value is always supplied. A default here would let a hand-written INSERT that forgot the
    -- column record a message as UNTENANTED when its tenant was simply never named -- a
    -- provenance column that quietly invents provenance. Without one, that INSERT fails loudly
    -- with ORA-01400.
    tenant_id           VARCHAR2(64)                   NOT NULL,
    message_type        VARCHAR2(500),
    message_metadata    CLOB,
    message_body        BLOB,
    occurred_on         TIMESTAMP(7) WITH TIME ZONE     DEFAULT SYSTIMESTAMP NOT NULL,
    attempts            NUMBER(10)     DEFAULT 0        NOT NULL,
    error_message       CLOB,
    moved_on            TIMESTAMP(7) WITH TIME ZONE     DEFAULT SYSTIMESTAMP NOT NULL,
    -- Composite on purpose, and NOT a pattern to copy onto the OUTBOX table. The outbox is
    -- drained by a claim-then-mark protocol that addresses a row by its id alone, so widening
    -- that key there is a correctness defect. Nothing addresses a dead letter by id: the read
    -- path pages by age and attempts, and the statistics path counts. The tenant is in the key
    -- here for the same reason it is in the SQL Server dead-letter table's key -- so a tenant
    -- can never be silently dropped from an entry and still satisfy the constraint.
    CONSTRAINT UQ_OUTBOX_DLQ_MESSAGE_ID UNIQUE (message_id, tenant_id)
);

-- ---------------------------------------------------------------------------
-- Durable leadership-fence control table
-- ---------------------------------------------------------------------------
-- Holds one durable row per fencing scope, recording the highest leadership token ever
-- accepted. It is deliberately SEPARATE from the outbox table: a successful drain DELETEs
-- the message rows it sent, so a high-water mark stored on those rows would be lowered by
-- the very act of draining, and a superseded leader's stale token would be accepted again
-- afterwards. Keeping the mark in its own table means routine drain and cleanup can never
-- lower it. Cleanup must not reference this table.
CREATE TABLE OUTBOX_FENCE (
    scope_key           VARCHAR2(600)                   NOT NULL,
    high_water_token    NUMBER(19)                      NOT NULL,
    CONSTRAINT PK_OUTBOX_FENCE PRIMARY KEY (scope_key)
);
