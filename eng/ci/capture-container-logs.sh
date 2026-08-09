#!/usr/bin/env bash
# capture-container-logs.sh — keep the containers' own account of a failed integration run.
#
# WHY THIS EXISTS
#   A fixture that cannot start its container throws a clear, named error -- "Container startup
#   failed after N attempt(s) within a total budget of Xs" -- and that message says everything about
#   the FIXTURE and nothing about the CONTAINER. Whether the image never pulled, or the process
#   started and died, or it started fine and the readiness probe was looking at the wrong port, all
#   produce the same sentence.
#
#   Measured cost, 2026-08-08: twelve tests were lost to that message across SQL Server and Cosmos
#   fixtures. The diagnosis had to be rebuilt from step timings and a registry lookup, and the
#   conclusion -- a slow pull rather than a broken image -- was INFERRED rather than read. Nothing in
#   the run said what the containers themselves had done, because nothing had asked them.
#
#   This runs BEFORE the cleanup step deliberately. Cleanup exists to kill containers that survived a
#   crashed host, and it is the right thing to do; it also destroys the only evidence of why they
#   were unhealthy. Capture first, then clean.
#
# WHAT IT COLLECTS, for every container the daemon still knows about:
#   - the inventory (docker ps -a), so a container that is ABSENT is distinguishable from one that
#     failed -- those have different causes and the same symptom
#   - inspect state: exit code, OOM kill flag, error string, start/finish times
#   - the container's stdout/stderr, tail-bounded
#   - daemon-level context: disk, image list, docker info
#
# THIS IS DIAGNOSTIC AND NEVER FAILS THE JOB. It makes no claim and gates nothing; a run that has
# already failed must not be given a second, more confusing failure by the tool trying to explain the
# first. Every command is allowed to fail and says so in the output.
#
# Usage: capture-container-logs.sh [output-dir]        (default: container-diagnostics)

set -uo pipefail

OUT="${1:-container-diagnostics}"
TAIL_LINES="${CONTAINER_LOG_TAIL:-400}"

mkdir -p "$OUT" || { echo "capture-container-logs: cannot create $OUT" >&2; exit 0; }

if ! command -v docker >/dev/null 2>&1; then
    echo "capture-container-logs: docker is not on PATH; nothing to collect." | tee "$OUT/NO-DOCKER.txt"
    exit 0
fi

echo "capture-container-logs: collecting into $OUT"

# The inventory first. An EMPTY inventory is itself the finding when a fixture reported a startup
# failure: it means no container was ever created, which points at the image (pull, auth, wrong tag)
# rather than at the container's own behaviour.
docker ps -a --no-trunc \
    --format 'table {{.ID}}\t{{.Image}}\t{{.Status}}\t{{.Names}}' > "$OUT/00-inventory.txt" 2>&1 \
    || echo "(docker ps failed)" >> "$OUT/00-inventory.txt"

{
    echo "=== docker info ==="
    docker info 2>&1 || echo "(docker info failed)"
    echo
    echo "=== disk ==="
    df -h 2>&1 || echo "(df failed)"
    echo
    echo "=== images present ==="
    docker images --format '{{.Repository}}:{{.Tag}}\t{{.Size}}' 2>&1 || echo "(docker images failed)"
} > "$OUT/01-daemon.txt" 2>&1

ids="$(docker ps -a --format '{{.ID}}' 2>/dev/null)"
if [ -z "$ids" ]; then
    echo "capture-container-logs: NO containers exist. A fixture that reported a startup failure with"
    echo "  no container present did not get as far as creating one -- look at the image (pull time,"
    echo "  tag, registry auth), not at container behaviour."
    exit 0
fi

count=0
vanished=0
for id in $ids; do
    # A container can vanish between the listing and the inspect -- TestContainers' own reaper is
    # removing them concurrently. That is not a finding, and writing a file full of "no such object"
    # for it buries the real ones. Skip it and say how many were skipped at the end.
    if ! inspected="$(docker inspect --format '{{.Name}}' "$id" 2>/dev/null)"; then
        vanished=$((vanished + 1))
        continue
    fi
    # Trailing separators become underscores through the character filter, so trim them: a file
    # called `postgres-1_.txt` looks like a truncation bug in the tool that wrote it.
    count=$((count + 1))
    name="$(printf '%s' "$inspected" | tr -d '/' | tr -c 'A-Za-z0-9_.-' '_' | sed 's/_*$//')"
    [ -n "$name" ] || name="$id"
    file="$OUT/${name}"

    {
        echo "=== $id ($name) ==="
        docker inspect --format \
          'image={{.Config.Image}}
state={{.State.Status}}
exit={{.State.ExitCode}}
oom={{.State.OOMKilled}}
error={{.State.Error}}
started={{.State.StartedAt}}
finished={{.State.FinishedAt}}' "$id" 2>&1 || echo "(inspect failed)"
        echo
        echo "--- last $TAIL_LINES log lines ---"
        docker logs --tail "$TAIL_LINES" "$id" 2>&1 || echo "(docker logs failed for $id)"
    } > "${file}.txt" 2>&1
done

echo "capture-container-logs: captured $count container(s); $vanished vanished mid-capture."
# Always 0: this explains a failure, it does not add one.
exit 0
