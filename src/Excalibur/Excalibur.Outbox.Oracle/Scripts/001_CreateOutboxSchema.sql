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
-- Oracle has no "CREATE TABLE IF NOT EXISTS", so every CREATE below is issued from a PL/SQL
-- block that swallows ORA-00955 (name is already used by an existing object) and nothing else.
-- That makes this script RE-RUNNABLE, which matters for more than convenience: it is also the
-- UPGRADE path. A bare CREATE TABLE aborts the whole file on the first object that already
-- exists -- under the WHENEVER SQLERROR directive below, before any later statement runs -- so
-- a database created by an earlier version could not be brought forward by packaged scripts at
-- all. The per-column additive block after the outbox table is what actually upgrades it.

-- ---------------------------------------------------------------------------
-- Outbox messages
-- ---------------------------------------------------------------------------
-- Every column below is written or read by the store's SQL. In particular the columns
-- carrying ordering and routing (priority, scheduled_at, partition_key, group_key,
-- sequence_number, target_transports, is_multi_transport) and failure state
-- (error_message, next_attempt_at) are not optional: the drain path selects them by name,
-- so a table provisioned without them fails with ORA-00904 (invalid identifier) rather
-- than silently degrading.
-- This script creates schema. Without the directive below SQL*Plus exits 0 even when a statement
-- fails -- ORA-00955 on an object that already exists, or an insufficient-privilege error -- so an
-- unattended runner records the schema as created and proceeds to the next script against a database
-- that does not have it. This is the FIRST script a consumer runs, so nothing downstream is safe if
-- it is wrong. The refusal-exit-code gate does not cover this script: its predicate is a raised
-- refusal, and there is none here -- the exposure is ordinary statement failure.
-- An operator running this inside an interactive session is ended by that non-zero exit;
-- to keep the session, issue WHENEVER SQLERROR CONTINUE before @-ing the file.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

DECLARE
    e_already_exists EXCEPTION;
    PRAGMA EXCEPTION_INIT(e_already_exists, -955);
BEGIN
    EXECUTE IMMEDIATE q'[CREATE TABLE OUTBOX (
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
)]';
EXCEPTION
    -- ORA-00955 ONLY. Every other failure still reaches WHENEVER SQLERROR and stops the file:
    -- an insufficient-privilege error must never be mistaken for "already there".
    WHEN e_already_exists THEN NULL;
END;
/

-- ---------------------------------------------------------------------------
-- Additive upgrade, for an outbox table created by an earlier version
-- ---------------------------------------------------------------------------
-- The block above creates the table or finds it already there. Finding it already there is NOT
-- the same as finding it in the CURRENT SHAPE: a database provisioned before TENANT_ID and
-- DESTINATION existed still lacks both, and the drain fails on its first poll with ORA-00904
-- (invalid identifier). The indexes below name some of these columns too, so they would fail the
-- same way before the store ever ran.
--
-- This enumerates EVERY column rather than only the two that arrived most recently. Which columns
-- a given database already has depends on the version it was provisioned under, and enumerating
-- all of them is what makes the outcome independent of that. Each ADD is guarded by reading
-- USER_TAB_COLUMNS, which is the idiom 002 and 003 in this package already use -- the guard reads
-- the catalogue rather than attempting the ADD and swallowing the error, so a re-run is a genuine
-- no-op rather than a silent rewrite.
--
-- ONE list, not a list of names beside a list of definitions: the column name is derived from the
-- definition's first token, so the two cannot drift apart.
--
-- TENANT_ID arrives NOT NULL carrying the reserved untenanted key as its default, so existing rows
-- are anchored to that key by the ADD itself. That is the same value 002_MakeOutboxTenantTotal.sql
-- converges to, and 002 then correctly finds nothing to do. 002 raises ORA-20002 telling the
-- operator to run this script first; before this block existed that instruction pointed at a script
-- that aborted on the first object it found, so it could not be followed.
--
-- OCCURRED_ON carries SYSTIMESTAMP as its default, so if it is genuinely absent every existing row
-- is stamped with the instant of the upgrade rather than when the message was produced. There is no
-- better value available: the column did not exist, so the original instant was never recorded.
--
-- MESSAGE_TYPE and MESSAGE_BODY ARE listed here, unlike the PostgreSQL sibling which excludes them.
-- That is not an oversight and the asymmetry is real: this table declares both NULLABLE, so adding
-- one to a table that has rows succeeds, whereas PostgreSQL declares both NOT NULL with no default
-- and the ADD would fail outright there.
DECLARE
    v_defs    SYS.ODCIVARCHAR2LIST := SYS.ODCIVARCHAR2LIST(
        'MESSAGE_TYPE VARCHAR2(500)',
        'MESSAGE_METADATA CLOB',
        'MESSAGE_BODY BLOB',
        'TENANT_ID VARCHAR2(64) DEFAULT ''__untenanted__'' NOT NULL',
        'DESTINATION VARCHAR2(500)',
        'CORRELATION_ID VARCHAR2(255)',
        'CAUSATION_ID VARCHAR2(255)',
        'OCCURRED_ON TIMESTAMP(7) WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL',
        'ATTEMPTS NUMBER(10) DEFAULT 0 NOT NULL',
        'ERROR_MESSAGE CLOB',
        'PRIORITY NUMBER(10) DEFAULT 0 NOT NULL',
        'DISPATCHER_ID VARCHAR2(100)',
        'DISPATCHER_TIMEOUT TIMESTAMP(7) WITH TIME ZONE',
        'NEXT_ATTEMPT_AT TIMESTAMP(7) WITH TIME ZONE',
        'SCHEDULED_AT TIMESTAMP(7) WITH TIME ZONE',
        'PARTITION_KEY VARCHAR2(255)',
        'GROUP_KEY VARCHAR2(255)',
        'SEQUENCE_NUMBER NUMBER(19) DEFAULT 0 NOT NULL',
        'TARGET_TRANSPORTS VARCHAR2(1000)',
        'IS_MULTI_TRANSPORT NUMBER(1) DEFAULT 0 NOT NULL');
    v_name    VARCHAR2(128);
    v_exists  NUMBER;
