-- Retained ordinary timer recovery metadata. Apply while timer/saga writers are stopped.
-- Independent of saga state and timeout payload retention. No TTL or deletion-on-delivery.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF SCHEMA_ID(N'dispatch') IS NULL EXEC(N'CREATE SCHEMA dispatch');
GO
IF OBJECT_ID(N'dispatch.SagaTimerRecoveryTargets',N'U') IS NULL
CREATE TABLE dispatch.SagaTimerRecoveryTargets
(
    TargetObjectId INT NOT NULL PRIMARY KEY,
    ReceiverId UNIQUEIDENTIFIER NOT NULL UNIQUE
);
GO
IF OBJECT_ID(N'dispatch.SagaTimerRecoveryScopes',N'U') IS NULL
CREATE TABLE dispatch.SagaTimerRecoveryScopes
(
    ScopeId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TargetObjectId INT NOT NULL,
    TenantKey BINARY(32) NOT NULL,
    TenantId NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,
    SagaId UNIQUEIDENTIFIER NOT NULL,
    FenceToken UNIQUEIDENTIFIER NOT NULL,
    IncarnationId UNIQUEIDENTIFIER NOT NULL,
    BatchSequence BIGINT NOT NULL,
    MutationRevision BIGINT NOT NULL,
    StateType NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL,
    BatchId UNIQUEIDENTIFIER NULL,
    ExpectedFence UNIQUEIDENTIFIER NULL,
    ObservedRevision BIGINT NULL,
    IntentDigest BINARY(32) NULL,
    CancelAllThrough BIGINT NOT NULL,
    CancelAllRevision BIGINT NOT NULL,
    CONSTRAINT UQ_SagaTimerRecoveryScopes_Domain UNIQUE(TargetObjectId,TenantKey,SagaId),
    CONSTRAINT CK_SagaTimerRecoveryScopes_Counters CHECK
      (BatchSequence>=0 AND MutationRevision>=0 AND CancelAllThrough>=-1 AND CancelAllRevision>=0)
);
GO
IF OBJECT_ID(N'dispatch.SagaTimerRecoveryTimers',N'U') IS NULL
CREATE TABLE dispatch.SagaTimerRecoveryTimers
(
    ScopeId UNIQUEIDENTIFIER NOT NULL,
    TimeoutKey BINARY(32) NOT NULL,
    TimeoutId NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
    TargetObjectId INT NOT NULL,
    IsTracked BIT NOT NULL,
    IncarnationId UNIQUEIDENTIFIER NULL,
    BatchSequence BIGINT NULL,
    StateType NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL,
    CreatedRevision BIGINT NOT NULL,
    IsCancelled BIT NOT NULL,
    CancelThrough BIGINT NOT NULL,
    CancelRevision BIGINT NOT NULL,
    CONSTRAINT PK_SagaTimerRecoveryTimers PRIMARY KEY(ScopeId,TimeoutKey),
    CONSTRAINT FK_SagaTimerRecoveryTimers_Scope FOREIGN KEY(ScopeId)
      REFERENCES dispatch.SagaTimerRecoveryScopes(ScopeId),
    CONSTRAINT CK_SagaTimerRecoveryTimers_Counters CHECK
      (CreatedRevision>=0 AND CancelThrough>=-1 AND CancelRevision>=0),
    CONSTRAINT CK_SagaTimerRecoveryTimers_Original CHECK
      (IsTracked=0 OR (IncarnationId IS NOT NULL AND BatchSequence>0 AND StateType IS NOT NULL))
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dispatch.SagaTimerRecoveryTimers')
  AND name=N'UX_SagaTimerRecoveryTimers_TrackedIdentity')
CREATE UNIQUE INDEX UX_SagaTimerRecoveryTimers_TrackedIdentity
ON dispatch.SagaTimerRecoveryTimers(TargetObjectId,TimeoutKey) WHERE IsTracked=1;
GO
IF OBJECT_ID(N'dispatch.SagaTimerRecoveryTokens',N'U') IS NULL
CREATE TABLE dispatch.SagaTimerRecoveryTokens
(
    ScopeId UNIQUEIDENTIFIER NOT NULL,
    FenceToken UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_SagaTimerRecoveryTokens PRIMARY KEY(ScopeId,FenceToken),
    CONSTRAINT FK_SagaTimerRecoveryTokens_Scope FOREIGN KEY(ScopeId)
      REFERENCES dispatch.SagaTimerRecoveryScopes(ScopeId)
);
GO
IF OBJECT_ID(N'dispatch.SagaTimerRecoveryResults',N'U') IS NULL
CREATE TABLE dispatch.SagaTimerRecoveryResults
(
    ScopeId UNIQUEIDENTIFIER NOT NULL,
    Ordinal INT NOT NULL,
    Action INT NOT NULL,
    TimeoutId NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
    Outcome INT NOT NULL,
    AuthorityRevision BIGINT NOT NULL,
    CONSTRAINT PK_SagaTimerRecoveryResults PRIMARY KEY(ScopeId,Ordinal),
    CONSTRAINT FK_SagaTimerRecoveryResults_Scope FOREIGN KEY(ScopeId)
      REFERENCES dispatch.SagaTimerRecoveryScopes(ScopeId),
    CONSTRAINT CK_SagaTimerRecoveryResults_Counters CHECK(Ordinal>=0 AND AuthorityRevision>=0)
);
GO
-- Select the actual timeout table configured by SqlServerSagaTimeoutStoreOptions.
-- For custom direct tables change only this identifier; do not create an alias/synonym.
DECLARE @Target NVARCHAR(261)=N'dbo.SagaTimeouts';
IF OBJECT_ID(@Target,N'U') IS NULL THROW 51004,'Apply the selected timeout payload schema first.',1;
DECLARE @Qualified NVARCHAR(261)=QUOTENAME(OBJECT_SCHEMA_NAME(OBJECT_ID(@Target,N'U')))+N'.'+QUOTENAME(OBJECT_NAME(OBJECT_ID(@Target,N'U')));
IF COL_LENGTH(@Target,N'OwnedIncarnationId') IS NULL
  EXEC(N'ALTER TABLE '+@Qualified+N' ADD OwnedIncarnationId UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH(@Target,N'OwnedGeneration') IS NULL
  EXEC(N'ALTER TABLE '+@Qualified+N' ADD OwnedGeneration BIGINT NULL;');
IF COL_LENGTH(@Target,N'OwnedStateType') IS NULL
  EXEC(N'ALTER TABLE '+@Qualified+N' ADD OwnedStateType NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL;');
IF NOT EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTargets WHERE TargetObjectId=OBJECT_ID(@Target,N'U'))
  INSERT dispatch.SagaTimerRecoveryTargets(TargetObjectId,ReceiverId) VALUES(OBJECT_ID(@Target,N'U'),NEWID());
GO
