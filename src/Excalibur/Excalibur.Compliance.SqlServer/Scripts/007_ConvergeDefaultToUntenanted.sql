-- SQL SERVER MIGRATION for Excalibur.Compliance.SqlServer — SINGLE-TENANT IDENTITY BRIDGE
-- Version: 1.0
--
-- Converges every row [compliance].[DataInventoryRegistrations] and
-- [compliance].[DiscoveredDataLocations] own from the single-tenant identity '__default__' onto
-- the reserved untenanted sentinel '__untenanted__'. This is the INVERSE of the event store's
-- 004/006 convergence pattern (which moves '__untenanted__' rows onto '__default__'), and the
-- SQL Server twin of Excalibur.Compliance.Postgres's 004_ConvergeDefaultToUntenanted.sql — read
-- that file's header for the full rationale; this one states only what differs.
--
-- There is no SQL Server equivalent of PostgresComplianceStore (consent/erasure/subject-access
-- records), so this script covers only the two data-inventory tables.
--
--
-- WHY THIS EXISTS
-- ----------------
-- SqlServerDataInventoryStore used to resolve its tenant term from whether an ITenantContext
-- happened to be REGISTERED, not from TenantContextOptions.RequireTenant.
-- SqlServerDataInventoryStoreServiceCollectionExtensions.AddSqlServerDataInventoryStore calls
-- AddDefaultTenantContext itself, so a context was ALWAYS registered -- meaning this store ALWAYS
-- resolved the framework's single-tenant default identity '__default__', never the untenanted
-- sentinel, even on a deployment that never called AddMultiTenancy(...).
--
-- The fix makes this store read RequireTenant like every other tenant-aware store in the
-- framework. A single-tenant deployment that was already running has existing rows filed under
-- '__default__', and tenant_id is part of the PRIMARY KEY of both tables (see 004), so a
-- stranded row also blocks a legitimate untenanted re-registration of the same table/field from
-- ever landing.
--
-- DO NOT RUN THIS SCRIPT AGAINST A MULTI-TENANT DEPLOYMENT.
--
--
-- WHAT THIS SCRIPT DOES NOT DO
-- ------------------------------
-- It does not change any schema. TenantId is already NOT NULL and already participates in each
-- table's primary key on every deployment this script can run against (004 makes that total).
-- This script only ever rewrites a VALUE.
--
--
-- COLLISION HANDLING / ORDERING / DOWNTIME
-- -------------------------------------------
-- Same discipline as 004 and as the event store's 004/006: each table is checked for a
-- collision -- a natural key already holding both a '__default__' row and an '__untenanted__'
-- row -- before anything is rewritten, and the whole script refuses rather than picking a
-- winner. Run during a maintenance window with the data-inventory store stopped; take a backup
-- you have restored at least once. Every step is guarded against the state it is about to
-- create, so re-running this against an already-converged database is a no-op.

-- ---------------------------------------------------------------------------------------
-- PRE-FLIGHT: REFUSE ON A MULTI-TENANT DEPLOYMENT
--
-- See the Postgres twin's header for the full rationale. Refusing raises, which aborts the
-- surrounding migration/transaction, so nothing is rewritten.
-- ---------------------------------------------------------------------------------------
SET NOCOUNT ON;

-- THIS SCRIPT HAS NO GO BATCH SEPARATOR, so a THROW below aborts the rest of it on its own:
-- with XACT_ABORT ON and the transaction wrapper, a refusal mid-script leaves every row
-- byte-for-byte unconverted. Nothing further is needed to stop it.
--
-- WHAT IS NOT COVERED IS THE PROCESS EXIT CODE. sqlcmd exits 0 after printing the REFUSED
-- message, so a pipeline that branches on $? reads a refused, no-op migration as a SUCCESS.
-- If a pipeline branches on this script's exit code, run it as
--     sqlcmd -b -i <this script>
-- which reports the refusal as a non-zero exit.

