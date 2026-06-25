#!/usr/bin/env bash
# bd-flush-guard.test.sh — author≠impl regression lock for bd-flush-guard.sh (Sprint 849 / I1 / xlnd5e).
#
# Drives the REAL eng/ci/bd-flush-guard.sh through an injected fake `bd` (BD_BIN) over an isolated
# tracker (BD_JSONL_PATH), asserting the durability + data-loss-safety contract pinned by the
# implementer (msg 15309). Composes .claude/rules/process/bd-flush-then-commit.md (clause 6: never
# blind `import`; foreign-change → loud exit 2) and no-pipe-masked-commit.md (exit captured directly).
#
# Behavioral non-vacuity (each fails on a broken/pre-fix helper):
#   A. genuine-fail fake bd            -> exit 1   (AC-I1.2: fail LOUD, never silently proceed)
#   B. on-disk .jsonl AHEAD of the DB  -> exit 2 + file byte-unchanged (EC-I1.1: data-loss guard)
#   C. fail-once-then-succeed fake bd  -> exit 0   (AC-I1.1: transient clears within the retry bound)
#   D. on-disk strictly-older subset   -> exit 0 + on-disk replaced by the authoritative DB dump
#   E. already-in-sync                 -> exit 0 + file byte-unchanged (no-op)
# Static guards (grep the helper source):
#   F. no `bd … | head/tail`           (no-pipe-masked-commit: exit must not be masked)
#   G. never calls `bd import`         (clause-6 data-loss guard: import would revert fresh closes)
#
# Run: bash eng/ci/bd-flush-guard.test.sh   (exit 0 = all green; non-zero = a lock failed)

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Guard under test; overridable (BDFG_GUARD) so a non-vacuity proof can point it at a mutant copy.
GUARD="${BDFG_GUARD:-$SCRIPT_DIR/bd-flush-guard.sh}"

FAILURES=0
pass() { printf '  [PASS] %s\n' "$1"; }
fail() { printf '  [FAIL] %s\n' "$1" >&2; FAILURES=$((FAILURES + 1)); }

[ -f "$GUARD" ] || { printf 'FATAL: guard not found at %s\n' "$GUARD" >&2; exit 3; }

WORK="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/bdflushtest.$$")"
mkdir -p "$WORK"
cleanup() { rm -rf "$WORK" 2>/dev/null || true; }
trap cleanup EXIT

# A fake `bd` whose behavior is driven by env. Handles only `export --no-auto-import -o <path>`.
#   FAKE_MODE=genuine_fail  -> always exit 1
#   FAKE_MODE=transient     -> exit 1 until call #FAKE_FAIL_TIMES+1, then write DB dump + exit 0
#   FAKE_MODE=success       -> write DB dump + exit 0
# The DB dump is the contents of $FAKE_DB_FILE. A counter file tracks transient calls.
make_fake_bd() {
	local path="$1"
	cat > "$path" <<'FAKEEOF'
#!/usr/bin/env bash
set -u
# locate the -o output path among the args
out=""
prev=""
for a in "$@"; do
	[ "$prev" = "-o" ] && out="$a"
	prev="$a"
done
case "${FAKE_MODE:-success}" in
	genuine_fail)
		echo "fake-bd: simulated genuine export failure" >&2
		exit 1
		;;
	transient)
		n=0
		[ -f "$FAKE_COUNTER" ] && n="$(cat "$FAKE_COUNTER")"
		n=$((n + 1)); printf '%s' "$n" > "$FAKE_COUNTER"
		if [ "$n" -le "${FAKE_FAIL_TIMES:-1}" ]; then
			echo "fake-bd: simulated transient failure $n" >&2
			exit 1
		fi
		cp "$FAKE_DB_FILE" "$out"; exit 0
		;;
	*)
		cp "$FAKE_DB_FILE" "$out"; exit 0
		;;
esac
FAKEEOF
	chmod +x "$path"
}

FAKE_BD="$WORK/fake-bd.sh"
make_fake_bd "$FAKE_BD"

run_guard() { # args: <jsonl-path>  ; env FAKE_* + attempts/backoff passed through
	BD_BIN="$FAKE_BD" BD_JSONL_PATH="$1" BD_FLUSH_BACKOFF=0 BD_FLUSH_ATTEMPTS="${ATTEMPTS:-3}" \
		FAKE_MODE="${FAKE_MODE:-success}" FAKE_DB_FILE="${FAKE_DB_FILE:-}" \
		FAKE_COUNTER="${FAKE_COUNTER:-}" FAKE_FAIL_TIMES="${FAKE_FAIL_TIMES:-1}" \
		bash "$GUARD" >/dev/null 2>&1
}

ISSUE_OLD='{"id":"tst-1","updated_at":"2026-06-25T10:00:00Z"}'
ISSUE_NEW='{"id":"tst-1","updated_at":"2026-06-25T12:00:00Z"}'
ISSUE_TWO='{"id":"tst-2","updated_at":"2026-06-25T11:00:00Z"}'
ISSUE_FOREIGN='{"id":"tst-9","updated_at":"2026-06-25T13:00:00Z"}'

