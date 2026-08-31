-- SQL Server MIGRATION for Excalibur.Workflows.SqlServer — SIGNAL-INBOX TENANT PARTITIONING
-- Version: 1.0
--
-- Adds the tenant term to workflow_signal_inbox and makes it part of the constraint that decides
-- ADMISSION, so one tenant's signal can no longer satisfy another tenant's uniqueness check.
--
-- BEFORE: UNIQUE (InstanceId, SignalId). Both are producer-supplied strings with no tenant
--         discriminator, so if two tenants can present the same pair, the second INSERT raises a
--         unique violation, the store catches it as a redelivery, and reports "not newly admitted".
-- AFTER:  UNIQUE (TenantId, InstanceId, SignalId), and the drain carries the tenant term too.
--
--
-- THE FAILURE THIS CLOSES IS A SILENT DROP, NOT A DISCLOSURE
-- ----------------------------------------------------------
-- The disclosure half is real — the drain read on InstanceId alone returned another tenant's
-- PayloadJson, which is producer-authored content. But the admission half is worse, because it
-- leaves nothing behind. The second tenant's signal is not stored, not logged, and not errored: the
-- store cannot distinguish "this tenant already sent this signal" from "a different tenant did", so
-- it takes the only action the narrower key allows and discards it. The workflow then waits for a
-- signal the system received and threw away. There is no row to find afterwards and no error to
-- alert on, which is why this is worth a migration rather than a note.
--
--
-- WIDENING A UNIQUE CONSTRAINT CANNOT FAIL ON EXISTING DATA
-- ---------------------------------------------------------
-- This script has no collision pre-flight, and unlike a backfill into an existing key that is a
-- provable property rather than an assumption. Adding a column to a uniqueness constraint can only
-- ever admit MORE rows: any pair of rows distinguished by (InstanceId, SignalId) is still
-- distinguished by (TenantId, InstanceId, SignalId). So no data that satisfied the old constraint
-- can violate the new one, and step 3 cannot fail partway on existing rows.
--
-- The reverse is not true, which is why this direction is safe and the opposite would not be: a
-- NARROWING migration would need exactly the pre-flight this one does not.
--
--
-- WHY EVERY EXISTING ROW BECOMES UNTENANTED, AND WHAT THAT COSTS
-- --------------------------------------------------------------
-- Step 2 backfills existing rows to the reserved '__untenanted__' sentinel — the one value a scoped
-- tenant can never resolve to, because a scoped partition rejects the sentinel outright.
--
--   A host that was single-tenant resolves that same sentinel at run time, so its rows are found
--   exactly as before: same admission decisions, same drain results, no behaviour change.
--
--   A host that was already multi-tenant leaves its historical signals in a partition no scoped
--   tenant will drain. Those signals become invisible rather than misattributed.
--
-- That is the direction to prefer for an inbox. An undrained signal is a signal that has not yet
-- been applied, and the producer's redelivery path is the designed recovery for exactly that state —
-- the redelivery is admitted, because under the widened key it no longer collides with the
-- untenanted row. The alternative, distributing historical rows among real tenants by inference,
-- would attribute one tenant's signal content to another and would be undetectable afterwards.
--
--
-- INDEX KEY WIDTH — STATED, BECAUSE IT IS WHAT DECIDES WHETHER THIS SHAPE IS AVAILABLE
-- ------------------------------------------------------------------------------------
-- SQL Server caps a CLUSTERED index key at 900 bytes and a NONCLUSTERED one at 1700. At 2 bytes per
-- NVARCHAR character:
--
--     BEFORE   InstanceId NVARCHAR(200)  ->  400 bytes
--              SignalId   NVARCHAR(200)  ->  400 bytes
--                                            ---------
--                                             800 bytes
--
--     AFTER    TenantId   NVARCHAR(64)   ->  128 bytes
--              InstanceId NVARCHAR(200)  ->  400 bytes
--              SignalId   NVARCHAR(200)  ->  400 bytes
--                                            ---------
--                                             928 bytes   (of 1700)
--
-- The binding cap is 1700, not 900, because the clustered index is already taken: Sequence is a
-- BIGINT IDENTITY primary key, so UQ_workflow_signal_inbox is NONCLUSTERED. This table therefore
-- needs no surrogate-key restructuring — it already has the shape that other tables in this
-- repository had to be migrated INTO in order to carry a tenant term. Do not copy that work here by
-- analogy; the arithmetic is what decides it, and it fits with room to spare.
--
--
-- THE STARTUP GUARD MOVES WITH THIS SCRIPT, AND WILL REFUSE A HALF-MIGRATED TABLE
-- -------------------------------------------------------------------------------
-- SqlServerWorkflowSignalInboxSchemaGuard asserts a unique index over EXACTLY the required columns
-- and refuses to start otherwise. It now requires all three. That means:
--
--   * a host running the new code against a table that has NOT had this script applied will fail to
--     start, loudly, naming the constraint — rather than starting and silently binding a TenantId
--     column that does not exist;
--   * the guard rejects the old two-column constraint deliberately, not incidentally. That table is
--     not merely out of date, it is actively dropping one tenant's signals.
--
-- Apply this script before deploying the code that requires it.
--
--
-- ORDERING / DOWNTIME
-- -------------------
-- Run during a maintenance window with signal producers stopped. Step 3 drops and recreates a unique
-- index. Take a backup you have restored at least once.
--
-- Every statement is guarded against the state it is about to create, so the script is safe to re-run.

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------------------
-- 0) PRE-FLIGHT. Reports what this migration will do. There is deliberately no collision check:
--    widening a uniqueness constraint cannot violate it on existing data (see the header), and a
--    check that can never fail is worse than no check, because it reads as protection.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[workflow_signal_inbox]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[workflow_signal_inbox]') AND name = N'TenantId')
BEGIN
    DECLARE @Signals BIGINT;
    SELECT @Signals = COUNT_BIG(*) FROM [dbo].[workflow_signal_inbox];

    PRINT N'002 pre-flight: ' + CONVERT(NVARCHAR(20), @Signals)
        + N' existing signal(s) will move to the untenanted partition.';
    PRINT N'002 pre-flight: on a single-tenant host this is a no-op at run time. On a multi-tenant host '
        + N'those signals will no longer be drained by a scoped tenant; a producer redelivery is the '
        + N'recovery path and will now be admitted rather than refused as a duplicate.';
