-- SQL Server Schema for Excalibur.Data.DataProcessing — TASK REQUEST QUEUE
-- Version: 1.0
--
-- Creates the table the data-processing orchestrator uses to queue bulk and backfill work
-- items. The orchestrator never creates this table at runtime: run this script against the
-- target database before the first task is enqueued. Without it, every enqueue and every
-- pending-work scan fails with Invalid object name.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     schema = "DataProcessor"
--     table  = "DataTaskRequests"
--
-- If you override either, rename the object below to match.
--
--
-- RETRY AND PROGRESS
-- ------------------
-- The pending scan selects work where Attempts < MaxAttempts, so those two columns together
-- decide whether an item is still eligible; an item that exhausts its attempts stops being
-- selected rather than being deleted, so a failed backfill remains visible for diagnosis.
--
-- The two cursor columns record how far a long-running task has read and processed. They are
-- inserted NULL and filled in as the task advances, and the update coalesces so that passing
-- NULL preserves the stored value rather than erasing progress.

IF SCHEMA_ID(N'DataProcessor') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA [DataProcessor];');
END
GO

CREATE TABLE [DataProcessor].[DataTaskRequests]
(
    [DataTaskId]      UNIQUEIDENTIFIER    NOT NULL,
    [CreatedAt]       DATETIMEOFFSET      NOT NULL,
    [RecordType]      NVARCHAR(256)       NOT NULL,

    -- Retry eligibility. The default matches the orchestrator's own default maximum, so a
    -- task enqueued without an explicit limit behaves the same whether the value was bound
    -- by the caller or supplied here.
    [Attempts]        INT                 NOT NULL DEFAULT 0,
    [MaxAttempts]     INT                 NOT NULL DEFAULT 3,

    -- Progress.
    [CompletedCount]  BIGINT              NOT NULL DEFAULT 0,
    [FetchCursor]     NVARCHAR(512)       NULL,
    [ProcessedCursor] NVARCHAR(512)       NULL,

    CONSTRAINT [PK_DataTaskRequests] PRIMARY KEY CLUSTERED ([DataTaskId])
);
GO
