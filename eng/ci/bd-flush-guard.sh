#!/usr/bin/env bash
# bd-flush-guard.sh — durable, data-loss-safe Beads tracker flush for the integration-commit path.
#
# Implements .claude/rules/process/bd-flush-then-commit.md (esp. clause 6) and the exit-handling
# discipline of no-pipe-masked-commit.md. Replaces the manual, flaky `bd sync --flush-only` step that
# taxed every sprint close (recurring S842 -> S848): the daemon auto-flush races the tracked .jsonl
# mtime, so `bd sync --flush-only` either refuses ("JSONL is newer than database — import first") or
# returns exit 0 while writing NOTHING (a fresh close stays un-persisted in the committed HEAD).
#
# Design (run -> read -> verify, never a blind import):
#   1. Produce the AUTHORITATIVE DB->jsonl dump via `bd export --no-auto-import` (bounded-retry on
#      transient daemon contention). `--no-auto-import` is load-bearing: it stops bd from first
#      importing the newer-mtime on-disk .jsonl into the DB (which would REVERT fresh closes).
#   2. FOREIGN-CHANGE GUARD (data-loss guard, EC-I1.1): if the on-disk .jsonl contains any issue the
#      DB lacks, or a NEWER updated_at than the DB for a shared issue, the on-disk file holds changes
#      the export would destroy (e.g. a `git pull` not yet imported). STOP LOUD (exit 2) — never export
#      over it. The decision rule: jsonl strictly-older/subset => export; jsonl ahead => reconcile.
#   3. If on-disk already equals the DB dump => no-op clean (exit 0).
#   4. Otherwise atomically replace the on-disk .jsonl with the authoritative DB dump (exit 0).
#   5. A genuine (non-transient) failure after the bounded retries fails LOUD with a non-zero exit and
#      NEVER silently proceeds — shipping an un-flushed/desynced tracker is worse than aborting.
#
# Exit codes: 0 = flushed (or already in sync / staged verified); 1 = genuine flush failure (diagnose);
#             2 = on-disk .jsonl is AHEAD of the DB (foreign change) — reconcile first;
#             3 = (--verify-staged only) the STAGED .jsonl is STALE vs the DB (missing fresh closes).
#
# Modes:
#   (default)        flush the working-tree tracker to reflect the DB (steps 1-4 below).
#   --verify-staged  forge clause-6 / bd-m3dmht backstop: assert the *staged* blob (what `git commit`
#                    will snapshot — `git show :<jsonl>`) reflects the DB, so a PRE-close snapshot that
#                    slipped past `git add` cannot be committed. Read-only; never writes the tracker.
#
# Knobs (env): BD_FLUSH_ATTEMPTS (default 3), BD_FLUSH_BACKOFF seconds (default 2),
#              BD_JSONL_PATH (default .beads/issues.jsonl), BD_BIN (default bd), PYTHON_BIN (default python3).
# The BD_BIN / PYTHON_BIN overrides exist so a regression test can inject a fake `bd` to exercise the
# transient-clears-within-bound and genuine-fails-loud paths non-vacuously.

set -u

JSONL="${BD_JSONL_PATH:-.beads/issues.jsonl}"
ATTEMPTS="${BD_FLUSH_ATTEMPTS:-3}"
BACKOFF="${BD_FLUSH_BACKOFF:-2}"
BD="${BD_BIN:-bd}"
PYTHON="${PYTHON_BIN:-python3}"

log() { printf '[bd-flush-guard] %s\n' "$*" >&2; }

db_dump=""
cleanup() { [ -n "$db_dump" ] && rm -f "$db_dump" 2>/dev/null || true; }
trap cleanup EXIT

db_dump="$(mktemp 2>/dev/null || echo "${TMPDIR:-/tmp}/bd-flush-guard.$$.jsonl")"

# produce_db_dump — authoritative DB -> "$db_dump", bounded-retry on transient daemon contention.
# Captures the export exit status DIRECTLY (no pipe to head/tail that would mask it —
# no-pipe-masked-commit). Returns 0 on success, 1 on genuine failure after the bounded retries.
produce_db_dump() {
    local attempt=1 export_exit
    while [ "$attempt" -le "$ATTEMPTS" ]; do
        "$BD" export --no-auto-import -o "$db_dump"
        export_exit=$?
        [ "$export_exit" -eq 0 ] && return 0
        log "export attempt $attempt/$ATTEMPTS failed (exit $export_exit; daemon contention?); retrying..."
        attempt=$((attempt + 1))
        [ "$attempt" -le "$ATTEMPTS" ] && sleep "$BACKOFF"
    done
    return 1
}

# content_staleness_check <db-dump> <candidate> — exit 3 if <candidate> is STALE vs <db-dump>
# (any DB issue missing from the candidate, or an OLDER updated_at for a shared issue). Content-based
# (parses both, compares id->updated_at) so it is robust to EOL/byte normalization of the staged blob.
content_staleness_check() {
    "$PYTHON" - "$1" "$2" <<'PYEOF'
import json, sys
from datetime import datetime, timezone

def load(path):
    out = {}
    try:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                obj = json.loads(line)
                iid = obj.get("id")
                if iid is not None:
                    out[iid] = obj
    except FileNotFoundError:
        pass
    return out

def parse_ts(value):
    if not value:
        return None
    try:
        dt = datetime.fromisoformat(str(value))
        return dt.replace(tzinfo=timezone.utc) if dt.tzinfo is None else dt
    except ValueError:
        return None

db = load(sys.argv[1])
cand = load(sys.argv[2])

stale = []
for iid, dobj in db.items():
    if iid not in cand:
        stale.append(f"  {iid}: in the DB but MISSING from the staged .jsonl (un-staged fresh close)")
        continue
    bts = parse_ts(dobj.get("updated_at"))
    cts = parse_ts(cand[iid].get("updated_at"))
    if bts is not None and cts is not None and cts < bts:
        stale.append(
            f"  {iid}: staged updated_at {cand[iid].get('updated_at')} is OLDER than DB {dobj.get('updated_at')}"
        )

if stale:
    sys.stderr.write("staged .jsonl is STALE vs the DB:\n" + "\n".join(stale) + "\n")
    sys.exit(3)
sys.exit(0)
PYEOF
}

