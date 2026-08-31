-- SQL Server MIGRATION for Excalibur.EventSourcing.SqlServer — SINGLE-TENANT IDENTITY BRIDGE
-- Version: 1.0
--
-- Converges every row this package owns from the reserved '__untenanted__' partition onto the
-- framework's single-tenant identity, '__default__'. Run this ONLY on a deployment that is,
-- and will remain, single-tenant.
--
--
-- WHY THIS EXISTS
-- ----------------
-- A single-tenant host resolves its ambient ITenantContext.TenantId as '__default__'
-- (TenantDefaults.DefaultTenantId) everywhere the framework reads it. But rows written by any
-- code path that supplied no tenant at all — the default construction path this store used
-- before it required a tenant context, a hand-written INSERT, an omitted column on an older
-- release — land under the reserved '__untenanted__' sentinel instead, because that is what an
-- absent tenant folds to at the storage boundary. One deployment ends up with its own data
-- split across two names for the same tenant, and a read scoped to '__default__' does not find
-- rows filed under '__untenanted__' — they are silently unreachable, not merely mis-labelled.
--
-- Converging the two identities is only correct for a genuinely single-tenant host. A
-- multi-tenant deployment's untenanted partition is a LIVE partition: rows that belong to no
-- real tenant (system records, rows predating a single-to-multi-tenant graduation) coexist with
-- rows that belong to named tenants, and folding "no tenant" into "the tenant named
-- '__default__'" would misattribute ownerless data to a specific, wrong tenant.
-- DO NOT RUN THIS SCRIPT AGAINST A MULTI-TENANT DEPLOYMENT.
--
--
-- WHAT THIS SCRIPT DOES NOT DO
-- ------------------------------
-- It does not change any schema. TenantId is already NOT NULL and already participates in each
-- table's identity on every deployment this script can run against (004 and 005 already close
-- that gap). This script only ever rewrites a VALUE, never a constraint, an index, or a column
-- definition.
--
-- It is adopted the same way the other scripts under Scripts/ are: copied into the deployment's
-- own migration set and applied from there. That set is not always applied by hand -- a host
-- configured to migrate on startup applies it at boot, this script with it -- so "an operator
-- runs this deliberately" is a description of the common case, not a property anything enforces.
-- That is why the single-tenant precondition below is a machine check and not only a paragraph.
--
--
-- SCOPE: FOUR INDEPENDENT TABLES, EACH GUARDED SEPARATELY
-- ---------------------------------------------------------
-- EventStoreEvents, EventStoreSnapshots, MaterializedViews, and MaterializedViewPositions are
-- converged in this one script because they are the same operation applied to every table this
-- package owns — but a deployment need not use all four (a consumer who never registered the
-- materialized-view store has no such tables). Each block below checks for its own table before
-- touching anything, so running this against a database that only has the event store is safe
-- and converges only what is present.
--
-- Table and schema names use the defaults (dbo.EventStoreEvents, dbo.EventStoreSnapshots,
-- dbo.MaterializedViews, dbo.MaterializedViewPositions); edit the literals below if you
-- overrode any of them (SqlServerEventStore, SqlServerSnapshotStore, and
-- SqlServerMaterializedViewStore all accept a table-name override).
--
--
-- COLLISION HANDLING
-- --------------------
-- The rewrite is safe in the general case: no real tenant can occupy the sentinel (a scoped
-- tenant that names it is rejected before it reaches the database), so collapsing
-- '__untenanted__' onto '__default__' preserves every existing uniqueness class UNLESS a given
-- identity already holds a row under BOTH values — a stream, aggregate, or view that was written
-- to at some point under an explicit '__default__' tenant AND also has legacy untenanted rows.
-- Each block below checks for that case first and REFUSES, naming the colliding identity,
-- rather than letting the UPDATE fail partway or silently pick a winner. Resolve the reported
-- rows (delete or re-key whichever is stale), then re-run.
--
--
-- ORDERING / DOWNTIME
-- ---------------------
-- Run during a maintenance window with the store stopped, for the same reason 004 and 005 do:
-- the backfill is a single set-based UPDATE per table and is not resumable. Take a backup you
-- have restored at least once.
--
-- Every step is guarded against the state it is about to create, so the script is safe to
-- re-run; a database with nothing left under '__untenanted__' is a no-op.

