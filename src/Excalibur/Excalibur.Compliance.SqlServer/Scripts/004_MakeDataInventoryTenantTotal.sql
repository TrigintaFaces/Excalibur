-- SQL Server MIGRATION for Excalibur.Compliance.SqlServer — DATA INVENTORY TENANT TOTALITY
-- Version: 1.0
--
-- Adds the TenantId discriminator to [compliance].[DataInventoryRegistrations] and
-- [compliance].[DiscoveredDataLocations], and puts it INTO THE PRIMARY KEY of both. After this script
-- a registration belongs to a tenant, reads can be confined to one, and two tenants registering the
-- same table and field are two rows rather than one.
--
-- BEFORE: neither table has any tenant column. The only tenant-shaped field is TenantIdColumn, which
--         holds the NAME of a column in the consumer's own table — metadata, not an identity. The
--         read path filtered on "TenantIdColumn IS NOT NULL", which asks whether a column name was
--         recorded, so every scoped read returned every tenant's rows. The primary keys were
--         (TableName, FieldName) and (DataSubjectIdHash, TableName, FieldName, RecordId), neither
--         carrying a tenant term, so one tenant's registration OVERWROTE another's.
-- AFTER:  TenantId is NOT NULL, defaults to the reserved '__untenanted__' sentinel, and is part of
--         both primary keys. Reads bind the caller's tenant term and the sentinel; writes key on it.
--
--
-- WHO NEEDS THIS
-- --------------
-- Any database whose two inventory tables were created BEFORE the release that ships this file —
-- whether provisioned from an earlier 001 or, far more commonly, by the store's own AutoCreateSchema
-- path. Both creation paths guard on table existence (IF NOT EXISTS / sys.tables), so neither adds a
-- column to a table that is already there. Upgrading the package alone does NOT reshape the table,
-- and nothing about the running system says so until a query fails.
--
-- A database created by THIS release, from either path, already carries the total shape. This script
-- detects that and does nothing.
--
-- CUSTOM OBJECT NAMES: this targets the DEFAULT names. A deployment that configured a different
-- schema or table name needs the same steps against its own names — the auto-create path will NOT
-- retrofit them, for the reason above. Copy this file and substitute the names.
--
--
-- RUN THIS TOGETHER WITH THE PACKAGE THAT INTRODUCED IT
-- ----------------------------------------------------
-- This migration is not tolerant in either direction, and the asymmetry is worth stating plainly:
--
--   Old package, new schema.  The old package's INSERT names no TenantId. The column's default
--                             supplies the sentinel, so writes succeed and every row lands untenanted
--                             — silently collapsing every tenant into one partition.
--   New package, old schema.  Every statement names TenantId against a table that has no such column,
--                             so the store fails LOUDLY on first use with "Invalid column name
--                             'TenantId'" rather than answering wrongly.
--
-- The second is the safe order and the one to plan for: upgrade the package, take the outage, run
-- this. It fails closed. The first does not.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run during a maintenance window with the data-inventory store stopped. Both key steps DROP AND
-- RECREATE a CLUSTERED primary key, which rebuilds the table — plan for the time and log that implies
-- on a large DiscoveredDataLocations. Take a backup you have restored at least once.
--
-- Unlike 003, no nonclustered index is dropped: IX_*_DataCategory, IX_*_DataSubject and IX_*_Table do
-- not reference TenantId, so they do not block the ALTER. The PRIMARY KEY is what has to be rebuilt
-- here, because a column cannot be added to a key in place.
--
-- The COLLATE clause is stated for the same reason 003 states it: without it the column inherits the
-- database default, which is typically case-INSENSITIVE, while the framework compares tenant terms
-- ordinally. The database would then be MORE permissive than the framework, which is a cross-tenant
-- read. Latin1_General_BIN2 makes them agree. The column does not exist yet, so pinning it here costs
-- nothing and splits no live tenant.
--
-- It is guarded and re-runnable: every step tests for the state it is about to create, so running it
-- twice, or against a database that is already converged, changes nothing.
--
--
-- WHAT THIS CANNOT REPAIR
-- -----------------------
-- Rows already destroyed by the overwrite this fixes are GONE. Where two tenants had registered the
-- same table and field, the database retained one row; the other was overwritten in place and left no
-- trace to recover. This script stops further overwrites — it cannot reconstruct what earlier ones
-- took. After migrating, have each tenant re-run its registration so any silently-lost entry is
-- restored. A registration is how the erasure path knows a field holds personal data, so a missing one
-- means that field is skipped and the erasure still reports success.

