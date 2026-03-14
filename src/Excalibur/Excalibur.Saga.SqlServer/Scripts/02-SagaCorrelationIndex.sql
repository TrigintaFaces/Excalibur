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
