-- SQL SERVER MIGRATION for Excalibur.Data.SqlServer — NARROW ActivityGroup.Name TO 128
-- Version: 1.0
--
-- Brings a database provisioned before the narrowing in line with 002_CreateActivityGroupSchema.sql,
-- which now declares Name as NVARCHAR(128). A database created by the earlier script holds
-- NVARCHAR(256) and is not repaired by re-running 002: that script guards on table EXISTENCE, so it
-- creates nothing and reports success against a table that is simply the wrong shape.
--
-- WHY THE COLUMN WAS NARROWED
-- ---------------------------
-- Name is part of the CLUSTERED primary key (TenantId, Name, ActivityName). SQL Server caps a
-- clustered key at 900 bytes. At NVARCHAR(256) the three columns total more than that, and the
-- failure it produces is the worst kind: CREATE TABLE SUCCEEDS with only a warning, and the table
-- then refuses an insert whose actual key values exceed 900 — a failure that depends on the DATA,
-- so it survives provisioning and every smoke test and arrives on a real registration with a long
-- group or activity name.
--
-- THIS SCRIPT REFUSES RATHER THAN TRUNCATES
-- -----------------------------------------
-- Narrowing a column silently truncates nothing in SQL Server — it errors — but an operator who
-- hits that error mid-migration has a half-applied change and no statement of what is wrong. So the
-- over-length rows are found and NAMED first, and the migration stops before touching the schema.
-- A group name is an authorization identity: truncating one would silently merge two distinct
-- groups into a single row, which is a privilege change, not a formatting change.
--
-- IDEMPOTENT. Re-running against an already-narrowed database does nothing.

SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'authz')
BEGIN
    PRINT 'authz schema absent — nothing to migrate.';
    RETURN;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables t
               JOIN sys.schemas s ON s.schema_id = t.schema_id
               WHERE s.name = N'authz' AND t.name = N'ActivityGroup')
BEGIN
    PRINT 'authz.ActivityGroup absent — nothing to migrate.';
    RETURN;
END;
GO

-- Already at the target width: nothing to do. max_length is in BYTES for NVARCHAR, so 128 chars = 256.
IF EXISTS (SELECT 1 FROM sys.columns c
           JOIN sys.tables t  ON t.object_id = c.object_id
           JOIN sys.schemas s ON s.schema_id = t.schema_id
           WHERE s.name = N'authz' AND t.name = N'ActivityGroup'
             AND c.name = N'Name' AND c.max_length <= 256)
BEGIN
    PRINT 'authz.ActivityGroup.Name is already NVARCHAR(128) or narrower — nothing to migrate.';
    RETURN;
END;
GO

-- REFUSE on data that cannot survive the narrowing, and say which rows.
IF EXISTS (SELECT 1 FROM authz.ActivityGroup WHERE LEN(Name) > 128)
BEGIN
    DECLARE @offenders NVARCHAR(MAX) =
        (SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), QUOTENAME(Name)), N', ')
         FROM (SELECT DISTINCT TOP (20) Name FROM authz.ActivityGroup WHERE LEN(Name) > 128) x);

    RAISERROR (
        N'REFUSED: authz.ActivityGroup holds group names longer than 128 characters, so narrowing Name would fail or merge distinct groups. A group name is an authorization identity; shortening one silently is a privilege change. Rename or remove these first (up to 20 shown): %s',
        16, 1, @offenders) WITH NOWAIT;
    RETURN;
END;
GO

-- Name participates in the clustered PK, so the constraint is dropped and rebuilt around the ALTER.
-- The PK definition here must stay identical to 002_CreateActivityGroupSchema.sql.
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'PK_ActivityGroup'
             AND parent_object_id = OBJECT_ID(N'authz.ActivityGroup'))
BEGIN
    ALTER TABLE authz.ActivityGroup DROP CONSTRAINT PK_ActivityGroup;
END;

ALTER TABLE authz.ActivityGroup
    ALTER COLUMN Name NVARCHAR(128) COLLATE Latin1_General_BIN2 NOT NULL;

ALTER TABLE authz.ActivityGroup
    ADD CONSTRAINT PK_ActivityGroup PRIMARY KEY CLUSTERED (TenantId, Name, ActivityName);

COMMIT TRANSACTION;
GO

PRINT 'authz.ActivityGroup.Name narrowed to NVARCHAR(128) and the clustered key rebuilt.';
GO
