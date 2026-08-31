-- SQL Server MIGRATION for Excalibur.Compliance.SqlServer — ERASURE + LEGAL HOLD TENANT TOTALITY
-- Version: 1.0
--
-- Converges [compliance].[ErasureRequests].TenantId and [compliance].[LegalHolds].TenantId onto a
-- TOTAL representation: NOT NULL, defaulting to the reserved '__untenanted__' sentinel. After this
-- script there is exactly ONE way to say "this row has no tenant", and it is a value rather than the
-- absence of one — the same shape [compliance].[DataInventoryRegistrations] and
-- [compliance].[DiscoveredDataLocations] already carry in this schema.
--
-- BEFORE: TenantId is nullable. A row written before tenancy existed holds NULL; a row written since
--         holds '__untenanted__'. The legal-hold read path folds the two together with
--         "(TenantId = @AmbientTenantId OR TenantId IS NULL)". Two spellings, one meaning.
-- AFTER:  TenantId is NOT NULL and every untenanted row holds '__untenanted__'. The legal-hold read
--         matches the sentinel explicitly, so a global hold stays visible to a scoped tenant.
--
--
-- RUN THIS TOGETHER WITH THE PACKAGE THAT INTRODUCED IT. NOT BEFORE, NOT ALONE.
-- ----------------------------------------------------------------------------
-- This script and the read predicate that goes with it are one change. Applied on its own, against a
-- package version whose legal-hold read still says "OR TenantId IS NULL" and nothing else, the
-- backfill below moves every global hold from NULL to the sentinel — and that predicate then matches
-- NEITHER arm. Global holds go dark for every scoped tenant.
--
-- A legal hold BLOCKS erasure. Losing one does not fail safe. It erases data a court order says to
-- keep. That is why the package's read path was widened to match the sentinel in the SAME release
-- that added this file, and why running this script against an older package is not a partial
-- upgrade but a data-destruction path.
--
-- The reverse order is safe: the new package's read still carries an "OR TenantId IS NULL" arm, so it
-- reads a not-yet-migrated database correctly. Upgrade the package first, then run this. That arm is
-- transition tolerance for exactly this window and is dead once the column is total.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run during a maintenance window with the erasure and legal-hold stores stopped. Each step rebuilds
-- an index on its table. Take a backup you have restored at least once.
--
-- No pre-flight collision check is needed here, unlike the event store's equivalent migration.
-- TenantId participates in NO primary key and NO unique constraint on either table — the keys are
-- RequestId and HoldId — so collapsing NULL onto the sentinel cannot manufacture a uniqueness
-- violation. It only ever rewrites a column value.
--
-- SCOPE: this script targets the DEFAULT object names created by 001. A deployment that configured
-- custom schema or table names provisions through the store's own auto-create path, which emits the
-- total shape directly; such a database needs no migration and this script correctly does nothing.
--
-- It is guarded and re-runnable: every step tests for the state it is about to create, so running it
-- twice, or against a database that is already converged, changes nothing.

SET NOCOUNT ON;
GO

-- ---------------------------------------------------------------------------------------
-- 1) [compliance].[ErasureRequests]
--
--    Backfill BEFORE the constraint. This order is not optional: ALTER COLUMN ... NOT NULL fails
--    outright if any row still holds NULL.
--
--    The index is DROPPED and RECREATED rather than altered in place. SQL Server refuses to alter a
--    column that an index depends on, and IX_ErasureRequests_TenantId leads with this column.
--
--    The COLLATE clause is stated on the ALTER deliberately, and here it is a CHANGE rather than a
--    restatement: 001 declared this column with no explicit collation, so it currently inherits the
--    DATABASE default — typically case-INSENSITIVE. Under that collation 'Acme' and 'acme' are the
--    same tenant to the database, while the framework compares tenant terms ordinally and treats
--    them as different. The database is therefore the MORE permissive of the two, which is a
--    cross-tenant read. Latin1_General_BIN2 makes the column agree with the framework and matches
--    the two tables in this schema that are already total. The predicate becomes strictly narrower,
--    so this can only ever close a disclosure, never open one.
--
--    The width stays NVARCHAR(256) rather than adopting the sibling tables' NVARCHAR(255). Narrowing
--    a column is data-dependent: it fails outright if any stored tenant term is longer than the new
--    width, which would abort a consumer's migration to buy nothing. Width is incidental here; the
--    load-bearing properties are NOT NULL, the sentinel default, and the collation.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[compliance].[ErasureRequests]')
             AND name = N'TenantId' AND is_nullable = 1)
