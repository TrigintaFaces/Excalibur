-- SQL Server migration for Excalibur.Compliance.SqlServer -- legal-hold release reason + optimistic concurrency
-- Version: 4.0
--
-- WHY THIS SCRIPT EXISTS AT ALL, which is the part worth reading.
--
-- The legal-hold store gained two columns: `ReleaseReason`, which records WHY a hold was released, and
-- `Version`, which lets releasing a hold refuse to write over a hold that moved under the writer -- the
-- release is a conditional UPDATE whose predicate names the version it read, so two concurrent releases
-- cannot both succeed and the loser is told rather than silently winning.
--
-- Both were added to the CREATE TABLE in 001 and to the store's own provisioning DDL. For a NEW database
-- that is sufficient. For a database already provisioned by an earlier version of this package it is not,
-- and the failure mode is the one that matters: the provisioning code is guarded by an existence check, so
-- on an existing database it sees the table, skips, and never adds the columns. The first release then fails
-- on the CONSUMER'S database and never on ours -- the shipped tests all run against a freshly created
-- schema, where both columns are always present.
--
-- So an upgrading consumer needs an ALTER, and that is this file.
--
-- RUN THIS ONCE, AFTER UPGRADING THE PACKAGE AND BEFORE STARTING THE APPLICATION.
--
-- RE-RUNNING IS SAFE, and it is safe by construction rather than by bookkeeping. Each block runs only if the
-- column is ABSENT, and the block adds it -- so the precondition is consumed by the act it guards. A second
-- run finds the column present and does nothing. There is no flag to remember and no migration table that
-- can fall out of step with the schema it describes.
--
-- A NOTE ON THE DEFAULT AND THE NAMED CONSTRAINT. `Version INT NOT NULL ... DEFAULT 0` matches the CREATE
-- TABLE in 001 exactly, including the constraint NAME -- an unnamed default gets a server-generated name
-- that differs between databases, which makes a later migration unable to refer to it. 0 is the right value
-- for a pre-existing row: it means "never released, never contended", which is true of every hold that
-- existed before this column did. Back-filling anything else would invent a history.
--
-- ReleaseReason is NULLABLE and deliberately NOT back-filled. A hold released before this column existed has
-- no recorded reason, and NULL says exactly that. Writing a placeholder would make an absent reason
-- indistinguishable from a recorded one.
--
-- TABLE NAME. This script uses the DEFAULT schema and table name. A deployment that configured a different
-- table name through options must apply the same ALTERs to that table instead -- the store reads the name
-- from configuration, and this script cannot.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[compliance].[LegalHolds]') AND name = N'ReleaseReason')
BEGIN
    ALTER TABLE [compliance].[LegalHolds]
        ADD ReleaseReason NVARCHAR(1000) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[compliance].[LegalHolds]') AND name = N'Version')
BEGIN
    ALTER TABLE [compliance].[LegalHolds]
        ADD Version INT NOT NULL
            CONSTRAINT DF_LegalHolds_Version DEFAULT 0;
END
GO
