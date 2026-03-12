-- Creates the saga idempotency tracking table.
-- Prevents duplicate message processing in saga handlers.
-- Schema and table name can be customized via SqlServerSagaIdempotencyOptions.
-- Defaults: schema = 'dispatch', table = 'saga_idempotency'.

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dispatch')
BEGIN
    EXEC('CREATE SCHEMA dispatch');
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'saga_idempotency' AND schema_id = SCHEMA_ID('dispatch'))
BEGIN
    CREATE TABLE [dispatch].[saga_idempotency] (
        SagaId NVARCHAR(256) NOT NULL,
        IdempotencyKey NVARCHAR(512) NOT NULL,
        ProcessedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_saga_idempotency PRIMARY KEY CLUSTERED (SagaId, IdempotencyKey)
    );

    -- Index for retention cleanup queries
    CREATE NONCLUSTERED INDEX IX_saga_idempotency_ProcessedAt
        ON [dispatch].[saga_idempotency] (ProcessedAt);
END;
