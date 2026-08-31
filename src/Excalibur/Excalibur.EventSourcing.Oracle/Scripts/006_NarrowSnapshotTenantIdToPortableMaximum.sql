-- Oracle MIGRATION for Excalibur.EventSourcing.Oracle — SNAPSHOT TENANT KEY, PORTABLE MAXIMUM
-- Version: 1.0
--
-- Narrows EVENTSTORESNAPSHOTS.TENANTID from VARCHAR2(255) to VARCHAR2(64) — the shape
-- 001_CreateSnapshotSchema.sql now provisions. Run this ONLY against an EVENTSTORESNAPSHOTS table
-- created by an earlier version of that script. A database provisioned from the current script
-- already has this shape, and this script detects that and does nothing.
--
-- EVENTSTOREEVENTS is not touched. Its table and its tenant column both arrived after the previous
-- release, so no shipped database has an over-wide tenant column there to narrow.
--
-- The table name is the default this package ships (EVENTSTORESNAPSHOTS; OracleSnapshotStore
-- accepts an override). If you overrode it, edit the literals below to match.
--
--
-- WHY THE CREATE SCRIPT CANNOT DELIVER THIS ON ITS OWN
-- -----------------------------------------------------
-- Oracle has no "CREATE TABLE IF NOT EXISTS": re-running 001 against an existing database raises
-- ORA-00955 and changes nothing, so the narrowed column never arrives. Re-running 001 is not an
-- upgrade path; this script is.
--
-- 002 does carry a MODIFY of this column, and it is not a substitute for this script: it exists to
-- make the column NOT NULL as part of the keyed-sentinel migration, it re-states whatever width was
-- current when it was written, and it is written to be run once against a nullable column. This
-- script is guarded on the property that is ACTUALLY changing, the declared length, so it reaches
-- exactly the databases that still need it and is a no-op everywhere else.
--
--
-- WHY 64 AND NOT WIDER
-- ---------------------
-- 64 is the NARROWEST tenant column across every shipped provider, and the framework now rejects a
-- longer identifier where it is constructed, before it can reach a database. Fixing every provider
-- at the narrowest is the only choice that cannot truncate: an identifier the framework accepts
-- must be storable whole by any provider it can reach. A column wider than that guard is not
-- harmless slack — it is a provider that accepts what a sibling provider silently shortens.
--
--
-- WHAT HAPPENS TO EXISTING ROWS
-- ------------------------------
-- Nothing is backfilled and no row changes partition. TENANTID was already NOT NULL in the version
-- this script upgrades from, so there is no absent value to fill: every row keeps its exact tenant
-- bytes, under its existing key.
--
-- A value that does not FIT is REFUSED, never truncated. Truncation here is a KEY MERGE, not a
-- lossy label: TENANTID is a component of UQ_EVENTSTORESNAPSHOTS_AGG (AGGREGATEID, AGGREGATETYPE,
-- TENANTID), the index that makes one snapshot per aggregate PER TENANT. Two tenants whose
-- identifiers share a truncated prefix would collapse onto ONE key, one tenant's snapshot would
-- satisfy the other tenant's upsert target, and the store would overwrite a snapshot it does not
-- own — the exact cross-tenant overwrite the triple key exists to make inexpressible. So this
-- script stops and names the rows rather than choosing for you. Re-key the reported rows to
-- identifiers that fit, then re-run.
--
-- Oracle itself would refuse an over-long value with ORA-01441 rather than truncating, so the data
-- is never at risk either way. The check below exists because ORA-01441 names neither how many rows
-- are in the way nor how long the longest one is, which is what an operator needs in order to act.
--
-- Such rows can only predate the length guard the framework now applies at construction. A
-- deployment that has only ever written through a current release has none.
--
--
-- WHY THE CHECK USES LENGTHB AND THE GUARD USES CHAR_LENGTH
-- ----------------------------------------------------------
-- 001 declares VARCHAR2(64) with no CHAR keyword, so the column takes the database's default length
-- semantics — BYTE on a stock instance. Under byte semantics the binding constraint is BYTES, and a
-- 64-character identifier outside US7ASCII can exceed 64 bytes, so LENGTHB is the measure that
-- decides whether a value fits. It is never smaller than LENGTH, so it also covers the character
-- case.
--
-- The "already narrowed" guard reads USER_TAB_COLUMNS.CHAR_LENGTH, which is the DECLARED length in
-- characters under either semantics — 64 for VARCHAR2(64) and for VARCHAR2(64 CHAR) alike, and 255
-- for the column this script upgrades from. DATA_LENGTH is in bytes and reports 256 for a
-- character-semantics column, so a guard on it would try to re-narrow a column that is already
-- correct.
--
--
-- NULLABILITY IS NOT RESTATED, AND THAT IS DELIBERATE
-- -----------------------------------------------------
-- The MODIFY below names only the type. Oracle keeps the existing NOT NULL when a MODIFY does not
-- mention nullability, and re-stating NOT NULL on a column that already has it raises ORA-01442 --
-- which would turn this migration into a hard failure on exactly the databases it is meant to
-- upgrade. Narrowing the width is the only change being made.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Oracle narrows a column in place and does not require the unique index to be dropped, so this is
-- a short data-dictionary operation rather than a table rewrite. It still takes an exclusive DDL
-- lock on the table and will wait behind an open transaction, so run it with the store stopped.
--
--
-- REPORTING A REFUSAL
-- ---------------------
-- A refusal below raises an application error, which every client surfaces as a failed statement.
-- If your runner executes this file statement-by-statement and continues past a failure, make it
-- stop on error — a refused migration reported as a success leaves the column un-narrowed with
-- nothing to show for it. The WHENEVER SQLERROR directive below makes that refusal a non-zero exit.
--
-- Every table-dependent statement runs through EXECUTE IMMEDIATE rather than static SQL,
-- deliberately, for the same reason 005 does: Oracle validates static SQL against the data
-- dictionary when the PL/SQL block is COMPILED, not when the branch referencing it executes, so a
-- plain SELECT against a table this consumer never provisioned would fail the whole script with
-- ORA-00942 even though the surrounding IF was written to skip it.
--
-- Deliberately no "SET SERVEROUTPUT ON" in this file: that is a SQL*Plus client directive, not a
-- SQL or PL/SQL statement, and a driver that replays this script statement-by-statement would
-- reject it outright. If you are running this by hand in SQL*Plus and want to see the notices
-- below, run "SET SERVEROUTPUT ON" yourself first.

