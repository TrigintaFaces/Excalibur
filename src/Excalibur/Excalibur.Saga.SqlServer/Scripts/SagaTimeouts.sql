-- Saga Timeouts Schema for SQL Server
-- Part of Excalibur.Saga.SqlServer package
--
-- Copyright (c) 2026 The Excalibur Project
-- See LICENSE files in project root for license information.

-- Create SagaTimeouts table for storing saga timeout requests
CREATE TABLE SagaTimeouts (
    TimeoutId NVARCHAR(450) NOT NULL,
    SagaId NVARCHAR(450) NOT NULL,
    SagaType NVARCHAR(512) NOT NULL,
    TimeoutType NVARCHAR(512) NOT NULL,
    TimeoutData VARBINARY(MAX) NULL,
    DueAt DATETIME2 NOT NULL,
    ScheduledAt DATETIME2 NOT NULL,

    CONSTRAINT PK_SagaTimeouts PRIMARY KEY (TimeoutId)
);

-- Critical index for polling performance
-- This index is essential for GetDueTimeoutsAsync to efficiently find due timeouts
CREATE INDEX IX_SagaTimeouts_DueAt
    ON SagaTimeouts (DueAt)
    WHERE DueAt IS NOT NULL;

-- Index for saga-level operations (CancelAllTimeoutsAsync)
-- Enables efficient lookup of all timeouts belonging to a specific saga
CREATE INDEX IX_SagaTimeouts_SagaId
    ON SagaTimeouts (SagaId);

-- Composite index for timeout identification within a saga
CREATE INDEX IX_SagaTimeouts_SagaId_TimeoutId
    ON SagaTimeouts (SagaId, TimeoutId);
