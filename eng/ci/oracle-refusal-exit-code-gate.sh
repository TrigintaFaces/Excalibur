#!/usr/bin/env bash
# oracle-exit-code-gate — every shipped Oracle script must exit non-zero when a statement fails.
#
# SQL*Plus returns exit 0 for a failed statement unless the script carries a WHENEVER SQLERROR EXIT
# FAILURE directive. Without it an unattended runner records a script that changed nothing as applied
# and runs the next step against a database that was never changed. The Postgres siblings do not have
# this problem: they carry ON_ERROR_STOP.
#
# THE REQUIREMENT IS EVERY SCRIPT, NOT ONLY THE ONES THAT CAN REFUSE. This gate once tested the
# narrower predicate "contains a refusal implies carries the directive", and that predicate is why the
# schema-creation scripts went unguarded: a script of plain DDL raises no refusal, so it was out of
# scope by construction and the gate stayed green across every one of them. Those are the FIRST
# scripts a consumer runs, and their exposure is ordinary statement failure — an object that already
# exists, an insufficient privilege — which the same directive catches and the narrower predicate
# never looked for. Scoping the check by what a script happens to contain reproduces, mechanically,
# the same blind spot a per-script hand-audit had already produced once.
#
# So the predicate is now the requirement itself, derived from the file list rather than from a
# maintained enumeration: a NEW Oracle script is covered the moment it is added, whatever it contains.
#
# Exit codes:
#   0  every shipped Oracle script carries the directive
#   1  at least one would exit 0 on a failed statement (gate fail)
#   2  usage / environment error
#   3  --self-test failed (the gate itself is broken or vacuous)
#
# Usage:
#   oracle-refusal-exit-code-gate.sh
#   oracle-refusal-exit-code-gate.sh --self-test
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# Whole-line comments are stripped first: both the refusal and the directive are only real when the
# server or the client actually sees them, and both words appear in the prose of these files.
uncommented() { sed 's/^[[:space:]]*--.*$//' "$1"; }

scan() {
	local failures=0 scripts=0 refusing=0 script
	local root="${ORACLE_REFUSAL_GATE_ROOT:-$REPO_ROOT}"

	while IFS= read -r script; do
		scripts=$((scripts + 1))
		local body
		body="$(uncommented "$script")"

		# Counted for the report only. An explicit refusal, or a PL/SQL block that re-raises, is the most
		# VISIBLE way for a script to end in an error the client must see -- it is not the only one, and it
		# no longer decides whether a script is examined. Every script found below is examined.
		if grep -qi 'RAISE_APPLICATION_ERROR' <<<"$body" \
			|| grep -qE '^[[:space:]]*/[[:space:]]*$' <<<"$body"; then
			refusing=$((refusing + 1))
		fi

		if ! grep -qiE '^[[:space:]]*WHENEVER[[:space:]]+SQLERROR[[:space:]]+EXIT[[:space:]]+FAILURE' <<<"$body"; then
			printf '  %s\n' "$script"
			failures=$((failures + 1))
		fi
	done < <(find "$root/src" -type d -name Scripts -path '*Oracle*' -exec find {} -name '*.sql' -type f \; 2>/dev/null | sort)

	if [ "$scripts" -eq 0 ]; then
		echo "oracle-refusal-exit-code-gate: no shipped Oracle scripts found under '$root/src' — refusing to report a vacuous pass." >&2
		return 2
	fi

	echo "EXAMINED: $scripts shipped Oracle script(s), every one of which must carry the directive; $refusing of them can also raise an explicit refusal."
	return $((failures > 0 ? 1 : 0))
}

self_test() {
	local tmp status rc=0
	tmp="$(mktemp -d)"
	trap 'rm -rf "$tmp"' RETURN

	mkdir -p "$tmp/src/Pkg.Oracle/Scripts"

	# LIVENESS arm: a refusing script with no directive must be REPORTED.
	cat > "$tmp/src/Pkg.Oracle/Scripts/planted-missing.sql" <<'FIXTURE'
BEGIN
  RAISE_APPLICATION_ERROR(-20099, 'planted refusal');
END;
/
FIXTURE
	ORACLE_REFUSAL_GATE_ROOT="$tmp" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 1 ]; then
		echo "self-test FAIL: a refusing script with no directive was not reported (got exit $status, wanted 1)." >&2
		rc=3
	fi

	# SAFETY arm: adding the directive must clear it — the gate must not be permanently red.
	cat > "$tmp/src/Pkg.Oracle/Scripts/planted-missing.sql" <<'FIXTURE'
WHENEVER SQLERROR EXIT FAILURE ROLLBACK
BEGIN
  RAISE_APPLICATION_ERROR(-20099, 'planted refusal');
