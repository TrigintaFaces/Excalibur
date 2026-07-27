-- SQL Server schema for Dead Letter Messages table
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
        [TenantId] [nvarchar](255) NOT NULL DEFAULT '__untenanted__',
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
        ADD [TenantId] [nvarchar](255) NOT NULL CONSTRAINT [DF_DeadLetterMessages_TenantId] DEFAULT '__untenanted__';
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

-- PostgreSQL schema (commented out, uncomment if using PostgreSQL)
/*
CREATE SCHEMA IF NOT EXISTS public;

CREATE TABLE IF NOT EXISTS public.dead_letter_messages (
    id VARCHAR(32) NOT NULL PRIMARY KEY,
    -- NOT NULL with the reserved sentinel: a nullable tenant fails open, because a scoped predicate
    -- never matches NULL and the row silently leaves its own tenant's results. See the SQL Server
    -- definition above for the full rationale, including why the key stays on id alone.
    tenant_id VARCHAR(255) NOT NULL DEFAULT '__untenanted__',
    message_id VARCHAR(128) NOT NULL,
    message_type VARCHAR(500) NOT NULL,
    message_body TEXT NOT NULL,
    message_metadata TEXT NOT NULL,
    reason VARCHAR(1000) NOT NULL,
    exception_details TEXT NULL,
    processing_attempts INTEGER NOT NULL DEFAULT 0,
    moved_to_dead_letter_at TIMESTAMPTZ NOT NULL,
    first_attempt_at TIMESTAMPTZ NULL,
    last_attempt_at TIMESTAMPTZ NULL,
    is_replayed BOOLEAN NOT NULL DEFAULT false,
    replayed_at TIMESTAMPTZ NULL,
    source_system VARCHAR(200) NULL,
    correlation_id VARCHAR(128) NULL,
    properties TEXT NULL
);

CREATE INDEX IF NOT EXISTS idx_dead_letter_messages_tenant_id_moved_at
ON public.dead_letter_messages (tenant_id, moved_to_dead_letter_at);

CREATE INDEX IF NOT EXISTS idx_dead_letter_messages_message_id
ON public.dead_letter_messages (message_id);

CREATE INDEX IF NOT EXISTS idx_dead_letter_messages_message_type 
ON public.dead_letter_messages (message_type) 
INCLUDE (moved_to_dead_letter_at, reason);

CREATE INDEX IF NOT EXISTS idx_dead_letter_messages_moved_to_dead_letter_at 
ON public.dead_letter_messages (moved_to_dead_letter_at) 
INCLUDE (message_type, reason);

CREATE INDEX IF NOT EXISTS idx_dead_letter_messages_correlation_id 
ON public.dead_letter_messages (correlation_id) 
WHERE correlation_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_dead_letter_messages_is_replayed 
ON public.dead_letter_messages (is_replayed) 
INCLUDE (message_id, replayed_at);
*/