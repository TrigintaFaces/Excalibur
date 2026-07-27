-- Saga Timeouts Schema for Oracle
-- Part of Excalibur.Saga.Oracle package
--
-- Copyright (c) 2026 The Excalibur Project
-- See LICENSE files in project root for license information.
--
-- Default schema is DISPATCH and default table is SAGATIMEOUTS; both are configurable via
-- OracleSagaTimeoutStoreOptions (SchemaName / TableName). Oracle resolves unquoted identifiers
-- case-insensitively by folding them to upper case, so the store issues unquoted SCHEMA.TABLE and
-- this script creates the objects unquoted to match.

CREATE TABLE DISPATCH.SAGATIMEOUTS (
    TimeoutId    VARCHAR2(450 CHAR)          NOT NULL,
    SagaId       VARCHAR2(450 CHAR)          NOT NULL,
    SagaType     VARCHAR2(512 CHAR)          NOT NULL,
    TimeoutType  VARCHAR2(512 CHAR)          NOT NULL,
    TimeoutData  BLOB                            NULL,
    -- TIMESTAMP WITH TIME ZONE, not TIMESTAMP: the store binds and reads DateTimeOffset, and a bare
    -- TIMESTAMP discards the offset, so a round trip would silently re-interpret the instant.
    DueAt        TIMESTAMP(7) WITH TIME ZONE NOT NULL,
    ScheduledAt  TIMESTAMP(7) WITH TIME ZONE NOT NULL,
    ClaimedAt    TIMESTAMP(7) WITH TIME ZONE     NULL,
    ClaimedBy    VARCHAR2(200 CHAR)              NULL,
    -- The tenant that owns the saga this timeout belongs to. NOT NULL with the reserved untenanted
    -- sentinel, matching the sagas table. Load-bearing on Oracle specifically: Oracle stores the empty
    -- string AS NULL, so a NULL-encoded untenanted partition is unaddressable here — neither
    -- `= :TenantId` with a null bind nor `= ''` can match a row. The sentinel is a non-empty reserved
    -- string, so it compares as an ordinary value.
    --
    -- Without this column the row identifies its saga by SagaId alone, which is not that saga's
    -- identity: saga identity is (TenantId, SagaId). A cancel-by-SagaId would delete another tenant's
    -- pending timeouts, and a claimed batch would hand one tenant's TimeoutData to a processor
    -- operating for another.
    TenantId     VARCHAR2(200 CHAR) DEFAULT '__untenanted__' NOT NULL,

    CONSTRAINT PK_SAGATIMEOUTS PRIMARY KEY (TimeoutId)
);

-- Claim path. ClaimDueTimeoutsAsync scans for rows that are due and either unclaimed or whose lease
-- has gone stale, under FOR UPDATE SKIP LOCKED. Leading with DueAt makes the range predicate the
-- driving access path; ClaimedAt follows so the lease test is answered from the index.
--
-- Oracle has no filtered ("partial") index, so unlike the SQL Server script there is no WHERE clause
-- here. Oracle also omits all-NULL keys from a B-tree, which is harmless: ClaimedAt is the trailing
-- column and DueAt is NOT NULL, so every row is indexed.
CREATE INDEX IX_SAGATIMEOUTS_DUEAT_CLAIMEDAT
    ON DISPATCH.SAGATIMEOUTS (DueAt, ClaimedAt);

-- Drain path. ClaimDueTimeoutsAsync re-reads the rows it just claimed by ProcessorId.
CREATE INDEX IX_SAGATIMEOUTS_CLAIMEDBY
    ON DISPATCH.SAGATIMEOUTS (ClaimedBy);

-- Saga-scoped cancellation, keyed by the full ruled saga identity (TenantId, SagaId). Serves both
-- CancelTimeoutAsync (TenantId AND SagaId AND TimeoutId) and CancelAllTimeoutsAsync (TenantId AND
-- SagaId), which uses the leading columns as an index prefix.
--
-- TenantId leads so a tenant's timeouts are contiguous and a scoped cancel is a range seek rather than
-- a scan with a residual filter.
--
-- No separate single-column (SagaId) index: it is a strict prefix of this one and would be dead
-- weight on every write. The SQL Server script carries both only for historical reasons.
CREATE INDEX IX_SAGATIMEOUTS_TENANTID_SAGAID_TIMEOUTID
    ON DISPATCH.SAGATIMEOUTS (TenantId, SagaId, TimeoutId);
