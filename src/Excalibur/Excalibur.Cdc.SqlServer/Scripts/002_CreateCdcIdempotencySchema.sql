-- SQL Server Schema for Excalibur.Cdc.SqlServer — CDC IDEMPOTENCY FILTER
-- Version: 1.0
--
-- Creates the table the SQL Server CDC idempotency filter uses to record which change records it
-- has already delivered. This provider never creates the table at runtime: run this script against
-- the target database before the first change is processed. Without it, every duplicate check fails
-- with Invalid object name and CDC processing stops.
--
-- This is a separate script from 001 on purpose. The state store (001) records how far each stream
-- has been read and is required by every SQL Server CDC deployment. The idempotency filter is
-- optional — a deployment that registers the in-memory filter, or none at all, never touches this
-- table — so its schema is separately obtainable rather than folded into the mandatory script.
--
-- Table and schema names are configurable. This script uses the defaults:
--
--     schema = "Cdc"
--     table  = "CdcProcessedEvents"
--
-- If you override either, rename the object below to match.
--
--
-- THE UNIQUE CONSTRAINT IS THE MECHANISM, NOT A SAFEGUARD
-- -------------------------------------------------------
-- The filter does not check-then-insert under a lock. MarkProcessedAsync issues a bare INSERT and
-- treats a duplicate-key violation as success:
--
--     catch (SqlException ex) when (IsDuplicateKeyViolation(ex))
--
-- That is what makes the filter correct when two instances process the same change concurrently —
-- the loser of the race is told so by the database rather than by a read that raced. Remove or
-- weaken the uniqueness below and the code does not fail; it silently stops deduplicating, because
-- the exception it relies on is never raised and both instances proceed as if they had won.
--
-- The key is therefore the full natural key (TableName, Lsn, SeqVal, ConsumerId), matching the
-- predicate in HasProcessedAsync exactly. ConsumerId is part of it and is not decoration: without
-- it the dedupe namespace is table-plus-position, so the first consumer to process a change marks
-- it done for every other consumer and the others skip a change they never saw.
--
--
-- INDEX KEY WIDTH — STATED, BECAUSE IT IS WHAT BOUNDS THE COLUMN SIZES
-- --------------------------------------------------------------------
-- SQL Server limits a CLUSTERED index key to 900 bytes. The natural key is the clustered key here,
-- so that is the limit that binds, and the column widths below are chosen to fit it rather than
-- chosen for comfort and discovered to fit:
--
--     [TableName]   NVARCHAR(128)  ->  256 bytes   (2 bytes per character)
--     [ConsumerId]  NVARCHAR(128)  ->  256 bytes
--     [Lsn]         BINARY(10)     ->   10 bytes
--     [SeqVal]      BINARY(10)     ->   10 bytes
--                                      ---------
--                                       532 bytes   (of 900)
--
-- 128 is not an arbitrary cap on TableName: the value is a SQL Server capture instance or table
-- name, and a SQL Server identifier is at most 128 characters, so the column cannot be narrower
-- than the domain requires nor usefully wider. ConsumerId is application-defined and is capped at
-- the same width deliberately — widening either column past NVARCHAR(128) pushes the key over 900
-- bytes, and that failure is quiet in the worst way: CREATE TABLE still SUCCEEDS with only a
-- warning, and the table then REFUSES oversized inserts at run time with Msg 1946. A row that
-- cannot be inserted is not a duplicate, so the filter's duplicate-key handling does not absorb it
-- — the INSERT throws out of MarkProcessedAsync and CDC processing fails.
--
-- If a deployment genuinely needs a longer ConsumerId, do NOT simply widen the column. Give the
-- table a surrogate clustered key and move the natural key to a NONCLUSTERED UNIQUE constraint,
-- which is bounded at 1700 bytes rather than 900 — the shape 001 uses for the state store, and for
-- this same reason.
--
--
-- Every statement is guarded, so the script is safe to re-run.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'Cdc') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA [Cdc];');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = N'Cdc' AND t.name = N'CdcProcessedEvents')
BEGIN
    CREATE TABLE [Cdc].[CdcProcessedEvents]
    (
        -- The source table, as the CDC capture instance names it. A SQL Server identifier is at
        -- most 128 characters; see the header for why this column may not be widened.
        [TableName]   NVARCHAR(128)   NOT NULL,

        -- SQL Server log sequence numbers are fixed-width binary(10). NOT NULL: a change record
        -- without a position cannot be deduplicated, and a NULL here would never compare equal to
        -- itself, so every re-delivery of it would be treated as new.
        [Lsn]         BINARY(10)      NOT NULL,
        [SeqVal]      BINARY(10)      NOT NULL,

        -- The consumer that processed the change. Part of the key: see the header.
        [ConsumerId]  NVARCHAR(128)   NOT NULL,

        -- Written by the INSERT as SYSUTCDATETIME(). The retention sweep compares against it.
        [ProcessedAt] DATETIME2(7)    NOT NULL,

        -- Load-bearing: this constraint IS the deduplication mechanism. See the header.
        CONSTRAINT [PK_CdcProcessedEvents] PRIMARY KEY CLUSTERED
            ([TableName] ASC, [Lsn] ASC, [SeqVal] ASC, [ConsumerId] ASC)
    );
END
GO

-- The retention sweep is `DELETE TOP (@batchSize) ... WHERE ProcessedAt < @cutoff`. ProcessedAt is
-- the last column of no index otherwise, so without this the sweep scans the whole table on every
-- pass — on a table sized by CDC throughput, and while CDC processing is trying to write to it.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[Cdc].[CdcProcessedEvents]')
      AND name = N'IX_CdcProcessedEvents_ProcessedAt')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CdcProcessedEvents_ProcessedAt]
        ON [Cdc].[CdcProcessedEvents] ([ProcessedAt] ASC);
END
GO
