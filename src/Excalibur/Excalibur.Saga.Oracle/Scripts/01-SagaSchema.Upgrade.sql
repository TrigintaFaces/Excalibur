-- Saga schema UPGRADE script for Oracle
-- Part of Excalibur.Saga.Oracle package
--
-- Copyright (c) 2026 The Excalibur Project
-- See LICENSE files in project root for license information.
--
-- Run this ONLY against a SAGAS table created by an earlier version of 01-SagaSchema.sql, to narrow
-- TenantId from VARCHAR2(200) to VARCHAR2(64). A database provisioned from the current
-- 01-SagaSchema.sql already has this shape; the block below detects that and does nothing.
--
-- Table and schema names are configurable. This script uses the same defaults 01-SagaSchema.sql
-- uses (DISPATCH.SAGAS). If you overrode either, edit the literals below to match.
--
-- Kept as a SEPARATE script deliberately, for the same reason SagaTimeouts.Upgrade.sql is:
-- 01-SagaSchema.sql is executed by tooling that splits it on semicolons, and the PL/SQL block below
-- contains its own semicolons. Mixing them would make the create script unsplittable. Execute this
-- file with a tool that honours the '/' block terminator (SQL*Plus, SQLcl, SQL Developer), with
-- SET SERVEROUTPUT ON if you want to see what it decided.
--
--
-- WHY THE CREATE SCRIPT CANNOT DELIVER THIS ON ITS OWN
-- -----------------------------------------------------
-- Oracle has no CREATE TABLE IF NOT EXISTS, so re-running 01-SagaSchema.sql against an existing
-- table raises ORA-00955 and changes nothing. That is the documented, safe outcome of re-running
-- it, and it is exactly why re-running it is not an upgrade path. This script is.
--
--
-- WHY 64 AND NOT WIDER
-- ---------------------
-- 64 is the NARROWEST tenant column across every shipped provider, and the framework now rejects a
-- longer identifier where it is constructed, before it can reach a database. Fixing every provider
-- at the narrowest is the only choice that cannot truncate: an identifier the framework accepts
-- must be storable whole by any provider it can reach. A column wider than that guard is not
-- harmless slack -- it is a provider that accepts what a sibling provider silently shortens.
--
--
-- WHAT HAPPENS TO EXISTING ROWS
-- ------------------------------
-- Nothing is backfilled, and no saga changes partition. TenantId was already NOT NULL with the
-- untenanted sentinel as its default in the version this script upgrades from, so there is no
-- absent value to fill: every row keeps its exact tenant bytes, under its existing primary key.
--
-- A value that does not fit VARCHAR2(64) is REFUSED, never truncated. Oracle refuses it itself with
-- ORA-01441 and changes nothing; the block below catches that and re-raises it saying what to do,
-- because the bare error names neither the consequence nor the remedy. The consequence is not a
-- shortened label: TenantId LEADS the primary key PK_SAGAS (TenantId, SagaId), so two tenants whose
-- identifiers no longer differ once shortened stop being two rows. One tenant's saga state would
-- satisfy the other's key, and a saga would resume from state belonging to a different tenant.
-- Re-key the offending rows to identifiers that fit, then re-run.
--
-- Such rows can only predate the length guard the framework now applies at construction. A
-- deployment that has only ever written through a current release has none.
--
--
-- TYPE, DEFAULT, NULLABILITY AND COLLATION
-- ------------------------------------------
-- VARCHAR2(64) is written bare, with no CHAR or BYTE qualifier, exactly as 01-SagaSchema.sql writes
-- it. Both therefore adopt the session's NLS_LENGTH_SEMANTICS, so the upgraded column ends up with
-- the same length semantics a fresh CREATE would give it in that same database. Qualifying it here
-- would be the divergence, not the fix.
--
-- DEFAULT is restated rather than assumed. MODIFY leaves an unnamed attribute alone, so the default
-- would survive regardless; stating it makes the upgraded column match 01-SagaSchema.sql by
-- declaration instead of by inheritance.
--
-- NOT NULL is deliberately NOT restated. MODIFY preserves it, and naming it on a column that is
-- already NOT NULL raises ORA-01442 on some releases -- a version-dependent failure in a script
-- whose whole job is to be safe to run.
--
-- No COLLATE clause, deliberately, and that is what keeps a fresh install and an upgraded one
-- identical: MODIFY preserves the column's collation, and a table provisioned by 01-SagaSchema.sql
-- (which also names none) carries the schema default. Both paths land on the same collation rather
-- than on two that merely look alike.

-- Narrow TenantId to the portable maximum.
-- ORA-01441: cannot decrease column length because some value is too big.
-- A refusal below must be visible to an unattended runner. Without this directive SQL*Plus exits 0
-- even when a block raises, so a pipeline records a declined migration as applied and runs the next
-- step against a database that was never changed. SQLcl and SQL Developer honour it too; drivers
-- that execute statements directly ignore client directives.
-- An operator running this inside an interactive session is ended by that non-zero exit;
-- to keep the session, issue WHENEVER SQLERROR CONTINUE before @-ing the file.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

DECLARE
    value_too_long EXCEPTION;
    PRAGMA EXCEPTION_INIT(value_too_long, -1441);
    v_declared NUMBER;
BEGIN
    -- CHAR_USED tells us which unit the column was declared in, so the comparison below is against
    -- the DECLARED length rather than against a byte count that a multibyte character set inflates.
    SELECT MAX(CASE WHEN CHAR_USED = 'C' THEN CHAR_LENGTH ELSE DATA_LENGTH END)
      INTO v_declared
      FROM ALL_TAB_COLUMNS
     WHERE OWNER = 'DISPATCH'
       AND TABLE_NAME = 'SAGAS'
       AND COLUMN_NAME = 'TENANTID';

    IF v_declared IS NULL THEN
        DBMS_OUTPUT.PUT_LINE('SAGAS upgrade: DISPATCH.SAGAS has no TenantId column, so this table did not come from any version of 01-SagaSchema.sql. Nothing has been changed.');
        RETURN;
    END IF;

    IF v_declared <= 64 THEN
        DBMS_OUTPUT.PUT_LINE('SAGAS upgrade: DISPATCH.SAGAS.TenantId is already VARCHAR2(64) or narrower; nothing to do.');
        RETURN;
    END IF;

    EXECUTE IMMEDIATE 'ALTER TABLE DISPATCH.SAGAS MODIFY (TenantId VARCHAR2(64) DEFAULT ''__untenanted__'')';
    DBMS_OUTPUT.PUT_LINE('SAGAS upgrade: DISPATCH.SAGAS.TenantId narrowed to VARCHAR2(64).');
EXCEPTION
    WHEN value_too_long THEN
        RAISE_APPLICATION_ERROR(
            -20064,
            'SAGAS upgrade REFUSED: DISPATCH.SAGAS holds at least one TenantId that does not fit VARCHAR2(64), so Oracle declined the change and nothing has been altered. Do not widen the column back or shorten the values in place: TenantId leads PK_SAGAS, so two tenants whose identifiers stop differing once shortened collapse onto one key and a saga can resume from another tenant state. Find them with: SELECT DISTINCT TenantId FROM DISPATCH.SAGAS WHERE LENGTH(TenantId) > 64; re-key those sagas to identifiers of 64 characters or fewer, then re-run this script.');
END;
/
