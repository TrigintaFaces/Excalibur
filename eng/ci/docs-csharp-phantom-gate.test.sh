#!/usr/bin/env bash
# docs-csharp-phantom-gate.test.sh — NON-VACUOUS self-test for the diff-scoped gate.
#
# Proves every arm against a throwaway fixture repo (minimal public surface = IDispatcher):
#   SAFETY      — a CHANGED doc with a new phantom API -> gate exits 1 AND names it.
#   LIVENESS    — a CHANGED doc using only a real API   -> gate exits 0 (does not over-fire).
#   DIFF-SCOPE  — a phantom in an UNCHANGED doc          -> gate exits 0 (tolerated; the point).
#   IGNORE      — a CHANGED doc whose phantom sits in a ```csharp ignore block -> exit 0.
#   NO-OP       — no changed DOC files (only src changed) -> exit 0 ("nothing to gate").
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="$SCRIPT_DIR/docs-csharp-phantom-gate.sh"

FIX="$(mktemp -d)"
trap 'rm -rf "$FIX"' EXIT
REPO="$FIX/repo"

fail() { echo "SELF-TEST FAIL: $1" >&2; exit 1; }

# --- fixture repo: minimal real public surface (IDispatcher is REAL) -----------------
mkdir -p "$REPO/src/Fake" "$REPO/docs"
cat > "$REPO/src/Fake/PublicAPI.Shipped.txt" <<'EOF'
Excalibur.Dispatch.IDispatcher
EOF

# CHANGED-clean: real API only -> must NOT flag (liveness)
cat > "$REPO/docs/clean.md" <<'EOF'
# Clean
```csharp
using Excalibur.Dispatch;
public class Demo { public Demo(IDispatcher dispatcher) { } }
```
EOF

# CHANGED-phantom: a fake framework type -> MUST flag (safety)
cat > "$REPO/docs/phantom.md" <<'EOF'
# Phantom
```csharp
using Excalibur.Dispatch;
public class Broken { public void Run() { var x = new FakePhantomTypeXyz(); } }
```
EOF

# CHANGED-ignore: same phantom, but the fence opts out -> must NOT flag
cat > "$REPO/docs/ignored.md" <<'EOF'
# Ignored placeholder
```csharp ignore
using Excalibur.Dispatch;
public class Broken { public void Run() { var x = new FakePhantomTypeXyz(); } }
```
EOF

# UNCHANGED-phantom: a phantom that exists in the repo but is NOT in the changed set
cat > "$REPO/docs/preexisting.md" <<'EOF'
# Pre-existing
```csharp
using Excalibur.Dispatch;
public class Old { public void Run() { var y = new AnotherPreexistingPhantomAbc(); } }
```
EOF

run_gate() { # $1 = changed-file list
    DOCS_GATE_REPO="$REPO" DOCS_GATE_CHANGED_FILES="$1" bash "$GATE" 2>&1
}

echo "=== ARM 1 (SAFETY): changed phantom doc -> exit 1 + names the phantom ==="
OUT="$(run_gate "docs/phantom.md")"; RC=$?
echo "$OUT"
[ "$RC" -eq 1 ] || fail "safety: expected exit 1, got $RC"
echo "$OUT" | grep -q "FakePhantomTypeXyz" || fail "safety: phantom name not reported"

echo
echo "=== ARM 2 (LIVENESS): changed clean doc -> exit 0 ==="
OUT="$(run_gate "docs/clean.md")"; RC=$?
echo "$OUT"
[ "$RC" -eq 0 ] || fail "liveness: expected exit 0, got $RC (over-firing on a real API?)"

echo
echo "=== ARM 3 (DIFF-SCOPE): phantom exists only in an UNCHANGED doc -> exit 0 ==="
OUT="$(run_gate "docs/clean.md")"; RC=$?
echo "$OUT"
[ "$RC" -eq 0 ] || fail "diff-scope: a pre-existing phantom in an unchanged file must be tolerated"
echo "$OUT" | grep -q "AnotherPreexistingPhantomAbc" && fail "diff-scope: reported a phantom from an UNCHANGED file"

echo
echo '=== ARM 4 (IGNORE): changed doc with a "csharp ignore" fence phantom -> exit 0 ==='
OUT="$(run_gate "docs/ignored.md")"; RC=$?
echo "$OUT"
[ "$RC" -eq 0 ] || fail "ignore: an opted-out placeholder must not fire"
echo "$OUT" | grep -q "FakePhantomTypeXyz" && fail "ignore: reported a phantom from an ignore-marked block"

echo
echo "=== ARM 5 (NO-OP): only a non-doc file changed -> exit 0 (nothing to gate) ==="
OUT="$(run_gate "src/Foo.cs")"; RC=$?
echo "$OUT"
[ "$RC" -eq 0 ] || fail "no-op: non-doc changes should gate nothing"
echo "$OUT" | grep -qi "nothing to gate" || fail "no-op: expected the 'nothing to gate' PASS message"

run_gate_lines() { # $1 = path:line changed-line list
    DOCS_GATE_REPO="$REPO" DOCS_GATE_CHANGED_LINES="$1" bash "$GATE" 2>&1
}

# phantom.md fixture: line 1 heading, lines 2-4 the ```csharp block (phantom on line 3).
echo
echo "=== ARM 6 (HUNK-SAFETY): a changed line INSIDE the phantom block -> exit 1 ==="
OUT="$(run_gate_lines "docs/phantom.md:3")"; RC=$?
echo "$OUT"
[ "$RC" -eq 1 ] || fail "hunk-safety: a phantom on a changed line must fire (got $RC)"
echo "$OUT" | grep -q "FakePhantomTypeXyz" || fail "hunk-safety: phantom name not reported"

echo
echo "=== ARM 7 (HUNK-SCOPE): a changed line OUTSIDE the block (edit an unrelated line) -> exit 0 ==="
OUT="$(run_gate_lines "docs/phantom.md:1")"; RC=$?
echo "$OUT"
[ "$RC" -eq 0 ] || fail "hunk-scope: editing a line outside a pre-existing phantom block must NOT fire (got $RC)"
echo "$OUT" | grep -q "FakePhantomTypeXyz" && fail "hunk-scope: reported a phantom the diff never touched"

echo
echo "SELF-TEST PASS: safety + liveness + diff-scope + ignore + no-op + hunk arms all green."
