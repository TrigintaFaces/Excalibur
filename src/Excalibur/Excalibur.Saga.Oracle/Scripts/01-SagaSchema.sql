-- Oracle Schema for Excalibur.Saga.Oracle
-- Version: 1.0
--
-- Creates the saga state table read and written by the Oracle saga store. The store never
-- creates it at runtime: run this script against the target database before the first saga
-- is started.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     OracleSagaStoreOptions.SchemaName = "DISPATCH"
--     OracleSagaStoreOptions.TableName  = "SAGAS"
--
-- If you override either, rename the object below to match.
--
-- This script covers the saga STATE table only. If you also use durable saga timeouts, run
-- the sibling script SagaTimeouts.sql, which provisions DISPATCH.SAGATIMEOUTS.
--
-- Oracle has no "CREATE TABLE IF NOT EXISTS"; re-running this script against an existing
-- table raises ORA-00955 (name is already used by an existing object), which is safe to
-- ignore.

CREATE TABLE DISPATCH.SAGAS (
    -- RAW(16), the Oracle representation of the GUID the store binds.
    SagaId       RAW(16)                                              NOT NULL,
    SagaType     VARCHAR2(500)                                        NOT NULL,
    StateJson    CLOB                                                 NOT NULL,
    -- Oracle has no boolean column type; the store reads and writes 0/1.
    IsCompleted  NUMBER(1)     DEFAULT 0                              NOT NULL,
    -- NOT NULL, carrying a reserved sentinel for a saga that is genuinely not tenant-scoped,
    -- never NULL. This is load-bearing on Oracle specifically: Oracle stores the empty string
    -- AS NULL, so a NULL-encoded untenanted partition is unaddressable -- neither `= :TenantId`
    -- with a null bind nor `= ''` can ever match such a row, while the store's tenant
    -- predicates are unconditional. A nullable discriminator also makes "global" and "the
    -- scope was forgotten" indistinguishable.
    TenantId     VARCHAR2(64) DEFAULT '__untenanted__'                NOT NULL,
    -- Application-level optimistic concurrency version. The store performs a compare-and-swap
    -- on this column: a save is version-gated, and a competing writer that has already
    -- advanced the row leaves both merge branches matching nothing, which surfaces as a
    -- concurrency conflict rather than a lost update.
    Version      NUMBER(19)    DEFAULT 0                               NOT NULL,
    CreatedUtc   TIMESTAMP     DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP)   NOT NULL,
    UpdatedUtc   TIMESTAMP     DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP)   NOT NULL,
    -- WITH TIME ZONE, unlike the two timestamps above. Those are stamped server-side already
    -- in UTC; this one is a consumer-supplied instant that the retention purge compares
    -- against a UTC threshold, so discarding its offset would purge a saga completed from a
    -- host east of UTC earlier than its retention window allows. NULL until the saga completes.
    CompletedAt  TIMESTAMP WITH TIME ZONE                              NULL,
    -- The tenant term is PART OF THE KEY, and leads it. Sagas are correlated by a BUSINESS key
    -- such as an order id, not by a per-tenant GUID, so two tenants can legitimately hold the
    -- same SagaId. Keyed on SagaId alone they are one row: the second tenant's save either
    -- violates the key or overwrites the first tenant's state and its tenant stamp, and the
    -- cross-tenant case becomes inexpressible. TenantId leading also makes a tenant's sagas
    -- physically contiguous, so a tenant-scoped read is a range scan.
    CONSTRAINT PK_SAGAS PRIMARY KEY (TenantId, SagaId)
);

-- Supports the retention purge, which deletes on CompletedAt:
--     DELETE ... WHERE CompletedAt IS NOT NULL AND CompletedAt < :Threshold
CREATE INDEX IX_SAGAS_COMPLETEDAT ON DISPATCH.SAGAS (CompletedAt);