END;
/
FIXTURE
	ORACLE_REFUSAL_GATE_ROOT="$tmp" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 0 ]; then
		echo "self-test FAIL: a refusing script WITH the directive was still reported (got exit $status, wanted 0)." >&2
		rc=3
	fi

	# The directive must be seen only where the client sees it — a commented one is not a directive.
	cat > "$tmp/src/Pkg.Oracle/Scripts/planted-missing.sql" <<'FIXTURE'
-- Run this with WHENEVER SQLERROR EXIT FAILURE.
BEGIN
  RAISE_APPLICATION_ERROR(-20099, 'planted refusal');
END;
/
FIXTURE
	ORACLE_REFUSAL_GATE_ROOT="$tmp" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 1 ]; then
		echo "self-test FAIL: a COMMENTED directive was accepted as the real thing (got exit $status, wanted 1)." >&2
		rc=3
	fi

	# LIVENESS arm: a PL/SQL block that RE-RAISES needs the directive just as much as an explicit
	# refusal -- without this arm the widened predicate could be dropped and the gate stay green.
	cat > "$tmp/src/Pkg.Oracle/Scripts/planted-missing.sql" <<'FIXTURE'
DECLARE
  already_done EXCEPTION;
  PRAGMA EXCEPTION_INIT(already_done, -1430);
BEGIN
  EXECUTE IMMEDIATE 'ALTER TABLE T ADD (C NUMBER)';
EXCEPTION
  WHEN already_done THEN NULL;
END;
/
FIXTURE
	ORACLE_REFUSAL_GATE_ROOT="$tmp" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 1 ]; then
		echo "self-test FAIL: a re-raising PL/SQL block with no directive was not reported (got exit $status, wanted 1)." >&2
		rc=3
	fi

	# LIVENESS arm, and the one this gate previously got wrong: a script of PLAIN DDL that cannot
	# refuse must STILL be reported when it lacks the directive. Under the old predicate this arm
	# asserted the opposite -- it required such a script to PASS -- which is precisely what let the
	# schema-creation scripts ship unguarded. If this arm is ever inverted back, the blind spot returns.
	rm "$tmp/src/Pkg.Oracle/Scripts/planted-missing.sql"
	printf 'CREATE TABLE T (ID NUMBER);
' > "$tmp/src/Pkg.Oracle/Scripts/planted-plain.sql"
	ORACLE_REFUSAL_GATE_ROOT="$tmp" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 1 ]; then
		echo "self-test FAIL: plain DDL with no directive was not reported (got exit $status, wanted 1)." >&2
		rc=3
	fi

	# SAFETY arm: the same plain DDL WITH the directive must clear, so the widened predicate cannot
	# be satisfied by simply failing everything.
	printf 'WHENEVER SQLERROR EXIT FAILURE ROLLBACK
CREATE TABLE T (ID NUMBER);
' > "$tmp/src/Pkg.Oracle/Scripts/planted-plain.sql"
	ORACLE_REFUSAL_GATE_ROOT="$tmp" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 0 ]; then
		echo "self-test FAIL: plain DDL WITH the directive was still reported (got exit $status, wanted 0)." >&2
		rc=3
	fi

	[ "$rc" -eq 0 ] && echo "✅ oracle-exit-code-gate --self-test: 6/6 arms pass (4 liveness, 2 safety)."
	return "$rc"
}

case "${1:-}" in
	--self-test) self_test; exit $? ;;
	"") ;;
	*) echo "usage: $(basename "$0") [--self-test]" >&2; exit 2 ;;
esac

# The status must be captured from scan ITSELF. Written as `if scan; then …; fi` the compound
# returns 0 when the condition is false and no else branch runs, so $? afterwards is 0 and an
# environment error (exit 2, "no scripts found") was reported as a gate FAIL (exit 1) instead.
# A gate that cannot find its inputs has measured nothing; that is not the same as a defect found.
scan
status=$?

if [ "$status" -eq 0 ]; then
	echo "✅ oracle-exit-code-gate: every shipped Oracle script exits non-zero when a statement fails."
	exit 0
fi

[ "$status" -eq 2 ] && exit 2
cat >&2 <<'MSG'
❌ oracle-exit-code-gate: the script(s) above exit 0 even when one of their statements fails.
   A pipeline reads that as success and applies the next migration to an unchanged database.
   This applies to plain schema-creation scripts too, not only ones that raise an explicit refusal:
   an object that already exists or an insufficient privilege ends the same way.
   Add, before the first statement:  WHENEVER SQLERROR EXIT FAILURE ROLLBACK
MSG
exit 1
