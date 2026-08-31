-- SQL Server MIGRATION for Excalibur.Outbox.SqlServer — TENANT COLUMNS, PORTABLE MAXIMUM
-- Version: 1.0
--
-- Narrows TenantId from NVARCHAR(255) to NVARCHAR(64) on the three tables 001_CreateOutboxSchema.sql
-- owns — [dbo].[OutboxMessages], [dbo].[OutboxMessageTransports] and [dbo].[DeadLetterQueue] — to
-- the shape 001 now provisions. Run this ONLY against tables created by an earlier version of that
-- script. A database provisioned from the current script already has this shape, and each block
-- below detects that and does nothing.
--
-- Schema and table names are the defaults 001 uses. If you overrode any of them, edit the literals
-- below to match.
--
--
-- WHY THE CREATE SCRIPT CANNOT DELIVER THIS ON ITS OWN
-- -----------------------------------------------------
-- 001 guards each table on the table being ABSENT. On a database that already ran an earlier
-- version the guard sees the table and skips the whole definition, so the narrowed column never
-- arrives. Re-running 001 is not an upgrade path; this script is.
--
-- 001 does carry ALTER blocks that touch TenantId, and none of them reaches this population, by
-- design: one is guarded on the column still being NULLABLE, another on the column NOT already
-- carrying the Latin1_General_BIN2 collation. An install from the previous release satisfies
-- neither condition — its column is already NOT NULL and already pinned to that collation — so
-- those blocks correctly do nothing and the width is left untouched. This script is guarded on the
-- property that is ACTUALLY changing, the declared length, so it reaches exactly the databases that
-- still need it and is a no-op everywhere else.
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
-- A value LONGER than 64 characters is REFUSED, never truncated. On [dbo].[DeadLetterQueue] that is
-- not a lossy label but a KEY MERGE: TenantId is a component of PK_DeadLetterQueue (Id, TenantId),
-- so two tenants whose identifiers share their first 64 characters stop being two rows, and one
-- tenant's dead-lettered message satisfies the other tenant's key. On the other two tables it
-- merges two tenants' operational scopes: a sweep, a retry query or a delivery audit scoped to
-- either would return the other tenant's rows with no error to show for it. Either way this script
-- stops and names the rows rather than choosing for you. Re-key the reported rows to identifiers of
-- 64 characters or fewer, then re-run.
--
-- Such rows can only predate the length guard the framework now applies at construction. A
-- deployment that has only ever written through a current release has none.
--
--
-- COLLATION AND NULLABILITY ARE RESTATED, NOT ASSUMED
-- ----------------------------------------------------
-- ALTER COLUMN replaces the whole column definition: an omitted COLLATE resets the column to the
-- database default, and an omitted NOT NULL makes it nullable. Both are restated on every ALTER
-- below, so an upgraded column matches a freshly provisioned one by declaration rather than by
-- inheritance.
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
-- THE REFUSAL IS ATOMIC ACROSS EVERY TABLE, AND THAT IS WHY THE COUNTS COME FIRST
-- --------------------------------------------------------------------------------
-- Every table is counted BEFORE any table is altered. If any one of them holds an over-long
-- identifier, nothing is narrowed at all -- not even the tables that are clean.
--
-- The alternative, refusing table by table, would leave a database with some tables narrow and
-- some still wide. That is a third shape, shipped by no version, MANUFACTURED by the safety path
-- of a script whose whole reason for existing is to remove exactly that divergence. It would also
-- make the word REFUSED untrue: an operator reads it and believes the database is untouched.
--
-- The cost is one indexed-free count per table ahead of the first alter, and the script stays
-- re-runnable. The per-table checks further down are kept as well, for a runner that continues
-- past a failed batch instead of stopping.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Narrowing a column rewrites it, and on [dbo].[DeadLetterQueue] the primary key must be dropped
-- and rebuilt around the alter because SQL Server will not alter a column a key depends on. Run
-- this during a maintenance window with the outbox processor stopped. TenantId participates in no
-- index on [dbo].[OutboxMessages] or [dbo].[OutboxMessageTransports], so those two alter in place.
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

