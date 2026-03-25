-- ============================================================================
-- Data Processing Background Service - Required Database Setup
-- ============================================================================
--
-- Run this script against your SQL Server database before starting the sample.
-- The data processing system requires one table to track task requests.
--
-- Default schema: [DataProcessor]
-- Default table:  [DataTaskRequests]
--
-- These defaults can be changed via DataProcessingOptions:
--   options.SchemaName = "myschema";
--   options.TableName  = "MyTaskTable";
--
-- If you change the defaults, update this script accordingly.
-- ============================================================================

-- 1. Create the schema (if it doesn't exist)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'DataProcessor')
BEGIN
    EXEC('CREATE SCHEMA [DataProcessor]');
END
GO

-- 2. Create the data task requests table
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[DataProcessor].[DataTaskRequests]') AND type = N'U')
BEGIN
    CREATE TABLE [DataProcessor].[DataTaskRequests]
    (
        [DataTaskId]    UNIQUEIDENTIFIER    NOT NULL,
        [CreatedAt]     DATETIMEOFFSET      NOT NULL,
        [RecordType]    NVARCHAR(256)       NOT NULL,
        [Attempts]      INT                 NOT NULL DEFAULT 0,
        [MaxAttempts]   INT                 NOT NULL DEFAULT 3,
        [CompletedCount] INT               NOT NULL DEFAULT 0,

        CONSTRAINT [PK_DataTaskRequests] PRIMARY KEY CLUSTERED ([DataTaskId])
    );

    -- Index for the polling query (SELECT ... WHERE Attempts < MaxAttempts ORDER BY CreatedAt)
    CREATE NONCLUSTERED INDEX [IX_DataTaskRequests_Pending]
        ON [DataProcessor].[DataTaskRequests] ([Attempts], [MaxAttempts])
        INCLUDE ([DataTaskId], [CreatedAt], [RecordType], [CompletedCount])
        WHERE [Attempts] < [MaxAttempts];
END
GO
