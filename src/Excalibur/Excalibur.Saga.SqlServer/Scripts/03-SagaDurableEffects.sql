-- Primary SQL durable saga effects schema proposal.
-- Apply ONLY with the reviewed matching exact-tenant and durable membership implementation.
-- This is not a standalone compatibility migration: old padded TenantId predicates must not
-- run against two exact tenant identities after the primary key changes.
-- Default shipped names are used. Custom deployments must rename EVERY participant consistently.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

IF OBJECT_ID(N'[dispatch].[sagas]', N'U') IS NULL
    THROW 51000, 'Apply the shipped saga schema before durable saga effects.', 1;
GO

-- Computed exact-UTF16 tenant hash keeps ordinary provider insert shapes valid. Matching
-- queries must ALSO compare original bytes+length and reject a hash collision, never trim.
IF COL_LENGTH(N'dispatch.sagas', N'TenantKey') IS NULL
    ALTER TABLE [dispatch].[sagas] ADD TenantKey AS
        ISNULL(CONVERT(BINARY(32),HASHBYTES('SHA2_256',CONVERT(VARBINARY(128),TenantId))),CONVERT(BINARY(32),0x)) PERSISTED;
GO

-- Preserve the logical (tenant, SagaId) identity. SagaType is NOT a second namespace.
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dispatch.sagas')
           AND name=N'PK_dispatch_sagas'
           AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                      WHERE ic.object_id=OBJECT_ID(N'dispatch.sagas') AND ic.index_id=1 AND ic.key_ordinal=1 AND c.name=N'TenantId'))
BEGIN
    ALTER TABLE [dispatch].[sagas] DROP CONSTRAINT PK_dispatch_sagas;
END;
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dispatch.sagas') AND type=N'PK')
    ALTER TABLE [dispatch].[sagas] ADD CONSTRAINT PK_dispatch_sagas PRIMARY KEY CLUSTERED (TenantKey, SagaId);
GO

