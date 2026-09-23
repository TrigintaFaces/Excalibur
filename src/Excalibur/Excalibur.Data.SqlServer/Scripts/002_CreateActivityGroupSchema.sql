-- SQL Server Schema for Excalibur.Data.SqlServer — AUTHORIZATION ACTIVITY GROUPS
-- Version: 1.0
--
-- Creates the table SqlServerActivityGroupStore reads and writes. This provider never creates the
-- table at runtime: run this script against the target database before registering the store.
-- Without it, every activity-group operation fails with "Invalid object name 'authz.ActivityGroup'".
--
-- WHAT THIS SCRIPT DOES NOT COVER. It creates the activity-group table only. The grant tables the
-- authorization stores also use (authz.Grant, authz.GrantHistory) are not created here; they are
-- provisioned separately.
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

IF OBJECT_ID(N'authz.ActivityGroup', N'U') IS NULL
BEGIN
    CREATE TABLE authz.ActivityGroup
    (
        -- The tenant the group belongs to. Never null: an untenanted group is stored under the
        -- reserved value below, so "no tenant" is one explicit value rather than a missing one.
        -- Binary collation makes the comparison exact, as the store's own comparisons are; a
        -- case-insensitive collation would let one tenant's identifier match another's.
        TenantId     NVARCHAR(64)  COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT DF_ActivityGroup_TenantId DEFAULT (N'__untenanted__'),

        -- The group's name. Unique per tenant, not globally: two tenants may each have a group with
        -- the same name, and those are distinct groups.
        Name         NVARCHAR(128) COLLATE Latin1_General_BIN2 NOT NULL,

        -- One activity the group confers. A group is a set of activities, stored one row per member.
        ActivityName NVARCHAR(256) COLLATE Latin1_General_BIN2 NOT NULL,

        -- One row is one (tenant, group, activity) triple, and the key is that triple. Its widths are
        -- sized to fit SQL Server's 900-byte limit on a clustered key: 64 + 128 + 256 characters at two
        -- bytes each is 896. A wider declaration still creates, and then rejects any row whose key data
        -- runs past 900 bytes -- so the store refuses an over-length name before the database does.
        -- The tenant is part of the key rather than merely a column: leaving it out would let two
        -- tenants' groups of the same name collide, so one tenant could read a row another wrote.
        CONSTRAINT PK_ActivityGroup PRIMARY KEY CLUSTERED (TenantId, Name, ActivityName)
    );
END;