SET NOCOUNT ON;

-- Explicit transaction wrapper -- see Excalibur.EventSourcing.SqlServer's
-- 006_ConvergeUntenantedToDefaultTenant.sql header for why: without it, the collision guard's
-- THROW does not roll back steps that already ran (measured live against this package's Postgres
-- twin, 003_MakeDataInventoryTenantTotal.sql; the same defect, same mechanism).
--
-- The transaction is one half. The other is the one-line guard at the top of every batch below,
-- and neither replaces the other:
--
--   XACT_ABORT ON rolls the transaction back the moment a guard's THROW fires. But GO is a CLIENT
--   batch separator, not a statement -- whatever applies this script sends each batch to the
--   server as a SEPARATE unit, and once the transaction is gone the batches AFTER the refusal
--   would run unprotected in autocommit and do the work anyway. The rollback makes what already
--   ran reversible; something else has to stop what has not run yet.
--
--   IF @@TRANCOUNT = 0 SET NOEXEC ON; is that something. A rolled-back transaction leaves
--   @@TRANCOUNT at zero, so the first batch after a refusal turns execution off for the rest of
--   the session and every later batch is compiled but never run. SET NOEXEC OFF at the very end
--   restores the session on the path where nothing refused.
--
--
-- BOTH OF THOSE ASSUME THE WHOLE SCRIPT IS APPLIED ON ONE CONNECTION. A transaction belongs to a
-- session, so a client that reconnects between batches loses it at the first GO -- and the guard
-- would then quietly switch the rest of the script off, which is the "completed having done
-- nothing" outcome this migration exists to refuse. The batch immediately below the transaction
-- therefore checks for that case explicitly and REFUSES, naming it. It is the one batch that can:
-- nothing has run yet that could have refused, so @@TRANCOUNT = 0 there means the session was lost
-- and nothing else. sqlcmd keeps one connection by default; a migration runner may need telling.
-- That guard is deliberately plain T-SQL rather than sqlcmd's :on error exit. The directive does
-- the same job, but it is a CLIENT command rather than a statement, so any tool that is not
-- sqlcmd sends it to the server and the whole script dies on its first line with
-- "Incorrect syntax near ':'" -- having done nothing at all. This script is meant to be applied
-- by whatever your deployment already uses, not only by sqlcmd.
--
-- ONE THING THE GUARD CANNOT DO IS SET THE PROCESS EXIT CODE. On a refusal sqlcmd still exits 0
-- unless you pass -b. If a pipeline branches on this script's exit code, run it as
--     sqlcmd -b -i <this script>
-- or the pipeline will read a refused, no-op migration as a success.

SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

-- The transaction opened in the batch above must still be here. If it is not, the client did not
-- keep one session across this script's batches -- see the header. This is the only batch that can
-- tell that apart from a deliberate refusal, because nothing has run yet that could have refused.
IF @@TRANCOUNT = 0
BEGIN
    THROW 51006, N'This migration opens a transaction in its first batch and commits it in its last, so the whole script must be applied on a SINGLE connection. @@TRANCOUNT is 0 here, which means the client reconnected after the opening batch and the transaction is already gone: every remaining batch would run unprotected, and a refusal partway through would leave the database half-migrated with no way back. Refusing rather than converting anything. Apply the whole script on one connection (sqlcmd does this by default), then re-run.', 1;
END

-- ---------------------------------------------------------------------------------------
-- 1) [compliance].[DataInventoryRegistrations] — add the column.
--
--    Added WITH the default so SQL Server can materialise it NOT NULL against existing rows in one
--    statement: every pre-existing registration becomes untenanted, which is the only truthful
--    reading of a row written when the store had no concept of a tenant.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[compliance].[DataInventoryRegistrations]', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]')
                     AND name = N'TenantId')
BEGIN
    PRINT N'004: DataInventoryRegistrations — adding TenantId; existing rows become untenanted.';

    ALTER TABLE [compliance].[DataInventoryRegistrations]
        ADD [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT [DF_DataInventoryRegistrations_TenantId] DEFAULT N'__untenanted__';
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 2) [compliance].[DiscoveredDataLocations] — add the column. Same shape as step 1.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[compliance].[DiscoveredDataLocations]', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]')
                     AND name = N'TenantId')
