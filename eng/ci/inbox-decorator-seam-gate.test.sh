#!/usr/bin/env bash
# inbox-decorator-seam-gate.test.sh — regression lock for eng/ci/inbox-decorator-seam-gate.sh.
#
# The gate's thesis: an inbox DECORATOR that declares ITransactionalInboxStore and omits
# IScopedTransactionalInboxStore hides the middleware's highest-precedence exactly-once path, because that
# path is selected by a type test on the OUTERMOST store instance. Wrapping a document-store inbox in
# encryption or telemetry then silently downgrades the consumer's atomicity guarantee with no error and no
# log line. This lock is the gate's WIRED control: it proves the gate produces all THREE verdicts on
# inputs it controls, FIRES on the REAL pre-fix decorator shape, and is SILENT on the fixed one.
#
# Behavioral 3-state (hermetic, via INBOXSEAM_ROOTS override):
#   A  planted defect (decorator, relational seam, no scoped seam) -> gate exit 1  (FAIL)
#   B  fixed shape (decorator forwarding both seams)               -> gate exit 0  (PASS)
#   C  empty scan root                                             -> gate exit 2  (REFUSE, never a pass)
#   D  a tree with zero decorators                                 -> gate exit 2  (REFUSE, never a pass)
#   A/B are a safety+liveness pair: flag-everything fails B, flag-nothing fails A. D is what stops a
#   renamed interface or a moved directory from degrading the gate into a silent no-op.
#
# Real-defect control (non-vacuity against reality, not only a synthetic mutant):
#   E  a decorator source carrying the pre-fix base list -> gate FAIL(1), naming the decorator
#   F  the same source with the scoped seam added        -> gate PASS(0)
#
# Scope control (the reason this gate can ship at all):
#   G  a PROVIDER implementing the relational seam and wrapping no IInboxStore -> PASS(0), uncounted.
#      Without this the gate would assert a design decision about providers it has no standing to make.
#
# Blindness controls (a scan that cannot see its subject reports a confident, wrong zero):
#   H  a C# 12 primary-constructor decorator is seen and FIRES
#   I  an interface named only in a comment is NOT read as a base list
#
# Static guards (grep the gate source — the seam guards must not regress):
#   J  3-state exit contract present (E_PASS=0 / E_FAIL=1 / E_REFUSE=2)
#   K  testability seam present (INBOXSEAM_ROOTS override)
#   L  non-vacuity floor present (INBOXSEAM_MIN_DECOS) + NO suppression cap / allowlist in code
#   M  no internal tracker/sprint/ADR ids in the gate source (public-surface hygiene — eng/ is mirrored)
#
# Run: bash eng/ci/inbox-decorator-seam-gate.test.sh   (exit 0 = all green; non-zero = a lock failed)

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="${INBOXSEAM_GATE:-$SCRIPT_DIR/inbox-decorator-seam-gate.sh}"