SET NOCOUNT ON;

-- Explicit transaction, plus a one-line guard at the top of every batch below. See the CORRECTED
-- note under the THROW claim further down for how this was arrived at. Both halves are required
-- and neither replaces the other: XACT_ABORT ON rolls the ACTIVE transaction back the instant a
-- THROW fires, but each `GO`-separated batch below is a SEPARATE unit the client sends to the
-- server, so once the transaction is gone a LATER batch would run unprotected in autocommit and
-- convert the remaining tables anyway. IF @@TRANCOUNT = 0 SET NOEXEC ON; at the head of each
-- batch is what stops that: a rolled-back transaction leaves @@TRANCOUNT at zero, so the first
-- batch after a refusal turns execution off for the rest of the session. SET NOEXEC OFF at the
-- very end restores the session on the path where nothing refused.
--
--
-- BOTH OF THOSE ASSUME THE WHOLE SCRIPT IS APPLIED ON ONE CONNECTION. A transaction belongs to a
-- session, so a client that reconnects between batches loses it at the first GO -- and the guard
-- would then quietly switch the rest of the script off, which is the "completed having done
-- nothing" outcome this migration exists to refuse. The batch immediately below the transaction
-- therefore checks for that case explicitly and REFUSES, naming it. It is the one batch that can:
-- nothing has run yet that could have refused, so @@TRANCOUNT = 0 there means the session was lost
-- and nothing else. sqlcmd keeps one connection by default; a migration runner may need telling.
-- The guard is plain T-SQL rather than sqlcmd's :on error exit deliberately. The directive does
-- the same job, but it is a CLIENT command rather than a statement, so any tool that is not
-- sqlcmd sends it to the server and the whole script dies on its first line with
-- "Incorrect syntax near ':'" -- having done nothing at all.
--
-- ONE THING THE GUARD CANNOT DO IS SET THE PROCESS EXIT CODE. On a refusal sqlcmd still exits 0
-- unless you pass -b. If a pipeline branches on this script's exit code, run it as
--     sqlcmd -b -i <this script>
-- or the pipeline will read a refused, no-op migration as a success.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

-- The transaction opened in the batch above must still be here. If it is not, the client did not
-- keep one session across this script's batches -- see the header. This is the only batch that can
-- tell that apart from a deliberate refusal, because nothing has run yet that could have refused.
IF @@TRANCOUNT = 0
BEGIN
    THROW 51006, N'This migration opens a transaction in its first batch and commits it in its last, so the whole script must be applied on a SINGLE connection. @@TRANCOUNT is 0 here, which means the client reconnected after the opening batch and the transaction is already gone: every remaining batch would run unprotected, and a refusal partway through would leave the database half-migrated with no way back. Refusing rather than converting anything. Apply the whole script on one connection (sqlcmd does this by default), then re-run.', 1;
END

