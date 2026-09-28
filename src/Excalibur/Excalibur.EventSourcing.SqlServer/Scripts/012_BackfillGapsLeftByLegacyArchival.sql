-- =============================================================================================
-- 012 — Backfill the global-position gaps left by the LEGACY archival, which DELETED event rows.
--
-- YOU PROBABLY DO NOT NEED THIS. Run it only if BOTH are true:
--   * you archived events on a version released before archival began tombstoning, AND
--   * your global-stream subscriber has stopped advancing, or you want it to keep working after
--     upgrading to a version that refuses to read past a gap.
-- It is a no-op on a database with no gaps, so running it when unsure is safe but pointless.
-- The final SELECT tells you which case you are in BEFORE anything is written.
--
-- ---------------------------------------------------------------------------------------------
-- WHY A GAP MATTERS NOW WHEN IT DID NOT BEFORE
--
-- The event store guarantees that the committed global positions form a CONTIGUOUS PREFIX. That is
-- a predicate on a STATE, and a subscriber's scan SPANS states — it examines each slot at a
-- different instant and never returns to one it has passed. So a scan can pass position N while N
-- is uncommitted, N commits, N+1 commits, and the scan then returns N+1. The subscriber advances
-- its high-water mark past N and never sees it again: silent, permanent event loss.
--
-- The reader now defends against that by delivering only the CONTIGUOUS run from the caller's
-- position and stopping at the first gap — sound, because under the contiguous-prefix guarantee a
-- missing position belongs to a transaction still in flight rather than to a hole.
--
-- THAT REASONING FAILS IF A POSITION IS PERMANENTLY ABSENT. Archival used to run
-- `DELETE FROM EventStoreEvents WHERE ... Version <= @ToVersion`, removing rows outright. It now
-- TOMBSTONES in place (`UPDATE ... SET EventData = NULL`), so no new hole can appear — but a
-- database archived by the older version carries permanent ones, and a subscriber whose checkpoint
-- sits below such a hole will WAIT AT IT FOREVER rather than skip past it.
--
-- A stall is the correct failure — it is loud and observable, where a skipped event is silent and
-- unrecoverable — but it is not an acceptable resting state, hence this script.
--
-- ---------------------------------------------------------------------------------------------
-- WHAT IT WRITES, AND WHY IT IS NOT INVENTING EVENTS
--
-- One row per missing position, with `EventData = NULL` and `ArchivedAt` set — byte-for-byte the
-- shape an ARCHIVED row already has. That is deliberate and it is what makes this safe: every
-- consumer of the global stream already skips a null-payload row structurally and advances past it
-- (the async projection host, the global-stream projection host, the ephemeral projection engine,
-- the rebuild and recovery services, and the live subscription all apply the same guard). So a
-- backfilled row introduces NO new event type, NO new branch for a consumer to write, and NO
-- deliverable event. It restores contiguity and nothing else.
--
-- It does NOT recover the deleted events. Nothing can — they were removed. This makes the stream
-- readable again and marks, permanently and visibly, where data was lost.
--
-- ---------------------------------------------------------------------------------------------
-- SAFETY
--   * IDEMPOTENT — it only inserts positions that are absent, so re-running writes nothing.
--   * BOUNDED — it fills only BELOW the current maximum position. It never extends the stream, so
--     it cannot collide with the allocator or with a concurrent append.
--   * NON-DESTRUCTIVE — it contains no UPDATE and no DELETE. Existing rows are untouched.
--   * Take a backup first anyway. This edits the table that IS your system of record.
--
-- RUN IT WHEN THE STREAM IS QUIET. A concurrent append allocating while this runs is harmless (it
-- allocates above the maximum, which this never touches), but a quiet window makes the before/after
-- counts meaningful.
-- =============================================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- ---------------------------------------------------------------------------------------------
-- STEP 1 — REPORT. Read this before running step 2. It writes nothing.
-- ---------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[EventStoreEvents]', N'U') IS NULL
BEGIN
    RAISERROR(N'EventStoreEvents does not exist in this database; nothing to do.', 10, 1) WITH NOWAIT;