FAILURES=0
pass() { printf '  [PASS] %s\n' "$1"; }
fail() { printf '  [FAIL] %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

run_gate() { # <root> -> prints output, returns gate exit
    INBOXSEAM_ROOTS="$1" INBOXSEAM_MIN_DECOS="${2:-1}" bash "$GATE" --sweep 2>&1
}
rc_gate() { # <root> -> gate exit code, captured DIRECTLY (never through a pipe or a trailing command)
    local out
    out="$(run_gate "$1" "${2:-1}")"
    local rc=$?
    printf '%s' "$out" > "$TMP/.last"
    return $rc
}

DEFECT='internal sealed class DefectDecorator : IInboxStore, IInboxStoreCapabilities, ITransactionalInboxStore
{
	private readonly IInboxStore _inner;
}'
FIXED='internal sealed class FixedDecorator : IInboxStore, IInboxStoreCapabilities, ITransactionalInboxStore, IScopedTransactionalInboxStore
{
	private readonly IInboxStore _inner;
}'
PROVIDER='public sealed class SomeProviderInboxStore : IInboxStore, IClaimableInboxStore, ITransactionalInboxStore
{
	private readonly Func<DbConnection> _connectionFactory;
}'

echo "=== inbox-decorator-seam-gate: regression lock ==="

# ---- A: planted defect -> FAIL(1) -------------------------------------------------------------------
mkdir -p "$TMP/a"; printf '%s\n' "$DEFECT" > "$TMP/a/d.cs"
rc_gate "$TMP/a"; RC=$?
if [ "$RC" -eq 1 ]; then pass "A defect tree -> FAIL(1)"; else fail "A defect tree -> expected 1, got $RC"; fi
if grep -q 'DefectDecorator' "$TMP/.last"; then pass "A names the offending decorator"; else fail "A did not name the decorator"; fi

# ---- B: fixed shape -> PASS(0) (liveness; flag-everything dies here) ---------------------------------
mkdir -p "$TMP/b"; printf '%s\n' "$FIXED" > "$TMP/b/f.cs"
rc_gate "$TMP/b"; RC=$?
if [ "$RC" -eq 0 ]; then pass "B fixed tree -> PASS(0)"; else fail "B fixed tree -> expected 0, got $RC"; fi

# ---- C: empty root -> REFUSE(2) ---------------------------------------------------------------------
mkdir -p "$TMP/c"
rc_gate "$TMP/c"; RC=$?
if [ "$RC" -eq 2 ]; then pass "C empty root -> REFUSE(2), never a pass"; else fail "C empty root -> expected 2, got $RC"; fi

# ---- D: zero decorators -> REFUSE(2) ----------------------------------------------------------------
mkdir -p "$TMP/d"; printf '%s\n' "$PROVIDER" > "$TMP/d/p.cs"
rc_gate "$TMP/d"; RC=$?
if [ "$RC" -eq 2 ]; then pass "D zero decorators -> REFUSE(2), not a silent no-op"; else fail "D zero decorators -> expected 2, got $RC"; fi

# ---- E/F: the REAL decorator shape, pre-fix and fixed ------------------------------------------------
# Built from the real base list rather than a synthetic name, so the lock binds the actual defect. Where
# the committed blob is reachable it is used verbatim; otherwise the shape is reconstructed (mirror-safe).
REAL_TELE="src/Excalibur/Excalibur.Inbox/Diagnostics/TelemetryInboxStoreDecorator.cs"
mkdir -p "$TMP/e"
REPO="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null || echo '')"
GOT_REAL=0
if [ -n "$REPO" ] && git -C "$REPO" cat-file -e "HEAD:$REAL_TELE" 2>/dev/null; then
	git -C "$REPO" show "HEAD:$REAL_TELE" > "$TMP/e/real.cs" 2>/dev/null && GOT_REAL=1
fi
if [ "$GOT_REAL" -eq 1 ] && ! grep -q 'IScopedTransactionalInboxStore' "$TMP/e/real.cs"; then
	rc_gate "$TMP/e"; RC=$?
	if [ "$RC" -eq 1 ]; then pass "E real pre-fix decorator source -> FAIL(1)"; else fail "E real pre-fix source -> expected 1, got $RC"; fi
	# F: add the scoped seam to that same real source; the gate must go silent.
	sed 's/, IDisposable$/, IScopedTransactionalInboxStore, IDisposable/' "$TMP/e/real.cs" > "$TMP/e/real.fixed"
	mkdir -p "$TMP/f"; mv "$TMP/e/real.fixed" "$TMP/f/real.cs"
	if grep -q 'IScopedTransactionalInboxStore' "$TMP/f/real.cs"; then
		rc_gate "$TMP/f"; RC=$?
		if [ "$RC" -eq 0 ]; then pass "F real source + scoped seam -> PASS(0)"; else fail "F real source + scoped seam -> expected 0, got $RC"; fi
	else
		pass "F SKIPPED (could not synthesise the fixed variant from this revision)"
	fi
else
	pass "E/F SKIPPED (HEAD already carries the fix, or blob unreachable) — A/B carry the class"
fi

# ---- G: scope bound — a provider is not a decorator --------------------------------------------------
if grep -q 'inbox-decorators-evaluated=0' "$TMP/.last" 2>/dev/null || true; then :; fi
mkdir -p "$TMP/g"; printf '%s\n' "$PROVIDER" > "$TMP/g/p.cs"; printf '%s\n' "$FIXED" > "$TMP/g/f.cs"
rc_gate "$TMP/g"; RC=$?
if [ "$RC" -eq 0 ] && grep -q 'inbox-decorators-evaluated=1' "$TMP/.last"; then
	pass "G a provider alongside a compliant decorator -> PASS(0), provider uncounted"
else
	fail "G provider scope bound wrong (rc=$RC): $(grep -o 'inbox-decorators-evaluated=[0-9]*' "$TMP/.last")"
fi

# ---- H: C# 12 primary constructor is not invisible ---------------------------------------------------
mkdir -p "$TMP/h"
printf '%s\n' 'internal sealed class PrimaryDecorator(IInboxStore inner) : IInboxStore, ITransactionalInboxStore
{
}' > "$TMP/h/p.cs"
rc_gate "$TMP/h"; RC=$?
if [ "$RC" -eq 1 ]; then pass "H primary-constructor decorator -> FAIL(1) (not structurally invisible)"; else fail "H primary-ctor -> expected 1, got $RC"; fi

# ---- I: comment mentions are not base lists ----------------------------------------------------------
mkdir -p "$TMP/i"
printf '%s\n' '// Mentions IInboxStore, ITransactionalInboxStore and IScopedTransactionalInboxStore in prose.
public sealed class NotAStore
{
	private readonly int _x;
}' > "$TMP/i/n.cs"
printf '%s\n' "$FIXED" > "$TMP/i/f.cs"
rc_gate "$TMP/i"; RC=$?
if [ "$RC" -eq 0 ] && grep -q 'inbox-decorators-evaluated=1' "$TMP/.last"; then
	pass "I comment-only mentions -> not read as a declaration"
else
	fail "I comment noise misread (rc=$RC)"
fi

# ---- J/K/L/M: static guards on the gate source -------------------------------------------------------
if grep -q 'E_PASS=0' "$GATE" && grep -q 'E_FAIL=1' "$GATE" && grep -q 'E_REFUSE=2' "$GATE"; then
	pass "J 3-state exit contract present"
else fail "J 3-state exit contract missing"; fi

if grep -q 'INBOXSEAM_ROOTS' "$GATE"; then pass "K testability seam present"; else fail "K testability seam missing"; fi

if grep -q 'INBOXSEAM_MIN_DECOS' "$GATE" && ! grep -qiE '^[^#]*(MAX_HITS|SUPPRESS|ALLOWLIST)=' "$GATE"; then
	pass "L non-vacuity floor present, no suppression cap or allowlist"
else fail "L non-vacuity floor missing or a suppression mechanism was introduced"; fi

if grep -nE 'bd-[a-z0-9]{6}|\bS[0-9]{3}\b|[Ss]print[- ]?[0-9]|ADR-?[0-9]' "$GATE" > "$TMP/.nir" 2>/dev/null; then
	fail "M internal tracker/sprint/ADR reference in a mirrored gate source: $(cat "$TMP/.nir")"
else
	pass "M no internal refs in the gate source (eng/ is publicly mirrored)"
fi

# ---- self-test must pass ------------------------------------------------------------------------------
bash "$GATE" --self-test > "$TMP/.selftest" 2>&1
RC=$?
if [ "$RC" -eq 0 ]; then pass "gate --self-test all arms pass"; else fail "gate --self-test failed (rc=$RC): $(tail -3 "$TMP/.selftest")"; fi

echo ""
if [ "$FAILURES" -ne 0 ]; then
	echo "inbox-decorator-seam-gate.test.sh: $FAILURES lock(s) FAILED"
	exit 1
fi
echo "inbox-decorator-seam-gate.test.sh: all locks pass"
exit 0
