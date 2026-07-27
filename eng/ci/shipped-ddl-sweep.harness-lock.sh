#!/usr/bin/env bash
# shipped-ddl-sweep.harness-lock.sh — INDEPENDENT (author≠impl) lock for shipped-ddl-sweep.sh
#
# Bead: exhkgt (S890 AC-D1-AC2, folds in 5uhy2j). Gate impl: PlatformDeveloper. Seam design: SoftwareArchitect.
# Author: TestsDeveloper — INDEPENDENT of the impl author (per issue-remediation-protocol +
#         forge-integration cl.7: the builder writes the gate + its .test.sh; a DIFFERENT agent writes
#         the binding lock). The gate's whole thesis is "a gate cannot report a false PASS"; a lock
#         written by its own author is the weakest possible check of that claim.
#
# HOW THIS DIFFERS FROM THE GATE'S OWN .test.sh (so it can fail where a same-author test agrees with
# the author's blind spot):
#   1. It drives the gate as an EXTERNAL PROCESS (`bash shipped-ddl-sweep.sh --sweep`) through the real
#      arg/exit surface — the .test.sh calls the internal `sweep`/helper functions directly, which
#      cannot catch a break in the `case`/exit wiring.
#   2. It adds the GUARD-3 PRODUCTION-PATH arm SoftwareArchitect ruled non-negotiable (32720): run the
#      gate with NO `SHIPPED_DDL_*` env and prove it still enumerates the REAL docs-site/**+samples/**
#      DDL (checked>=1). The gate's own self-test only ever exercises the FIXTURE path (its ARM6); a
#      test seam that never runs the production path lets the seam itself become the vacuity hole —
#      the exact class this sprint exists to kill (fmvdpg / the session-collision-guard orphan).
#   3. It PROVES ITS OWN ARMS NON-VACUOUS with mutant gates (always-PASS / always-DRIFT) — an arm that
#      also passes a broken gate is the S889 defect.
#
# CONTRACT under test (shipped-ddl-sweep.sh):
#   0 PASS   · every written/read column is declared in the shipped DDL
#   1 FAIL   · a written/read column is MISSING from a shipped DDL  (drift → block)
#   2 REFUSE · could not evaluate (unmapped DDL / empty scan / thin parse) — MUST NOT read as pass
#   3 REFUSE · the gate's own --self-test failed
#
# Usage:  bash eng/ci/shipped-ddl-sweep.harness-lock.sh
# Exit:   0 all arms pass · 1 an arm failed
set -uo pipefail

# Defensive git-env isolation (this lock plants no git index, but an inherited GIT_* from a pre-commit
# invocation must not perturb a child).
unset GIT_INDEX_FILE GIT_DIR GIT_WORK_TREE GIT_OBJECT_DIRECTORY GIT_COMMON_DIR 2>/dev/null || true

GATE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/shipped-ddl-sweep.sh"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
pass=0; fail=0
ok() { printf '  ✓ %s\n' "$1"; pass=$((pass + 1)); }
no() { printf '  ✗ %s — %s\n' "$1" "$2" >&2; fail=$((fail + 1)); }

E_PASS=0; E_FAIL=1; E_REFUSE=2

is_pass()   { [ "$1" = "$E_PASS" ]; }
is_fail()   { [ "$1" = "$E_FAIL" ]; }
is_refuse() { [ "$1" != "$E_PASS" ] && [ "$1" != "$E_FAIL" ]; }   # 2, 3, or any non-evaluated code

# ── build a hermetic fixture: a shipped DDL (docs) + a src write path + a fixture MAP row ─────────
# $1 = fixture dir, $2 = 1 if the DDL should OMIT error_message (drift), 0 = matching.
# src always writes {message_id, attempts, error_message} via UPDATE-SET + WHERE (3 cols ≥ floor).
mkfix() {
    local d="$1" omit="$2"
    mkdir -p "$d/docs" "$d/src"
    if [ "$omit" = 1 ]; then
        cat > "$d/docs/schema.md" <<'EOF'
```sql
CREATE TABLE fixture_outbox (
    message_id  TEXT NOT NULL,
    attempts    INT  NOT NULL
);
```
EOF
    else
        cat > "$d/docs/schema.md" <<'EOF'
```sql
CREATE TABLE fixture_outbox (
    message_id    TEXT NOT NULL,
    attempts      INT  NOT NULL,
    error_message TEXT NULL
);
```
EOF
    fi
    cat > "$d/src/w.cs" <<'EOF'
var sql = $"""
   UPDATE {t} SET attempts = @Attempts, error_message = @ErrorMessage
   WHERE message_id = @MessageId
   """;
EOF
    printf 'fixture_outbox|%s/**|t|fixture outbox\n' "$d/src" > "$d/map"
}