-- ---------------------------------------------------------------------------------------
-- PRE-FLIGHT: REFUSE ON A MULTI-TENANT DEPLOYMENT
--
-- The header says do not run this against a multi-tenant deployment. Prose does not stop
-- anything, and it is read once, by whoever assembles the migration set -- not by whatever
-- applies it later. This script is adopted the way the others here are: copied into a
-- deployment's own migration set and applied with it. Where that set is applied automatically
-- at startup, this script goes with it, and nothing on that path pauses to ask whether the
-- deployment is single-tenant. So the instruction is made a machine check.
--
-- The test is the data, because the data is what this script is about to rewrite. A row filed
-- under any tenant other than the untenanted sentinel or the single-tenant default identity is
-- proof that this deployment has real, named tenants -- and therefore that its untenanted
-- partition is a live partition holding ownerless rows, not a legacy spelling of '__default__'.
-- Converging it would misattribute that data to one named tenant.
--
-- THROW aborts the ENCLOSING transaction -- see the explicit BEGIN TRANSACTION/SET XACT_ABORT ON
-- a few lines above, added because this script does not run inside one on its own. That matters
-- because the four tables below are converged in sequence and a failure discovered at the fourth
-- would otherwise leave the first three already moved, with no resumable path back.
--
-- CORRECTED, TWICE. First correction: an earlier revision of this comment claimed THROW "aborts
-- the surrounding migration transaction" as though one always exists. It does not: run standalone
-- via sqlcmd with default settings (no framework-supplied transaction -- the realistic way an
-- operator without a migration tool applies a copied script), a THROW in one table's guard did
-- not stop later GO-separated batches from running -- exactly the partially-converged, multi-
-- table state this guard exists to prevent, reached BY the guard. Measured live against a real
-- Postgres container on this script's Postgres twin.
--
-- Second correction, on the fix itself: adding SET XACT_ABORT ON + an explicit BEGIN/COMMIT
-- TRANSACTION alone is NOT sufficient on this dialect, and was measured insufficient live against
-- a real SQL Server container -- XACT_ABORT correctly rolls back the transaction the instant the
-- THROW fires, but each `GO`-separated batch after that is a SEPARATE unit the client still sends
-- to the server, and with no transaction left to abort, it just runs in ordinary autocommit and
-- converts the remaining tables anyway. The observed symptom was as bad as the original bug: all
-- four tables converged despite the REFUSE firing. The IF @@TRANCOUNT = 0 SET NOEXEC ON; guard at
-- the head of every batch, above, is what actually stops the later batches from doing anything --
-- the transaction makes the batches that DID run reversible; the guard is what keeps any more of
-- them from running. Both are required.
--
-- The probe is built as dynamic SQL so a database that has only some of the four tables is
-- asked only about the ones it has -- a static query naming an absent table fails to bind.
--
-- KNOWN LIMIT, stated rather than left to be discovered: this detects a multi-tenant deployment
-- that has written at least one named-tenant row. A multi-tenant host that has so far written
-- only untenanted rows is indistinguishable, in its data, from a single-tenant one -- and for
-- that host the convergence is also harmless, because there is no named tenant for the rows to
-- be misattributed away from. The case the check does not cover is the case that does no harm.
-- ---------------------------------------------------------------------------------------
DECLARE @Probe NVARCHAR(MAX) = N'';

IF OBJECT_ID(N'[dbo].[EventStoreEvents]', N'U') IS NOT NULL
    SET @Probe = @Probe + N'SELECT ''dbo.EventStoreEvents'' AS SourceTable, [TenantId] FROM [dbo].[EventStoreEvents] UNION ALL ';

IF OBJECT_ID(N'[dbo].[EventStoreSnapshots]', N'U') IS NOT NULL
    SET @Probe = @Probe + N'SELECT ''dbo.EventStoreSnapshots'', [TenantId] FROM [dbo].[EventStoreSnapshots] UNION ALL ';

IF OBJECT_ID(N'[dbo].[MaterializedViews]', N'U') IS NOT NULL
    SET @Probe = @Probe + N'SELECT ''dbo.MaterializedViews'', [TenantId] FROM [dbo].[MaterializedViews] UNION ALL ';

IF OBJECT_ID(N'[dbo].[MaterializedViewPositions]', N'U') IS NOT NULL
    SET @Probe = @Probe + N'SELECT ''dbo.MaterializedViewPositions'', [TenantId] FROM [dbo].[MaterializedViewPositions] UNION ALL ';

