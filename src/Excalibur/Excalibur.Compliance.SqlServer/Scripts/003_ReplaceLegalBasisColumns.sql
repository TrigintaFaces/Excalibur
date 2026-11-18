-- SQL Server migration for Excalibur.Compliance.SqlServer -- legal-basis column replacement
-- Version: 2.0
--
-- WHAT THIS DOES: IT CONVERTS THE STORED ORDINAL. IT DOES NOT DISCARD IT.
--
-- The legal-basis enumerations gained a zero member meaning "no ground was established", and every other
-- member moved up by one. These values are persisted as INT, so every row written before that change holds
-- an ordinal that now names a DIFFERENT legal basis: a stored 1 that meant "legal obligation" would read
-- back as "freedom of expression".
--
-- THE RENUMBERING WAS A PURE SHIFT, which is what makes conversion exact and free. Every member of both
-- affected enumerations moved up by exactly one, and none was renamed, reordered or removed, so old + 1
-- maps every stored value onto the member it was written as. This script adds the new column, backfills
-- it from the old one, and only then drops the old column.
--
-- A VALUE OUTSIDE THE OLD RANGE IS THE ONE CASE THAT CANNOT BE CONVERTED. It named no member of the old
-- enumeration, so it becomes 0 -- "not established" -- and each block REPORTS how many rows that applied
-- to, rather than reporting a clean conversion. If that count is zero, every ground was carried across.
--
-- SUPERSEDED, AND STATED BECAUSE ACTING ON IT COST DATA: an earlier version of this script discarded the
-- old ordinal, landing every row on 0, and justified it by asserting that "this framework can no longer
-- say what ground the row was written under". That was false -- the mapping above was always available.
-- If you have already run that version, the ordinal is gone and cannot be recovered from the database:
-- this script will find no old column and report that there is nothing to do. The tell is a population of
-- pre-upgrade erasure requests, certificates or legal holds whose basis reads "not established" while the
-- records themselves predate your upgrade.
--
-- A RENAME WOULD BE THE WRONG SHAPE HERE, and it is worth stating because it is the obvious first idea.
-- Renaming preserves the values, so the old ordinals would survive under the new column name and be read
-- through the new numbering -- which is precisely the silent misreading this migration exists to prevent.
-- Renaming is the right tool when the encoding is unchanged; it is the wrong tool when the encoding is the
-- thing that changed.
--
-- THE NEW COLUMN NAME IS THE PROTECTION. The store binds LegalBasisV2 and BasisV2, so a database that has
-- not run this script fails at startup naming the missing column, rather than starting and misreading every
-- stored basis. A loud error you fix in one step is the better failure.
--
-- RUN THIS ONCE, AFTER UPGRADING THE PACKAGE AND BEFORE STARTING THE APPLICATION.
--
-- RE-RUNNING IS SAFE. Each block runs only if the OLD column is still present, and the block drops that
-- column -- so the precondition is CONSUMED by the act it guards. A second run finds no old column and does
-- nothing. There is no flag to remember and no bookkeeping table that can fall out of step with the schema.
-- Each block is one transaction, so an interrupted run leaves the old column intact, still readable by the
-- previous package version and still migratable by running this again.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Erasure requests: the Article 17(1) ground the erasure was requested under.
IF COL_LENGTH('compliance.ErasureRequests', 'LegalBasis') IS NOT NULL
BEGIN
    BEGIN TRANSACTION;

    -- The default exists only so the column can be added NOT NULL to a populated table; the UPDATE below
    -- immediately overwrites every row with the converted value. It is then dropped so the column
    -- matches what the create script declares: NOT NULL with no default, because the store always supplies
    -- the value on insert.
    ALTER TABLE [compliance].[ErasureRequests]
        ADD LegalBasisV2 INT NOT NULL CONSTRAINT DF_ErasureRequests_LegalBasisV2 DEFAULT 0;

    -- CONVERT, do not discard. Every member of the old enumeration moved up by exactly one
    -- and none was renamed, reordered or removed, so LegalBasis + 1 recovers the stored ground
    -- exactly. A stored value outside the old range named no member of it, and that is the
    -- one case where 'not established' is the honest answer rather than a discarded one.
    --
    -- Dynamic SQL is load-bearing here: LegalBasisV2 is added in THIS batch, and a column added in the
    -- current batch is not visible to a statement compiled in it. The ADD sits inside
    -- IF ... BEGIN, so a GO cannot separate them; deferring compilation is what makes the
    -- backfill legal.
    EXEC sp_executesql N'UPDATE [compliance].[ErasureRequests] SET LegalBasisV2 = CASE WHEN LegalBasis BETWEEN 0 AND 6 THEN LegalBasis + 1 ELSE 0 END;';

    DECLARE @ErasureRequestsUnmapped INT, @ErasureRequestsConverted INT;
    EXEC sp_executesql
        N'SELECT @u = COUNT(*) FROM [compliance].[ErasureRequests] WHERE LegalBasisV2 = 0;',
        N'@u INT OUTPUT', @u = @ErasureRequestsUnmapped OUTPUT;
    EXEC sp_executesql
        N'SELECT @c = COUNT(*) FROM [compliance].[ErasureRequests] WHERE LegalBasisV2 <> 0;',
        N'@c INT OUTPUT', @c = @ErasureRequestsConverted OUTPUT;
    ALTER TABLE [compliance].[ErasureRequests] DROP CONSTRAINT DF_ErasureRequests_LegalBasisV2;
    ALTER TABLE [compliance].[ErasureRequests] DROP COLUMN LegalBasis;

    COMMIT TRANSACTION;

    PRINT 'compliance.ErasureRequests: LegalBasis converted to LegalBasisV2; ' + CAST(@ErasureRequestsConverted AS VARCHAR(20)) + ' row(s) carried their ground across the renumbering, ' + CAST(@ErasureRequestsUnmapped AS VARCHAR(20)) + ' row(s) held a value the old enumeration never named and now read as not established.';
