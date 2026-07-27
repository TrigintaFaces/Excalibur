#!/usr/bin/env bash
# shipped-ddl-sweep.test.sh — regression lock for eng/ci/shipped-ddl-sweep.sh (S890 exhkgt / AC-D1-AC2).
#
# The gate's thesis is "cannot report a false PASS" (r4dzl2 exit-hole class): a consumer copies our
# shipped CREATE TABLE and it must declare every column our code writes/reads, or the gate blocks.
# This lock is the gate's WIRED control — it proves the gate produces all THREE distinct verdicts on
# inputs it controls: FAIL on a planted omission, PASS on a matching pair, REFUSE on a can't-evaluate.
# G (omission->FAIL) and H (match->PASS) are a safety+liveness pair (testing-patterns §3): a gate that
# flagged everything would fail H; one that flagged nothing would fail G. A gate returning three
# DIFFERENT exit codes for three different scenarios cannot be a constant-verdict no-op.
#
# The deeper mutant rigor (always-PASS / always-DRIFT mutants rejected, attribution names the column)
# lives in the INDEPENDENT author!=impl lock `shipped-ddl-sweep.harness-lock.sh` (TestsDeveloper) —
# this .test.sh is the fast, wired pre-commit control, deliberately kept to the 3-state behavioral
# core so it stays affordable (a gate/control too slow to run is a gate nobody runs — this sprint's
# own inert-gate defect). Every arm drives the gate through the SA-blessed hermetic override
# (SHIPPED_DDL_DOC_ROOTS + SHIPPED_DDL_MAP_FILE + SHIPPED_DDL_REPO_ROOT -> no git, absolute-path find).
#
# Behavioral 3-state (hermetic):
#   G  planted omission (src writes a col the DDL omits) -> gate exit 1  (FAIL — the 34k958 class)
#   H  matching DDL/code pair                            -> gate exit 0  (PASS, no false positive)
#   I  empty doc root                                    -> gate exit 2  (REFUSE, never a pass)
# Static guards (grep the gate source — the SA seam guards must not regress):
#   D  3-state exit codes present (E_PASS=0 / E_FAIL=1 / E_REFUSE=2)
#   E  testability seam present (SHIPPED_DDL_MAP_FILE + SHIPPED_DDL_DOC_ROOTS override)
#   F  non-vacuity floor present (MIN_WRITTEN_COLS) + NO suppression cap (fmvdpg class)
#   J  guard-1: production default roots are the REAL surface (docs-site samples)
#
# Run: bash eng/ci/shipped-ddl-sweep.test.sh   (exit 0 = all green; non-zero = a lock failed)

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="${SHIPPED_DDL_GATE:-$SCRIPT_DIR/shipped-ddl-sweep.sh}"

FAILURES=0
pass() { printf '  [PASS] %s\n' "$1"; }
fail() { printf '  [FAIL] %s\n' "$1" >&2; FAILURES=$((FAILURES + 1)); }

[ -f "$GATE" ] || { printf 'FATAL: gate not found at %s\n' "$GATE" >&2; exit 3; }

WORK="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/shippedddltest.$$")"
mkdir -p "$WORK"
cleanup() { rm -rf "$WORK" 2>/dev/null || true; }
trap cleanup EXIT

echo "shipped-ddl-sweep.test.sh — locking $GATE"

# ── Hermetic fixture: a shipped doc DDL + a src write path, driven via the blessed override ───────
FX="$WORK/fx"; mkdir -p "$FX/docs" "$FX/src"
cat > "$FX/src/w.cs" <<'EOF'
var sql = $"""
   UPDATE {t} SET attempts = @Attempts, error_message = @ErrorMessage
   WHERE message_id = @MessageId
   """;
EOF
printf 'outbox_messages|%s/**|t|fixture outbox\n' "$FX/src" > "$FX/map"

# run the gate over the current fixture docs; prints its exit code. SHIPPED_DDL_REPO_ROOT="$PWD"
# skips the gate's git rev-parse (git spawns dominate under AV; this lock runs in pre-commit).
run_gate() {  # $1 = doc root ; prints exit code
    SHIPPED_DDL_DOC_ROOTS="$1" SHIPPED_DDL_SRC_ROOTS="$FX/src" \
      SHIPPED_DDL_MAP_FILE="$FX/map" SHIPPED_DDL_MIN_COLS=1 SHIPPED_DDL_REPO_ROOT="$PWD" \
      bash "$GATE" --sweep >/dev/null 2>&1
    echo $?
}

