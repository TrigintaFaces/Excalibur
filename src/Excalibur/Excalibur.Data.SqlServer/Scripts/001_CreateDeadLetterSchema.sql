-- SQL Server Schema for Excalibur.Data.SqlServer — DEAD LETTER MESSAGES
-- Version: 1.0
--
-- Creates the table SqlServerDeadLetterStore reads and writes. This provider never creates the
-- table at runtime: run this script against the target database before the first message is dead-
-- lettered. Without it, the first failure that should have been captured is instead lost to an
-- Invalid object name — the one moment the store exists for.
--
-- This schema previously shipped from the Excalibur.Dispatch package, which contains no SQL store.
-- It now ships from the package that owns the store, so the DDL and the statements it must satisfy
-- version together and a consumer looks for the schema in the package they installed for the store.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     schema = "dbo"
--     table  = "DeadLetterMessages"
--
-- If you override either, rename the object below to match.
--
-- Every statement is guarded, so the script is safe to re-run, including against a database
-- provisioned from the older Excalibur.Dispatch copy of this schema: the table guard is a no-op
-- there and the additive upgrades below still apply.

-- SQL Server requires SET QUOTED_IDENTIFIER ON to create a FILTERED index (one with a WHERE
-- clause), and sqlcmd defaults it OFF. Without these, every filtered index below fails with
-- Msg 1934 and is simply absent from the resulting database -- a script runner that does not
-- check exit status gets a schema silently missing its most selective indexes.
-- No GO after these: GO is a client batch separator, not T-SQL, and this script is also
-- executed as a SINGLE command by callers that do not split batches. The setting applies
-- within its own batch and persists across later ones, so it is not needed.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- This script creates the necessary table for storing poison messages

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dbo')
BEGIN
    EXEC('CREATE SCHEMA dbo')
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DeadLetterMessages]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[DeadLetterMessages](
        [Id] [nvarchar](32) NOT NULL,
        -- The owning tenant. A dead-letter row holds the failed message BODY, so an unscoped read
        -- discloses one tenant's message content to another. The column is NOT NULL and defaults to the
        -- reserved untenanted sentinel rather than permitting NULL: a nullable tenant fails OPEN, because
        -- a scoped predicate written as TenantId = @TenantId never matches NULL and the row silently
        -- leaves its own tenant's results while remaining in the table. A single-tenant deployment binds
        -- the sentinel, which is a real term like any other.
        --
        -- The collation is pinned for the same reason the column is NOT NULL: the comparison must not
        -- fail open. Every read is a scoped `TenantId = @TenantId`, and SQL Server's server default is
        -- typically case-INSENSITIVE, under which 'Acme' = 'acme'. Without an explicit binary collation
        -- a tenant whose identifier differs from another's only by case reads that tenant's rows —
        -- which, for this table, means reading their message bodies. Nothing errors; the predicate
        -- simply matches more than it should.
        [TenantId] [nvarchar](64) COLLATE Latin1_General_BIN2 NOT NULL DEFAULT '__untenanted__',
        [MessageId] [nvarchar](128) NOT NULL,
        [MessageType] [nvarchar](500) NOT NULL,
        [MessageBody] [nvarchar](max) NOT NULL,
        [MessageMetadata] [nvarchar](max) NOT NULL,
        [Reason] [nvarchar](1000) NOT NULL,
        [ExceptionDetails] [nvarchar](max) NULL,
        [ProcessingAttempts] [int] NOT NULL DEFAULT 0,
        [MovedToDeadLetterAt] [datetimeoffset](7) NOT NULL,
        [FirstAttemptAt] [datetimeoffset](7) NULL,
        [LastAttemptAt] [datetimeoffset](7) NULL,
        [IsReplayed] [bit] NOT NULL DEFAULT 0,
        [ReplayedAt] [datetimeoffset](7) NULL,
        [SourceSystem] [nvarchar](200) NULL,
        [CorrelationId] [nvarchar](128) NULL,
        [Properties] [nvarchar](max) NULL,
        -- The primary key stays on [Id] ALONE and deliberately does NOT become ([Id], [TenantId]).
        -- A composite key would permit the same Id to exist in two tenants, and every lookup written as
        -- WHERE Id = @Id would then resolve an arbitrary tenant's row. Keeping Id globally unique makes
        -- that ambiguity unrepresentable instead of requiring each predicate to remember the tenant term
        -- for correctness. The tenant term is still bound on every statement, but for ISOLATION rather
        -- than for identity.
        CONSTRAINT [PK_DeadLetterMessages] PRIMARY KEY CLUSTERED ([Id] ASC)
    )
END
GO

