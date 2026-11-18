-- Atomic saga original timeout authority; apply with atomic writers stopped.
SET XACT_ABORT ON;
IF OBJECT_ID(N'dispatch.SagaTimeoutMembership',N'U') IS NULL
    THROW 51004,'Apply the complete durable saga effects schema first.',1;
GO
IF COL_LENGTH(N'dispatch.SagaTimeoutMembership',N'StateType') IS NULL
BEGIN
    IF EXISTS(SELECT 1 FROM dispatch.SagaTimeoutMembership)
        THROW 51004,'Populated incomplete timeout metadata requires reviewed migration.',1;
    ALTER TABLE dispatch.SagaTimeoutMembership ADD StateType NVARCHAR(500) COLLATE Latin1_General_BIN2 NOT NULL;
END;
GO
IF COL_LENGTH(N'dispatch.SagaTimeoutMembership',N'IsCancelled') IS NULL
BEGIN
    IF EXISTS(SELECT 1 FROM dispatch.SagaTimeoutMembership)
        THROW 51004,'Populated incomplete timeout metadata requires reviewed migration.',1;
    ALTER TABLE dispatch.SagaTimeoutMembership ADD IsCancelled BIT NOT NULL CONSTRAINT DF_SagaTimeoutMembership_IsCancelled DEFAULT(0);
END;
GO
-- No selected timeout store is a valid profile. Its payload table need not exist.
IF OBJECT_ID(N'dbo.SagaTimeouts',N'U') IS NOT NULL
   AND (COL_LENGTH(N'dbo.SagaTimeouts',N'OwnedIncarnationId') IS NULL
     OR COL_LENGTH(N'dbo.SagaTimeouts',N'OwnedGeneration') IS NULL
     OR COL_LENGTH(N'dbo.SagaTimeouts',N'OwnedStateType') IS NULL)
BEGIN
    IF EXISTS(SELECT 1 FROM dbo.SagaTimeouts t JOIN dispatch.SagaTimeoutMembership m
      ON t.TimeoutId COLLATE Latin1_General_BIN2=m.TimeoutId AND DATALENGTH(t.TimeoutId)=DATALENGTH(m.TimeoutId)
      AND CONVERT(VARBINARY(900),t.TimeoutId)=CONVERT(VARBINARY(900),m.TimeoutId))
        THROW 51004,'Populated partial owned payload projection requires reviewed migration.',1;
END;
GO
IF OBJECT_ID(N'dbo.SagaTimeouts',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.SagaTimeouts',N'OwnedIncarnationId') IS NULL
    ALTER TABLE dbo.SagaTimeouts ADD OwnedIncarnationId UNIQUEIDENTIFIER NULL;
GO
IF OBJECT_ID(N'dbo.SagaTimeouts',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.SagaTimeouts',N'OwnedGeneration') IS NULL
    ALTER TABLE dbo.SagaTimeouts ADD OwnedGeneration BIGINT NULL;
GO
IF OBJECT_ID(N'dbo.SagaTimeouts',N'U') IS NOT NULL AND COL_LENGTH(N'dbo.SagaTimeouts',N'OwnedStateType') IS NULL
    ALTER TABLE dbo.SagaTimeouts ADD OwnedStateType NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL;
GO
IF OBJECT_ID(N'dbo.SagaTimeouts',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.CK_SagaTimeouts_OwnedOriginal',N'C') IS NULL
    ALTER TABLE dbo.SagaTimeouts WITH CHECK ADD CONSTRAINT CK_SagaTimeouts_OwnedOriginal CHECK
    ((OwnedIncarnationId IS NULL AND OwnedGeneration IS NULL AND OwnedStateType IS NULL)
     OR (OwnedIncarnationId IS NOT NULL AND OwnedGeneration IS NOT NULL AND OwnedStateType IS NOT NULL
       AND OwnedIncarnationId<>'00000000-0000-0000-0000-000000000000' AND OwnedGeneration>=0 AND LEN(OwnedStateType)>0));
GO