printf '== bd-flush-guard regression lock ==\n'

# ── A: genuine failure → exit 1 ──────────────────────────────────────────────
db="$WORK/a.db"; printf '%s\n' "$ISSUE_NEW" > "$db"
jsonl="$WORK/a.jsonl"; printf '%s\n' "$ISSUE_OLD" > "$jsonl"
FAKE_MODE=genuine_fail FAKE_DB_FILE="$db" ATTEMPTS=2 run_guard "$jsonl"
[ "$?" -eq 1 ] && pass "A genuine-fail -> exit 1 (loud)" || fail "A genuine-fail expected exit 1"

# ── B: on-disk AHEAD (foreign id) → exit 2, file unchanged ───────────────────
db="$WORK/b.db"; printf '%s\n' "$ISSUE_NEW" > "$db"
jsonl="$WORK/b.jsonl"; printf '%s\n%s\n' "$ISSUE_OLD" "$ISSUE_FOREIGN" > "$jsonl"
before="$(cat "$jsonl")"
FAKE_MODE=success FAKE_DB_FILE="$db" run_guard "$jsonl"
rc=$?
if [ "$rc" -eq 2 ] && [ "$(cat "$jsonl")" = "$before" ]; then
	pass "B on-disk-ahead -> exit 2 + file byte-unchanged (no data loss)"
else
	fail "B on-disk-ahead expected exit 2 + unchanged (got exit $rc)"
fi

# ── C: transient (fail-once) → exit 0 ────────────────────────────────────────
db="$WORK/c.db"; printf '%s\n' "$ISSUE_OLD" > "$db"
jsonl="$WORK/c.jsonl"; printf '%s\n' "$ISSUE_OLD" > "$jsonl"
counter="$WORK/c.counter"; : > "$counter"
FAKE_MODE=transient FAKE_DB_FILE="$db" FAKE_COUNTER="$counter" FAKE_FAIL_TIMES=1 ATTEMPTS=3 run_guard "$jsonl"
[ "$?" -eq 0 ] && pass "C transient-clears-within-bound -> exit 0" || fail "C transient expected exit 0"

# ── D: on-disk strictly-older subset → exit 0, replaced by DB dump ───────────
db="$WORK/d.db"; printf '%s\n%s\n' "$ISSUE_NEW" "$ISSUE_TWO" > "$db"
jsonl="$WORK/d.jsonl"; printf '%s\n' "$ISSUE_OLD" > "$jsonl"
FAKE_MODE=success FAKE_DB_FILE="$db" run_guard "$jsonl"
rc=$?
if [ "$rc" -eq 0 ] && cmp -s "$db" "$jsonl"; then
	pass "D older-subset -> exit 0 + flushed to authoritative DB dump"
else
	fail "D older-subset expected exit 0 + on-disk == DB dump (got exit $rc)"
fi

# ── E: already-in-sync → exit 0, file unchanged ──────────────────────────────
db="$WORK/e.db"; printf '%s\n' "$ISSUE_OLD" > "$db"
jsonl="$WORK/e.jsonl"; printf '%s\n' "$ISSUE_OLD" > "$jsonl"
before="$(cat "$jsonl")"
FAKE_MODE=success FAKE_DB_FILE="$db" run_guard "$jsonl"
rc=$?
if [ "$rc" -eq 0 ] && [ "$(cat "$jsonl")" = "$before" ]; then
	pass "E already-in-sync -> exit 0 (no-op)"
else
	fail "E already-in-sync expected exit 0 + unchanged (got exit $rc)"
fi

# ── F: no `bd … | head/tail` (exit must not be pipe-masked) ──────────────────
# Match the bd reference in quoted ("$BD"), braced (${BD}) or bare (bd) form before the pipe.
if grep -Eq '("?\$\{?BD\}?"?|[^a-zA-Z_]bd)[^|]*\|[[:space:]]*(head|tail)' "$GUARD"; then
	fail "F helper pipes a bd invocation into head/tail (masks exit)"
else
	pass "F no bd|head/tail pipe-mask in helper"
fi

# ── G: never calls `bd import` (would revert fresh closes) ───────────────────
# Tolerate the quote/brace between the bd reference and the `import` subcommand ("$BD" import).
if grep -Eq '("?\$\{?BD\}?"?|[^a-zA-Z_]bd)["[:space:]]+import' "$GUARD"; then
	fail "G helper calls 'bd import' (clause-6 data-loss hazard)"
else
	pass "G helper never calls 'bd import'"
fi

printf '== %s ==\n' "$([ "$FAILURES" -eq 0 ] && echo 'ALL GREEN' || echo "$FAILURES FAILED")"
[ "$FAILURES" -eq 0 ]
