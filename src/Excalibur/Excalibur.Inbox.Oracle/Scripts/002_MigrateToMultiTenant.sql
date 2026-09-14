-- Oracle MIGRATION for Excalibur.Inbox.Oracle — SINGLE-TENANT -> MULTI-TENANT
-- Version: 2.0
--
-- Run this ONCE to grow an existing single-tenant inbox table (created by
-- 001_CreateInboxSchema.sql, keyed on the pair (MessageId, HandlerType), no TenantId
-- column) into the multi-tenant schema (keyed on the triple, TenantId NOT NULL) before
-- registering multi-tenancy.
--
-- This is an expand-contract migration. Existing pre-multi-tenant rows are anchored to
-- the reserved sentinel '__untenanted__': it is a concrete, non-null, reserved value
-- (the framework rejects it as a tenant id), so migrated rows sit in their
-- own partition and can never collide with a future real tenant. Note DEFAULT '__untenanted__'
-- is Oracle-legal, whereas DEFAULT '' is not (Oracle folds '' to NULL). Rows added after
-- the migration carry their real tenant id.
--
-- Table name uses the default (INBOX_MESSAGES); rename to match if overridden.
--
-- ORDERING / DOWNTIME: run this during a maintenance window with the store stopped, because
-- it rebuilds the primary key. After it completes, register multi-tenancy and restart — the
-- startup handshake then confirms the triple key.
--
-- OPTIONAL: if all pre-migration rows belong to one known first tenant, replace the DEFAULT
-- below with that real tenant id instead of the sentinel.
--
--
-- WHY STEP 2 REFUSES INSTEAD OF SIMPLY REBUILDING
-- ------------------------------------------------
-- Oracle commits DDL implicitly, so the drop and the recreate CANNOT be made one unit of work the
-- way they can on a database with transactional DDL. If the recreate fails the drop is already
-- permanent, and the table is left with NO primary key: an inbox with no key admits a second
-- delivery of a message it has already handled, which is the one outcome an inbox exists to
-- prevent, and nothing reports it -- every subsequent handler simply runs twice.
--
-- What cannot be undone must therefore not be started. Step 2 checks, BEFORE the drop, the two
-- conditions the recreate needs, and refuses having changed nothing if either fails:
--
--   * TENANTID must exist and be NOT NULL. A PRIMARY KEY cannot be defined over a nullable
--     column, and a table that reached a nullable one by some other route would fail with
--     ORA-01449 after the drop had committed.
--   * No two rows may already share (MESSAGEID, HANDLERTYPE, TENANTID). Step 1 cannot create such
--     a pair -- it writes one constant value into a table whose pair key was already unique -- but
--     a table that acquired its tenant column elsewhere can hold one, and the recreate would then
--     fail with ORA-02437 after the drop had committed.
--
-- Each step is also guarded on the state it creates, so re-running this script changes nothing.
--
--
-- A refusal below must be visible to an unattended runner. Without this directive SQL*Plus exits 0
-- even when a block raises, so a pipeline records a declined migration as applied and runs the next
-- step against a database that was never changed. SQLcl and SQL Developer honour it too; drivers
-- that execute statements directly ignore client directives.
-- An operator running this inside an interactive session is ended by that non-zero exit;
-- to keep the session, issue WHENEVER SQLERROR CONTINUE before @-ing the file.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

-- 1) Add the TenantId column NOT NULL, anchoring existing rows to the reserved sentinel.
--    Guarded on the column being absent so a second run is a genuine no-op rather than an
--    ORA-01430 that a runner which continues past errors would carry into step 2.
DECLARE
    v_table_count   NUMBER;
    v_column_count  NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_table_count
      FROM USER_TABLES WHERE TABLE_NAME = 'INBOX_MESSAGES';

    IF v_table_count = 0 THEN
        RAISE_APPLICATION_ERROR(-20003,
            '002 REFUSED: INBOX_MESSAGES is not present. Nothing has been changed. Provision a single-tenant inbox with 001_CreateInboxSchema.sql and migrate it with this script, or provision a multi-tenant one directly with 001_CreateInboxSchema.MultiTenant.sql.');
    END IF;

    SELECT COUNT(*) INTO v_column_count
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'INBOX_MESSAGES' AND COLUMN_NAME = 'TENANTID';

    IF v_column_count = 0 THEN
        EXECUTE IMMEDIATE
            'ALTER TABLE INBOX_MESSAGES ADD (TenantId VARCHAR2(64) DEFAULT ''__untenanted__'' NOT NULL)';
    END IF;
END;
/