BEGIN
    FOR i IN 1 .. v_defs.COUNT LOOP
        v_name := SUBSTR(v_defs(i), 1, INSTR(v_defs(i), ' ') - 1);

        SELECT COUNT(*) INTO v_exists
          FROM USER_TAB_COLUMNS
         WHERE TABLE_NAME = 'OUTBOX' AND COLUMN_NAME = v_name;

        IF v_exists = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE OUTBOX ADD (' || v_defs(i) || ')';
        END IF;
    END LOOP;
END;
/


-- Supports the claim cursor's ordering. The drain selects eligible rows ordered by
-- partition_key, sequence_number, occurred_on, which is what preserves per-partition
-- ordering across concurrent drains.
DECLARE
    e_already_exists EXCEPTION;
    e_already_indexed EXCEPTION;
    PRAGMA EXCEPTION_INIT(e_already_exists, -955);
    -- ORA-01408: this column list is already indexed under another name. A re-run must not fail
    -- on an index a consumer created by hand.
    PRAGMA EXCEPTION_INIT(e_already_indexed, -1408);
BEGIN
    EXECUTE IMMEDIATE 'CREATE INDEX IX_OUTBOX_CLAIM_ORDER ON OUTBOX (partition_key, sequence_number, occurred_on)';
EXCEPTION
    WHEN e_already_exists THEN NULL;
    WHEN e_already_indexed THEN NULL;
END;
/

-- Supports the second half of the claim: having reserved rows under its own token, the
-- drain reads them back by dispatcher_id.
DECLARE
    e_already_exists EXCEPTION;
    e_already_indexed EXCEPTION;
    PRAGMA EXCEPTION_INIT(e_already_exists, -955);
    -- ORA-01408: this column list is already indexed under another name. A re-run must not fail
    -- on an index a consumer created by hand.
    PRAGMA EXCEPTION_INIT(e_already_indexed, -1408);
BEGIN
    EXECUTE IMMEDIATE 'CREATE INDEX IX_OUTBOX_DISPATCHER ON OUTBOX (dispatcher_id)';
EXCEPTION
    WHEN e_already_exists THEN NULL;
    WHEN e_already_indexed THEN NULL;
END;
/

-- Supports the scheduled-message sweep, which selects rows whose scheduled_at has come due.
DECLARE
    e_already_exists EXCEPTION;
    e_already_indexed EXCEPTION;
    PRAGMA EXCEPTION_INIT(e_already_exists, -955);
    -- ORA-01408: this column list is already indexed under another name. A re-run must not fail
    -- on an index a consumer created by hand.
    PRAGMA EXCEPTION_INIT(e_already_indexed, -1408);
BEGIN
    EXECUTE IMMEDIATE 'CREATE INDEX IX_OUTBOX_SCHEDULED_AT ON OUTBOX (scheduled_at)';
EXCEPTION
    WHEN e_already_exists THEN NULL;
    WHEN e_already_indexed THEN NULL;
END;
/

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
DECLARE
    e_already_exists EXCEPTION;
    PRAGMA EXCEPTION_INIT(e_already_exists, -955);
BEGIN
    EXECUTE IMMEDIATE q'[CREATE TABLE OUTBOX_DEAD_LETTERS (
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
)]';
EXCEPTION
    -- ORA-00955 ONLY. Every other failure still reaches WHENEVER SQLERROR and stops the file:
    -- an insufficient-privilege error must never be mistaken for "already there".
    WHEN e_already_exists THEN NULL;
END;
/

-- ---------------------------------------------------------------------------
-- Durable leadership-fence control table
-- ---------------------------------------------------------------------------
-- Holds one durable row per fencing scope, recording the highest leadership token ever
-- accepted. It is deliberately SEPARATE from the outbox table: a successful drain DELETEs
-- the message rows it sent, so a high-water mark stored on those rows would be lowered by
-- the very act of draining, and a superseded leader's stale token would be accepted again
-- afterwards. Keeping the mark in its own table means routine drain and cleanup can never
-- lower it. Cleanup must not reference this table.
DECLARE
    e_already_exists EXCEPTION;
    PRAGMA EXCEPTION_INIT(e_already_exists, -955);
BEGIN
    EXECUTE IMMEDIATE q'[CREATE TABLE OUTBOX_FENCE (
    scope_key           VARCHAR2(600)                   NOT NULL,
    high_water_token    NUMBER(19)                      NOT NULL,
    CONSTRAINT PK_OUTBOX_FENCE PRIMARY KEY (scope_key)
)]';
EXCEPTION
    -- ORA-00955 ONLY. Every other failure still reaches WHENEVER SQLERROR and stops the file:
    -- an insufficient-privilege error must never be mistaken for "already there".
    WHEN e_already_exists THEN NULL;
END;
/
