#!/usr/bin/env bash
# pre-push-significance.test.sh — non-vacuous self-test for the pre-push CHANGELOG significance rule.
#
# WHY THIS EXISTS
#   CHANGELOG.md is the CONSUMER surface. The significance rule decides which pushes must carry an
#   entry, and it had classified internal tooling as shipping code -- so a change to a gate script
#   demanded a consumer-facing CHANGELOG line describing an internal process fact. There is no
#   honest entry for that, which left a lie or a bypass as the only ways forward, and a gate whose
#   only escape is a bypass teaches everyone to bypass it.
#
#   The rule is two regexes inside the hook. This reads them OUT of the real hook file rather than
#   restating them, so a copy here cannot drift from the copy that runs.
#
#   Every SAFETY arm (a path that must NOT demand an entry) is paired with a LIVENESS arm (a path
#   that MUST). A rule that classified nothing as significant would satisfy the safety arms alone
#   and silently stop asking for a CHANGELOG at all.
#
# Usage:  bash eng/hooks/pre-push-significance.test.sh
# Exit:   0 all arms pass · 1 an arm failed · 2 could not evaluate

set -uo pipefail

HOOK="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/pre-push"
PASS=0
FAIL=0

ok()  { PASS=$((PASS + 1)); printf '  PASS  %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  FAIL  %s\n     -> %s\n' "$1" "$2" >&2; }

[ -r "$HOOK" ] || { printf 'REFUSE: cannot read %s\n' "$HOOK" >&2; exit 2; }

# Take the definitions from the hook itself. Restating them here would let this file pass while
# the hook that actually runs says something else.
SIG_RE="$(grep -m1 '^SIG_RE=' "$HOOK" | sed "s/^SIG_RE='//; s/'$//")"
EXCL_RE="$(grep -m1 '^EXCL_RE=' "$HOOK" | sed "s/^EXCL_RE='//; s/'$//")"

[ -n "$SIG_RE" ] && [ -n "$EXCL_RE" ] || {
    printf 'REFUSE: could not read SIG_RE/EXCL_RE out of %s\n' "$HOOK" >&2
    exit 2
}

# The classification the hook performs, on one path.
is_significant() {
    printf '%s\n' "$1" | grep -E "$SIG_RE" | grep -vE "$EXCL_RE" | grep -q .
}

expect_significant() {
    if is_significant "$1"; then ok "significant: $1"; else bad "significant: $1" "classified as NOT significant"; fi
}

expect_not_significant() {
    if is_significant "$1"; then bad "not significant: $1" "classified as significant"; else ok "not significant: $1"; fi
}

echo "-- LIVENESS: a consumer-visible change still demands a CHANGELOG entry"
expect_significant "src/Dispatch/Excalibur.Dispatch/Delivery/Dispatcher.cs"
expect_significant ".github/workflows/release.yml"
expect_significant "Directory.Packages.props"
expect_significant "RELEASE.md"

echo "-- SAFETY: internal tooling and process artifacts do not"
expect_not_significant "eng/ci/some-gate.sh"
expect_not_significant "eng/hooks/pre-push"
expect_not_significant ".claude/rules/process/some-rule.md"
expect_not_significant "management/architecture/adr-999-something.md"
expect_not_significant "management/sprints/plan.md"
expect_not_significant "tests/unit/Something/SomethingShould.cs"

echo "-- SAFETY: a self-test script is never shipping code, wherever it lives"
expect_not_significant "eng/ci/some-gate.test.sh"

printf '\npre-push-significance: %d arms passed, %d failed\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ] || exit 1