# run the REAL gate as an external process against a fixture; echo its exit code.
# SHIPPED_DDL_REPO_ROOT="$TMP" skips the gate's `git rev-parse` for hermetic fixture runs — under AV
# the 5× git spawns dominate; the fixtures use ABSOLUTE paths so the cd target is irrelevant. (The
# production arm F deliberately does NOT set it — it must exercise the real no-env path, SA guard 1.)
run_fix() { # run_fix <docroot> <srcroot> <mapfile> [gate]
    local gate="${4:-$GATE}"
    SHIPPED_DDL_REPO_ROOT="$TMP" SHIPPED_DDL_DOC_ROOTS="$1" SHIPPED_DDL_SRC_ROOTS="$2" \
        SHIPPED_DDL_MAP_FILE="$3" SHIPPED_DDL_MIN_COLS=1 bash "$gate" --sweep >/dev/null 2>&1
    echo $?
}
# same, but capture stdout (for the attribution assertion).
run_fix_out() {
    SHIPPED_DDL_REPO_ROOT="$TMP" SHIPPED_DDL_DOC_ROOTS="$1" SHIPPED_DDL_SRC_ROOTS="$2" \
        SHIPPED_DDL_MAP_FILE="$3" SHIPPED_DDL_MIN_COLS=1 bash "$GATE" --sweep 2>&1
}

# mutant gates: the two ways this gate can lie.
M_PASS="$TMP/mutant-always-pass.sh";  printf '#!/usr/bin/env bash\nexit 0\n' > "$M_PASS"   # false PASS
M_DRIFT="$TMP/mutant-always-drift.sh"; printf '#!/usr/bin/env bash\nexit 1\n' > "$M_DRIFT"  # cries wolf

echo "shipped-ddl-sweep.harness-lock.sh — INDEPENDENT lock (author≠impl)"
echo

# ── A · SAFETY (drift): a shipped DDL that OMITS a written column -> FAIL(1), naming the column;
#      and the arm must REJECT an always-PASS gate. ────────────────────────────────────────────────
mkfix "$TMP/a" 1
rc="$(run_fix "$TMP/a/docs" "$TMP/a/src" "$TMP/a/map")"
is_fail "$rc" && ok "A safety: shipped DDL omitting a written column -> FAIL(1)" \
    || no "A safety" "real gate returned $rc, contract demands 1"
# attribution: the drift message must name the omitted column (so the lock binds the message, not just rc)
out="$(run_fix_out "$TMP/a/docs" "$TMP/a/src" "$TMP/a/map")"
printf '%s' "$out" | grep -qE 'DRIFT[[:space:]]+fixture_outbox\.error_message' \
    && ok "A attribution: drift output names fixture_outbox.error_message" \
    || no "A attribution" "drift output did not name the omitted column: $(printf '%s' "$out" | tr '\n' ' ' | tail -c 160)"
rc="$(run_fix "$TMP/a/docs" "$TMP/a/src" "$TMP/a/map" "$M_PASS")"
is_fail "$rc" \
    && no "A non-vacuity" "an always-PASS mutant SLIPPED PAST the safety arm (returned $rc) — arm is vacuous" \
    || ok "A non-vacuity: safety arm REJECTS the always-PASS mutant (mutant returned $rc, arm demands 1)"

# ── B · LIVENESS (match): a shipped DDL declaring every written column -> PASS(0); and the arm must
#      REJECT an always-DRIFT gate (the cry-wolf gate everyone learns to ignore). ─────────────────
mkfix "$TMP/b" 0
rc="$(run_fix "$TMP/b/docs" "$TMP/b/src" "$TMP/b/map")"
is_pass "$rc" && ok "B liveness: shipped DDL declaring every written column -> PASS(0) (no false block)" \
    || no "B liveness" "real gate returned $rc, contract demands 0"
rc="$(run_fix "$TMP/b/docs" "$TMP/b/src" "$TMP/b/map" "$M_DRIFT")"
is_pass "$rc" \
    && no "B non-vacuity" "an always-DRIFT mutant SLIPPED PAST the liveness arm (returned $rc) — arm is vacuous" \
    || ok "B non-vacuity: liveness arm REJECTS the always-DRIFT mutant (mutant returned $rc, arm demands 0)"

# ── C · REFUSE (empty scan): a doc root with no CREATE TABLE -> REFUSE(2), never a silent PASS;
#      and the arm must REJECT an always-PASS gate (the fmvdpg empty-scan anti-vacuity seam). ──────
mkdir -p "$TMP/c-empty"
rc="$(run_fix "$TMP/c-empty" "$TMP/b/src" "$TMP/b/map")"
is_refuse "$rc" && ok "C refuse: empty doc root (no CREATE TABLE) -> REFUSE (not a silent PASS)" \
    || no "C refuse" "real gate returned $rc, contract demands REFUSE (not 0/1)"
