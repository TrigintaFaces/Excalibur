-- SQL Server MIGRATION for Excalibur.Saga.SqlServer — TENANT COLUMNS, PORTABLE MAXIMUM
-- Version: 1.0
--
-- Narrows TenantId from NVARCHAR(200) to NVARCHAR(64) on the two tables this package owns —
-- dispatch.sagas (01-SagaSchema.sql) and SagaTimeouts (SagaTimeouts.sql) — to the shape those
-- scripts now provision. Run this ONLY against tables created by an earlier version of them. A
-- database provisioned from the current scripts already has this shape, and each block below
-- detects that and does nothing.
--
-- SagaTimeouts is optional: it exists only where durable saga timeouts are used. Its block reports
-- and skips when the table is absent.
--
-- Schema and table names are the defaults those scripts use. If you overrode any of them, edit the
-- literals below to match.
--
--
-- WHY THE CREATE SCRIPTS CANNOT DELIVER THIS ON THEIR OWN
-- --------------------------------------------------------
-- 01-SagaSchema.sql guards its table on the table being ABSENT. On a database that already ran an
-- earlier version the guard sees the table and skips the whole definition, so the narrowed column
-- never arrives. Its one ALTER of TenantId is guarded on the column still being NULLABLE, which an
-- install from the previous release is not, so that block does not fire either. SagaTimeouts.sql
-- creates its table unguarded and is not re-runnable at all, and SagaTimeouts.Upgrade.sql only ADDS
-- the column to a table that lacks one. Re-running any of them is not an upgrade path; this script
-- is.
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
-- A value LONGER than 64 characters is REFUSED, never truncated. On dispatch.sagas that is a KEY
-- MERGE rather than a lossy label: TenantId leads PK_dispatch_sagas (TenantId, SagaId), and sagas
-- are correlated by a BUSINESS key, so tenant A's Order-123 saga and tenant B's Order-123 saga are
-- distinguished by the tenant term alone. Two tenants sharing their first 64 characters would
-- collapse onto ONE key and one tenant's saga state would overwrite the other's. On SagaTimeouts it
-- merges two tenants' timeout scopes, so a cancel-by-saga would delete another tenant's pending
-- timeouts and a claimed batch would hand one tenant's TimeoutData to the other. So this script
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
-- inheritance. The DEFAULT constraint is unaffected by ALTER COLUMN and is left in place.
--
--
-- WHY max_length IS COMPARED AGAINST 128 AND NOT 64
-- --------------------------------------------------
-- sys.columns.max_length is in BYTES, and NVARCHAR stores two bytes per character. A 64-character
-- NVARCHAR column therefore reads back as max_length = 128, and the 200-character column this
-- script upgrades from reads back as 400. (An NVARCHAR(MAX) column reads back as -1, which is also
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
-- SQL Server will not alter a column an index or key depends on, so the dependent objects are
-- dropped and rebuilt around each alter: the CLUSTERED primary key on dispatch.sagas (which
-- rewrites the whole table, twice), and the two tenant-leading indexes on SagaTimeouts. Run this
-- during a maintenance window with the saga host and the timeout dispatcher stopped: between each
-- drop and its rebuild the key is not enforced and the polling queries have no index to seek.
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

IF COL_LENGTH(N'dispatch.sagas', N'TenantId') IS NOT NULL
BEGIN
    -- Dynamic, because a static reference to TenantId would fail this whole batch at compile time
    -- on a table that exists without the column -- a case the per-table blocks below refuse with a
    -- message that explains it.
    SET @preflightSql = N'INSERT INTO #TenantNarrowPreflight (TableName, OffendingRows, Longest)
        SELECT N''dispatch.sagas'', COUNT_BIG(*), MAX(DATALENGTH(TenantId) / 2)
          FROM dispatch.sagas
         WHERE DATALENGTH(TenantId) / 2 > 64
        HAVING COUNT_BIG(*) > 0;';
    EXEC sp_executesql @preflightSql;
END

IF COL_LENGTH(N'SagaTimeouts', N'TenantId') IS NOT NULL
BEGIN
    -- Dynamic, because a static reference to TenantId would fail this whole batch at compile time
    -- on a table that exists without the column -- a case the per-table blocks below refuse with a
    -- message that explains it.
    SET @preflightSql = N'INSERT INTO #TenantNarrowPreflight (TableName, OffendingRows, Longest)
        SELECT N''SagaTimeouts'', COUNT_BIG(*), MAX(DATALENGTH(TenantId) / 2)
          FROM SagaTimeouts
         WHERE DATALENGTH(TenantId) / 2 > 64
        HAVING COUNT_BIG(*) > 0;';
    EXEC sp_executesql @preflightSql;
END

IF EXISTS (SELECT * FROM #TenantNarrowPreflight)
BEGIN
    DECLARE @preflightMsg NVARCHAR(2048) = N'03 REFUSED: a stored tenant identifier is longer than 64 characters, so NOTHING has been narrowed -- not even the tables that are clean, because a part-narrowed database is a shape no version of this schema ships. Offending table(s):';

    SELECT @preflightMsg = @preflightMsg + N' ' + TableName
         + N' (' + CONVERT(NVARCHAR(20), OffendingRows) + N' row(s), longest '
         + CONVERT(NVARCHAR(20), Longest) + N');'
      FROM #TenantNarrowPreflight;

    SET @preflightMsg = @preflightMsg
        + N' Narrowing would truncate those identifiers, merging two tenants that share their first 64 characters into one -- and on dispatch.sagas, where TenantId leads the primary key and sagas are correlated by a business key, that is a key merge rather than a lossy label. Nothing has been changed. Re-key the reported rows to identifiers of 64 characters or fewer, then re-run.';

    THROW 50004, @preflightMsg, 1;
END

DROP TABLE #TenantNarrowPreflight;
GO

-- ---------------------------------------------------------------------------
-- dispatch.sagas — TenantId LEADS the clustered primary key PK_dispatch_sagas (TenantId, SagaId).
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dispatch.sagas', N'U') IS NULL
BEGIN
    PRINT '03: dispatch.sagas is not present; nothing to upgrade. Provision with 01-SagaSchema.sql instead.';
END
ELSE
BEGIN
    DECLARE @width INT = (SELECT max_length FROM sys.columns
                          WHERE object_id = OBJECT_ID(N'dispatch.sagas') AND name = N'TenantId');
    DECLARE @msg NVARCHAR(2048);
    DECLARE @offenders BIGINT;
    DECLARE @longest INT;
    DECLARE @pkName SYSNAME;

    IF @width IS NULL
    BEGIN
        THROW 50004, '03 REFUSED: dispatch.sagas has no TenantId column at all, so this table did not come from any version of 01-SagaSchema.sql. Nothing has been changed. Re-run 01-SagaSchema.sql, which adds the tenant discriminator and re-keys the table, then re-run this script; this one narrows a column, it does not add one.', 1;
    END

    IF @width = 128
    BEGIN
        PRINT '03: dispatch.sagas.TenantId is already NVARCHAR(64); nothing to do.';
    END
    ELSE
    BEGIN
        SELECT @offenders = COUNT_BIG(*), @longest = MAX(DATALENGTH(TenantId) / 2)
          FROM dispatch.sagas
         WHERE DATALENGTH(TenantId) / 2 > 64;

        IF @offenders > 0
        BEGIN
            SET @msg = CONCAT(N'03 REFUSED: ', @offenders,
                N' row(s) in dispatch.sagas hold a tenant identifier longer than 64 characters (longest: ',
                @longest,
                N'). TenantId leads PK_dispatch_sagas (TenantId, SagaId), and sagas are correlated by a business key rather than a per-tenant identifier, so narrowing the column would not merely truncate a label: two tenants sharing their first 64 characters would collapse onto ONE key, and one tenant''s saga state would overwrite the other''s. Nothing has been changed. Re-key those rows to identifiers of 64 characters or fewer, then re-run.');
            THROW 50004, @msg, 1;
        END

        SET @pkName = (SELECT kc.name FROM sys.key_constraints kc
                       WHERE kc.parent_object_id = OBJECT_ID(N'dispatch.sagas') AND kc.type = N'PK');

        IF @pkName IS NOT NULL
        BEGIN
            EXEC(N'ALTER TABLE dispatch.sagas DROP CONSTRAINT ' + @pkName);
        END

        ALTER TABLE dispatch.sagas
            ALTER COLUMN TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

        ALTER TABLE dispatch.sagas
            ADD CONSTRAINT PK_dispatch_sagas PRIMARY KEY CLUSTERED (TenantId, SagaId);

        PRINT '03: dispatch.sagas.TenantId narrowed to NVARCHAR(64) and the clustered primary key rebuilt as PK_dispatch_sagas (TenantId, SagaId).';
    END
END
GO

-- ---------------------------------------------------------------------------
-- SagaTimeouts — TenantId leads two indexes; PK_SagaTimeouts is on TimeoutId alone and is not
-- affected. The table is optional and only exists where durable saga timeouts are used.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'SagaTimeouts', N'U') IS NULL
BEGIN
    PRINT '03: SagaTimeouts is not present; nothing to upgrade. It is provisioned by SagaTimeouts.sql only where durable saga timeouts are used.';
END
ELSE
BEGIN
    DECLARE @twidth INT = (SELECT max_length FROM sys.columns
                           WHERE object_id = OBJECT_ID(N'SagaTimeouts') AND name = N'TenantId');
    DECLARE @tmsg NVARCHAR(2048);
    DECLARE @toffenders BIGINT;
    DECLARE @tlongest INT;

    IF @twidth IS NULL
    BEGIN
        THROW 50004, '03 REFUSED: SagaTimeouts has no TenantId column at all. Without it a cancel-by-saga deletes another tenant''s pending timeouts, and this script narrows a column rather than adding one. Nothing has been changed. Run SagaTimeouts.Upgrade.sql, which adds the column and re-keys the saga indexes, then re-run this script.', 1;
    END

    IF @twidth = 128
    BEGIN
        PRINT '03: SagaTimeouts.TenantId is already NVARCHAR(64); nothing to do.';
    END
    ELSE
    BEGIN
        SELECT @toffenders = COUNT_BIG(*), @tlongest = MAX(DATALENGTH(TenantId) / 2)
          FROM SagaTimeouts
         WHERE DATALENGTH(TenantId) / 2 > 64;

        IF @toffenders > 0
        BEGIN
            SET @tmsg = CONCAT(N'03 REFUSED: ', @toffenders,
                N' row(s) in SagaTimeouts hold a tenant identifier longer than 64 characters (longest: ',
                @tlongest,
                N'). Narrowing the column would truncate them, merging two tenants whose identifiers share their first 64 characters into one timeout scope: a cancel-by-saga would delete the other tenant''s pending timeouts and a claimed batch would hand one tenant''s TimeoutData to the other. Nothing has been changed. Re-key those rows to identifiers of 64 characters or fewer, then re-run.');
            THROW 50004, @tmsg, 1;
        END

        IF EXISTS (SELECT * FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'SagaTimeouts') AND name = N'IX_SagaTimeouts_TenantId_SagaId')
        BEGIN
            DROP INDEX IX_SagaTimeouts_TenantId_SagaId ON SagaTimeouts;
        END

        IF EXISTS (SELECT * FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'SagaTimeouts') AND name = N'IX_SagaTimeouts_TenantId_SagaId_TimeoutId')
        BEGIN
            DROP INDEX IX_SagaTimeouts_TenantId_SagaId_TimeoutId ON SagaTimeouts;
        END

        ALTER TABLE SagaTimeouts
            ALTER COLUMN TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL;

        CREATE INDEX IX_SagaTimeouts_TenantId_SagaId
            ON SagaTimeouts (TenantId, SagaId);

        CREATE INDEX IX_SagaTimeouts_TenantId_SagaId_TimeoutId
            ON SagaTimeouts (TenantId, SagaId, TimeoutId);

        PRINT '03: SagaTimeouts.TenantId narrowed to NVARCHAR(64) and the two tenant-leading indexes rebuilt.';
    END
END
GO