-- ---------------------------------------------------------------------------
-- PRE-FLIGHT: count every table before altering any of them.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'tempdb..#TenantNarrowPreflight') IS NOT NULL
BEGIN
    DROP TABLE #TenantNarrowPreflight;
END

CREATE TABLE #TenantNarrowPreflight (
    TableName     SYSNAME NOT NULL,
    OffendingRows BIGINT  NOT NULL,
    Longest       INT     NOT NULL
);

DECLARE @preflightSql NVARCHAR(MAX);

IF COL_LENGTH(N'[dbo].[OutboxMessages]', N'TenantId') IS NOT NULL
BEGIN
    -- Dynamic, because a static reference to TenantId would fail this whole batch at compile time
    -- on a table that exists without the column -- a case the per-table blocks below refuse with a
    -- message that explains it.
    SET @preflightSql = N'INSERT INTO #TenantNarrowPreflight (TableName, OffendingRows, Longest)
        SELECT N''[dbo].[OutboxMessages]'', COUNT_BIG(*), MAX(DATALENGTH(TenantId) / 2)
          FROM [dbo].[OutboxMessages]
         WHERE DATALENGTH(TenantId) / 2 > 64
        HAVING COUNT_BIG(*) > 0;';
    EXEC sp_executesql @preflightSql;
END

IF COL_LENGTH(N'[dbo].[OutboxMessageTransports]', N'TenantId') IS NOT NULL
BEGIN
    -- Dynamic, because a static reference to TenantId would fail this whole batch at compile time
    -- on a table that exists without the column -- a case the per-table blocks below refuse with a
    -- message that explains it.
    SET @preflightSql = N'INSERT INTO #TenantNarrowPreflight (TableName, OffendingRows, Longest)
        SELECT N''[dbo].[OutboxMessageTransports]'', COUNT_BIG(*), MAX(DATALENGTH(TenantId) / 2)
          FROM [dbo].[OutboxMessageTransports]
         WHERE DATALENGTH(TenantId) / 2 > 64
        HAVING COUNT_BIG(*) > 0;';
    EXEC sp_executesql @preflightSql;
END

IF COL_LENGTH(N'[dbo].[DeadLetterQueue]', N'TenantId') IS NOT NULL
BEGIN
    -- Dynamic, because a static reference to TenantId would fail this whole batch at compile time
    -- on a table that exists without the column -- a case the per-table blocks below refuse with a
    -- message that explains it.
    SET @preflightSql = N'INSERT INTO #TenantNarrowPreflight (TableName, OffendingRows, Longest)
        SELECT N''[dbo].[DeadLetterQueue]'', COUNT_BIG(*), MAX(DATALENGTH(TenantId) / 2)
          FROM [dbo].[DeadLetterQueue]
         WHERE DATALENGTH(TenantId) / 2 > 64
        HAVING COUNT_BIG(*) > 0;';
    EXEC sp_executesql @preflightSql;
END