-- A refusal below must be visible to an unattended runner. Without this directive SQL*Plus exits 0
-- even when a block raises, so a pipeline records a declined migration as applied and runs the next
-- step against a database that was never changed. SQLcl and SQL Developer honour it too; drivers
-- that execute statements directly ignore client directives.
-- An operator running this inside an interactive session is ended by that non-zero exit;
-- to keep the session, issue WHENEVER SQLERROR CONTINUE before @-ing the file.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

DECLARE
    v_table_exists NUMBER;
    v_declared     NUMBER;
    v_offenders    NUMBER;
    v_longest      NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_table_exists
      FROM USER_TABLES
     WHERE TABLE_NAME = 'EVENTSTORESNAPSHOTS';

    IF v_table_exists = 0 THEN
        DBMS_OUTPUT.PUT_LINE('006: EVENTSTORESNAPSHOTS is not present; nothing to upgrade. Provision with 001_CreateSnapshotSchema.sql instead.');
        RETURN;
    END IF;

    BEGIN
        SELECT CHAR_LENGTH INTO v_declared
          FROM USER_TAB_COLUMNS
         WHERE TABLE_NAME = 'EVENTSTORESNAPSHOTS'
           AND COLUMN_NAME = 'TENANTID';
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RAISE_APPLICATION_ERROR(-20006,
                '006 REFUSED: EVENTSTORESNAPSHOTS has no TENANTID column at all, so this table did not come from any version of 001_CreateSnapshotSchema.sql. Nothing has been changed. Reconcile the table against 001 before upgrading it; this script narrows a column, it does not add one.');
    END;

    IF v_declared = 64 THEN
        DBMS_OUTPUT.PUT_LINE('006: EVENTSTORESNAPSHOTS.TENANTID is already 64 characters wide; nothing to do.');
        RETURN;
    END IF;

    EXECUTE IMMEDIATE
        'SELECT COUNT(*), NVL(MAX(LENGTHB(TENANTID)), 0) FROM EVENTSTORESNAPSHOTS WHERE LENGTHB(TENANTID) > 64'
        INTO v_offenders, v_longest;

    IF v_offenders > 0 THEN
        RAISE_APPLICATION_ERROR(-20006,
            '006 REFUSED: ' || v_offenders ||
            ' row(s) in EVENTSTORESNAPSHOTS hold a tenant identifier that does not fit in 64 bytes (longest: ' ||
            v_longest ||
            ' bytes). TENANTID is a component of UQ_EVENTSTORESNAPSHOTS_AGG (AGGREGATEID, AGGREGATETYPE, TENANTID), so shortening those identifiers would not merely truncate a label: two tenants sharing a truncated prefix would collapse onto ONE key, and one tenant''s snapshot would satisfy the other tenant''s upsert target and be overwritten. Nothing has been changed. Re-key those rows to identifiers that fit, then re-run.');
    END IF;

    EXECUTE IMMEDIATE 'ALTER TABLE EVENTSTORESNAPSHOTS MODIFY (TENANTID VARCHAR2(64))';

    DBMS_OUTPUT.PUT_LINE('006: EVENTSTORESNAPSHOTS.TENANTID narrowed from ' || v_declared || ' to VARCHAR2(64).');
END;
/