END
ELSE
BEGIN
    DECLARE @MaxPosition BIGINT = (SELECT ISNULL(MAX([Position]), 0) FROM [dbo].[EventStoreEvents]);
    DECLARE @RowCount    BIGINT = (SELECT COUNT_BIG(*)              FROM [dbo].[EventStoreEvents]);
    DECLARE @Missing     BIGINT = CASE WHEN @MaxPosition > 0 THEN @MaxPosition - @RowCount ELSE 0 END;

    SELECT
        [MaxPosition]     = @MaxPosition,
        [RowsPresent]     = @RowCount,
        [PositionsAbsent] = @Missing,
        [Verdict]         = CASE
                                WHEN @MaxPosition = 0 THEN N'Empty store. Nothing to do.'
                                WHEN @Missing = 0     THEN N'CONTIGUOUS. No gaps. Do not run step 2.'
                                ELSE N'GAPS PRESENT. Step 2 will insert this many tombstone rows.'
                            END;
END
GO

-- ---------------------------------------------------------------------------------------------
-- STEP 2 — BACKFILL. Uncomment the body to run it.
--
-- It is commented out on purpose. This script ships as content a consumer applies BY HAND against
-- their own system of record, and a file that writes to the event store the moment it is executed
-- is the wrong default for something most databases do not need. Read step 1's verdict first.
-- ---------------------------------------------------------------------------------------------
/*
SET XACT_ABORT ON;
BEGIN TRANSACTION;

    DECLARE @Max BIGINT = (SELECT ISNULL(MAX([Position]), 0) FROM [dbo].[EventStoreEvents]);

    ;WITH [Numbers] AS
    (
        -- A numbers set over 1..@Max, built from system catalogs so no helper table is required.
        SELECT TOP (CASE WHEN @Max > 0 THEN @Max ELSE 0 END)
               [Position] = CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS BIGINT)
        FROM sys.all_columns a CROSS JOIN sys.all_columns b
    )
    INSERT INTO [dbo].[EventStoreEvents]
        ([Position], [EventId], [AggregateId], [AggregateType], [EventType],
         [EventData], [Metadata], [Version], [Timestamp], [ArchivedAt], [TenantId])
    SELECT
        n.[Position],
        -- Deterministic, so a re-run cannot produce a second row for the same position even if the
        -- absence check were somehow bypassed.
        N'gap-backfill-' + CAST(n.[Position] AS NVARCHAR(32)),
        N'__gap_backfill__',
        N'__gap_backfill__',
        N'__GapBackfill__',
        NULL,                    -- EventData NULL: this is the shape every consumer already skips.
        NULL,
        -- Version = Position, and this column is LOAD-BEARING rather than cosmetic.
        -- UQ_EventStoreEvents_Stream is UNIQUE (AggregateId, AggregateType, Version, TenantId),
        -- and the other three columns are constants on every backfilled row. A literal 0 here
        -- therefore makes every row after the FIRST a duplicate key: with XACT_ABORT ON the
        -- whole transaction aborts and nothing is written. Legacy archival deleted RANGES, so
        -- that is the normal case, not an edge one. Position is unique by definition, so using
        -- it makes the quad unique and leaves the row traceable to the gap it fills.
        n.[Position],
        SYSDATETIMEOFFSET(),
        SYSDATETIMEOFFSET(),     -- ArchivedAt set: the payload is genuinely, permanently gone.
        N'__untenanted__'        -- the reserved sentinel; these rows belong to no tenant.
    FROM [Numbers] n
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[EventStoreEvents] e WHERE e.[Position] = n.[Position]);

    DECLARE @Inserted BIGINT = @@ROWCOUNT;

COMMIT TRANSACTION;

-- Report whether the postcondition was actually reached, not merely how many rows were written.
-- The numbers set above is bounded by sys.all_columns CROSS JOIN sys.all_columns; a store whose
-- maximum position exceeds that bound is UNDER-FILLED SILENTLY, because TOP (@Max) simply yields
-- fewer rows and the insert reports a smaller count without complaining. An operation that can
-- fall short must say so.
DECLARE @RemainingMax BIGINT = (SELECT ISNULL(MAX([Position]), 0) FROM [dbo].[EventStoreEvents]);
DECLARE @RemainingRows BIGINT = (SELECT COUNT_BIG(*)              FROM [dbo].[EventStoreEvents]);
DECLARE @StillAbsent   BIGINT = CASE WHEN @RemainingMax > 0 THEN @RemainingMax - @RemainingRows ELSE 0 END;

SELECT
    [TombstonesInserted] = @Inserted,
    [PositionsStillAbsent] = @StillAbsent,
    [Verdict] = CASE
                    WHEN @StillAbsent = 0 THEN N'CONTIGUOUS. The backfill achieved its postcondition.'
                    ELSE N'INCOMPLETE -- positions are STILL absent. Do not treat this as done; the '
                       + N'numbers set did not cover the full range. Re-run, or widen it.'
                END;
GO
*/