IF EXISTS (SELECT * FROM #TenantNarrowPreflight)
BEGIN
    DECLARE @preflightMsg NVARCHAR(2048) = N'002 REFUSED: a stored tenant identifier is longer than 64 characters, so NOTHING has been narrowed -- not even the tables that are clean, because a part-narrowed database is a shape no version of this schema ships. Offending table(s):';

    SELECT @preflightMsg = @preflightMsg + N' ' + TableName
         + N' (' + CONVERT(NVARCHAR(20), OffendingRows) + N' row(s), longest '
         + CONVERT(NVARCHAR(20), Longest) + N');'
      FROM #TenantNarrowPreflight;

    SET @preflightMsg = @preflightMsg
        + N' Narrowing would truncate those identifiers, merging two tenants that share their first 64 characters into one -- and on [dbo].[DeadLetterQueue], where TenantId is a component of the primary key, that is a key merge rather than a lossy label. Nothing has been changed. Re-key the reported rows to identifiers of 64 characters or fewer, then re-run.';

    THROW 50002, @preflightMsg, 1;
END

DROP TABLE #TenantNarrowPreflight;
GO

-- ---------------------------------------------------------------------------
-- [dbo].[OutboxMessages] — TenantId is in no index; alters in place.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[OutboxMessages]', N'U') IS NULL
BEGIN
    PRINT '002: [dbo].[OutboxMessages] is not present; nothing to upgrade. Provision with 001_CreateOutboxSchema.sql instead.';
END
ELSE
BEGIN
    DECLARE @width INT = (SELECT max_length FROM sys.columns
                          WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessages]') AND name = N'TenantId');
    DECLARE @msg NVARCHAR(2048);
    DECLARE @offenders BIGINT;
    DECLARE @longest INT;

    IF @width IS NULL
    BEGIN
        THROW 50002, '002 REFUSED: [dbo].[OutboxMessages] has no TenantId column at all, so this table did not come from any version of 001_CreateOutboxSchema.sql. Nothing has been changed. Reconcile the table against 001 before upgrading it.', 1;
    END

    IF @width = 128
    BEGIN
        PRINT '002: [dbo].[OutboxMessages].TenantId is already NVARCHAR(64); nothing to do.';
    END
    ELSE
    BEGIN
        SELECT @offenders = COUNT_BIG(*), @longest = MAX(DATALENGTH(TenantId) / 2)
          FROM [dbo].[OutboxMessages]
         WHERE DATALENGTH(TenantId) / 2 > 64;

        IF @offenders > 0
        BEGIN
            SET @msg = CONCAT(N'002 REFUSED: ', @offenders,
                N' row(s) in [dbo].[OutboxMessages] hold a tenant identifier longer than 64 characters (longest: ',
                @longest,
                N'). Narrowing the column would truncate them, merging two tenants whose identifiers share their first 64 characters into one operational scope: a sweep or retry query scoped to either would return the other tenant''s messages. Nothing has been changed. Re-key those rows to identifiers of 64 characters or fewer, then re-run.');
            THROW 50002, @msg, 1;
        END

        ALTER TABLE [dbo].[OutboxMessages]
            ALTER COLUMN TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

        PRINT '002: [dbo].[OutboxMessages].TenantId narrowed to NVARCHAR(64).';
    END
END
GO

-- ---------------------------------------------------------------------------
-- [dbo].[OutboxMessageTransports] — TenantId is in no index; alters in place.
--
-- This table's tenant column arrives through an upgrade block in 001, so a database may legitimately
-- not have it yet. That is a different repair from this one, so it is refused rather than guessed at.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[OutboxMessageTransports]', N'U') IS NULL
BEGIN
    PRINT '002: [dbo].[OutboxMessageTransports] is not present; nothing to upgrade. Provision with 001_CreateOutboxSchema.sql instead.';
END
ELSE
BEGIN
    DECLARE @twidth INT = (SELECT max_length FROM sys.columns
                           WHERE object_id = OBJECT_ID(N'[dbo].[OutboxMessageTransports]') AND name = N'TenantId');
    DECLARE @tmsg NVARCHAR(2048);
    DECLARE @toffenders BIGINT;
    DECLARE @tlongest INT;

    IF @twidth IS NULL
    BEGIN
        THROW 50002, '002 REFUSED: [dbo].[OutboxMessageTransports] has no TenantId column at all. Without it no tenant predicate is expressible on this table, and this script narrows a column rather than adding one. Nothing has been changed. Re-run 001_CreateOutboxSchema.sql, which adds the column and recovers each row''s tenant from its parent message, then re-run this script.', 1;
    END

    IF @twidth = 128
    BEGIN
        PRINT '002: [dbo].[OutboxMessageTransports].TenantId is already NVARCHAR(64); nothing to do.';
    END
    ELSE
    BEGIN
        SELECT @toffenders = COUNT_BIG(*), @tlongest = MAX(DATALENGTH(TenantId) / 2)
          FROM [dbo].[OutboxMessageTransports]
         WHERE DATALENGTH(TenantId) / 2 > 64;

        IF @toffenders > 0
        BEGIN
            SET @tmsg = CONCAT(N'002 REFUSED: ', @toffenders,
                N' row(s) in [dbo].[OutboxMessageTransports] hold a tenant identifier longer than 64 characters (longest: ',
                @tlongest,
                N'). Narrowing the column would truncate them, merging two tenants whose identifiers share their first 64 characters into one delivery scope. Nothing has been changed. Re-key those rows to identifiers of 64 characters or fewer, then re-run.');
            THROW 50002, @tmsg, 1;
        END

        ALTER TABLE [dbo].[OutboxMessageTransports]
            ALTER COLUMN TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

        PRINT '002: [dbo].[OutboxMessageTransports].TenantId narrowed to NVARCHAR(64).';
    END
END
GO

-- ---------------------------------------------------------------------------
-- [dbo].[DeadLetterQueue] — TenantId is a component of PK_DeadLetterQueue (Id, TenantId).
-- SQL Server will not alter a column a key depends on, so the key is dropped and rebuilt around the
-- alter. No other index on this table names TenantId.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[DeadLetterQueue]', N'U') IS NULL
BEGIN
    PRINT '002: [dbo].[DeadLetterQueue] is not present; nothing to upgrade. Provision with 001_CreateOutboxSchema.sql instead.';
END
ELSE
BEGIN
    DECLARE @dwidth INT = (SELECT max_length FROM sys.columns
                           WHERE object_id = OBJECT_ID(N'[dbo].[DeadLetterQueue]') AND name = N'TenantId');
    DECLARE @dmsg NVARCHAR(2048);
    DECLARE @doffenders BIGINT;
    DECLARE @dlongest INT;

    IF @dwidth IS NULL
    BEGIN
        THROW 50002, '002 REFUSED: [dbo].[DeadLetterQueue] has no TenantId column at all, so this table did not come from any version of 001_CreateOutboxSchema.sql. Nothing has been changed. Reconcile the table against 001 before upgrading it.', 1;
    END

    IF @dwidth = 128
    BEGIN
        PRINT '002: [dbo].[DeadLetterQueue].TenantId is already NVARCHAR(64); nothing to do.';
    END
    ELSE
    BEGIN
        SELECT @doffenders = COUNT_BIG(*), @dlongest = MAX(DATALENGTH(TenantId) / 2)
          FROM [dbo].[DeadLetterQueue]
         WHERE DATALENGTH(TenantId) / 2 > 64;

        IF @doffenders > 0
        BEGIN
            SET @dmsg = CONCAT(N'002 REFUSED: ', @doffenders,
                N' row(s) in [dbo].[DeadLetterQueue] hold a tenant identifier longer than 64 characters (longest: ',
                @dlongest,
                N'). TenantId is a component of PK_DeadLetterQueue (Id, TenantId), so narrowing the column would not merely truncate a label: two tenants sharing their first 64 characters would collapse onto ONE key, and one tenant''s dead-lettered message would satisfy the other tenant''s key. Nothing has been changed. Re-key those rows to identifiers of 64 characters or fewer, then re-run.');
            THROW 50002, @dmsg, 1;
        END

        IF EXISTS (SELECT * FROM sys.key_constraints
                   WHERE parent_object_id = OBJECT_ID(N'[dbo].[DeadLetterQueue]')
                     AND name = N'PK_DeadLetterQueue' AND type = N'PK')
        BEGIN
            ALTER TABLE [dbo].[DeadLetterQueue] DROP CONSTRAINT PK_DeadLetterQueue;
        END

        ALTER TABLE [dbo].[DeadLetterQueue]
            ALTER COLUMN TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

        ALTER TABLE [dbo].[DeadLetterQueue]
            ADD CONSTRAINT PK_DeadLetterQueue PRIMARY KEY (Id, TenantId);

        PRINT '002: [dbo].[DeadLetterQueue].TenantId narrowed to NVARCHAR(64) and PK_DeadLetterQueue rebuilt.';
    END
END
GO