# ── Mode: --verify-staged (forge clause-6 / bd-m3dmht backstop) ──────────────────────────────────
# Assert the STAGED tracker blob reflects the DB; never writes. Makes "commit a stale tracker"
# structurally inexpressible even if a pre-close snapshot slipped past `git add`.
if [ "${1:-}" = "--verify-staged" ]; then
    if ! produce_db_dump; then
        log "ERROR: 'bd export --no-auto-import' failed after $ATTEMPTS attempts during --verify-staged —"
        log "       cannot establish the DB ground truth; aborting LOUD."
        exit 1
    fi
    staged_tmp="$(mktemp 2>/dev/null || echo "${TMPDIR:-/tmp}/bd-flush-guard-staged.$$.jsonl")"
    if ! git show ":$JSONL" > "$staged_tmp" 2>/dev/null; then
        rm -f "$staged_tmp"
        log "ERROR: '$JSONL' has no staged blob (not staged) — stage the flushed tracker first."
        exit 1
    fi
    content_staleness_check "$db_dump" "$staged_tmp"
    verify_exit=$?
    rm -f "$staged_tmp"
    if [ "$verify_exit" -eq 3 ]; then
        log "ERROR: STAGED '$JSONL' is STALE vs the DB (a fresh close is not in the staged blob)."
        log "       Re-run the flush + 'git add $JSONL', then retry the commit (forge clause 6)."
        exit 3
    elif [ "$verify_exit" -ne 0 ]; then
        log "ERROR: staged verification failed (exit $verify_exit) — aborting LOUD."
        exit 1
    fi
    log "STAGED '$JSONL' reflects the DB (--verify-staged OK)."
    exit 0
fi

# ── Step 1: authoritative DB -> temp dump, bounded-retry on transient contention ────────────────
if ! produce_db_dump; then
    log "ERROR: 'bd export --no-auto-import' failed after $ATTEMPTS attempts — likely a genuine failure"
    log "       (corrupt/locked DB). Tracker NOT flushed; aborting LOUD rather than ship a desynced tracker."
    exit 1
fi

# ── Step 2: foreign-change guard — never export over on-disk changes the DB lacks ────────────────
if [ -f "$JSONL" ]; then
    "$PYTHON" - "$db_dump" "$JSONL" <<'PYEOF'
import json, sys
from datetime import datetime, timezone

db_path, disk_path = sys.argv[1], sys.argv[2]

def load(path):
    out = {}
    try:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                obj = json.loads(line)
                iid = obj.get("id")
                if iid is not None:
                    out[iid] = obj
    except FileNotFoundError:
        pass
    return out

def parse_ts(value):
    if not value:
        return None
    try:
        dt = datetime.fromisoformat(str(value))
        if dt.tzinfo is None:
            dt = dt.replace(tzinfo=timezone.utc)
        return dt
    except ValueError:
        return None

db = load(db_path)
disk = load(disk_path)

foreign = []
for iid, dobj in disk.items():
    if iid not in db:
        foreign.append(f"  {iid}: present on-disk .jsonl but ABSENT from the DB (un-imported foreign change)")
        continue
    dts = parse_ts(dobj.get("updated_at"))
    bts = parse_ts(db[iid].get("updated_at"))
    if dts is not None and bts is not None and dts > bts:
        foreign.append(
            f"  {iid}: on-disk updated_at {dobj.get('updated_at')} is NEWER than DB {db[iid].get('updated_at')}"
        )

if foreign:
    sys.stderr.write(
        "on-disk .jsonl is AHEAD of the DB -- exporting would DESTROY these changes:\n"
        + "\n".join(foreign) + "\n"
    )
    sys.exit(2)
sys.exit(0)
PYEOF
    guard_exit=$?
    if [ "$guard_exit" -eq 2 ]; then
        log "ERROR: on-disk '$JSONL' holds changes the DB lacks (likely a pull not yet imported)."
        log "       Refusing to export (would revert them). Reconcile first (e.g. 'bd sync --import-only'), then re-run."
        exit 2
    elif [ "$guard_exit" -ne 0 ]; then
        log "ERROR: foreign-change verification failed (exit $guard_exit) — aborting LOUD without flushing."
        exit 1
    fi
fi

# ── Step 3: already in sync? no-op clean ────────────────────────────────────────────────────────
if [ -f "$JSONL" ] && cmp -s "$db_dump" "$JSONL"; then
    log "tracker already in sync with the DB (no-op)."
    exit 0
fi

# ── Step 4: atomically replace the on-disk tracker with the authoritative DB dump ────────────────
if ! mv -f "$db_dump" "$JSONL"; then
    log "ERROR: failed to write authoritative dump to '$JSONL' — aborting LOUD."
    exit 1
fi
db_dump=""   # consumed by mv; nothing to clean
log "tracker flushed: '$JSONL' now reflects the DB (export --no-auto-import, foreign-change-guarded)."
exit 0
