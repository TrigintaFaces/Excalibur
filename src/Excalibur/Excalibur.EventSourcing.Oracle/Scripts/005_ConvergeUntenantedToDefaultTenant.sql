-- Oracle MIGRATION for Excalibur.EventSourcing.Oracle — SINGLE-TENANT IDENTITY BRIDGE
-- Version: 1.0
--
-- Converges every row this package owns from the reserved '__untenanted__' partition onto the
-- framework's single-tenant identity, '__default__'. Run this ONLY on a deployment that is, and
-- will remain, single-tenant.
--
--
-- WHY THIS EXISTS
-- ----------------
-- A single-tenant host resolves its ambient ITenantContext.TenantId as '__default__'
-- (TenantDefaults.DefaultTenantId) everywhere the framework reads it. But rows written by any
-- code path that supplied no tenant at all — the default construction path this store used
-- before it required a tenant context, a hand-written INSERT, an omitted column on an older
-- release — land under the reserved '__untenanted__' sentinel instead, because that is what an
-- absent tenant folds to at the storage boundary. One deployment ends up with its own data split
-- across two names for the same tenant, and a read scoped to '__default__' does not find rows
-- filed under '__untenanted__' — they are silently unreachable, not merely mis-labelled.
--
-- Converging the two identities is only correct for a genuinely single-tenant host. A
-- multi-tenant deployment's untenanted partition is a LIVE partition: rows that belong to no
-- real tenant (system records, rows predating a single-to-multi-tenant graduation) coexist with
-- rows that belong to named tenants, and folding "no tenant" into "the tenant named
-- '__default__'" would misattribute ownerless data to a specific, wrong tenant.
-- DO NOT RUN THIS SCRIPT AGAINST A MULTI-TENANT DEPLOYMENT.
--
--
-- WHAT THIS SCRIPT DOES NOT DO
-- ------------------------------
-- It does not change any schema. TENANTID is already NOT NULL and already participates in each
-- table's identity on every deployment this script can run against (004 for the event table, 002
-- for the snapshot store already close that gap). This script only ever rewrites a VALUE, never
-- a constraint, an index, or a column definition.
--
-- It is never invoked automatically. Nothing in this package runs it for you; a single-tenant
-- operator applies it deliberately, once, the same way the other scripts under Scripts/ are
-- applied.
--
--
-- SCOPE: TWO INDEPENDENT TABLES, EACH GUARDED SEPARATELY
-- ----------------------------------------------------------
-- EVENTSTOREEVENTS and EVENTSTORESNAPSHOTS are converged in this one script because they are the
-- same operation applied to every table this package owns. There is no materialized-view store
-- shipped for Oracle, so unlike the SQL Server and PostgreSQL siblings of this script, only these
-- two tables apply. Each block below checks for its own table before touching anything, so
-- running this against a database that only has the event store is safe.
--
-- Table names use the defaults this package ships (EVENTSTOREEVENTS, EVENTSTORESNAPSHOTS); edit
-- the literals below if you overrode either (OracleEventStore and OracleSnapshotStore both accept
-- a table-name override).
--
-- Every table-dependent statement below runs through EXECUTE IMMEDIATE rather than static SQL,
-- deliberately: Oracle validates STATIC SQL against the data dictionary when the PL/SQL block is
-- COMPILED, not merely when the branch that references it executes, so a plain SELECT against
-- EVENTSTORESNAPSHOTS would fail the whole script with ORA-00942 the moment a consumer who never
-- registered the snapshot store tried to run it -- even though the surrounding IF was written to
-- skip that table. Dynamic SQL defers the reference to run time, after the existence check has
-- already decided whether to run it at all.
--
--
-- COLLISION HANDLING
-- --------------------
-- The rewrite is safe in the general case: no real tenant can occupy the sentinel (a scoped
-- tenant that names it is rejected before it reaches the database), so collapsing
-- '__untenanted__' onto '__default__' preserves every existing uniqueness class UNLESS a given
-- identity already holds a row under BOTH values — a stream or aggregate that was written to at
-- some point under an explicit '__default__' tenant AND also has legacy untenanted rows. Each
-- block below checks for that case first and REFUSES, naming the colliding identity, rather than
-- letting the UPDATE fail partway or silently pick a winner. Resolve the reported rows (delete or
-- re-key whichever is stale), then re-run.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Run during a maintenance window with the store stopped, for the same reason 004 does: the
-- backfill is a single set-based UPDATE per table and is not resumable. Take a backup you have
-- restored at least once.
--
-- Every step is guarded against the state it is about to create, so the script is safe to
-- re-run; a database with nothing left under '__untenanted__' is a no-op.
--
-- Deliberately no "SET SERVEROUTPUT ON" in this file: that is a SQL*Plus client directive, not a
-- SQL or PL/SQL statement, and a driver that replays this script statement-by-statement (rather
-- than through an interactive SQL*Plus session) would reject it outright. It is not load-bearing
-- either way -- DBMS_OUTPUT.PUT_LINE below always succeeds; SERVEROUTPUT only controls whether an
-- interactive client DISPLAYS the buffered output. If you are running this by hand in SQL*Plus and
-- want to see the verify block's counts, run "SET SERVEROUTPUT ON" yourself before this script.