END
ELSE
BEGIN
    PRINT 'compliance.ErasureRequests: already migrated, nothing to do.';
END
GO

-- Erasure certificates: the same ground, projected onto the certificate row.
IF COL_LENGTH('compliance.ErasureCertificates', 'LegalBasis') IS NOT NULL
BEGIN
    BEGIN TRANSACTION;

    ALTER TABLE [compliance].[ErasureCertificates]
        ADD LegalBasisV2 INT NOT NULL CONSTRAINT DF_ErasureCertificates_LegalBasisV2 DEFAULT 0;

    -- CONVERT, do not discard. Every member of the old enumeration moved up by exactly one
    -- and none was renamed, reordered or removed, so LegalBasis + 1 recovers the stored ground
    -- exactly. A stored value outside the old range named no member of it, and that is the
    -- one case where 'not established' is the honest answer rather than a discarded one.
    --
    -- Dynamic SQL is load-bearing here: LegalBasisV2 is added in THIS batch, and a column added in the
    -- current batch is not visible to a statement compiled in it. The ADD sits inside
    -- IF ... BEGIN, so a GO cannot separate them; deferring compilation is what makes the
    -- backfill legal.
    EXEC sp_executesql N'UPDATE [compliance].[ErasureCertificates] SET LegalBasisV2 = CASE WHEN LegalBasis BETWEEN 0 AND 6 THEN LegalBasis + 1 ELSE 0 END;';

    DECLARE @ErasureCertificatesUnmapped INT, @ErasureCertificatesConverted INT;
    EXEC sp_executesql
        N'SELECT @u = COUNT(*) FROM [compliance].[ErasureCertificates] WHERE LegalBasisV2 = 0;',
        N'@u INT OUTPUT', @u = @ErasureCertificatesUnmapped OUTPUT;
    EXEC sp_executesql
        N'SELECT @c = COUNT(*) FROM [compliance].[ErasureCertificates] WHERE LegalBasisV2 <> 0;',
        N'@c INT OUTPUT', @c = @ErasureCertificatesConverted OUTPUT;
    ALTER TABLE [compliance].[ErasureCertificates] DROP CONSTRAINT DF_ErasureCertificates_LegalBasisV2;
    ALTER TABLE [compliance].[ErasureCertificates] DROP COLUMN LegalBasis;

    COMMIT TRANSACTION;

    PRINT 'compliance.ErasureCertificates: LegalBasis converted to LegalBasisV2; ' + CAST(@ErasureCertificatesConverted AS VARCHAR(20)) + ' row(s) carried their ground across the renumbering, ' + CAST(@ErasureCertificatesUnmapped AS VARCHAR(20)) + ' row(s) held a value the old enumeration never named and now read as not established.';
