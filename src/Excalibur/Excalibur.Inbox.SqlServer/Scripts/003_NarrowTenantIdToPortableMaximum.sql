-- SQL Server MIGRATION for Excalibur.Inbox.SqlServer — TENANT COLUMN, PORTABLE MAXIMUM
-- Version: 1.0
--
-- Narrows [dbo].[inbox_messages].TenantId from NVARCHAR(255) to NVARCHAR(64) — the shape
-- 001_CreateInboxSchema.MultiTenant.sql now provisions, and 002_MigrateToMultiTenant.sql now adds.
-- Run this ONLY against a multi-tenant inbox table created or migrated by an earlier version of
-- those scripts. A database provisioned from the current ones already has this shape, and this
-- script detects that and does nothing.
--
-- This applies only to a MULTI-TENANT deployment. The single-tenant schema
-- (001_CreateInboxSchema.sql) has no TenantId column at all; running this against it reports that
-- and changes nothing, which is the correct outcome — see the refusal note below.
--
-- Schema and table names are the defaults those scripts use. If you overrode either, edit the
-- literals below to match.
--
--
-- WHY THE CREATE SCRIPT CANNOT DELIVER THIS ON ITS OWN
-- -----------------------------------------------------
-- 001 guards its table on the table being ABSENT. On a database that already ran an earlier version
-- the guard sees the table and skips the whole definition, so the narrowed column never arrives.
-- 002's own ALTER is guarded on the column not already carrying the Latin1_General_BIN2 collation,
-- and an install from the previous release already has that collation, so that block does not fire
-- either. Re-running either script is not an upgrade path; this one is.
--
-- This script is guarded on the property that is ACTUALLY changing, the declared length, so it
-- reaches exactly the databases that still need it and is a no-op everywhere else.
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
-- Nothing is backfilled and no row changes partition. TenantId was already NOT NULL in the version
-- this script upgrades from, so there is no absent value to fill: every row keeps its exact tenant
-- bytes, under its existing key.
--
-- A value LONGER than 64 characters is REFUSED, never truncated. Truncation here is a KEY MERGE,
-- not a lossy label: TenantId is a component of PK_inbox_messages (MessageId, HandlerType,
-- TenantId), which is the dedup and claim key. Two tenants whose identifiers share their first 64
-- characters would collapse onto ONE key, so one tenant's delivery of a message would be seen as
-- the other tenant's duplicate and SKIPPED — a silently dropped message, which is the exact outcome
-- an inbox exists to prevent. So this script stops and names the rows rather than choosing for you.
-- Re-key the reported rows to identifiers of 64 characters or fewer, then re-run.
--
-- Such rows can only predate the length guard the framework now applies at construction. A
-- deployment that has only ever written through a current release has none.
--
--
-- COLLATION AND NULLABILITY ARE RESTATED, NOT ASSUMED
-- ----------------------------------------------------
-- ALTER COLUMN replaces the whole column definition: an omitted COLLATE resets the column to the
-- database default, and an omitted NOT NULL makes it nullable. Both are restated below, so an
-- upgraded column matches a freshly provisioned one by declaration rather than by inheritance.
--
--
-- WHY max_length IS COMPARED AGAINST 128 AND NOT 64
-- --------------------------------------------------
-- sys.columns.max_length is in BYTES, and NVARCHAR stores two bytes per character. A 64-character
-- NVARCHAR column therefore reads back as max_length = 128, and the 255-character column this
-- script upgrades from reads back as 510. (An NVARCHAR(MAX) column reads back as -1, which is also
-- "not 128" and is narrowed like any other over-wide column.)
--
-- The row check uses DATALENGTH(TenantId) / 2 rather than LEN(TenantId), deliberately: LEN ignores
-- trailing spaces, so an identifier of 64 significant characters followed by trailing spaces would
-- measure as 64 and then lose those bytes in the alter. DATALENGTH counts what is actually stored.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- TenantId participates in PK_inbox_messages, and SQL Server will not alter a column a key depends
-- on, so the key is dropped and rebuilt around the alter. Run this during a maintenance window with
-- the inbox stopped: between the drop and the rebuild the dedup key is not enforced.
--
--
-- REPORTING A REFUSAL
-- ---------------------
-- A refusal below raises a SQL Server error with severity 16, which every client surfaces as a
-- failed batch. If your runner executes this file batch-by-batch and continues past a failure, make
-- it stop on error — a refused migration reported as a success leaves the column un-narrowed with
-- nothing to show for it. Under sqlcmd, run with -b.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'[dbo].[inbox_messages]', N'U') IS NULL
BEGIN
    PRINT '003: [dbo].[inbox_messages] is not present; nothing to upgrade. Provision with 001_CreateInboxSchema.MultiTenant.sql instead.';
END
ELSE
BEGIN
    DECLARE @width INT = (SELECT max_length FROM sys.columns
                          WHERE object_id = OBJECT_ID(N'[dbo].[inbox_messages]') AND name = N'TenantId');
    DECLARE @msg NVARCHAR(2048);
    DECLARE @offenders BIGINT;
    DECLARE @longest INT;

    IF @width IS NULL
    BEGIN
        THROW 50003, '003 REFUSED: [dbo].[inbox_messages] has no TenantId column at all. Either this is a SINGLE-TENANT inbox (001_CreateInboxSchema.sql), which has no tenant discriminator and needs no part of this script, or the table did not come from any version of the shipped schema. Nothing has been changed. If you are growing a single-tenant inbox into a multi-tenant one, run 002_MigrateToMultiTenant.sql first; this script narrows a column, it does not add one.', 1;
    END

    IF @width = 128
    BEGIN
        PRINT '003: [dbo].[inbox_messages].TenantId is already NVARCHAR(64); nothing to do.';
    END
    ELSE
    BEGIN
        SELECT @offenders = COUNT_BIG(*), @longest = MAX(DATALENGTH(TenantId) / 2)
          FROM [dbo].[inbox_messages]
         WHERE DATALENGTH(TenantId) / 2 > 64;

        IF @offenders > 0
        BEGIN
            SET @msg = CONCAT(N'003 REFUSED: ', @offenders,
                N' row(s) in [dbo].[inbox_messages] hold a tenant identifier longer than 64 characters (longest: ',
                @longest,
                N'). TenantId is a component of PK_inbox_messages (MessageId, HandlerType, TenantId), the dedup and claim key, so narrowing the column would not merely truncate a label: two tenants sharing their first 64 characters would collapse onto ONE key, and one tenant''s delivery would be seen as the other tenant''s duplicate and skipped. Nothing has been changed. Re-key those rows to identifiers of 64 characters or fewer, then re-run.');
            THROW 50003, @msg, 1;
        END

        IF EXISTS (SELECT * FROM sys.key_constraints
                   WHERE parent_object_id = OBJECT_ID(N'[dbo].[inbox_messages]')
                     AND name = N'PK_inbox_messages' AND type = N'PK')
        BEGIN
            ALTER TABLE [dbo].[inbox_messages] DROP CONSTRAINT PK_inbox_messages;
        END

        ALTER TABLE [dbo].[inbox_messages]
            ALTER COLUMN TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

        ALTER TABLE [dbo].[inbox_messages]
            ADD CONSTRAINT PK_inbox_messages PRIMARY KEY (MessageId, HandlerType, TenantId);

        PRINT '003: [dbo].[inbox_messages].TenantId narrowed to NVARCHAR(64) and PK_inbox_messages rebuilt.';
    END
END
GO
