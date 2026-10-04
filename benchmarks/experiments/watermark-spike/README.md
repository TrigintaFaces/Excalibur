# Watermark spike experiments

Research evidence for `Excalibur_Dispatch-257j9n.10.18`, including protocol experiments and a local
feed comparison. This is not a shipping provider or a production-capacity benchmark.
Run only against a fresh disposable engine. The PostgreSQL script creates its own schema and uses
three `dblink` sessions to deterministically order database operations, without timing sleeps.
It stops on any assertion failure. It demonstrates stale-view omission, a rollback gap and cached
causal inversion, with a fresh-view positive control. A pass means the counterexample occurred.

Create a unique disposable container with no published ports or pre-existing data volumes, mounting this
directory read-only at `/experiment`. Use the locally inspected PostgreSQL image ID, set
`POSTGRES_USER=spike` and `POSTGRES_HOST_AUTH_METHOD=trust`, and disable container networking.
Trust authentication is confined to this isolated container; never apply it to a deployed database.
After `pg_isready -U spike -d postgres` succeeds, execute:

```text
docker exec <container> psql -X -U spike -d postgres -f /experiment/postgres-counterexamples.sql
```

Retain stdout, stderr, exit code, image ID, server settings, source hashes and repository revision
under the run's evidence directory. Remove only the uniquely named container created for that run.
Re-execution requires a fresh database: existing objects cause failure rather than hiding state.

`postgres-logical-feed.sql` uses the same PostgreSQL isolation and mount setup with the additional
server argument `-c wal_level=logical`. Run it with the same `psql` command, replacing the filename.
Its four assertions verify uncommitted-transaction exclusion, commit order differing from sequence
allocation order, rollback exclusion and healthy-session consumption progress. It uses the bundled
`test_decoding` plugin, not a framework adapter. Temporary evidence tables can introduce unrelated
transaction markers, so assertions explicitly scope data rows to `public.ledger`. Retain the full
trace: row-change LSNs need not increase in decoded commit order and are not safe standalone cursors.

The three `postgres-crash-*.sql` scripts exercise a real process crash. Start a fresh isolated
instance with `wal_level=logical` and `checkpoint_timeout=1h`; run setup, then start the writer with
`PGAPPNAME=watermark-crash-writer` in a separate session. Before crashing, require exactly one
matching `pg_stat_activity` row with `state=active`, `wait_event=PgSleep` and a non-null transaction
start, and retain its query plus writer output showing `INSERT 0 1`. This proves the writer passed
its insert and remains inside the transaction. Send SIGKILL only to this disposable container,
retain its terminal state, restart the same instance and run the verify script after readiness.
Keep any replay count in the evidence: the test permits and reports a previously consumed event
being returned after recovery. It requires the in-flight event to disappear and the new committed
event to appear. This is a single-node crash experiment, not failover qualification.

PostgreSQL images can create an anonymous data volume automatically. Retain its mount identity
with the run evidence and remove it using `docker rm -v` on the exact disposable container after
verification. Do not remove unrelated volumes or reuse a deployed database for these experiments.

`sqlserver-rowversion.sh` runs three assertions in a fresh disposable SQL Server container with
`/opt/mssql-tools18/bin/sqlcmd`. Mount this directory read-only at `/experiment`, disable networking
and publish no ports, and supply a generated throwaway `MSSQL_SA_PASSWORD`. After SQL Server reports
recovery complete and a loopback login succeeds, execute:

```text
docker exec <container> bash /experiment/sqlserver-rowversion.sh
```

The optional `kill` argument terminates the uniquely named experiment writer through SQL Server
after proving that its event remains uncommitted. It asserts that the client fails, the event is
rolled back and the boundary advances past the retired gap to the remaining committed event.
Run `commit` (the default) and `kill` against separate fresh databases. This is session termination,
not database crash, failover or an application-lease timeout simulation.

The script creates `WatermarkSpike`, gates an uncommitted writer with an explicit release row,
contrasts `@@DBTS` with `MIN_ACTIVE_ROWVERSION`, then demonstrates that updating an event changes
its rowversion. A dirty read is used only for the injected-writer readiness barrier; feed assertions
use committed RCSI reads. There is a bounded failure deadline. Existing databases cause failure.
Retain the same provenance and raw evidence as for PostgreSQL; destroy only the disposable container.

See the [decision and evidence](../../../management/specs/arb-2026-10-02-watermark-spike.md)
for the invariant, separate safety/liveness obligations and completed local comparison.
The 2026-10-03 decision retains the gapless production default. The sparse SQL protocol is feasible
within its tested scope, but the positive replacement criteria are not established and lower-load
five-event batches exceeded the approved p99 regression tolerance. No production replacement is qualified.