-- ---------------------------------------------------------------------------------------
-- EVENTSTOREEVENTS
--
-- Natural key excluding the tenant: (AGGREGATEID, AGGREGATETYPE, VERSION). A collision means one
-- stream position is claimed by both an untenanted and a default-tenant row.
-- ---------------------------------------------------------------------------------------
-- A refusal below must be visible to an unattended runner. Without this directive SQL*Plus exits 0
-- even when a block raises, so a pipeline records a declined migration as applied and runs the next
-- step against a database that was never changed. SQLcl and SQL Developer honour it too; drivers
-- that execute statements directly ignore client directives.
-- An operator running this inside an interactive session is ended by that non-zero exit;
-- to keep the session, issue WHENEVER SQLERROR CONTINUE before @-ing the file.
WHENEVER SQLERROR EXIT FAILURE ROLLBACK

DECLARE
    v_table_exists NUMBER;
    v_collisions   NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_table_exists
      FROM USER_TABLES
     WHERE TABLE_NAME = 'EVENTSTOREEVENTS';

    IF v_table_exists = 0 THEN
        DBMS_OUTPUT.PUT_LINE('005: EVENTSTOREEVENTS is not present; nothing to converge for the event store.');
    ELSE
        EXECUTE IMMEDIATE
            'SELECT COUNT(*) FROM (SELECT AGGREGATEID, AGGREGATETYPE, VERSION FROM EVENTSTOREEVENTS ' ||
            'WHERE TENANTID IN (''__untenanted__'', ''__default__'') ' ||
            'GROUP BY AGGREGATEID, AGGREGATETYPE, VERSION HAVING COUNT(*) > 1)'
            INTO v_collisions;

        IF v_collisions > 0 THEN
            RAISE_APPLICATION_ERROR(-20005,
                '005 ABORT: ' || v_collisions || ' stream position(s) in EVENTSTOREEVENTS hold BOTH an untenanted row and a row already under the single-tenant identity ''__default__''. This deployment is configured as single-tenant, so the untenanted rows would be moved onto ''__default__'' -- but that stream position already has one there, and both would occupy the same key. Delete or re-key whichever event is stale, then re-run. If this host is actually multi-tenant, do not run this script at all.');
        END IF;

        EXECUTE IMMEDIATE
            'UPDATE EVENTSTOREEVENTS SET TENANTID = ''__default__'' WHERE TENANTID = ''__untenanted__''';
        COMMIT;
    END IF;
END;
/

-- ---------------------------------------------------------------------------------------
-- EVENTSTORESNAPSHOTS
--
-- Natural key excluding the tenant: (AGGREGATEID, AGGREGATETYPE). A collision means one
-- aggregate's snapshot is claimed by both an untenanted and a default-tenant row.
-- ---------------------------------------------------------------------------------------
DECLARE
    v_table_exists NUMBER;
    v_collisions   NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_table_exists
      FROM USER_TABLES
     WHERE TABLE_NAME = 'EVENTSTORESNAPSHOTS';

    IF v_table_exists = 0 THEN
        DBMS_OUTPUT.PUT_LINE('005: EVENTSTORESNAPSHOTS is not present; nothing to converge for the snapshot store.');
    ELSE
        EXECUTE IMMEDIATE
            'SELECT COUNT(*) FROM (SELECT AGGREGATEID, AGGREGATETYPE FROM EVENTSTORESNAPSHOTS ' ||
            'WHERE TENANTID IN (''__untenanted__'', ''__default__'') ' ||
            'GROUP BY AGGREGATEID, AGGREGATETYPE HAVING COUNT(*) > 1)'
            INTO v_collisions;

        IF v_collisions > 0 THEN
            RAISE_APPLICATION_ERROR(-20005,
                '005 ABORT: ' || v_collisions || ' aggregate(s) in EVENTSTORESNAPSHOTS hold BOTH an untenanted snapshot and one already under the single-tenant identity ''__default__''. Delete or re-key whichever snapshot is stale, then re-run. Snapshots are a rebuildable cache: deleting either row and letting it regenerate from the event stream is a legitimate alternative to resolving the collision by hand.');
        END IF;

        EXECUTE IMMEDIATE
            'UPDATE EVENTSTORESNAPSHOTS SET TENANTID = ''__default__'' WHERE TENANTID = ''__untenanted__''';
        COMMIT;
    END IF;
END;
/

-- ---------------------------------------------------------------------------------------
-- VERIFY. Expected output: "0" for whichever table(s) are present.
--
-- Same reason as the two blocks above: dynamic SQL, guarded by the same existence check, so a
-- consumer who only has one of the two tables does not hit ORA-00942 on the verification step.
-- ---------------------------------------------------------------------------------------
DECLARE
    v_table_exists NUMBER;
    v_remaining    NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_table_exists FROM USER_TABLES WHERE TABLE_NAME = 'EVENTSTOREEVENTS';
    IF v_table_exists > 0 THEN
        EXECUTE IMMEDIATE
            'SELECT COUNT(*) FROM EVENTSTOREEVENTS WHERE TENANTID = ''__untenanted__''' INTO v_remaining;
        DBMS_OUTPUT.PUT_LINE('005 verify: EVENTSTOREEVENTS rows remaining under __untenanted__: ' || v_remaining);
    END IF;

    SELECT COUNT(*) INTO v_table_exists FROM USER_TABLES WHERE TABLE_NAME = 'EVENTSTORESNAPSHOTS';
    IF v_table_exists > 0 THEN
        EXECUTE IMMEDIATE
            'SELECT COUNT(*) FROM EVENTSTORESNAPSHOTS WHERE TENANTID = ''__untenanted__''' INTO v_remaining;
        DBMS_OUTPUT.PUT_LINE('005 verify: EVENTSTORESNAPSHOTS rows remaining under __untenanted__: ' || v_remaining);
    END IF;
END;
/
