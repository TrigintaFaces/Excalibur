-- SQL Server requires SET QUOTED_IDENTIFIER ON to create a FILTERED index (one with a WHERE
-- clause), and sqlcmd defaults it OFF. Without these, every filtered index below fails with
-- Msg 1934 and is simply absent from the resulting database -- a script runner that does not
-- check exit status gets a schema silently missing its most selective indexes.
-- No GO after these: GO is a client batch separator, not T-SQL, and this script is also
-- executed as a SINGLE command by callers that do not split batches. The setting applies
-- within its own batch and persists across later ones, so it is not needed.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- SQL Server Schema Migration for Saga Monitoring
-- Version: 2.0
-- This script adds columns required for saga monitoring functionality.
-- Part of Excalibur.Saga.SqlServer package

-- Add CompletedAt column (nullable for backward compatibility)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dispatch.sagas') AND name = 'CompletedAt')
BEGIN
    ALTER TABLE dispatch.sagas
    ADD CompletedAt DATETIMEOFFSET(7) NULL;
END
GO

-- Add FailureReason column (nullable)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dispatch.sagas') AND name = 'FailureReason')
BEGIN
    ALTER TABLE dispatch.sagas
    ADD FailureReason NVARCHAR(MAX) NULL;
END
GO

-- Index for stuck saga queries (non-completed, ordered by UpdatedUtc)
-- Used by GetStuckSagasAsync to efficiently find sagas not updated within threshold
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sagas_IsCompleted_UpdatedUtc' AND object_id = OBJECT_ID('dispatch.sagas'))
BEGIN
    CREATE INDEX IX_Sagas_IsCompleted_UpdatedUtc
    ON dispatch.sagas (IsCompleted, UpdatedUtc)
    WHERE IsCompleted = 0;
END
GO

-- Index for failed saga queries
-- Used by GetFailedSagasAsync to efficiently find sagas with failure reasons
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sagas_FailureReason' AND object_id = OBJECT_ID('dispatch.sagas'))
BEGIN
    CREATE INDEX IX_Sagas_FailureReason
    ON dispatch.sagas (UpdatedUtc DESC)
    WHERE FailureReason IS NOT NULL;
END
GO

-- Index for completed saga queries with date filtering
-- Used by GetCompletedCountAsync and GetAverageCompletionTimeAsync
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sagas_CompletedAt' AND object_id = OBJECT_ID('dispatch.sagas'))
BEGIN
    CREATE INDEX IX_Sagas_CompletedAt
    ON dispatch.sagas (CompletedAt)
    WHERE CompletedAt IS NOT NULL;
END
GO
