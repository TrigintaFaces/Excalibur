#!/usr/bin/env bash
# package-metadata-gate.test.sh — regression lock for eng/ci/package-metadata-gate.sh.
#
# The gate's thesis: a shipping NuGet package must carry its consumer-visible metadata (projectUrl,
# icon, deterministic lock-file restore). The real defect was a `src/Directory.Build.props` that did
# not inherit the repository root, so a root-only field shipped BLANK — invisible in source, visible on
# nuget.org. This lock proves the gate produces all THREE verdicts on inputs it controls, using the
# hermetic injected-values seam so the behavioral arms need no dotnet, plus one real evaluation.
#
# Behavioral 3-state (hermetic, via PKGMETA_VALUES — no dotnet):
#   A  a blank projectUrl / icon / non-true lockfile -> gate exit 1  (FAIL — would ship blank)
#   B  a fully-populated triple                      -> gate exit 0  (PASS, no false positive)
#   C  zero evaluable input                          -> gate exit 2  (REFUSE, never a pass)
# A/B are a safety+liveness pair: flag-everything fails B, flag-nothing fails A.
#
# Real-evaluation control (non-vacuity against reality, needs the SDK — skippable):
#   D  the real default shipping project evaluates PASS (its metadata is populated today)
#
# Static guards (grep the gate source — the seam guards must not regress):
#   E  3-state exit contract present (E_PASS=0 / E_FAIL=1 / E_REFUSE=2)
#   F  testability seam present (PKGMETA_VALUES + PKGMETA_PROJECTS override)
#   G  the effective-value instrument is used (dotnet msbuild -getProperty), NOT a static props grep
#   H  no internal tracker/sprint/ADR ids in the gate source (public-surface hygiene — eng/ is mirrored)
#
# Run: bash eng/ci/package-metadata-gate.test.sh   (exit 0 = all green; non-zero = a lock failed)

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="${PKGMETA_GATE:-$SCRIPT_DIR/package-metadata-gate.sh}"

FAILURES=0
pass() { printf '  [PASS] %s\n' "$1"; }
fail() { printf '  [FAIL] %s\n' "$1" >&2; FAILURES=$((FAILURES + 1)); }
skip() { printf '  [SKIP] %s\n' "$1"; }

[ -f "$GATE" ] || { printf 'FATAL: gate not found at %s\n' "$GATE" >&2; exit 3; }

echo "package-metadata-gate.test.sh — locking $GATE"

run_injected() {  # $1 = PKGMETA_VALUES ; prints exit code
    PKGMETA_VALUES="$1" PKGMETA_REPO_ROOT="$PWD" bash "$GATE" --sweep >/dev/null 2>&1
    echo $?
}

# ── A. blank field -> FAIL(1) (each of the three) ───────────────────────────
rc="$(run_injected '|icon.png|true')"
[ "$rc" -eq 1 ] && pass "A1: blank projectUrl -> FAIL(1)" || fail "A1: blank projectUrl gave $rc, expected 1"
rc="$(run_injected 'https://github.com/x/y||true')"
[ "$rc" -eq 1 ] && pass "A2: blank icon -> FAIL(1)" || fail "A2: blank icon gave $rc, expected 1"
rc="$(run_injected 'https://github.com/x/y|icon.png|false')"
[ "$rc" -eq 1 ] && pass "A3: lockfile != true -> FAIL(1)" || fail "A3: non-true lockfile gave $rc, expected 1"

# ── B. fully-populated -> PASS(0) ────────────────────────────────────────────
rc="$(run_injected 'https://github.com/x/y|icon.png|true')"
[ "$rc" -eq 0 ] && pass "B: populated triple -> PASS(0) (no false positive)" || fail "B: populated triple gave $rc, expected 0"

# ── C. zero evaluable input -> REFUSE(2) ─────────────────────────────────────
rc="$(PKGMETA_VALUES='' PKGMETA_PROJECTS='/nonexistent/none.csproj' PKGMETA_REPO_ROOT="$PWD" bash "$GATE" --sweep >/dev/null 2>&1; echo $?)"
[ "$rc" -eq 2 ] && pass "C: zero evaluable input -> REFUSE(2) (never a pass)" || fail "C: zero input gave $rc, expected 2"

# ── D. real evaluation (skippable): the default shipping project evaluates PASS ──
if command -v dotnet >/dev/null 2>&1; then
    rc="$(PKGMETA_REPO_ROOT="$PWD" bash "$GATE" --sweep >/dev/null 2>&1; echo $?)"
    if [ "$rc" -eq 0 ]; then
        pass "D: real default shipping project evaluates PASS(0) (metadata populated today)"
    elif [ "$rc" -eq 2 ]; then
        skip "D: msbuild could not evaluate here (e.g. no restore) — hermetic arms A/B/C carry the class"
    else
        fail "D: the real shipping project FAILED metadata (rc=$rc) — a live regression, not a test bug"
    fi
else
    skip "D: dotnet unavailable — hermetic arms A/B/C carry the class"
fi

# ── Static guards ────────────────────────────────────────────────────────────
grep -Eq '^E_PASS=0; E_FAIL=1; E_REFUSE=2' "$GATE" \
    && pass "E: gate declares the 3-state exit contract" || fail "E: gate 3-state exit contract missing"

grep -q 'PKGMETA_VALUES' "$GATE" && grep -q 'PKGMETA_PROJECTS' "$GATE" \
    && pass "F: gate exposes the testability seam (PKGMETA_VALUES + PKGMETA_PROJECTS)" \
    || fail "F: gate missing the env-override testability seam"

grep -q 'dotnet msbuild' "$GATE" && grep -q '\-getProperty' "$GATE" \
    && pass "G: gate uses the effective-value instrument (dotnet msbuild -getProperty), not a static grep" \
    || fail "G: gate does not use the evaluated-property instrument"

if grep -EnZ 'bd-[a-z0-9]{6}|[^A-Za-z]S[0-9]{3}[^0-9]|[Ss]print[- ]?[0-9]|ADR-?[0-9]' "$GATE" >/dev/null 2>&1; then
    fail "H: gate source leaks an internal tracker/sprint/ADR id (public-surface hygiene)"
    grep -EnH 'bd-[a-z0-9]{6}|[^A-Za-z]S[0-9]{3}[^0-9]|[Ss]print[- ]?[0-9]|ADR-?[0-9]' "$GATE" >&2 || true
else
    pass "H: gate source carries no internal tracker/sprint/ADR ids (mirror-clean)"
fi

echo ""
if [ "$FAILURES" -eq 0 ]; then
    echo "✅ package-metadata-gate.test.sh: ALL GREEN"
    exit 0
fi
echo "❌ package-metadata-gate.test.sh: $FAILURES lock(s) FAILED" >&2
exit 1