### SQL Server resolved-feed prototype

Select `-Cases sql-feed` in the runner below to execute `sqlserver-resolved-feed.sql` in a fresh
isolated database. This additional case is opt-in; the original six-case default is unchanged.
The experiment separates an immutable rowversion ordering ledger from mutable payloads, commits
events and outbox rows together, persists an exclusive stable boundary, and applies ordered pages
with projection effects and checkpoint in one transaction. Checkpoint locking precedes cursor
reading and page selection. Its cursor carries both position and an inclusive/exclusive flag:
after an exhausted interval the next event can occupy exactly the saved boundary.

Assertions exercise rollback holes, a failed effects/checkpoint transaction, bounded pages,
per-event replay classification against a fresh head while publication lags, noncommutative
projection effects, erasure without moving positions, unresolved payload refusal, empty-interval
progress and stale-incarnation rejection. No signed conversion or rowversion arithmetic is used.

The shell harness also gates two contending readers before checkpoint acquisition, races an older
publisher against newer publication, and pauses a reader while its captured boundary becomes old.
Both readers must succeed. A separate rowversion transaction demonstrates database-wide blocking
and subsequent recovery. The runner kills the database process with an actual incomplete append,
then checks publication, replay head, effects and checkpoint after restart and verifies new progress.

This is a single-source, single-tenant, single-event-append protocol experiment. Its fresh RCSI
scans use the authoritative database. It does not qualify general consumer failover,
incarnation reset/restore fencing, ordered multi-event allocation, real
network faults, framework adapters, performance, or concurrent erasure fencing. The database-wide
stable boundary can be held by unrelated transactions using rowversion; progress requires those
transactions to resolve. Ledger rows cannot be updated or deleted within an incarnation.

### Feed comparison instrument

The benchmark executable's `feed-spike` route compares a dense counter kernel with an immutable
rowversion ledger and durable publisher. Use `run-feed-comparison.ps1` with a compiled benchmark
application, a new evidence directory and a JSON plan containing cells with `id`, `arm`, `writers`,
`batch`, `rate` and `requests`. Arms are `gapless` and `watermark`. The runner creates and removes
only its labelled disposable SQL Server, client and private network. Existing evidence is preserved.

The workload appends new independent streams and includes outbox writes plus ordered projection
effects/checkpoints. Timing begins at scheduled arrival and ends after SQL commits the whole batch's
projection effects. Every offered request is retained, including admission failures and incomplete work.
Warmup runs are separate records. Both exit code zero and `complete: true` are required for a cell
to pass; `runnerCompleted` only means the frozen plan ran and cleanup completed. Optional `fault`
cells (`payload`, `counter`, `projection`, `after-drain`, `late-append`, `late-apply`) are deliberately failing instrument controls,
never performance data. See the assessment for the frozen screen and precise scope limitations.

### Repeatable isolated runner

Run `pwsh -File benchmarks/experiments/watermark-spike/run-engine-checks.ps1 -EvidenceDirectory <new-absolute-directory>`
from the repository root to execute all six cases. The required local Docker image IDs are pinned
in the runner. `-Repetitions` accepts 1 through 30; `-Cases` selects named cases when invoked from
PowerShell. Each case gets a new isolated container with no published ports or pre-existing data
mounts. Evidence includes normalized executed inputs, hashes, raw step outputs and a final summary.
Only a summary with `success: true`, the expected case count and all cases passed certifies that run.
The runner verifies container ownership before removing its container and anonymous volumes.
Failed or incomplete runs must be retained and are not passes. The crash cases inject process kills,
not host power loss. These checks establish selected engine behaviors, not framework correctness,
complete failure-window coverage or performance relative to the gapless implementation.

The four `sqlserver-crash-*.sql` scripts cover database-process crash recovery. In a fresh isolated
SQL Server instance, execute setup with `sqlcmd -b`, start writer in a separate session with
`-H watermark-crash-writer`, then execute barrier. Require barrier success before SIGKILL: it
asserts an open transaction in WAITFOR, the pending lower event and the separately committed higher
event. Restart that same disposable instance and execute verify after recovery. Retain all outputs,
terminal crash state, engine recovery log and script hashes. The test checks rollback, durable
survival, stable-boundary progress and a new post-restart append; it does not certify failover,
storage-device power loss, throughput or the framework adapter. Remove the exact disposable
container and its owned anonymous volumes only after evidence capture.
