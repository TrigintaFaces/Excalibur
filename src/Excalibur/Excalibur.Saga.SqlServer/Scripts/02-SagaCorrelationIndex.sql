-- SQL Server requires SET QUOTED_IDENTIFIER ON to create a FILTERED index (one with a WHERE
-- clause), and sqlcmd defaults it OFF. Without these, every filtered index below fails with
-- Msg 1934 and is simply absent from the resulting database -- a script runner that does not
-- check exit status gets a schema silently missing its most selective indexes.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- SQL Server Schema Migration: Correlation Query Support
-- Version: 1.1
-- Adds a persisted computed column and index for efficient correlation ID lookups.
-- Required by SqlServerSagaCorrelationQuery.

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dispatch.sagas')
      AND name = N'CorrelationId'
)
BEGIN
    ALTER TABLE dispatch.sagas
        ADD CorrelationId AS CAST(JSON_VALUE(StateJson, '$.CorrelationId') AS NVARCHAR(200)) PERSISTED;
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dispatch.sagas')
      AND name = N'IX_dispatch_sagas_CorrelationId'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_dispatch_sagas_CorrelationId
        ON dispatch.sagas (CorrelationId)
        INCLUDE (SagaType, IsCompleted)
        WHERE CorrelationId IS NOT NULL;
END
GO
