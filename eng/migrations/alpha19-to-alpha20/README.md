# SQL Server alpha.19 to alpha.20 migration

This is a schema-only migration for the default `dbo` names on SQL Server 2017 or later. It preserves the old checkpoint table as `SubscriptionCheckpoints_Alpha19`, leaves `SubscriptionCheckpointsDefinitions` unchanged, and creates the new member-binding and reader-binding tables. It does not rewrite events, snapshots, operation receipts, CDC, or projection data. No outbox/inbox changes are included.

## Before executing

Rehearse on a restored production backup with the exact candidate packages and application configuration. Stop every API, worker, scheduler and other event/projection writer. Keep them stopped through validation and cutover. Retain a recoverable backup and the old projection destinations.

Open `Upgrade.sql` in the **checkpoint database**. Set its four declarations:

```sql
DECLARE @ExpectedDatabase sysname = N'YourCheckpointDatabase';
DECLARE @EventDatabase sysname = N'YourEventDatabase';
DECLARE @AllHostsStopped bit = 1;
DECLARE @FreshProjectionReplayPlanned bit = 1;
```

Both databases must be on the same SQL Server instance for the coherent transaction used here. They may be the same database. Custom names or remote event databases require a reviewed adaptation; do not remove the preflight to bypass it.

Run the complete script as a principal able to inspect/lock the source and reproduce checkpoint permissions. It is transactional and safe to repeat after its own successful migration: existing new bindings and reader positions remain unchanged. Unexpected schemas/dependencies/permissions are refused. Explicit object grants, denies and grant options are copied to both replacement tables; verify access using the application's actual principal before starting processing.

## Event-position gaps are an upgrade blocker

Fresh checkpoint bindings replay from the beginning. A legacy cursor beyond a historical gap can hide it until an upgrade resets progress. Replay then stops at the missing position; reads and connectivity health can remain green while Inline writes wait or the application reports an error after events have committed. Never resubmit those commands merely because projection acknowledgement failed.

The script acquires the allocator lock before the event-table lock, matching append order, and holds both until migration completes. It requires exactly one allocator row (`Id=1`), unique positive positions forming `1..head`, and an allocator value equal to that head. Empty stores require an allocator of zero. It rejects leading, interior and trailing holes, duplicate/nonpositive positions, and missing/lagging counters **before changing checkpoint tables**.

For diagnosis, with all writers stopped, run this in the event database:

```sql
SELECT Id, Value AS AllocatorValue FROM dbo.EventStoreEventsPosition;
SELECT COUNT_BIG(*) AS EventCount, COUNT_BIG(DISTINCT Position) AS UniquePositions,
       MIN(Position) AS FirstPosition, MAX(Position) AS LastPosition
FROM dbo.EventStoreEvents;
WITH Positions AS (
    SELECT CAST(0 AS bigint) AS Position
    UNION SELECT Position FROM dbo.EventStoreEvents
), Ranges AS (
    SELECT Position, LEAD(Position) OVER (ORDER BY Position) AS NextPosition FROM Positions
)
SELECT Position + 1 AS MissingFrom, NextPosition - 1 AS MissingThrough
FROM Ranges WHERE NextPosition > Position + 1;
```

The range query reports leading/interior gaps; compare the allocator with `MAX(Position)` to detect trailing gaps. These diagnostic queries are not a substitute for the script's locked preflight.

**No generic gap-skipping repair is safe.** Missing rows may be recoverable events, historical deletions, or an append not yet committed. Do not renumber source positions, lower the allocator, or advance every projection cursor over the gap. Determine provenance from backups and append/allocation records, restore missing history where possible, and repeat verification. If absence is established but restoration is impossible, design an application-specific migration with a separately reviewed position/session-token mapping and complete model comparison. Leave the original store intact until that recovery is proven.

## Replay and cutover remain required

Alpha.19 encoded projection cursors in names and stored set-wide definition hashes. Alpha.20 binds explicit projection keys, per-member hashes, destinations/scopes, and lifetime IDs. This script cannot infer that metadata and does not copy old positions or hashes into active bindings.

Provision fresh projection destinations and let the alpha.20 application establish bindings and replay complete available histories (including archived payloads). Reusing old positioned rows can suppress events that need correction. Elasticsearch mapping corrections require new indexes; SQL cannot perform that change. Ordinary SQL Inline/Async tables are unchanged; TransactionalInline is opt-in and uses dedicated tables provisioned separately.

Verify source counts/fingerprints, all member checkpoints against the captured source head, every rebuilt model against an independent replay, and every retained document ID before switching readers and writers together. Investigate missing/orphaned IDs and deletion semantics. Hosts must agree on definitions, revisions, source identity and destination configuration, including Elasticsearch node lists. Only resume traffic after successful verification.

## Rollback

Do not run alpha.19 against the new checkpoint schema. Before processing resumes, a reviewed rollback can archive the new tables and restore the old checkpoint name while all hosts are stopped. After new processing/events, names alone do not restore consistent progress: retain both generations and use a coherent backup or verified rebuild. Never delete events or projections as a rollback shortcut.

## Tests and release scope

```powershell
./eng/migrations/alpha19-to-alpha20/Test-Upgrade.ps1 -Container <disposable-sql-container>
```

The container must expose `MSSQL_SA_PASSWORD` and `/opt/mssql-tools18/bin/sqlcmd`. The test creates uniquely named scratch databases and removes only those. It uses the actual tagged alpha.19 checkpoint DDL and compares migration output with the candidate's shipped schema. It exercises populated/empty sources, gap/allocator failures, late-failure rollback, preserved rows/timestamps/grants, binding CAS, reader foreign keys, constraints, reruns and schema drift. Source-position fixtures deliberately permit malformed legacy states so refusal is tested independently of current constraints.

The baseline is `v10.0.0-alpha.19`; the target is the published alpha.20 checkpoint schema. Its DDL matches source commit `44c28f4ef8aead059d8d656c0b6bc3bc99d24fa3` in the `TrigintaFaces/Excalibur` release repository, as recorded by the published NuGet package. TransactionalInline starts in alpha.21 and is not part of the alpha.20 schema upgrade. Passing these tests does not replace a rehearsal on the production backup. This tooling does not publish or deploy anything.