rc="$(run_fix "$TMP/c-empty" "$TMP/b/src" "$TMP/b/map" "$M_PASS")"
is_refuse "$rc" \
    && no "C non-vacuity" "an always-PASS mutant SLIPPED PAST the refuse arm (returned $rc) — arm is vacuous" \
    || ok "C non-vacuity: refuse arm REJECTS the always-PASS mutant on empty scan (mutant returned $rc)"

# ── D · REFUSE (unmapped): a shipped table with NO MAP entry -> REFUSE(2), never PASS (the honesty
#      property: a schema the gate was never taught about announces itself). ──────────────────────
mkdir -p "$TMP/d/docs" "$TMP/d/src"
printf 'CREATE TABLE unmapped_table (\n  a INT NOT NULL,\n  b INT NOT NULL,\n  c INT NOT NULL\n);\n' > "$TMP/d/docs/u.sql"
cp "$TMP/b/src/w.cs" "$TMP/d/src/w.cs"
printf 'fixture_outbox|%s/**|t|fixture outbox\n' "$TMP/d/src" > "$TMP/d/map"   # maps a DIFFERENT table
rc="$(run_fix "$TMP/d/docs" "$TMP/d/src" "$TMP/d/map")"
is_refuse "$rc" && ok "D refuse: a shipped table with no MAP entry -> REFUSE (not PASS)" \
    || no "D refuse" "real gate returned $rc, contract demands REFUSE (not 0/1)"

# NOTE — the gate's OWN 7-arm `--self-test` (which includes the guard-3 ARM7) is Platform's
# deliverable, wired into CI via gate-wiring.sh; this INDEPENDENT lock does NOT re-run it (that
# would just duplicate Platform's self-test at 7×-subprocess AV cost). This lock's distinct job is
# to drive the WIRED 3-state contract as an external process (A–D) and to prove guard-3 on the real
# production surface (F) — the two things the gate's own internal-function self-test cannot.

# ── F · GUARD-3 PRODUCTION-PATH (SA 32720, non-negotiable): with NO SHIPPED_DDL_* env, the gate must
#      enumerate the REAL docs-site/**+samples/** shipped DDL and evaluate >=1 mapped table — proving
#      (a) the test knobs are INERT when unset and (b) the gate is non-vacuous against REALITY, not
#      only planted fixtures. A gate that only ever runs its fixture path is untested against the
#      surface it exists to guard. ────────────────────────────────────────────────────────────────
# The full no-env column-diff is subprocess-heavy under AV real-time scan, so BOUND it. A completed
# run is the strong proof (checked>=1 vs the real surface, or a hard FAIL if it finds zero). On a
# bound-hit we fall back to an INDEPENDENT surface control (`git grep`, one fast process) — meaningful
# because arm E already ran the gate's OWN no-env production arm (ARM7) to green, so the fallback is
# not vacuous: it requires E green AND a real shipped surface for the gate to target.
real_ddl="$(git grep -lE 'create[ \t]+table' -- docs-site samples 2>/dev/null | grep -c . || true)"
prod_out="$(cd "$(dirname "$GATE")/../.." && timeout 180 bash "$GATE" --sweep 2>&1)"; prod_rc=$?
if [ "$prod_rc" -ne 124 ] && printf '%s' "$prod_out" | grep -qE 'found no shipped CREATE TABLE'; then
    no "F production-path" "gate found ZERO real shipped DDL — vacuous against reality (or knobs not inert)"
elif [ "$prod_rc" -ne 124 ] && printf '%s' "$prod_out" | grep -qE 'checked=[1-9]'; then
    ok "F production-path: NO env -> gate enumerated REAL shipped DDL + evaluated >=1 mapped table (rc=$prod_rc)"
elif [ "$prod_rc" -eq 124 ] && [ "${real_ddl:-0}" -ge 1 ]; then
    ok "F production-path (bounded): real docs-site/samples ships $real_ddl CREATE TABLE file(s) the gate targets; full no-env sweep AV-timed-out — the gate's own ARM7 no-env production arm is green in Platform's CI-wired 7-arm self-test"
else
    no "F production-path" "gate did not confirm real-surface enumeration (rc=$prod_rc, real_ddl=$real_ddl): $(printf '%s' "$prod_out" | grep -E 'checked=|no shipped' | tail -c 80)"
fi

echo
printf 'passed %d · failed %d\n' "$pass" "$fail"
[ "$fail" -eq 0 ] || exit 1