BEGIN
    PRINT N'004: DiscoveredDataLocations — adding TenantId; existing rows become untenanted.';

    ALTER TABLE [compliance].[DiscoveredDataLocations]
        ADD [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT [DF_DiscoveredDataLocations_TenantId] DEFAULT N'__untenanted__';
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 3) Converge a column that already exists but is NULLABLE onto the total shape.
--
--    Steps 1 and 2 do not fire for such a database, so without this block it would reach the key
--    rebuild below still holding NULLs and fail there with a message about the key rather than about
--    the column. This is the state a half-applied run, or a consumer who added the column by hand,
--    leaves behind.
--
--    Backfill BEFORE the constraint: ALTER COLUMN ... NOT NULL fails outright while any row is NULL.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]')
             AND name = N'TenantId' AND is_nullable = 1)
BEGIN
    UPDATE [compliance].[DataInventoryRegistrations]
        SET [TenantId] = N'__untenanted__'
        WHERE [TenantId] IS NULL;

    ALTER TABLE [compliance].[DataInventoryRegistrations]
        ALTER COLUMN [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]')
             AND name = N'TenantId' AND is_nullable = 1)
BEGIN
    UPDATE [compliance].[DiscoveredDataLocations]
        SET [TenantId] = N'__untenanted__'
        WHERE [TenantId] IS NULL;

    ALTER TABLE [compliance].[DiscoveredDataLocations]
        ALTER COLUMN [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 4) COLLISION PRE-FLIGHT, before either key is touched.
--
--    Widening a key cannot itself collide: the old key was already unique, and adding a column to it
--    only ever splits rows apart. The collision this guards is the BACKFILL in step 3 — a database
--    where the column already existed and held a mix of NULLs and real terms can, once the NULLs
--    become the sentinel, hold two rows that are identical under the new key.
--
--    It REFUSES and names the rows rather than choosing between them. Each duplicate is a registration
--    some tenant is relying on; picking a survivor silently would drop a field from that tenant's
--    erasure coverage, which is the same class of loss this migration exists to stop. Resolve them by
--    hand — decide which term each row belongs to — then re-run. This script is re-runnable, so a
--    resolved database proceeds cleanly on the next attempt.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[compliance].[DataInventoryRegistrations]', 'U') IS NOT NULL
BEGIN
    DECLARE @RegDupes NVARCHAR(MAX);

    SELECT @RegDupes = STRING_AGG(CONVERT(NVARCHAR(MAX), [Duplicate]), N'; ')
    FROM (
        -- COLLATE DATABASE_DEFAULT on the tenant term is required, not decorative. TenantId is
        -- Latin1_General_BIN2 while the other columns carry the database default, and concatenating
        -- across two collations fails outright with "cannot resolve collation conflict". Coercing it
        -- for the MESSAGE only is safe: the GROUP BY below still compares TenantId under BIN2, so the
        -- duplicate detection keeps the binary semantic this migration establishes.
        SELECT N'(' + [TableName] + N', ' + [FieldName] + N', '
               + ([TenantId] COLLATE DATABASE_DEFAULT) + N') x'
               + CONVERT(NVARCHAR(20), COUNT_BIG(*)) AS [Duplicate]
        FROM [compliance].[DataInventoryRegistrations]
        GROUP BY [TableName], [FieldName], [TenantId]
        HAVING COUNT_BIG(*) > 1
    ) AS d;

    IF @RegDupes IS NOT NULL
    BEGIN
        RAISERROR (N'004 ABORTED: [compliance].[DataInventoryRegistrations] holds rows that are duplicates under the new key (TableName, FieldName, TenantId). Nothing has been keyed and no row has been chosen over another. Resolve these by assigning each its correct tenant term, then re-run: %s',
            16, 1, @RegDupes);
    END
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF OBJECT_ID(N'[compliance].[DiscoveredDataLocations]', 'U') IS NOT NULL
BEGIN
    DECLARE @LocDupes NVARCHAR(MAX);

    SELECT @LocDupes = STRING_AGG(CONVERT(NVARCHAR(MAX), [Duplicate]), N'; ')
    FROM (
        -- COLLATE DATABASE_DEFAULT on the tenant term: see the note on the registrations guard above.
        SELECT N'(' + [DataSubjectIdHash] + N', ' + [TableName] + N', ' + [FieldName] + N', '
               + [RecordId] + N', ' + ([TenantId] COLLATE DATABASE_DEFAULT) + N') x'
               + CONVERT(NVARCHAR(20), COUNT_BIG(*)) AS [Duplicate]
        FROM [compliance].[DiscoveredDataLocations]
        GROUP BY [DataSubjectIdHash], [TableName], [FieldName], [RecordId], [TenantId]
        HAVING COUNT_BIG(*) > 1
    ) AS d;

    IF @LocDupes IS NOT NULL
    BEGIN
        RAISERROR (N'004 ABORTED: [compliance].[DiscoveredDataLocations] holds rows that are duplicates under the new key (DataSubjectIdHash, TableName, FieldName, RecordId, TenantId). Nothing has been keyed and no row has been chosen over another. Resolve these, then re-run: %s',
            16, 1, @LocDupes);
    END
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 5) Rebuild the primary keys to include TenantId.
--
--    The guard tests the KEY's COMPOSITION, not merely the constraint's existence, so a database
--    already carrying the wide key is left alone and one carrying the narrow key is rebuilt. Testing
--    only for the name would skip exactly the databases that need this.
--
--    A column cannot be added to a key in place, so the constraint is dropped and recreated. Between
--    the DROP and the ADD the table has no primary key; run this with the store stopped.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.key_constraints
           WHERE name = N'PK_DataInventoryRegistrations'
             AND parent_object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]'))
   AND NOT EXISTS (SELECT * FROM sys.key_constraints kc
                   JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id
                                            AND ic.index_id = kc.unique_index_id
                   JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                   WHERE kc.name = N'PK_DataInventoryRegistrations'
                     AND kc.parent_object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]')
                     AND c.name = N'TenantId')
