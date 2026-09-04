#!/usr/bin/env bash
# shard-hang-timeout-gate.test.sh — non-vacuity proof for shard-hang-timeout-gate.sh.
#
# A gate that cannot FAIL is a success signal with no effect behind it, and a gate that cannot PASS gets
# disabled within a week. Both arms are asserted here (testing-patterns §3 safety + liveness):
#
#   SAFETY   an unbounded `dotnet test <shard>.slnf`            -> gate MUST exit 1
#   LIVENESS a bounded invocation                                -> gate MUST exit 0
#   LIVENESS a bounded invocation split across CONTINUATION lines-> gate MUST exit 0   (false-positive guard)
#   LIVENESS a file with no shard invocation at all              -> gate MUST exit 0
#   SAFETY   an empty scan set                                   -> gate MUST exit 2   (REFUSE != PASS)
#   LIVENESS an explicit pragma opt-out                          -> gate MUST exit 0
#
set -uo pipefail

GATE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/shard-hang-timeout-gate.sh"
[ -x "$GATE" ] || chmod +x "$GATE" 2>/dev/null

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

pass=0
fail=0

check() { # name expected_exit dir
    local name="$1" expected="$2" dir="$3" actual
    SCAN_ROOT="$dir" bash "$GATE" >/dev/null 2>&1
    actual=$?
    if [ "$actual" -eq "$expected" ]; then
        echo "  ok   $name (exit $actual)"
        pass=$((pass + 1))
    else
        echo "  FAIL $name — expected exit $expected, got $actual"
        fail=$((fail + 1))
    fi
}

echo "[shard-hang-timeout-gate.test] running..."

# --- SAFETY: the violation the gate exists to catch -------------------------------------------------
mkdir -p "$TMP/unbounded"
cat > "$TMP/unbounded/runner.sh" <<'EOF'
dotnet test eng/ci/shards/UnitTests-Transport.slnf -c Release --no-restore
EOF
check "SAFETY: unbounded shard invocation is REJECTED" 1 "$TMP/unbounded"

# --- LIVENESS: the correct thing still passes -------------------------------------------------------
mkdir -p "$TMP/bounded"
cat > "$TMP/bounded/runner.sh" <<'EOF'
dotnet test eng/ci/shards/UnitTests-Transport.slnf -c Release --blame-hang-timeout 10m
EOF
check "LIVENESS: bounded invocation is ACCEPTED" 0 "$TMP/bounded"

# --- LIVENESS: multi-line continuation must not read as a violation ---------------------------------
mkdir -p "$TMP/continued"
cat > "$TMP/continued/runner.sh" <<'EOF'
dotnet test eng/ci/shards/UnitTests-Transport.slnf -c Release --no-restore -v q \
  --blame-hang-timeout 10m \
  -- RunConfiguration.TestSessionTimeout=3600000
EOF
check "LIVENESS: continuation-split bounded invocation is ACCEPTED" 0 "$TMP/continued"

# --- LIVENESS: unrelated files are not swept in -----------------------------------------------------
mkdir -p "$TMP/unrelated"
cat > "$TMP/unrelated/build.sh" <<'EOF'
dotnet build Excalibur.sln -c Release --no-incremental
EOF
check "LIVENESS: file with no shard invocation is ACCEPTED" 0 "$TMP/unrelated"

# --- LIVENESS: PROSE mentioning the command is not an invocation ------------------------------------
# Without this the gate flags its own documentation and every explanation of the wedge, which is the
# fastest way to get a gate switched off.
mkdir -p "$TMP/prose"
cat > "$TMP/prose/notes.md" <<'EOF'
- Never run `dotnet test` at solution root — always shard `.slnf` files.
> A local `dotnet test .slnf` had no hang timeout and blocked indefinitely.
EOF
check "LIVENESS: prose MENTIONING the command is ACCEPTED" 0 "$TMP/prose"

# --- SAFETY: a markdown/table/YAML-decorated real command is still caught ----------------------------
mkdir -p "$TMP/decorated"
cat > "$TMP/decorated/doc.md" <<'EOF'
| Unit | Required | `dotnet test eng/ci/shards/UnitTests-Core.slnf -c Release --no-build` |
EOF
check "SAFETY: decorated (table-cell) unbounded command is REJECTED" 1 "$TMP/decorated"

# --- SAFETY: an empty scan must REFUSE, never silently pass -----------------------------------------
mkdir -p "$TMP/empty"
check "SAFETY: empty scan set REFUSES (exit 2, not a free pass)" 2 "$TMP/empty"

# --- LIVENESS: the documented opt-out actually works ------------------------------------------------
mkdir -p "$TMP/pragma"
cat > "$TMP/pragma/runner.sh" <<'EOF'
dotnet test eng/ci/shards/UnitTests-Transport.slnf -c Release  # pragma: no-hang-timeout measuring the wedge itself
EOF
check "LIVENESS: explicit pragma opt-out is ACCEPTED" 0 "$TMP/pragma"

echo "[shard-hang-timeout-gate.test] $pass passed, $fail failed"
[ "$fail" -eq 0 ] || exit 1
exit 0