END
GO

-- ---------------------------------------------------------------------------------------
-- 1-2) Add the column and populate it in one statement.
--
--      The DEFAULT here is a MIGRATION DEVICE, not a standing behaviour, and step 4 removes it.
--      It exists so ADD COLUMN is total over existing rows; leaving it in place would mean a write
--      that omitted the tenant landed silently in the untenanted partition, making "I forgot to
--      supply the tenant" indistinguishable from "this signal is deliberately untenanted" — the one
--      confusion the reserved sentinel exists to remove.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[workflow_signal_inbox]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT * FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[workflow_signal_inbox]') AND name = N'TenantId')
BEGIN
    ALTER TABLE [dbo].[workflow_signal_inbox]
        ADD [TenantId] NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL
            CONSTRAINT [DF_workflow_signal_inbox_TenantId_Backfill] DEFAULT N'__untenanted__';
END
GO

-- ---------------------------------------------------------------------------------------
-- 3) Rebuild the admission constraint around the now-total column.
--
--    Dropped and recreated rather than altered: SQL Server will not alter the column set of an
--    existing key constraint in place. The guard asserts an EXACT column set, so a table left with
--    both constraints would still fail startup — the old one must go, not merely be joined.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[workflow_signal_inbox]', N'U') IS NOT NULL
   AND EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[workflow_signal_inbox]') AND name = N'TenantId')
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes AS i
       INNER JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
       INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
       WHERE i.object_id = OBJECT_ID(N'[dbo].[workflow_signal_inbox]')
         AND i.is_unique = 1
         AND c.name IN (N'TenantId', N'InstanceId', N'SignalId')
       GROUP BY i.index_id
       HAVING COUNT(DISTINCT c.name) = 3 AND COUNT(*) = 3)
BEGIN
    IF EXISTS (SELECT * FROM sys.key_constraints
               WHERE parent_object_id = OBJECT_ID(N'[dbo].[workflow_signal_inbox]')
                 AND name = N'UQ_workflow_signal_inbox' AND type = N'UQ')
    BEGIN
        ALTER TABLE [dbo].[workflow_signal_inbox] DROP CONSTRAINT [UQ_workflow_signal_inbox];
    END

    ALTER TABLE [dbo].[workflow_signal_inbox]
        ADD CONSTRAINT [UQ_workflow_signal_inbox]
            UNIQUE ([TenantId], [InstanceId], [SignalId]);
END
GO

-- ---------------------------------------------------------------------------------------
-- 4) Drop the migration default. Guarded separately from the block above on purpose: a database that
--    acquired the column by some other route still needs the default removed, and once step 1 has
--    run the column is no longer absent, so a single combined guard would skip this.
-- ---------------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.default_constraints
           WHERE parent_object_id = OBJECT_ID(N'[dbo].[workflow_signal_inbox]')
             AND name = N'DF_workflow_signal_inbox_TenantId_Backfill')
BEGIN
    ALTER TABLE [dbo].[workflow_signal_inbox]
        DROP CONSTRAINT [DF_workflow_signal_inbox_TenantId_Backfill];
END
GO