-- Explicit transaction wrapper (this script only -- not present in 004/006, which this script is
-- otherwise modelled on). Verified the equivalent gap against real Postgres for the twin script:
-- without an enclosing transaction, a THROW on one table does not roll back an UPDATE a prior
-- table block already committed, if the caller runs statements independently rather than as one
-- transaction. XACT_ABORT ON makes an uncaught error roll back the whole batch's transaction
-- immediately, and the explicit BEGIN/COMMIT makes that transaction span every table below, so a
-- REFUSE or an ABORT on any table undoes everything this script has done so far -- regardless of
-- how the caller invokes it.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'[compliance].[DataInventoryRegistrations]', N'U') IS NOT NULL
   AND EXISTS (
       SELECT 1 FROM [compliance].[DataInventoryRegistrations]
       WHERE [TenantId] NOT IN (N'__untenanted__', N'__default__')
   )
BEGIN
    THROW 50007, N'007 REFUSED: [compliance].[DataInventoryRegistrations] holds rows under a named tenant. This deployment has named tenants; converging its default-identity rows to untenanted would be wrong for this host. Nothing has been changed.', 1;
END;

IF OBJECT_ID(N'[compliance].[DiscoveredDataLocations]', N'U') IS NOT NULL
   AND EXISTS (
       SELECT 1 FROM [compliance].[DiscoveredDataLocations]
       WHERE [TenantId] NOT IN (N'__untenanted__', N'__default__')
   )
BEGIN
    THROW 50007, N'007 REFUSED: [compliance].[DiscoveredDataLocations] holds rows under a named tenant. This deployment has named tenants; converging its default-identity rows to untenanted would be wrong for this host. Nothing has been changed.', 1;
END;

-- ---------------------------------------------------------------------------------------
-- DATAINVENTORYREGISTRATIONS
--
-- Natural key excluding the tenant: (TableName, FieldName). A collision means one table/field
-- registration holds both a default-identity row and an already-untenanted row.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[compliance].[DataInventoryRegistrations]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM [compliance].[DataInventoryRegistrations]
        WHERE [TenantId] IN (N'__default__', N'__untenanted__')
        GROUP BY [TableName], [FieldName]
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 50007, N'007 ABORT: one or more registrations in [compliance].[DataInventoryRegistrations] hold BOTH a default-identity row and a row already under the untenanted sentinel for the same (TableName, FieldName). Resolve by hand -- decide which registration is current -- then re-run. Leaving this unresolved means the erasure path may skip the field entirely, silently narrowing erasure coverage.', 1;
    END;

    UPDATE [compliance].[DataInventoryRegistrations]
       SET [TenantId] = N'__untenanted__'
     WHERE [TenantId] = N'__default__';
END;

-- ---------------------------------------------------------------------------------------
-- DISCOVEREDDATALOCATIONS
--
-- Natural key excluding the tenant: (DataSubjectIdHash, TableName, FieldName, RecordId). A
-- collision means one discovered record holds both a default-identity row and an
-- already-untenanted row.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[compliance].[DiscoveredDataLocations]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM [compliance].[DiscoveredDataLocations]
        WHERE [TenantId] IN (N'__default__', N'__untenanted__')
        GROUP BY [DataSubjectIdHash], [TableName], [FieldName], [RecordId]
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 50007, N'007 ABORT: one or more records in [compliance].[DiscoveredDataLocations] hold BOTH a default-identity row and a row already under the untenanted sentinel for the same (DataSubjectIdHash, TableName, FieldName, RecordId). Resolve by hand, then re-run. Leaving this unresolved means an erasure request may miss one of the two rows for the same subject, understating what was actually located and erased.', 1;
    END;

    UPDATE [compliance].[DiscoveredDataLocations]
       SET [TenantId] = N'__untenanted__'
     WHERE [TenantId] = N'__default__';
END;

COMMIT TRANSACTION;
