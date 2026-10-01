-- SQL Server migration for Excalibur.Compliance.SqlServer -- legal-basis column replacement
-- Version: 2.0
--
-- WHAT THIS DOES, AND WHY IT DISCARDS THE OLD VALUE RATHER THAN CONVERTING IT
--
-- The legal-basis enumerations gained a zero member meaning "no ground was established", and every other
-- member moved up by one. These values are persisted as INT, so every row written before that change holds
-- an ordinal that now names a DIFFERENT legal basis: a stored 1 that meant "legal obligation" would read
-- back as "freedom of expression".
--
-- This script REPLACES the column rather than renaming it, and the replacement is deliberately empty of
-- the old data. Pre-upgrade ordinals are NOT carried forward. Every existing row lands on 0, which is the
-- enumeration's "not established" member, and that is the honest value: this framework can no longer say
-- what ground the row was written under, and the enumeration now has a member that says exactly that.
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

    -- The default backfills every existing row with 0 (not established). It is then dropped so the column
    -- matches what the create script declares: NOT NULL with no default, because the store always supplies
    -- the value on insert.
    ALTER TABLE [compliance].[ErasureRequests]
        ADD LegalBasisV2 INT NOT NULL CONSTRAINT DF_ErasureRequests_LegalBasisV2 DEFAULT 0;
    ALTER TABLE [compliance].[ErasureRequests] DROP CONSTRAINT DF_ErasureRequests_LegalBasisV2;
    ALTER TABLE [compliance].[ErasureRequests] DROP COLUMN LegalBasis;

    COMMIT TRANSACTION;

    PRINT 'compliance.ErasureRequests: LegalBasis replaced by LegalBasisV2; existing rows read as not established.';
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
    ALTER TABLE [compliance].[ErasureCertificates] DROP CONSTRAINT DF_ErasureCertificates_LegalBasisV2;
    ALTER TABLE [compliance].[ErasureCertificates] DROP COLUMN LegalBasis;

    COMMIT TRANSACTION;

    PRINT 'compliance.ErasureCertificates: LegalBasis replaced by LegalBasisV2; existing rows read as not established.';
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
    ALTER TABLE [compliance].[LegalHolds] DROP CONSTRAINT DF_LegalHolds_BasisV2;
    ALTER TABLE [compliance].[LegalHolds] DROP COLUMN Basis;

    COMMIT TRANSACTION;

    PRINT 'compliance.LegalHolds: Basis replaced by BasisV2; existing holds read as not established.';
END
ELSE
BEGIN
    PRINT 'compliance.LegalHolds: already migrated, nothing to do.';
END
GO

-- WHAT THIS MEANS FOR YOUR EXISTING RECORDS. A pre-upgrade erasure request, certificate or legal hold will
-- report its legal basis as "not established" after this migration. The record itself is intact -- its
-- identifiers, timestamps, counts, signature and every other claim are untouched. Only the legal-basis
-- ordinal is not carried across the renumbering. A signed certificate's own payload is stored separately
-- and in full, so its signature still verifies; this column is a projection of one claim, not the source of
-- it.
--
-- WHAT THIS SCRIPT DOES NOT TOUCH. Consent records carry a different enumeration -- the Article 6 lawful
-- basis for processing -- which this change does not renumber. If your deployment stores consent, its
-- legal-basis column is unaffected and must NOT be replaced.
