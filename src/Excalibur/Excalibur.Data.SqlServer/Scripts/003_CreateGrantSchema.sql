-- SQL Server Schema for Excalibur.Data.SqlServer — AUTHORIZATION GRANTS
-- Version: 1.0
--
-- Creates the two tables SqlServerGrantStore reads and writes: authz.Grant, the grants in force, and
-- authz.GrantHistory, the record of every grant that was revoked. This provider never creates them at
-- runtime: run this script against the target database before registering the store. Without it, every
-- grant operation fails with "Invalid object name 'authz.Grant'".
--
-- Every statement is guarded, so the script is safe to re-run.
--
-- It is written as ONE batch, with no GO separators, so a caller that submits the whole file as a
-- single command runs it unchanged. CREATE SCHEMA must be the only statement in its batch, which is
-- why it is issued through EXEC rather than directly.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'authz')
BEGIN
    EXEC(N'CREATE SCHEMA authz');
END;

IF OBJECT_ID(N'authz.Grant', N'U') IS NULL
BEGIN
    CREATE TABLE authz.[Grant]
    (
        -- The four identity columns use a binary collation, so every comparison is exact, as the store's
        -- own comparisons are. In authorization 'Admin' and 'admin' are different grants; a
        -- case-insensitive collation would let one match the other.
        UserId      NVARCHAR(128) COLLATE Latin1_General_BIN2 NOT NULL,

        -- Never null: an untenanted grant is stored under the reserved value below, so "no tenant" is one
        -- explicit value rather than a missing one.
        TenantId    NVARCHAR(64)  COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT DF_Grant_TenantId DEFAULT (N'__untenanted__'),

        GrantType   NVARCHAR(64)  COLLATE Latin1_General_BIN2 NOT NULL,
        Qualifier   NVARCHAR(192) COLLATE Latin1_General_BIN2 NOT NULL,
        FullName    NVARCHAR(256) NULL,
        ExpiresOn   DATETIMEOFFSET NULL,
        GrantedBy   NVARCHAR(128) NOT NULL,
        GrantedOn   DATETIMEOFFSET NOT NULL,

        -- One row is one grant, identified by (user, tenant, type, qualifier) -- the four values every
        -- statement that addresses a single grant uses. Saving a grant that already exists replaces it.
        -- The user comes first because the authorization check reads a user's grants by user alone.
        -- Tenant isolation is a property of each statement's predicate, not of key order.
        -- The widths fit SQL Server's 900-byte limit on a clustered key: (128 + 64 + 64 + 192) characters
        -- at two bytes each is 896. A wider declaration still creates, and then rejects any row whose key
        -- runs past 900 bytes -- so the store refuses an over-length value before the database does.
        CONSTRAINT PK_Grant PRIMARY KEY CLUSTERED (UserId, TenantId, GrantType, Qualifier)
    );
END;

IF OBJECT_ID(N'authz.GrantHistory', N'U') IS NULL
BEGIN
    CREATE TABLE authz.GrantHistory
    (
        -- A surrogate key: history is append-only, and one grant may be revoked, re-granted and revoked
        -- again, so the four identity columns repeat here and cannot be the key.
        HistoryId   BIGINT IDENTITY(1, 1) NOT NULL,

        UserId      NVARCHAR(128) COLLATE Latin1_General_BIN2 NOT NULL,
        TenantId    NVARCHAR(64)  COLLATE Latin1_General_BIN2 NOT NULL,
        GrantType   NVARCHAR(64)  COLLATE Latin1_General_BIN2 NOT NULL,
        Qualifier   NVARCHAR(192) COLLATE Latin1_General_BIN2 NOT NULL,
        FullName    NVARCHAR(256) NULL,
        ExpiresOn   DATETIMEOFFSET NULL,
        GrantedBy   NVARCHAR(128) NOT NULL,
        GrantedOn   DATETIMEOFFSET NOT NULL,
        RevokedBy   NVARCHAR(128) NULL,
        RevokedOn   DATETIMEOFFSET NULL,

        CONSTRAINT PK_GrantHistory PRIMARY KEY CLUSTERED (HistoryId)
    );

    CREATE NONCLUSTERED INDEX IX_GrantHistory_Grant
        ON authz.GrantHistory (UserId, TenantId, GrantType, Qualifier);
END;