IF @Probe <> N''
BEGIN
    DECLARE @ForeignTenant NVARCHAR(400) = NULL;

    -- The trailing UNION ALL is closed by an empty terminator rather than trimmed off. LEN()
    -- ignores trailing spaces, so trimming by length here would silently cut one character short.
    SET @Probe = N'SELECT TOP (1) @Out = SourceTable + N'' holds rows under tenant '''''' + [TenantId] + N''''''''
                   FROM ('  + @Probe + N'SELECT CONVERT(NVARCHAR(400), NULL), CONVERT(NVARCHAR(450), NULL) WHERE 1 = 0'
                 + N') AS candidates
                   WHERE [TenantId] IS NOT NULL AND [TenantId] NOT IN (N''__untenanted__'', N''__default__'');';

    EXEC sp_executesql @Probe, N'@Out NVARCHAR(400) OUTPUT', @Out = @ForeignTenant OUTPUT;

    IF @ForeignTenant IS NOT NULL
    BEGIN
        DECLARE @RefusalMsg NVARCHAR(1000) = N'006 REFUSED: ' + @ForeignTenant
            + N', which is neither the untenanted sentinel nor the single-tenant identity '
            + N'''__default__''. This deployment has named tenants, so its untenanted rows belong '
            + N'to no tenant rather than to ''__default__'', and converging them would file '
            + N'ownerless data under one specific, wrong tenant. Nothing has been changed. Do not '
            + N'run this script against a multi-tenant deployment; if this host is genuinely '
            + N'single-tenant, re-key or remove the named-tenant rows first.';
        THROW 50006, @RefusalMsg, 1;
    END
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- EVENTSTOREEVENTS
--
-- Natural key excluding the tenant: (AggregateId, AggregateType, Version). A collision means one
-- stream position is claimed by both an untenanted and a default-tenant row.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[EventStoreEvents]', N'U') IS NOT NULL
BEGIN
    DECLARE @EventCollisions BIGINT;

    SELECT @EventCollisions = COUNT_BIG(*)
    FROM (
        SELECT [AggregateId], [AggregateType], [Version]
        FROM [dbo].[EventStoreEvents]
        WHERE [TenantId] IN (N'__untenanted__', N'__default__')
        GROUP BY [AggregateId], [AggregateType], [Version]
        HAVING COUNT(*) > 1
    ) AS c;

    IF @EventCollisions > 0
    BEGIN
        DECLARE @EventMsg NVARCHAR(400) = N'006 ABORT: ' + CONVERT(NVARCHAR(20), @EventCollisions)
            + N' stream position(s) in EventStoreEvents hold BOTH an untenanted row and a row '
            + N'already under the single-tenant identity ''__default__''. This deployment is '
            + N'configured as single-tenant, so the untenanted rows would be moved onto '
            + N'''__default__'' -- but that stream position already has one there, and both would '
            + N'occupy the same key. Delete or re-key whichever event is stale, then re-run. If '
            + N'this host is actually multi-tenant, do not run this script at all.';
        THROW 50006, @EventMsg, 1;
    END

    UPDATE [dbo].[EventStoreEvents]
        SET [TenantId] = N'__default__'
        WHERE [TenantId] = N'__untenanted__';
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- EVENTSTORESNAPSHOTS
--
-- Natural key excluding the tenant: (AggregateId, AggregateType). A collision means one
-- aggregate's snapshot is claimed by both an untenanted and a default-tenant row.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[EventStoreSnapshots]', N'U') IS NOT NULL
BEGIN
    DECLARE @SnapshotCollisions BIGINT;

    SELECT @SnapshotCollisions = COUNT_BIG(*)
    FROM (
        SELECT [AggregateId], [AggregateType]
        FROM [dbo].[EventStoreSnapshots]
        WHERE [TenantId] IN (N'__untenanted__', N'__default__')
        GROUP BY [AggregateId], [AggregateType]
        HAVING COUNT(*) > 1
    ) AS c;

    IF @SnapshotCollisions > 0
    BEGIN
        DECLARE @SnapshotMsg NVARCHAR(400) = N'006 ABORT: ' + CONVERT(NVARCHAR(20), @SnapshotCollisions)
            + N' aggregate(s) in EventStoreSnapshots hold BOTH an untenanted snapshot and one '
            + N'already under the single-tenant identity ''__default__''. Delete or re-key '
            + N'whichever snapshot is stale, then re-run. Snapshots are a rebuildable cache: '
            + N'deleting either row and letting it regenerate from the event stream is a '
            + N'legitimate alternative to resolving the collision by hand.';
        THROW 50006, @SnapshotMsg, 1;
    END

    UPDATE [dbo].[EventStoreSnapshots]
        SET [TenantId] = N'__default__'
        WHERE [TenantId] = N'__untenanted__';
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- MATERIALIZEDVIEWS
--
-- Natural key excluding the tenant: (ViewName, ViewId). A collision means one named view is
-- claimed by both an untenanted and a default-tenant row.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[MaterializedViews]', N'U') IS NOT NULL
BEGIN
    DECLARE @ViewCollisions BIGINT;

    SELECT @ViewCollisions = COUNT_BIG(*)
    FROM (
        SELECT [ViewName], [ViewId]
        FROM [dbo].[MaterializedViews]
        WHERE [TenantId] IN (N'__untenanted__', N'__default__')
        GROUP BY [ViewName], [ViewId]
        HAVING COUNT(*) > 1
    ) AS c;

    IF @ViewCollisions > 0
    BEGIN
        DECLARE @ViewMsg NVARCHAR(400) = N'006 ABORT: ' + CONVERT(NVARCHAR(20), @ViewCollisions)
            + N' view(s) in MaterializedViews hold BOTH an untenanted row and a row already under '
            + N'the single-tenant identity ''__default__''. Delete or re-key whichever is stale, '
            + N'then re-run. Views are rebuildable from the event stream: deleting either row and '
            + N'letting the projection replay it is a legitimate alternative.';
        THROW 50006, @ViewMsg, 1;
    END

    UPDATE [dbo].[MaterializedViews]
        SET [TenantId] = N'__default__'
        WHERE [TenantId] = N'__untenanted__';
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

-- ---------------------------------------------------------------------------------------
-- MATERIALIZEDVIEWPOSITIONS
--
-- Natural key excluding the tenant: (ViewName). A collision means one view's checkpoint is
-- claimed by both an untenanted and a default-tenant row.
--
-- Converging a checkpoint is not cosmetic: it decides which position a single-tenant host's
-- projector resumes from. Leaving a checkpoint under '__untenanted__' while reads resolve
-- '__default__' means the checkpoint is never found, and the projection replays from the
-- beginning on every restart -- silent, and it never converges on its own, because nothing
-- ever finds and advances the '__default__' checkpoint that reads are actually looking for.
-- ---------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[MaterializedViewPositions]', N'U') IS NOT NULL
BEGIN
    DECLARE @PositionCollisions BIGINT;

    SELECT @PositionCollisions = COUNT_BIG(*)
    FROM (
        SELECT [ViewName]
        FROM [dbo].[MaterializedViewPositions]
        WHERE [TenantId] IN (N'__untenanted__', N'__default__')
        GROUP BY [ViewName]
        HAVING COUNT(*) > 1
    ) AS c;

    IF @PositionCollisions > 0
    BEGIN
        DECLARE @PositionMsg NVARCHAR(400) = N'006 ABORT: ' + CONVERT(NVARCHAR(20), @PositionCollisions)
            + N' checkpoint(s) in MaterializedViewPositions hold BOTH an untenanted position and '
            + N'one already under the single-tenant identity ''__default__''. Picking a survivor '
            + N'decides which position the projector resumes from -- an operator must choose, not '
            + N'this script. Delete whichever checkpoint is stale, then re-run.';
        THROW 50006, @PositionMsg, 1;
    END

    UPDATE [dbo].[MaterializedViewPositions]
        SET [TenantId] = N'__default__'
        WHERE [TenantId] = N'__untenanted__';
END
GO

IF @@TRANCOUNT = 0 SET NOEXEC ON;

COMMIT TRANSACTION;
GO

SET NOEXEC OFF;
GO
