#!/usr/bin/env bash
# Serialize builds that share one working tree.
#
# Concurrent builds of different projects in the same tree race each other's
# obj/ and bin/ directories. The symptom is not a clean failure: it surfaces as
# MSB3030 / CS0006 / CS0579 cascades that name a file the caller never touched,
# so the caller diagnoses a defect that is not there. Worse, an assembly can
# simply vanish from a run with no "Failed!" line, because the build that would
# have produced it died on a locked output.
#
# This wrapper takes an exclusive lock for the duration of one build command.
# mkdir is atomic on every POSIX filesystem, so it is a correct mutex; a plain
# "test -e then create" is not.
#
# Usage:  eng/ci/build-lock.sh <command> [args...]
#   eng/ci/build-lock.sh ./.dotnet/dotnet build src/Foo/Foo.csproj -c Release
#
# Environment:
#   BUILD_LOCK_TIMEOUT   seconds to wait for the lock (default 1800)
#   BUILD_LOCK_DIR       lock location (default .build-lock at the repo root)
#   BUILD_LOCK_STALE_AFTER  seconds after which a lock is presumed leaked and is
#                        reclaimed regardless of what the pid check says (default 3600)
#
# Exit code is the wrapped command's own, unchanged. That matters: this script
# is often the last statement before a caller captures $?, and a wrapper that
# substituted its own exit would hide exactly the failures it exists to expose.
set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
lock_dir="${BUILD_LOCK_DIR:-$repo_root/.build-lock}"
timeout_s="${BUILD_LOCK_TIMEOUT:-1800}"
# 1200s, not an hour. See the AGE FALLBACK note below: on this platform the pid
# liveness check is not merely imperfect, it is structurally unreliable, so this is
# the PRIMARY reclaim mechanism rather than a backstop -- and an hour of blocking
# every build is too high a price for a lock nobody holds. A full solution build
# here measures ~10 minutes, so 20 gives real headroom without the wait.
stale_after_s="${BUILD_LOCK_STALE_AFTER:-1200}"

if [ "$#" -eq 0 ]; then
    echo "build-lock: no command given" >&2
    exit 2
fi

waited=0
until mkdir "$lock_dir" 2>/dev/null; do
    # Reclaim a lock whose owner is gone. Without this a killed build wedges
    # every later one, which is a worse failure than the contention.
    if [ -f "$lock_dir/pid" ]; then
        owner="$(cat "$lock_dir/pid" 2>/dev/null || true)"
        if [ -n "$owner" ] && ! kill -0 "$owner" 2>/dev/null; then
            echo "build-lock: reclaiming lock from dead process $owner" >&2
            rm -rf "$lock_dir"
            continue
        fi
    fi

    # AGE FALLBACK -- and on this platform it is the PRIMARY mechanism, not a backstop.
    #
    # MEASURED, because the earlier version of this comment understated it. A lock left
    # by a dead owner was NOT reclaimed by the check above:
    #
    #     lock owner pid 41055
    #     bash  kill -0 41055   -> SUCCEEDS   (reports ALIVE)
    #     pwsh  Get-Process 41055 -> GONE
    #
    # and the reason generalises: git-bash pids and Windows pids are DISJOINT namespaces.
    # This shell'"'"'s own $$ was 53539, and Get-Process reported 53539 GONE too. So `kill -0`
    # is not answering "is the process that took this lock alive" -- it is answering a
    # question about a different namespace, and on a stale pid it can answer yes forever.
    #
    # The check is kept because when it DOES report dead the answer is trustworthy and the
    # reclaim is immediate. But it cannot be relied on, so the age bound below is what
    # actually guarantees a leaked lock clears. The original reasons still apply too:
    #
    #   1. The EXIT/INT/TERM trap does not run if the holder is SIGKILLed, so the lock
    #      outlives it.
    #   2. `kill -0` asks "is SOME process alive with this pid", not "is the process
    #      that took this lock alive". After the holder dies its pid can be reused, and
    #      then the check says live forever and the lock never reclaims. Under git-bash
    #      there is the further wrinkle that a bash pid and a Windows pid are different
    #      namespaces, so a human checking with Get-Process and the script checking with
    #      kill -0 can honestly disagree.
    #
    # Either way the failure mode is the same and it is the worst one: a permanent
    # block on a lock nobody holds. So bound it by age. The threshold is deliberately
    # far longer than any real build here (a full solution build measures ~10 min), so
    # this cannot steal a live lock from a slow build -- it only rescues a leaked one.
    if [ -d "$lock_dir" ]; then
        lock_age="$(( $(date +%s) - $(stat -c %Y "$lock_dir" 2>/dev/null || date +%s) ))"
        if [ "$lock_age" -ge "$stale_after_s" ]; then
            echo "build-lock: lock is ${lock_age}s old (>= ${stale_after_s}s) and is presumed LEAKED" >&2
            echo "build-lock: reclaiming. If a build really has run this long, it is the bug." >&2
            rm -rf "$lock_dir"
            continue
        fi
    fi
    if [ "$waited" -ge "$timeout_s" ]; then
        echo "build-lock: timed out after ${timeout_s}s waiting for $lock_dir" >&2
        echo "build-lock: NOTHING WAS BUILT. This is not a build failure." >&2
        exit 2
    fi
    sleep 2
    waited=$((waited + 2))
done

echo "$$" > "$lock_dir/pid"
trap 'rm -rf "$lock_dir"' EXIT INT TERM

"$@"
BUILD_EXIT=$?

exit "$BUILD_EXIT"