# --- G. planted omission -> FAIL(1) ----------------------------------------
cat > "$FX/docs/schema.md" <<'EOF'
```sql
CREATE TABLE outbox_messages (
    message_id TEXT NOT NULL,
    attempts   INT  NOT NULL
);
```
EOF
rc="$(run_gate "$FX/docs")"
if [ "$rc" -eq 1 ]; then
    pass "G: planted omission -> gate FAIL(1) (the 34k958 missing-column class is caught)"
else
    fail "G: planted omission did NOT FAIL (got exit $rc, expected 1)"
fi

# --- H. matching DDL/code pair -> PASS(0) ----------------------------------
cat > "$FX/docs/schema.md" <<'EOF'
```sql
CREATE TABLE outbox_messages (
    message_id    TEXT NOT NULL,
    attempts      INT  NOT NULL,
    error_message TEXT NULL
);
```
EOF
rc="$(run_gate "$FX/docs")"
if [ "$rc" -eq 0 ]; then
    pass "H: matching DDL/code pair -> gate PASS(0) (no false positive)"
else
    fail "H: matching pair did NOT PASS (got exit $rc, expected 0)"
fi

# --- I. empty doc root -> REFUSE(2), never a pass ---------------------------
EMPTY="$WORK/empty"; mkdir -p "$EMPTY"
rc="$(run_gate "$EMPTY")"
if [ "$rc" -eq 2 ]; then
    pass "I: empty scan -> gate REFUSE(2) (empty enumeration is not a clean result)"
else
    fail "I: empty scan did NOT REFUSE (got exit $rc, expected 2) — a false PASS on nothing"
fi

# --- K. TAB-indented INSERT paren-list extraction (7o2vuu regression) --------
# The real SqlServer write path (InsertOutboxMessageRequest.cs) wraps the INSERT
# column list onto TAB-indented continuation lines. A grep bracket of [ \t] does
# NOT match a literal TAB (it matches space, backslash, and 't'), so the extractor
# read 0 columns -> REFUSE on the real dbo.OutboxMessages table. The prior fixtures
# were all SPACE-indented, which masked it (SA guard-3: fixtures mask reality).
# This arm drives a genuinely TAB-indented INSERT fixture + a matching DDL and
# asserts PASS(0): RED (REFUSE 2, zero cols) on the pre-fix [ \t] gate, GREEN on
# [[:space:]]. printf embeds real tab characters (\t).
KFX="$WORK/kfx"; mkdir -p "$KFX/docs" "$KFX/src"
printf 'var sql = $"""\n\tINSERT INTO {t}\n\t\t(seq_id, payload)\n\tVALUES (@SeqId, @Payload)\n\t""";\n' > "$KFX/src/ins.cs"
printf 'k_outbox|%s/**|t|tab-indented insert fixture\n' "$KFX/src" > "$KFX/map"
cat > "$KFX/docs/schema.md" <<'EOF'
```sql
CREATE TABLE k_outbox (
    seq_id  BIGINT NOT NULL,
    payload TEXT   NOT NULL
);
```
EOF
rc="$(SHIPPED_DDL_DOC_ROOTS="$KFX/docs" SHIPPED_DDL_SRC_ROOTS="$KFX/src" \
      SHIPPED_DDL_MAP_FILE="$KFX/map" SHIPPED_DDL_MIN_COLS=1 SHIPPED_DDL_REPO_ROOT="$PWD" \
      bash "$GATE" --sweep >/dev/null 2>&1; echo $?)"
if [ "$rc" -eq 0 ]; then
    pass "K: TAB-indented INSERT paren-list extracted -> PASS(0) (7o2vuu: [[:space:]] matches real tabs; [ \\t] did not)"
else
    fail "K: TAB-indented INSERT not extracted (got exit $rc, expected 0) — the [ \\t] tab-class regression"
fi