-- 2) Rebuild the unique key: drop the pair PK, add the triple PK -- but only once the recreate is
--    known to be possible. See the header for why this refuses rather than attempting it.
DECLARE
    v_nullable      USER_TAB_COLUMNS.NULLABLE%TYPE;
    v_key_is_triple NUMBER;
    v_key_exists    NUMBER;
    v_collisions    NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_key_exists
      FROM USER_CONSTRAINTS
     WHERE TABLE_NAME = 'INBOX_MESSAGES' AND CONSTRAINT_NAME = 'PK_INBOX_MESSAGES';

    SELECT COUNT(*) INTO v_key_is_triple
      FROM USER_CONS_COLUMNS
     WHERE TABLE_NAME = 'INBOX_MESSAGES' AND CONSTRAINT_NAME = 'PK_INBOX_MESSAGES'
       AND COLUMN_NAME = 'TENANTID';

    IF v_key_is_triple > 0 THEN
        DBMS_OUTPUT.PUT_LINE('002: PK_INBOX_MESSAGES already carries TENANTID; nothing to rebuild.');
        RETURN;
    END IF;

    IF v_key_exists = 0 THEN
        RAISE_APPLICATION_ERROR(-20003,
            '002 REFUSED: INBOX_MESSAGES has no PK_INBOX_MESSAGES constraint, so this table was not created by this package''s 001_CreateInboxSchema.sql. Nothing has been changed. Verify the table name and edit the literals in this script if you overrode it -- this script rebuilds a key it can identify, and will not guess at the shape of one it cannot.');
    END IF;

    SELECT NULLABLE INTO v_nullable
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'INBOX_MESSAGES' AND COLUMN_NAME = 'TENANTID';

    IF v_nullable = 'Y' THEN
        RAISE_APPLICATION_ERROR(-20003,
            '002 REFUSED: INBOX_MESSAGES.TENANTID is nullable, and a PRIMARY KEY cannot be defined over a nullable column. Checking this after the drop would be too late -- Oracle commits DDL, so the drop would stand and the recreate would fail with ORA-01449, leaving the inbox with no dedup key and every handler free to run a second time on a message it has already handled. Nothing has been changed. Make the column total first: UPDATE INBOX_MESSAGES SET TENANTID = ''__untenanted__'' WHERE TENANTID IS NULL, COMMIT, then ALTER TABLE INBOX_MESSAGES MODIFY (TENANTID NOT NULL), then re-run this script.');
    END IF;

    EXECUTE IMMEDIATE
        'SELECT COUNT(*) FROM (SELECT 1 FROM INBOX_MESSAGES
                                GROUP BY MESSAGEID, HANDLERTYPE, TENANTID
                               HAVING COUNT(*) > 1)'
        INTO v_collisions;

    IF v_collisions > 0 THEN
        RAISE_APPLICATION_ERROR(-20003,
            '002 REFUSED: ' || v_collisions ||
            ' group(s) of rows in INBOX_MESSAGES already share (MESSAGEID, HANDLERTYPE, TENANTID), so the triple key cannot be created. Oracle commits DDL, so attempting it would drop the existing key and then fail with ORA-02437, leaving the inbox with no dedup key at all. Nothing has been changed. Find them with: SELECT MESSAGEID, HANDLERTYPE, TENANTID, COUNT(*) FROM INBOX_MESSAGES GROUP BY MESSAGEID, HANDLERTYPE, TENANTID HAVING COUNT(*) > 1; resolve each group to one row, then re-run.');
    END IF;

    EXECUTE IMMEDIATE 'ALTER TABLE INBOX_MESSAGES DROP CONSTRAINT PK_INBOX_MESSAGES';
    EXECUTE IMMEDIATE
        'ALTER TABLE INBOX_MESSAGES ADD CONSTRAINT PK_INBOX_MESSAGES PRIMARY KEY (MessageId, HandlerType, TenantId)';

    DBMS_OUTPUT.PUT_LINE('002: PK_INBOX_MESSAGES rebuilt over (MessageId, HandlerType, TenantId).');
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        -- Reached only if step 1 neither created TENANTID nor stopped the script, which means the
        -- runner ignored the WHENEVER SQLERROR directive above and carried on past a failed block.
        RAISE_APPLICATION_ERROR(-20003,
            '002 REFUSED: INBOX_MESSAGES has no TENANTID column, so step 1 of this script did not complete and your runner continued past its failure. The key has NOT been dropped and nothing has been changed. Re-run the whole script with a runner that stops on error, and read step 1''s error.');
END;
/

-- 3) Optional: drop the sentinel default now that the key is rebuilt, so future inserts
--    must supply a real tenant id (the store always binds one on the multi-tenant path).
ALTER TABLE INBOX_MESSAGES MODIFY (TenantId DEFAULT NULL);