END
ELSE
BEGIN
    PRINT 'compliance.ErasureCertificates: already migrated, nothing to do.';
END
GO

-- Legal holds: the Article 17(3) ground the hold is asserted under.
IF COL_LENGTH('compliance.LegalHolds', 'Basis') IS NOT NULL
BEGIN
    BEGIN TRANSACTION;

    ALTER TABLE [compliance].[LegalHolds]
        ADD BasisV2 INT NOT NULL CONSTRAINT DF_LegalHolds_BasisV2 DEFAULT 0;

    -- CONVERT, do not discard. Every member of the old enumeration moved up by exactly one
    -- and none was renamed, reordered or removed, so Basis + 1 recovers the stored ground
    -- exactly. A stored value outside the old range named no member of it, and that is the
    -- one case where 'not established' is the honest answer rather than a discarded one.
    --
    -- Dynamic SQL is load-bearing here: BasisV2 is added in THIS batch, and a column added in the
    -- current batch is not visible to a statement compiled in it. The ADD sits inside
    -- IF ... BEGIN, so a GO cannot separate them; deferring compilation is what makes the
    -- backfill legal.
    EXEC sp_executesql N'UPDATE [compliance].[LegalHolds] SET BasisV2 = CASE WHEN Basis BETWEEN 0 AND 6 THEN Basis + 1 ELSE 0 END;';

    DECLARE @LegalHoldsUnmapped INT, @LegalHoldsConverted INT;
    EXEC sp_executesql
        N'SELECT @u = COUNT(*) FROM [compliance].[LegalHolds] WHERE BasisV2 = 0;',
        N'@u INT OUTPUT', @u = @LegalHoldsUnmapped OUTPUT;
    EXEC sp_executesql
        N'SELECT @c = COUNT(*) FROM [compliance].[LegalHolds] WHERE BasisV2 <> 0;',
        N'@c INT OUTPUT', @c = @LegalHoldsConverted OUTPUT;
    ALTER TABLE [compliance].[LegalHolds] DROP CONSTRAINT DF_LegalHolds_BasisV2;
    ALTER TABLE [compliance].[LegalHolds] DROP COLUMN Basis;

    COMMIT TRANSACTION;

    PRINT 'compliance.LegalHolds: Basis converted to BasisV2; ' + CAST(@LegalHoldsConverted AS VARCHAR(20)) + ' row(s) carried their ground across the renumbering, ' + CAST(@LegalHoldsUnmapped AS VARCHAR(20)) + ' row(s) held a value the old enumeration never named and now read as not established.';
END
ELSE
BEGIN
    PRINT 'compliance.LegalHolds: already migrated, nothing to do.';
END
GO

-- WHAT THIS MEANS FOR YOUR EXISTING RECORDS. A pre-upgrade erasure request, certificate or legal hold
-- KEEPS the legal basis it was written under: the ordinal is converted, not dropped, and the record is
-- otherwise untouched -- its identifiers, timestamps, counts and signature are unchanged. A signed
-- certificate's own payload is stored separately and in full, so its signature still verifies; this
-- column is a projection of one claim, not the source of it. Only a value that named no member of the old
-- enumeration lands on "not established", and each block's notice tells you how many rows that was.
--
-- WHAT THIS SCRIPT DOES NOT TOUCH. Consent records carry a different enumeration -- the Article 6 lawful
-- basis for processing -- which this change does not renumber. If your deployment stores consent, its
-- legal-basis column is unaffected and must NOT be replaced.
