#!/usr/bin/env bash
# docs-csharp-extract.test.sh — NON-VACUOUS self-test for docs-csharp-extract.py.
#
# Proves both arms:
#   SAFETY   — a planted phantom API (using Excalibur.Dispatch; + a fake type) is
#              flagged: the gate exits non-zero AND prints the phantom name.
#   LIVENESS — a real API (IDispatcher, verified present in a PublicAPI*.txt) is
#              NOT flagged: the gate exits 0. (A detector that flags everything would
#              fail this arm — that is the point.)
#   CLASSIFY — `csharp runnable` => tier-2 (compile); plain `csharp` => tier-1 (resolve).
#
# The tool resolves the real public surface from the repo (--repo), while the doc scan
# is scoped to the throwaway fixture (--root), so the fixtures test the real symbol set.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$SCRIPT_DIR/../.." && pwd)"
TOOL="$SCRIPT_DIR/docs-csharp-extract.py"

FIX="$(mktemp -d)"
trap 'rm -rf "$FIX"' EXIT

fail() { echo "SELF-TEST FAIL: $1" >&2; exit 1; }

# --- fixture (a): REAL framework type -> must NOT be flagged (liveness) --------------
mkdir -p "$FIX/real/docs"
cat > "$FIX/real/docs/real.md" <<'EOF'
# Real
```csharp
using Excalibur.Dispatch;

public class Demo
{
    public Demo(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }
}
```
EOF

# --- fixture (b): PHANTOM framework type -> MUST be flagged (safety) -----------------
mkdir -p "$FIX/phantom/docs"
cat > "$FIX/phantom/docs/phantom.md" <<'EOF'
# Phantom
```csharp
using Excalibur.Dispatch;

public class Broken
{
    public void Run()
    {
        var svc = new ITotallyFakeDispatcherXyz();
        svc.DoThing();
    }
}
```
EOF

# --- fixture (c/d): classification -> runnable=tier-2, plain=tier-1 ------------------
mkdir -p "$FIX/classify/docs"
cat > "$FIX/classify/docs/classify.md" <<'EOF'
# Classify
```csharp runnable
using Excalibur.Dispatch;
public class T2 { }
```

```csharp
using Excalibur.Dispatch;
public class T1 { }
```
EOF

echo "=== ARM 1 (SAFETY): phantom fixture must exit 1 + name the phantom ==="
OUT_P="$(python3 "$TOOL" --root "$FIX/phantom" --repo "$REPO" 2>&1)"; RC_P=$?
echo "$OUT_P"
[ "$RC_P" -eq 1 ] || fail "phantom fixture expected exit 1, got $RC_P"
echo "$OUT_P" | grep -q "ITotallyFakeDispatcherXyz" \
    || fail "phantom name not present in output"
echo "$OUT_P" | grep -q "1 phantom(s)" || fail "expected exactly 1 phantom in summary"

echo
echo "=== ARM 2 (LIVENESS): real fixture must exit 0 (not flag IDispatcher) ==="
OUT_R="$(python3 "$TOOL" --root "$FIX/real" --repo "$REPO" 2>&1)"; RC_R=$?
echo "$OUT_R"
[ "$RC_R" -eq 0 ] || fail "real fixture expected exit 0, got $RC_R (over-flagging?)"
echo "$OUT_R" | grep -q "0 phantom(s)" || fail "expected 0 phantoms on real fixture"

echo
echo "=== ARM 3 (CLASSIFY): runnable=tier-2 compile, plain=tier-1 resolve (--json) ==="
OUT_J="$(python3 "$TOOL" --root "$FIX/classify" --repo "$REPO" --json 2>&1)"; RC_J=$?
echo "$OUT_J"
[ "$RC_J" -eq 0 ] || fail "--json expected exit 0, got $RC_J"
python3 - "$OUT_J" <<'PY' || fail "classification mismatch"
import json, sys
recs = json.loads(sys.argv[1])
tiers = sorted(r["tier"] for r in recs)
assert tiers == ["compile", "resolve"], f"expected [compile, resolve], got {tiers}"
assert all(r["lang"] == "csharp" for r in recs)
assert all({"file", "startLine", "lang", "tier", "code"} <= set(r) for r in recs), "record schema drift"
print("classification OK:", tiers)
PY

echo
echo '=== ARM 4 (IGNORE): a "csharp ignore" fence block is excluded from gating ==='
mkdir -p "$FIX/ignore/docs"
cat > "$FIX/ignore/docs/ignore.md" <<'EOF'
# Ignore
```csharp ignore
using Excalibur.Dispatch;
public class Broken { public void Run() { var x = new ITotallyFakeDispatcherXyz(); } }
```
EOF
OUT_I="$(python3 "$TOOL" --root "$FIX/ignore" --repo "$REPO" 2>&1)"; RC_I=$?
echo "$OUT_I"
[ "$RC_I" -eq 0 ] || fail "ignore-marked phantom expected exit 0, got $RC_I"
echo "$OUT_I" | grep -q "ITotallyFakeDispatcherXyz" && fail "ignore-marked phantom must NOT be reported"
echo "$OUT_I" | grep -q "1 ignored" || fail "expected the ignored block counted in the summary"

echo
echo "SELF-TEST PASS: safety + liveness + classification + ignore arms all green."
