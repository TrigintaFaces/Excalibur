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
-- WHAT THE CONSTRAINT GUARANTEES, AND WHAT IT DOES NOT.
--
-- It guarantees AT MOST ONE ROW per (TableName, Lsn, SeqVal, ConsumerId, DatabaseName). It does NOT guarantee at
-- most one EXECUTION, and an earlier version of this comment claimed it did -- that it "makes the
-- filter correct when two instances process the same change concurrently." That was wrong, and it
-- was wrong in the direction that makes a consumer under-engineer their handler.
--
-- The insert is a RECORD, not a CLAIM. A claim is taken before the work; this one is taken after it.
-- The order in CdcChangeApplier is: IsProcessedAsync, then the handler, then MarkProcessedAsync. So
-- two processors sharing a consumer identity both read "not processed" -- honestly, because nothing
-- has been written yet -- both invoke the handler, and only then does one of them lose the insert.
-- The loser IS told, after it has already acted, and the exception is swallowed by design. There is
-- no execution in which the duplicate-key violation prevents a second handler invocation.
--
-- That ordering is deliberate and correct. Marking first would permit a change to be recorded as
-- processed that was never handled, which loses data; a duplicate only reprocesses. The subsystem's
-- ARCHITECTURE.md states the resulting guarantee properly: delivery is at-least-once, and nothing
-- here deduplicates unless this filter is registered. What the filter buys is the restart and retry
-- case -- a change already recorded is skipped on a later pass -- not mutual exclusion between live
-- processors.
--
-- The constraint's purpose is therefore that the loser's MarkProcessedAsync is a NO-OP rather than an
-- error, so a race does not fail the batch. Remove or weaken the uniqueness below and the code does
-- not fail; it silently stops deduplicating even across restarts, because the exception it relies on
-- is never raised.
--
-- The key is the full natural key (TableName, Lsn, SeqVal, ConsumerId, DatabaseName), matching the
-- predicate in the filter exactly, and carrying EVERY axis the checkpoint store matches on. That
-- correspondence is the invariant: the checkpoint matches on (DatabaseConnectionIdentifier,
-- DatabaseName, TableName), so a dedupe key missing any of those axes is COARSER than the position it
-- guards -- and a coarser dedupe namespace suppresses rather than duplicates, because the first
-- consumer to reach a position marks it done for everyone sharing the coarser key.
--
-- Neither ConsumerId nor DatabaseName is decoration. Nothing elsewhere establishes that a connection
-- identifier is unique across whatever shares one dedupe table: the job options validator checks only
-- that the collection is non-empty, the fan-out de-duplicates configurations by reference rather than
-- by value, and the dedupe table's own database is chosen independently of any source. So the key does
-- not rely on identifiers happening to differ -- it carries the axes outright.
--
--
-- INDEX KEY WIDTH — STATED, BECAUSE IT IS WHAT BOUNDS THE COLUMN SIZES
-- --------------------------------------------------------------------
-- SQL Server limits a CLUSTERED index key to 900 bytes. The natural key is the clustered key here,
-- so that is the limit that binds, and the column widths below are chosen to fit it rather than
-- chosen for comfort and discovered to fit:
--
--     [TableName]    NVARCHAR(128)  ->  256 bytes   (2 bytes per character)
--     [ConsumerId]   NVARCHAR(128)  ->  256 bytes
--     [DatabaseName] NVARCHAR(128)  ->  256 bytes
--     [Lsn]          BINARY(10)     ->   10 bytes
--     [SeqVal]       BINARY(10)     ->   10 bytes
--                                       ---------
--                                        788 bytes   (of 900)
--
-- The margin is now 112 bytes, down from 368 when the key had four columns. A SIXTH axis of identifier
-- width does NOT fit: 128 more characters would be 1044 bytes. If one is ever genuinely needed, the
-- natural key moves to a UNIQUE constraint over a surrogate clustered key -- which is exactly what the
-- CDC state-store table next door already does, and for this reason. Do not widen these columns and do
-- not add a sixth identifier to this key without making that change first.
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

        -- The consumer that processed the change, as a connection-string NAME. Part of the key.
        [ConsumerId]  NVARCHAR(128)   NOT NULL,

        -- The configured source database. Part of the key, and it was MISSING: the checkpoint store
        -- matches on (DatabaseConnectionIdentifier, DatabaseName, TableName), so a dedupe key without
        -- this column is coarser than the position it guards. Two configured sources that share a
        -- connection identifier and differ here kept separate checkpoints while sharing one dedupe
        -- namespace, and the first to reach a position marked it done for the other -- a change skipped
        -- that was never processed.
        [DatabaseName] NVARCHAR(128)  NOT NULL,

        -- Written by the INSERT as SYSUTCDATETIME(). The retention sweep compares against it.
        [ProcessedAt] DATETIME2(7)    NOT NULL,

        -- Load-bearing: this constraint IS the deduplication mechanism. See the header.
        CONSTRAINT [PK_CdcProcessedEvents] PRIMARY KEY CLUSTERED
            ([TableName] ASC, [Lsn] ASC, [SeqVal] ASC, [ConsumerId] ASC, [DatabaseName] ASC)
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