IF OBJECT_ID(N'dbo.SagaTimeouts', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.SagaTimeouts', N'TenantKey') IS NULL
    ALTER TABLE [dbo].[SagaTimeouts] ADD TenantKey AS
        ISNULL(CONVERT(BINARY(32),HASHBYTES('SHA2_256',CONVERT(VARBINARY(128),TenantId))),CONVERT(BINARY(32),0x)) PERSISTED;
GO

IF OBJECT_ID(N'[dispatch].[SagaDurableScopes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dispatch].[SagaDurableScopes]
    (
        ScopeId UNIQUEIDENTIFIER NOT NULL,
        TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,
        TenantKey BINARY(32) NOT NULL,
        SagaId UNIQUEIDENTIFIER NOT NULL,
        SagaType NVARCHAR(500) COLLATE Latin1_General_BIN2 NOT NULL,
        IncarnationId UNIQUEIDENTIFIER NOT NULL,
        Generation BIGINT NOT NULL,
        IsClosed BIT NOT NULL,
        CommittedVersion BIGINT NOT NULL,
        NextEffectSequence BIGINT NOT NULL,
        HasTimerParticipant BIT NOT NULL CONSTRAINT DF_SagaScope_TimerParticipant DEFAULT(0),
        CONSTRAINT PK_SagaDurableScopes PRIMARY KEY (TenantKey, SagaId),
        CONSTRAINT UQ_SagaDurableScopes_ScopeId UNIQUE (ScopeId),
        CONSTRAINT CK_SagaDurableScopes_TenantKey CHECK (TenantKey=HASHBYTES('SHA2_256', CONVERT(VARBINARY(128),TenantId))),
        CONSTRAINT CK_SagaDurableScopes_Nonnegative CHECK (Generation>=0 AND CommittedVersion>=0 AND
            NextEffectSequence>=0)
    );
END;
GO

IF OBJECT_ID(N'[dispatch].[SagaTransitionReceipts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dispatch].[SagaTransitionReceipts]
    (
        ScopeId UNIQUEIDENTIFIER NOT NULL,
        IncarnationId UNIQUEIDENTIFIER NOT NULL,
        TransitionId UNIQUEIDENTIFIER NOT NULL,
        ExpectedVersion BIGINT NOT NULL,
        CommittedVersion BIGINT NOT NULL,
        Digest BINARY(32) NOT NULL,
        DeliveryIdHash BINARY(32) NULL,
        DeliveryId NVARCHAR(MAX) COLLATE Latin1_General_BIN2 NULL,
        EncodedEffectManifest VARBINARY(MAX) NOT NULL,
        TimerCount INT NOT NULL,
        MessageCount INT NOT NULL,
        CommittedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_SagaReceipt_CommittedAt DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(),0),
        CONSTRAINT PK_SagaTransitionReceipts PRIMARY KEY (ScopeId, IncarnationId, TransitionId),
        CONSTRAINT FK_SagaReceipt_Scope FOREIGN KEY(ScopeId) REFERENCES [dispatch].[SagaDurableScopes](ScopeId),
        CONSTRAINT CK_SagaReceipt_Version CHECK(ExpectedVersion>=0 AND CommittedVersion=ExpectedVersion+1),
        CONSTRAINT CK_SagaReceipt_Counts CHECK(TimerCount>=0 AND MessageCount>=0),
        CONSTRAINT CK_SagaReceipt_Delivery CHECK((DeliveryId IS NULL AND DeliveryIdHash IS NULL) OR
            (DeliveryId IS NOT NULL AND DeliveryIdHash IS NOT NULL AND DeliveryIdHash=HASHBYTES('SHA2_256',CONVERT(VARBINARY(MAX),DeliveryId))))
    );
    CREATE UNIQUE INDEX UX_SagaReceipt_Delivery ON [dispatch].[SagaTransitionReceipts](ScopeId, IncarnationId, DeliveryIdHash)
        WHERE DeliveryIdHash IS NOT NULL;
END;
GO

IF OBJECT_ID(N'[dispatch].[SagaOutboxMembership]', N'U') IS NULL
BEGIN
    -- Intent membership survives payload cleanup. It records original identities and wire hashes,
    -- not external delivery acceptance, and introduces no global head/FIFO protocol.
    CREATE TABLE [dispatch].[SagaOutboxMembership]
    (
        MessageId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL,
        ScopeId UNIQUEIDENTIFIER NOT NULL,
        IncarnationId UNIQUEIDENTIFIER NOT NULL,
        TransitionId UNIQUEIDENTIFIER NOT NULL,
        EffectOrdinal INT NOT NULL,
        EffectSequence BIGINT NOT NULL,
        CanonicalWireHash BINARY(32) NOT NULL,
        CONSTRAINT PK_SagaOutboxMembership PRIMARY KEY(ScopeId,IncarnationId,TransitionId,EffectOrdinal),
        CONSTRAINT UQ_SagaOutboxMembership_Sequence UNIQUE(ScopeId,IncarnationId,EffectSequence),
        CONSTRAINT FK_SagaOutboxMembership_Receipt FOREIGN KEY(ScopeId,IncarnationId,TransitionId)
            REFERENCES [dispatch].[SagaTransitionReceipts](ScopeId,IncarnationId,TransitionId),
        CONSTRAINT CK_SagaOutboxMembership_Ordinal CHECK(EffectOrdinal>=0 AND EffectSequence>=0)
    );
END;
GO

IF OBJECT_ID(N'[dispatch].[SagaTimeoutMembership]', N'U') IS NULL
BEGIN
    CREATE TABLE [dispatch].[SagaTimeoutMembership]
    (
        TimeoutId NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
        ScopeId UNIQUEIDENTIFIER NOT NULL,
        IncarnationId UNIQUEIDENTIFIER NOT NULL,
        Generation BIGINT NOT NULL,
        TransitionId UNIQUEIDENTIFIER NOT NULL,
        EffectOrdinal INT NOT NULL,
        StateType NVARCHAR(500) COLLATE Latin1_General_BIN2 NOT NULL,
        IsCancelled BIT NOT NULL CONSTRAINT DF_SagaTimeoutMembership_IsCancelled DEFAULT(0),
        CONSTRAINT PK_SagaTimeoutMembership PRIMARY KEY(TimeoutId),
        CONSTRAINT FK_SagaTimeoutMembership_Receipt FOREIGN KEY(ScopeId,IncarnationId,TransitionId)
            REFERENCES [dispatch].[SagaTransitionReceipts](ScopeId,IncarnationId,TransitionId),
        CONSTRAINT CK_SagaTimeoutMembership_Generation CHECK(Generation>=0 AND EffectOrdinal>=0)
    );
END;
GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dispatch.sagas') AND name=N'IX_SagaLegacyTenantLookup')
    CREATE INDEX IX_SagaLegacyTenantLookup ON dispatch.sagas(TenantId,SagaId);
GO