-- Additive upgrade for databases created before the tenant column existed. Existing rows predate
-- multi-tenancy and are therefore untenanted: they bind the sentinel rather than NULL, so they stay
-- readable by an untenanted scope instead of vanishing from every tenant's results.
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DeadLetterMessages]') AND type in (N'U'))
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[DeadLetterMessages]') AND name = N'TenantId')
BEGIN
    ALTER TABLE [dbo].[DeadLetterMessages]
        ADD [TenantId] [nvarchar](64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT [DF_DeadLetterMessages_TenantId] DEFAULT '__untenanted__';
END
GO

-- BRING AN EXISTING TenantId COLUMN TO THE CURRENT DECLARATION: nvarchar(64), Latin1_General_BIN2.
--
-- The additive upgrade above is guarded on the column being ABSENT, so it does nothing for an
-- install that already has TenantId in some earlier shape. Those installs are the ones already
-- holding more than one tenant's rows, so they are the only ones that can leak, and they are
-- exactly the ones the additive guard cannot reach.
--
-- THIS BLOCK IS GUARDED ON BOTH PROPERTIES THAT HAVE CHANGED, WIDTH AND COLLATION, AND THAT MATTERS.
-- An earlier version of this block was guarded on the collation ALONE. An install from the previous
-- release already had Latin1_General_BIN2 and a 255-character column, so that guard was FALSE for
-- precisely the population that needed narrowing: the block read as an upgrade path and could not
-- fire, and the column stayed 255 forever while every fresh install provisioned 64. A guard must
-- test the property that is actually changing, not a neighbouring one that some earlier version
-- already satisfies.
--
-- max_length is in BYTES and nvarchar stores two per character, so a 64-character column reads back
-- as 128 and the 255-character column this upgrades from reads back as 510. (An nvarchar(max)
-- column reads back as -1, which is also "not 128" and is narrowed like any other over-wide one.)
--
-- A stored identifier that will not FIT in 64 characters is REFUSED, never truncated: two tenants
-- whose identifiers share their first 64 characters would merge into one dead-letter scope, so a
-- tenant-scoped read or replay would return the other tenant's failed messages with no error to
-- show for it. The script stops and names the rows instead of choosing for you. Re-key them to
-- identifiers of 64 characters or fewer, then re-run. Such rows can only predate the length guard
-- the framework now applies at construction, so a deployment that has only ever written through a
-- current release has none.
--
-- The row check uses DATALENGTH(TenantId) / 2 rather than LEN(TenantId), deliberately: LEN ignores
-- trailing spaces, so an identifier of 64 significant characters followed by trailing spaces would
-- measure as 64 and then lose those bytes in the alter. DATALENGTH counts what is actually stored.
--
-- TenantId leads IX_DeadLetterMessages_TenantId_MovedToDeadLetterAt, and SQL Server will not alter a
-- column an index depends on, so the index is dropped and rebuilt around the alter. Run this during
-- a maintenance window: between the drop and the rebuild the tenant-scoped reads have no index to
-- seek. TenantId is deliberately NOT part of this table's primary key, which stays on [Id] alone, so
-- no key is dropped here.
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[dbo].[DeadLetterMessages]')
             AND name = N'TenantId'
             AND (max_length <> 128 OR collation_name <> N'Latin1_General_BIN2'))
BEGIN
    DECLARE @dlqOffenders BIGINT, @dlqLongest INT, @dlqMsg NVARCHAR(2048);

    SELECT @dlqOffenders = COUNT_BIG(*), @dlqLongest = MAX(DATALENGTH([TenantId]) / 2)
      FROM [dbo].[DeadLetterMessages]
     WHERE DATALENGTH([TenantId]) / 2 > 64;

    IF @dlqOffenders > 0
    BEGIN
        SET @dlqMsg = CONCAT(N'REFUSED: ', @dlqOffenders,
            N' row(s) in [dbo].[DeadLetterMessages] hold a tenant identifier longer than 64 characters (longest: ',
            @dlqLongest,
            N'). Narrowing the column would truncate them, merging two tenants whose identifiers share their first 64 characters into one dead-letter scope: a tenant-scoped read or replay would return the other tenant''s failed messages. Nothing has been changed. Re-key those rows to identifiers of 64 characters or fewer, then re-run this script.');
        THROW 50010, @dlqMsg, 1;
    END

    IF EXISTS (SELECT * FROM sys.indexes
               WHERE name = N'IX_DeadLetterMessages_TenantId_MovedToDeadLetterAt'
                 AND object_id = OBJECT_ID(N'[dbo].[DeadLetterMessages]'))
    BEGIN
        DROP INDEX [IX_DeadLetterMessages_TenantId_MovedToDeadLetterAt]
            ON [dbo].[DeadLetterMessages];
    END

    ALTER TABLE [dbo].[DeadLetterMessages]
        ALTER COLUMN [TenantId] [nvarchar](64) COLLATE Latin1_General_BIN2 NOT NULL;

    CREATE NONCLUSTERED INDEX [IX_DeadLetterMessages_TenantId_MovedToDeadLetterAt]
        ON [dbo].[DeadLetterMessages] ([TenantId], [MovedToDeadLetterAt])
        INCLUDE ([MessageType], [Reason]);
END
GO

-- Create indexes for common query patterns

-- Every read is tenant-scoped, so the tenant term leads the index rather than trailing it.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DeadLetterMessages_TenantId_MovedToDeadLetterAt')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DeadLetterMessages_TenantId_MovedToDeadLetterAt]
    ON [dbo].[DeadLetterMessages] ([TenantId], [MovedToDeadLetterAt])
    INCLUDE ([MessageType], [Reason])
END
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DeadLetterMessages_MessageId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DeadLetterMessages_MessageId] 
    ON [dbo].[DeadLetterMessages] ([MessageId])
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DeadLetterMessages_MessageType')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DeadLetterMessages_MessageType] 
    ON [dbo].[DeadLetterMessages] ([MessageType])
    INCLUDE ([MovedToDeadLetterAt], [Reason])
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DeadLetterMessages_MovedToDeadLetterAt')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DeadLetterMessages_MovedToDeadLetterAt] 
    ON [dbo].[DeadLetterMessages] ([MovedToDeadLetterAt])
    INCLUDE ([MessageType], [Reason])
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DeadLetterMessages_CorrelationId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DeadLetterMessages_CorrelationId] 
    ON [dbo].[DeadLetterMessages] ([CorrelationId])
    WHERE [CorrelationId] IS NOT NULL
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DeadLetterMessages_IsReplayed')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DeadLetterMessages_IsReplayed] 
    ON [dbo].[DeadLetterMessages] ([IsReplayed])
    INCLUDE ([MessageId], [ReplayedAt])
END
GO

