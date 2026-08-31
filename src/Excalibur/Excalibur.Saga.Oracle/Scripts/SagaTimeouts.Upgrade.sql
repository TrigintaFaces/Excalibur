-- Saga Timeouts UPGRADE script for Oracle
-- Part of Excalibur.Saga.Oracle package
--
-- Copyright (c) 2026 The Excalibur Project
-- See LICENSE files in project root for license information.
--
-- Run this ONLY against a SAGATIMEOUTS table created by an earlier version of SagaTimeouts.sql, to add
-- the tenant discriminator and re-key the saga index on the ruled (TenantId, SagaId) saga identity.
-- A database provisioned from the current SagaTimeouts.sql already has this shape and needs nothing here.
--
-- Kept as a SEPARATE script deliberately: SagaTimeouts.sql is executed by tooling that splits it on
-- semicolons, and the PL/SQL blocks below contain their own semicolons. Mixing them would make the
-- create script unsplittable. Execute this file with a tool that honours the '/' block terminator
-- (SQL*Plus, SQLcl, SQL Developer).
--
-- Each block swallows only the "already applied" error and re-raises anything else, so running this
-- repeatedly is safe.

-- ---------------------------------------------------------------------------------------------
-- Upgrade path for a SAGATIMEOUTS table created by an earlier version of this script. The CREATE
-- above is unguarded, so these statements are the supported way to bring an existing table to the
-- shape declared here. Oracle has no IF NOT EXISTS for DDL, so each is wrapped in a block that
-- swallows only the "already done" error and re-raises anything else.
-- ---------------------------------------------------------------------------------------------

-- 1. Add the discriminator WITH its default, so existing rows adopt the untenanted sentinel rather
-- than NULL. Those rows predate tenant-aware timeouts, so the untenanted partition is their home.
-- ORA-01430: column being added already exists in table.
DECLARE
    column_exists EXCEPTION;
    PRAGMA EXCEPTION_INIT(column_exists, -1430);
BEGIN
    EXECUTE IMMEDIATE 'ALTER TABLE DISPATCH.SAGATIMEOUTS ADD (TenantId VARCHAR2(64 CHAR) DEFAULT ''__untenanted__'' NOT NULL)';
EXCEPTION
    WHEN column_exists THEN NULL;
END;
/

-- 2. Replace the tenant-less saga index with its tenant-leading equivalent.
-- ORA-01418: specified index does not exist.
DECLARE
    index_missing EXCEPTION;
    PRAGMA EXCEPTION_INIT(index_missing, -1418);
BEGIN
    EXECUTE IMMEDIATE 'DROP INDEX IX_SAGATIMEOUTS_SAGAID_TIMEOUTID';
EXCEPTION
    WHEN index_missing THEN NULL;
END;
/

-- ORA-00955: name is already used by an existing object.
DECLARE
    index_exists EXCEPTION;
    PRAGMA EXCEPTION_INIT(index_exists, -955);
BEGIN
    EXECUTE IMMEDIATE 'CREATE INDEX IX_SAGATIMEOUTS_TENANTID_SAGAID_TIMEOUTID ON DISPATCH.SAGATIMEOUTS (TenantId, SagaId, TimeoutId)';
EXCEPTION
    WHEN index_exists THEN NULL;
END;
/
