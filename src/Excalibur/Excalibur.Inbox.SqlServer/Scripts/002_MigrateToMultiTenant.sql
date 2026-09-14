-- SQL Server MIGRATION for Excalibur.Inbox.SqlServer — SINGLE-TENANT -> MULTI-TENANT
-- Version: 2.0
--
-- Run this ONCE to grow an existing single-tenant inbox table (created by
-- 001_CreateInboxSchema.sql, keyed on the pair (MessageId, HandlerType), no TenantId
-- column) into the multi-tenant schema (keyed on the triple, TenantId NOT NULL) before
-- registering multi-tenancy.
--
-- This is an expand-contract migration. Existing pre-multi-tenant rows are anchored to
-- the reserved sentinel '__untenanted__': it is a concrete, non-null, reserved value
-- (the framework rejects that exact identifier, so it can never name a real tenant), so
-- migrated rows sit in their own partition and can never collide with a future real tenant.
-- Rows added after the migration carry their real tenant id.
--
-- Table/schema names use the defaults (dbo.inbox_messages); rename to match if overridden.
--
-- ORDERING / DOWNTIME: run this during a maintenance window (or with a dual-read strategy)
-- with the store stopped, because it rebuilds the primary key. After it completes,
-- register multi-tenancy and restart — the startup handshake then confirms the triple key.
--
-- OPTIONAL: if all pre-migration rows belong to one known first tenant, replace the
-- DEFAULT below (and the UPDATE) with that real tenant id instead of the sentinel.

-- 1) Add the TenantId column, anchoring existing rows to the reserved sentinel.
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[inbox_messages]') AND name = N'TenantId')
BEGIN
    ALTER TABLE [dbo].[inbox_messages]
        ADD TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL CONSTRAINT DF_inbox_messages_TenantId DEFAULT N'__untenanted__';
END
GO

-- 2) Rebuild the unique key: drop the pair PK, add the triple PK.
--
--    The drop and the recreate are ONE unit of work, because between them the table has no
--    dedup key at all: an inbox with no key admits a second delivery of a message it has
--    already handled, which is the one outcome an inbox exists to prevent. If step 1 did not
--    leave TenantId in place and non-nullable -- because it was skipped, or because this
--    database reached a TenantId column by some other route -- the recreate fails, and
--    without the transaction the drop would already have committed. SQL Server rolls DDL
--    back, so the transaction is what makes that impossible.
IF EXISTS (SELECT * FROM sys.key_constraints
           WHERE parent_object_id = OBJECT_ID(N'[dbo].[inbox_messages]') AND name = N'PK_inbox_messages' AND type = N'PK')
BEGIN
    BEGIN TRANSACTION;

    BEGIN TRY
        ALTER TABLE [dbo].[inbox_messages] DROP CONSTRAINT PK_inbox_messages;
        ALTER TABLE [dbo].[inbox_messages]
            ADD CONSTRAINT PK_inbox_messages PRIMARY KEY (MessageId, HandlerType, TenantId);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Returns the table to the key it had. Re-raise afterwards: a migration that failed
        -- must not look clean, or the host is registered for multi-tenancy against a table
        -- that was never re-keyed.
        IF XACT_STATE() <> 0
        BEGIN
            ROLLBACK TRANSACTION;
        END;

        THROW;
    END CATCH
END
GO

-- 3) Optional: drop the sentinel default now that the key is rebuilt, so future inserts
--    must supply a real tenant id (the store always binds one on the multi-tenant path).
IF EXISTS (SELECT * FROM sys.default_constraints
           WHERE parent_object_id = OBJECT_ID(N'[dbo].[inbox_messages]') AND name = N'DF_inbox_messages_TenantId')
BEGIN
    ALTER TABLE [dbo].[inbox_messages] DROP CONSTRAINT DF_inbox_messages_TenantId;
END
GO

-- 3) RE-COLLATE an already-migrated table.
--
-- Steps 1 and 2 are guarded on the column being ABSENT, so they do nothing for a consumer who
-- ran an earlier version of this script: that install already has TenantId, and it has it in the
-- server's default collation -- which is typically case-INSENSITIVE. Those installs are the ones
-- that have already adopted multi-tenancy, so they are the only ones holding more than one
-- tenant's rows, and therefore the only ones that can leak: 'Acme' matches 'acme' and the tenant
-- predicate fails OPEN.
--
-- This block is guarded on the COLLATION rather than on the column's existence, so it reaches
-- exactly that population and is a no-op once applied (and on a fresh install, where 001 already
-- created the column pinned).
--
-- TenantId is part of PK_inbox_messages, and SQL Server will not alter a column that participates
-- in a key, so the key is dropped and rebuilt around the alter. Run with the store stopped.
--
-- All three statements are ONE unit of work. The ALTER COLUMN in the middle can fail on a table
-- this script did not create -- a NULL in TenantId is rejected by NOT NULL, and the column may
-- carry an index or constraint this block does not know to drop -- and without the transaction
-- that failure would leave the table with no dedup key at all, which is strictly less protection
-- than the script found and admits a second delivery of an already-handled message.
IF EXISTS (SELECT * FROM sys.columns c
           JOIN sys.objects o ON o.object_id = c.object_id
           WHERE o.object_id = OBJECT_ID(N'[dbo].[inbox_messages]')
             AND c.name = N'TenantId'
             AND c.collation_name <> N'Latin1_General_BIN2')
BEGIN
    BEGIN TRANSACTION;

    BEGIN TRY
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

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- The table keeps the key and the collation it had. Re-raise: a re-collation that
        -- failed must not look clean, because the case-insensitive column it leaves behind is
        -- the tenant leak this block exists to close.
        IF XACT_STATE() <> 0
        BEGIN
            ROLLBACK TRANSACTION;
        END;

        THROW;
    END CATCH
END
GO