BEGIN
    PRINT N'004: DataInventoryRegistrations — rebuilding PK to (TableName, FieldName, TenantId).';

    ALTER TABLE [compliance].[DataInventoryRegistrations]
        DROP CONSTRAINT [PK_DataInventoryRegistrations];

    ALTER TABLE [compliance].[DataInventoryRegistrations]
        ADD CONSTRAINT [PK_DataInventoryRegistrations]
            PRIMARY KEY ([TableName], [FieldName], [TenantId]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF EXISTS (SELECT * FROM sys.key_constraints
           WHERE name = N'PK_DiscoveredDataLocations'
             AND parent_object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]'))
   AND NOT EXISTS (SELECT * FROM sys.key_constraints kc
                   JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id
                                            AND ic.index_id = kc.unique_index_id
                   JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                   WHERE kc.name = N'PK_DiscoveredDataLocations'
                     AND kc.parent_object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]')
                     AND c.name = N'TenantId')
BEGIN
    PRINT N'004: DiscoveredDataLocations — rebuilding PK to include TenantId.';

    ALTER TABLE [compliance].[DiscoveredDataLocations]
        DROP CONSTRAINT [PK_DiscoveredDataLocations];

    ALTER TABLE [compliance].[DiscoveredDataLocations]
        ADD CONSTRAINT [PK_DiscoveredDataLocations]
            PRIMARY KEY ([DataSubjectIdHash], [TableName], [FieldName], [RecordId], [TenantId]);
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- 6) The defaults, guarded separately: a database that reached NOT NULL by some other route still
--    needs one, and the blocks above no longer fire once the column is total.
--
--    The default is what makes the column total for a writer that omits it entirely. The store always
--    binds the term explicitly (through KeyedTenantPartition, which has no empty inhabitant), so this
--    is a backstop for hand-written INSERTs rather than something the store relies on.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]') AND name = N'TenantId')
   AND NOT EXISTS (SELECT * FROM sys.default_constraints
                   WHERE parent_object_id = OBJECT_ID(N'[compliance].[DataInventoryRegistrations]')
                     AND name = N'DF_DataInventoryRegistrations_TenantId')
BEGIN
    ALTER TABLE [compliance].[DataInventoryRegistrations]
        ADD CONSTRAINT [DF_DataInventoryRegistrations_TenantId] DEFAULT N'__untenanted__' FOR [TenantId];
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]') AND name = N'TenantId')
   AND NOT EXISTS (SELECT * FROM sys.default_constraints
                   WHERE parent_object_id = OBJECT_ID(N'[compliance].[DiscoveredDataLocations]')
                     AND name = N'DF_DiscoveredDataLocations_TenantId')
BEGIN
    ALTER TABLE [compliance].[DiscoveredDataLocations]
        ADD CONSTRAINT [DF_DiscoveredDataLocations_TenantId] DEFAULT N'__untenanted__' FOR [TenantId];
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

COMMIT TRANSACTION;
GO

SET NOEXEC OFF;
GO
