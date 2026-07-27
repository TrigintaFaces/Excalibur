-- Saga Timeouts Schema for SQL Server
-- Part of Excalibur.Saga.SqlServer package
--
-- Copyright (c) 2026 The Excalibur Project
-- See LICENSE files in project root for license information.

-- Create SagaTimeouts table for storing saga timeout requests
-- DATETIMEOFFSET, not DATETIME2. SagaTimeout declares DueAt/ScheduledAt as DateTimeOffset, and
-- DATETIME2 has no offset: the client writes the local wall-clock of the supplied instant and the
-- offset is discarded. A timeout scheduled from a host at UTC+1 is then compared against UTC and
-- fires an hour early. The column must hold what the contract carries.
CREATE TABLE SagaTimeouts (
    TimeoutId NVARCHAR(450) NOT NULL,
    SagaId NVARCHAR(450) NOT NULL,
    SagaType NVARCHAR(512) NOT NULL,
    TimeoutType NVARCHAR(512) NOT NULL,
    TimeoutData VARBINARY(MAX) NULL,
    DueAt DATETIMEOFFSET(7) NOT NULL,
    ScheduledAt DATETIMEOFFSET(7) NOT NULL,
    ClaimedAt DATETIMEOFFSET(7) NULL,
    ClaimedBy NVARCHAR(200) NULL,

    -- The tenant that owns the saga this timeout belongs to. NOT NULL with the reserved untenanted
    -- sentinel, and COLLATE Latin1_General_BIN2 so in-engine equality on the tenant term is
    -- case-sensitive like .NET's Ordinal comparison — the same shape as the sagas table and the audit
    -- schema. Without this column the timeout row identifies its saga by SagaId alone, which is not
    -- that saga's identity: saga identity is (TenantId, SagaId), so a cancel-by-SagaId would delete
    -- another tenant's pending timeouts and a claimed batch would hand one tenant's TimeoutData to a
    -- processor operating for another.
    TenantId NVARCHAR(200) COLLATE Latin1_General_BIN2 NOT NULL DEFAULT '__untenanted__',

    CONSTRAINT PK_SagaTimeouts PRIMARY KEY (TimeoutId)
);

-- Critical index for polling performance
-- This index is essential for ClaimDueTimeoutsAsync to efficiently find claimable due timeouts
-- (due, and either never claimed or whose lease has gone stale).
CREATE INDEX IX_SagaTimeouts_DueAt_ClaimedAt
    ON SagaTimeouts (DueAt, ClaimedAt)
    WHERE DueAt IS NOT NULL;

-- Index for saga-level operations (CancelAllTimeoutsAsync)
-- Enables efficient lookup of all timeouts belonging to a specific saga. TenantId leads because the
-- saga-level predicates are now (TenantId, SagaId) — the ruled saga identity — so a tenant's timeouts
-- are contiguous and a scoped cancel is a range seek rather than a scan with a residual filter.
CREATE INDEX IX_SagaTimeouts_TenantId_SagaId
    ON SagaTimeouts (TenantId, SagaId);

-- Composite index for timeout identification within a saga, keyed by the full saga identity.
CREATE INDEX IX_SagaTimeouts_TenantId_SagaId_TimeoutId
    ON SagaTimeouts (TenantId, SagaId, TimeoutId);