# --- L. DELIMITED identifiers: bracketed T-SQL + quoted Postgres (48opzo) ----
# The table-name extractor used a BARE-identifier class ([A-Za-z_][A-Za-z0-9_.]*),
# which cannot match a delimited identifier. Two shipped dialects are affected:
#   T-SQL      CREATE TABLE [EventSourcing].[Events]
#   Postgres   CREATE TABLE "public"."snapshots"
# The failure was not a clean miss. On `CREATE TABLE IF NOT EXISTS [dbo].[X]` the
# optional IF-NOT-EXISTS group stops participating and the literal `IF` is captured
# as the table name — so the gate reported a PHANTOM table and never checked the
# real one. Either way the event-store and snapshot DDLs were invisible: no PASS,
# no REFUSE, no signal at all — the one outcome a 3-state gate must never produce.
#
# BOTH ARMS, deliberately: a fix that made the gate refuse everything would satisfy
# the safety arm alone (testing-patterns §3).
#   L1 SAFETY   bracketed DDL missing a written column -> FAIL(1)
#   L2 LIVENESS bracketed DDL matching the write path  -> PASS(0)
LFX="$WORK/lfx"; mkdir -p "$LFX/docs" "$LFX/src"
printf 'var sql = $"""\n    INSERT INTO {t} (event_id, payload, occurred_at)\n    VALUES (@EventId, @Payload, @OccurredAt)\n    """;\n' > "$LFX/src/ins.cs"
printf 'eventsourcing[.]events|%s/**|t|bracketed T-SQL fixture\n' "$LFX/src" > "$LFX/map"

run_lgate() {
    SHIPPED_DDL_DOC_ROOTS="$LFX/docs" SHIPPED_DDL_SRC_ROOTS="$LFX/src" \
      SHIPPED_DDL_MAP_FILE="$LFX/map" SHIPPED_DDL_MIN_COLS=1 SHIPPED_DDL_REPO_ROOT="$PWD" \
      bash "$GATE" --sweep >/dev/null 2>&1
    echo $?
}

# L1 — SAFETY: bracketed DDL omits occurred_at, which the write path INSERTs.
cat > "$LFX/docs/schema.md" <<'EOF'
```sql
CREATE TABLE [EventSourcing].[Events] (
    event_id BIGINT NOT NULL,
    payload  NVARCHAR(MAX) NOT NULL
);
```
EOF
rc="$(run_lgate)"
if [ "$rc" -eq 1 ]; then
    pass "L1: bracketed T-SQL divergence -> gate FAIL(1) (48opzo: delimited identifiers are no longer invisible)"
else
    fail "L1: bracketed T-SQL divergence NOT caught (got exit $rc, expected 1) — the 48opzo blind spot"
fi

# L2 — LIVENESS: same bracketed table, DDL now matches the write path.
cat > "$LFX/docs/schema.md" <<'EOF'
```sql
CREATE TABLE [EventSourcing].[Events] (
    event_id    BIGINT NOT NULL,
    payload     NVARCHAR(MAX) NOT NULL,
    occurred_at DATETIMEOFFSET NOT NULL
);
```
EOF
rc="$(run_lgate)"
if [ "$rc" -eq 0 ]; then
    pass "L2: bracketed T-SQL matching pair -> gate PASS(0) (liveness: the fix does not refuse everything)"
else
    fail "L2: bracketed T-SQL matching pair did NOT PASS (got exit $rc, expected 0)"
fi

# --- M. NOT-APPLICABLE: the declared fourth state (1mquo5) -------------------
# Not every shipped CREATE TABLE is a promise about framework-owned schema; a sample's
# own fixture has no framework write path to diff against. The exemption channel is only
# safe if it CANNOT become a suppression list, so all four arms are locked -- the two
# permissive ones AND the two abuses that would hollow the gate out:
#   M1 default    an UNdeclared table still REFUSEs               (default unchanged)
#   M2 liveness   a declared table passes AND is printed          (usable, and visible)
#   M3 abuse      declared with no reason -> REFUSE               (reason is mandatory)
#   M4 abuse      declare EVERYTHING -> REFUSE                    (evaluated nothing)
# M4 is the important one: without it, exempting every table buys a green verdict having
# compared nothing -- a gate that passes by measuring less, which is this file's own
# cry-wolf failure wearing the opposite mask.
MFX="$WORK/mfx"; mkdir -p "$MFX/docs" "$MFX/src"
printf 'var sql = $"""\n    INSERT INTO {t} (a, b)\n    VALUES (@A, @B)\n    """;\n' > "$MFX/src/w.cs"
cat > "$MFX/docs/s.md" <<'EOF'
```sql
CREATE TABLE real_tbl (
    a INT NOT NULL,
    b INT NOT NULL
);
CREATE TABLE sample_fixture (
    x INT NOT NULL
);
```
EOF
run_mgate() {
    SHIPPED_DDL_DOC_ROOTS="$MFX/docs" SHIPPED_DDL_SRC_ROOTS="$MFX/src" \
      SHIPPED_DDL_MAP_FILE="$MFX/map" SHIPPED_DDL_MIN_COLS=1 SHIPPED_DDL_REPO_ROOT="$PWD" \
      bash "$GATE" --sweep 2>&1
}

