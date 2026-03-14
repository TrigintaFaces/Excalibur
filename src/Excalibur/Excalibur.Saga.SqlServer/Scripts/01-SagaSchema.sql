-- SQL Server Schema for Excalibur.Saga.SqlServer
-- Version: 1.0
-- This script creates the saga storage schema for the Excalibur framework.

-- Create schema if not exists
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dispatch')
BEGIN
    EXEC('CREATE SCHEMA dispatch');
END
GO

-- Create sagas table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dispatch.sagas') AND type = N'U')
BEGIN
    CREATE TABLE dispatch.sagas (
        -- Primary key
        SagaId UNIQUEIDENTIFIER NOT NULL,

        -- Saga metadata
        SagaType NVARCHAR(500) NOT NULL,
        StateJson NVARCHAR(MAX) NOT NULL,
        IsCompleted BIT NOT NULL DEFAULT 0,

        -- Timestamps
        CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        -- Concurrency control
        RowVersion ROWVERSION NOT NULL,

        -- Constraints
        CONSTRAINT PK_dispatch_sagas PRIMARY KEY CLUSTERED (SagaId)
    );

    -- Index for querying by saga type
    CREATE NONCLUSTERED INDEX IX_dispatch_sagas_SagaType
        ON dispatch.sagas (SagaType)
        INCLUDE (IsCompleted);

    -- Index for querying incomplete sagas
    CREATE NONCLUSTERED INDEX IX_dispatch_sagas_IsCompleted
        ON dispatch.sagas (IsCompleted)
        WHERE IsCompleted = 0;
END
GO
