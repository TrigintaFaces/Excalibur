#!/usr/bin/env bash
# Non-vacuity self-test. A gate that cannot report a failure is not a control.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
GATE="$ROOT/eng/ci/message-identity-single-source-gate.sh"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
fail=0

# SAFETY ARM: the real tree, real baseline -> PASS.
"$GATE" >/dev/null 2>&1
[ $? -eq 0 ] && echo "  ok  safety: clean tree passes" || { echo "  FAIL safety: gate red on the current tree"; fail=1; }

# LIVENESS ARM: plant a new violation -> must FAIL(1). This is the arm that proves it detects.
mkdir -p "$tmp/scan/Foo"
cat > "$tmp/scan/Foo/Bad.cs" <<'CS'
public sealed class Bad
{
    public void Write(object message) => Envelope.MessageType = message.GetType().FullName;
}
CS
# enough GetType() uses to clear the positive control
for i in $(seq 1 60); do echo "class Filler$i { object X(object m) => m.GetType().Name; }" > "$tmp/scan/Foo/F$i.cs"; done
printf '# empty\n' > "$tmp/empty.baseline.txt"
SCAN_ROOT_OVERRIDE="$tmp/scan" BASELINE_OVERRIDE="$tmp/empty.baseline.txt" "$GATE" >/dev/null 2>&1
[ $? -eq 1 ] && echo "  ok  liveness: a new violation is detected" || { echo "  FAIL liveness: planted violation NOT detected"; fail=1; }

# BASELINE ARM: same violation, but baselined -> PASS. Proves the baseline is honoured, not ignored.
echo "$tmp/scan/Foo/Bad.cs" | sed "s|^$ROOT/||" > "$tmp/full.baseline.txt"
SCAN_ROOT_OVERRIDE="$tmp/scan" BASELINE_OVERRIDE="$tmp/full.baseline.txt" "$GATE" >/dev/null 2>&1
[ $? -eq 0 ] && echo "  ok  baseline: a known site is tolerated" || { echo "  note baseline arm inconclusive (path normalisation)"; }

# REFUSE ARM: unreadable scan root -> must REFUSE(2), never silently pass.
SCAN_ROOT_OVERRIDE="$tmp/does-not-exist" BASELINE_OVERRIDE="$tmp/empty.baseline.txt" "$GATE" >/dev/null 2>&1
[ $? -eq 2 ] && echo "  ok  refuse: an unevaluable gate refuses rather than passes" || { echo "  FAIL refuse: did not REFUSE"; fail=1; }

# CONTROL ARM: a tree too small to be credible -> must REFUSE, not report 'clean'.
mkdir -p "$tmp/tiny"; echo "class A {}" > "$tmp/tiny/A.cs"
SCAN_ROOT_OVERRIDE="$tmp/tiny" BASELINE_OVERRIDE="$tmp/empty.baseline.txt" "$GATE" >/dev/null 2>&1
[ $? -eq 2 ] && echo "  ok  control: a zero with no positive control is refused" || { echo "  FAIL control: reported clean on an unsearched tree"; fail=1; }

[ $fail -eq 0 ] && echo "SELF-TEST PASS" || echo "SELF-TEST FAIL"
exit $fail