printf 'real_tbl|%s/**|t|real\n' "$MFX/src" > "$MFX/map"
rc="$(run_mgate >/dev/null 2>&1; echo $?)"
if [ "$rc" -eq 2 ]; then
    pass "M1: UNdeclared shipped table -> REFUSE(2) (the default is unchanged; silence still fails)"
else
    fail "M1: undeclared table did NOT REFUSE (got exit $rc, expected 2)"
fi

printf 'real_tbl|%s/**|t|real\nsample_fixture|NOT-APPLICABLE|-|sample fixture, framework never writes it\n' "$MFX/src" > "$MFX/map"
out="$(run_mgate)"; rc=$?
if [ "$rc" -eq 0 ] && printf '%s' "$out" | grep -q 'n/a   sample_fixture'; then
    pass "M2: declared NOT-APPLICABLE -> PASS(0) and the declaration is PRINTED (never silent)"
else
    fail "M2: declared NOT-APPLICABLE not honoured or not printed (exit $rc)"
fi

printf 'real_tbl|%s/**|t|real\nsample_fixture|NOT-APPLICABLE|-|\n' "$MFX/src" > "$MFX/map"
rc="$(run_mgate >/dev/null 2>&1; echo $?)"
if [ "$rc" -eq 2 ]; then
    pass "M3: NOT-APPLICABLE with no reason -> REFUSE(2) (an unargued exemption is a suppression)"
else
    fail "M3: reasonless exemption was ACCEPTED (got exit $rc, expected 2) — the suppression hole"
fi

printf 'real_tbl|NOT-APPLICABLE|-|nope\nsample_fixture|NOT-APPLICABLE|-|nope\n' > "$MFX/map"
rc="$(run_mgate >/dev/null 2>&1; echo $?)"
if [ "$rc" -eq 2 ]; then
    pass "M4: ALL tables declared NOT-APPLICABLE -> REFUSE(2) (a gate that compared nothing has not passed)"
else
    fail "M4: blanket exemption bought a PASS (got exit $rc, expected 2) — the gate is hollow"
fi

# --- Static guards on the gate source (SA seam guards must not regress) -----
if grep -Eq '^E_PASS=0; E_FAIL=1; E_REFUSE=2' "$GATE"; then
    pass "D: gate declares the 3-state exit contract (E_PASS=0 / E_FAIL=1 / E_REFUSE=2)"
else
    fail "D: gate 3-state exit contract missing or altered"
fi

if grep -q 'SHIPPED_DDL_MAP_FILE' "$GATE" && grep -q 'SHIPPED_DDL_DOC_ROOTS' "$GATE"; then
    pass "E: gate exposes the testability seam (SHIPPED_DDL_MAP_FILE + SHIPPED_DDL_DOC_ROOTS)"
else
    fail "E: gate missing the env-override testability seam"
fi

# Strip comments before the negative grep: the gate DOCUMENTS "deliberately NO suppression cap"
# (naming f5-sweep's F5_MAX_HITS_PER_TOKEN in prose), so an unstripped grep matches the gate's own
# explanation of why it has no cap — the self-referential-scanner trap. Assert no cap in CODE.
if grep -q 'MIN_WRITTEN_COLS' "$GATE" && ! sed -E 's/#.*$//' "$GATE" | grep -qE 'MAX_HITS|SUPPRESS'; then
    pass "F: gate has the non-vacuity floor (MIN_WRITTEN_COLS) and NO suppression cap in code (fmvdpg class)"
else
    fail "F: gate missing MIN_WRITTEN_COLS floor, or introduced a suppression cap (in code) that can mute a verdict"
fi

if grep -Eq 'SHIPPED_DDL_DOC_ROOTS:-docs-site samples' "$GATE"; then
    pass "J: guard-1 — production default roots are the real surface (docs-site samples), inert override"
else
    fail "J: guard-1 regressed — production default roots are not 'docs-site samples'"
fi

echo ""
if [ "$FAILURES" -eq 0 ]; then
    echo "✅ shipped-ddl-sweep.test.sh: ALL GREEN"
    exit 0
fi
echo "❌ shipped-ddl-sweep.test.sh: $FAILURES lock(s) FAILED" >&2
exit 1