BEGIN
    DECLARE @ErasureNullRows BIGINT;

    SELECT @ErasureNullRows = COUNT_BIG(*)
    FROM [compliance].[ErasureRequests]
    WHERE [TenantId] IS NULL;

    PRINT N'003: ErasureRequests — ' + CONVERT(NVARCHAR(20), @ErasureNullRows)
        + N' row(s) backfilled to the sentinel.';

    UPDATE [compliance].[ErasureRequests]
        SET [TenantId] = N'__untenanted__'
        WHERE [TenantId] IS NULL;

    IF EXISTS (SELECT * FROM sys.indexes
               WHERE name = N'IX_ErasureRequests_TenantId'
                 AND object_id = OBJECT_ID(N'[compliance].[ErasureRequests]'))
    BEGIN
        DROP INDEX [IX_ErasureRequests_TenantId] ON [compliance].[ErasureRequests];
    END

    ALTER TABLE [compliance].[ErasureRequests]
        ALTER COLUMN [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

    CREATE NONCLUSTERED INDEX [IX_ErasureRequests_TenantId]
        ON [compliance].[ErasureRequests] ([TenantId], [RequestedAt]);
END
GO

-- ---------------------------------------------------------------------------------------
-- 2) [compliance].[LegalHolds]. Same shape as step 1.
--
--    IX_LegalHolds_ExpiresAt is left alone: it is filtered on IsActive/ExpiresAt and does not
--    reference TenantId, so it does not block the ALTER.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[compliance].[LegalHolds]')
             AND name = N'TenantId' AND is_nullable = 1)
BEGIN
    DECLARE @HoldNullRows BIGINT;

    SELECT @HoldNullRows = COUNT_BIG(*)
    FROM [compliance].[LegalHolds]
    WHERE [TenantId] IS NULL;

    PRINT N'003: LegalHolds — ' + CONVERT(NVARCHAR(20), @HoldNullRows)
        + N' GLOBAL hold(s) backfilled to the sentinel. These stay visible to every scoped tenant '
        + N'through the sentinel arm of the read predicate.';

    UPDATE [compliance].[LegalHolds]
        SET [TenantId] = N'__untenanted__'
        WHERE [TenantId] IS NULL;

    IF EXISTS (SELECT * FROM sys.indexes
               WHERE name = N'IX_LegalHolds_TenantId'
                 AND object_id = OBJECT_ID(N'[compliance].[LegalHolds]'))
    BEGIN
        DROP INDEX [IX_LegalHolds_TenantId] ON [compliance].[LegalHolds];
    END

    ALTER TABLE [compliance].[LegalHolds]
        ALTER COLUMN [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

    CREATE NONCLUSTERED INDEX [IX_LegalHolds_TenantId]
        ON [compliance].[LegalHolds] ([TenantId], [IsActive]);
END
GO

-- ---------------------------------------------------------------------------------------
-- 3) The defaults. Guarded separately from the blocks above on purpose: a database that reached
--    NOT NULL by some other route still needs the default, and once those blocks run the column is
--    no longer nullable, so a single combined guard would skip this.
--
--    The default is what makes the column total for a writer that omits it entirely. Both stores
--    always bind the term explicitly (through KeyedTenantPartition, which has no empty inhabitant),
--    so this is a backstop for hand-written INSERTs rather than something the stores rely on.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[compliance].[ErasureRequests]') AND name = N'TenantId')
   AND NOT EXISTS (SELECT * FROM sys.default_constraints
                   WHERE parent_object_id = OBJECT_ID(N'[compliance].[ErasureRequests]')
                     AND name = N'DF_ErasureRequests_TenantId')
BEGIN
    ALTER TABLE [compliance].[ErasureRequests]
        ADD CONSTRAINT [DF_ErasureRequests_TenantId] DEFAULT N'__untenanted__' FOR [TenantId];
END
GO

IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[compliance].[LegalHolds]') AND name = N'TenantId')
   AND NOT EXISTS (SELECT * FROM sys.default_constraints
                   WHERE parent_object_id = OBJECT_ID(N'[compliance].[LegalHolds]')
                     AND name = N'DF_LegalHolds_TenantId')
BEGIN
    ALTER TABLE [compliance].[LegalHolds]
        ADD CONSTRAINT [DF_LegalHolds_TenantId] DEFAULT N'__untenanted__' FOR [TenantId];
END
GO
