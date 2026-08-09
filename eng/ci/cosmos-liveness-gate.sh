#!/usr/bin/env bash
# Cosmos LIVENESS gate — proves the emulator was REACHED, and refuses a run that reported success
# while executing nothing against it.
#
# ⚠ WHY THIS GATE CANNOT BE KEYED ON COUNTS, STATED FIRST BECAUSE IT IS THE WHOLE DESIGN.
#
# A dynamic in-test skip is recorded by the runner as an EXECUTED, PASSING test. Measured on the
# Cosmos telemetry class: a blanket skip fires for all 14 tests and the result file reads
# total=14 executed=14 passed=14 notExecuted=0. So every counter a gate could compare — executed
# against expected, passed against total, notExecuted against zero — is SATISFIED by a run that
# touched no emulator. An accounting gate is vacuous here BY CONSTRUCTION, not by an error in its
# arithmetic, and no amount of tightening the comparison repairs that.
#
# This gate therefore keys on POSITIVE EVIDENCE that a test genuinely reached the emulator:
# execution records the fixtures append, one per genuinely-executed test, to the file named by the
# environment. A SKIPPED TEST WRITES NOTHING. Absence of records is the signal; it is the one thing
# a skip cannot fake, and it is the exact inverse of a counter, which a skip fakes perfectly.
#
# SCOPE, HONESTLY. This proves records of the required SHAPE are PRESENT in the quantity required.
# It does NOT re-verify the round-trip that produced them — if the fixtures were ever changed to
# append a record unconditionally, this gate would pass. That property is owned by the test side and
# by the fixture-pattern gate; the guarantee here is narrower and must not be cited as wider.
#
# THE SCOPE ARGUMENT IS REQUIRED, AND THAT IS THE ZERO-GUARD.
# "No Cosmos class was admitted on this path" and "Cosmos classes were admitted and produced
# nothing" are DIFFERENT states with opposite verdicts, and a gate that returned 0 for both would be
# passing because there was nothing to check — the same defect it exists to catch, wearing the
# gate's own uniform. So the caller states which path it is on and the gate asserts accordingly:
#
#   in   Cosmos classes ARE admitted here. Evidence is REQUIRED; its absence is a REFUSE.
#   out  Cosmos classes are deliberately NOT admitted here (the pull-request path, where the
#        emulator suite is excluded and runs nightly instead). Evidence must be ABSENT; records
#        appearing here mean an emulator class drifted onto a path declared emulator-free.
#
# The gate is not switched off under 'out' — it is INVERTED, so it still fires. Both directions are
# asserted, so neither scope can be used to make the gate say nothing.
#
# Exit codes are deliberately distinct so a REFUSE is never read as an ordinary test failure:
#   0  the emulator was genuinely reached (scope in) / genuinely untouched as required (scope out)
#   1  usage or environment error — bad arguments, unreadable input
#   2  --self-test failed (the gate itself is broken or vacuous)
#   3  REFUSE — the run reported success while producing no evidence of reaching the emulator
#
# On 1-versus-3 for a bad argument: the two sibling Cosmos scripts have no usage-error code at all
# and spend 3 on it. This one defines 1 for that, so a malformed invocation is distinguishable from
# a measured refusal. Both are non-zero and neither can be mistaken for a pass; what changes is
# whether the log sends someone to look at the emulator or at the caller.
set -uo pipefail

# A genuine execution record carries a UTC timestamp AND a class name. BOTH halves are load-bearing
# and each is proven so by its own self-test arm:
#
#   a timestamp alone  — a bare clock reading is not a record of anything having run
#   a class name alone — a mention of the class is not a record of it having executed
#
# Everything else on the line is ignored on purpose. The producing side is free to add fields, and a
# parser that broke on an unrecognised one would turn an improvement to the evidence into a red gate.
EVIDENCE_TS_ERE='[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}([.,][0-9]+)?Z?'

refuse() { echo "::error::REFUSE — $*" >&2; exit 3; }
usage_error() { echo "::error::cosmos-liveness-gate: $*" >&2; usage >&2; exit 1; }

usage() {
    cat <<'USAGE'
Usage: cosmos-liveness-gate.sh <evidence-file> <emulator-scope> [expected-min]
       cosmos-liveness-gate.sh --self-test
       cosmos-liveness-gate.sh --help

  <evidence-file>   the path the test run was told to append execution records to.
  <emulator-scope>  in  — Cosmos classes WERE admitted here; evidence is REQUIRED.
                    out — Cosmos classes were NOT admitted here; evidence must be ABSENT.
                    Required, never defaulted: the two scopes have opposite verdicts, and guessing
                    would answer a question the caller did not ask.
  [expected-min]    minimum genuine records required under scope 'in' (default 1; must be >= 1,
                    because a minimum of zero is satisfied by an empty file).

Exit: 0 pass | 1 usage/environment error | 2 self-test failed | 3 REFUSE
USAGE
}

# count_records <file> — prints "<valid> <malformed>".
#
# valid     lines carrying both halves of a record.
# malformed lines that are neither blank nor a comment and are NOT a record. Counted rather than
#           ignored: a file full of unrecognised content is not a clean bill of health, and silently
#           discarding it would let a producer that writes the wrong shape read as a producer that
#           wrote nothing — which is the same verdict here but for the wrong reason, and the reason
#           is what a person reading the log needs.
count_records() {
    local f="$1" body total valid
    body="$(grep -vE '^[[:space:]]*($|#)' "$f" 2>/dev/null)"
    total="$(printf '%s\n' "$body" | grep -c . || true)"
    # Filter to lines bearing a timestamp, REMOVE the timestamp, then require a name-shaped token in
    # what is left. Stripping first is what makes the two halves independent: a bare timestamp
    # contains the letter T and a Z, so a naive "line has letters" test would accept it as carrying
    # a class name and the second half of the requirement would quietly become the first half twice.
    valid="$(printf '%s\n' "$body" \
        | grep -E "$EVIDENCE_TS_ERE" \
        | sed -E "s/$EVIDENCE_TS_ERE//g" \
        | grep -cE '[A-Za-z]{3,}' || true)"
    total="${total:-0}"; valid="${valid:-0}"
    printf '%s %s' "$valid" "$(( total - valid ))"
}

# --- self-test -------------------------------------------------------------------------------------
# Every arm invokes the SHIPPING entry point (bash "$0" ...) rather than a helper function, so the
# arms grade the code that runs in CI, including its argument handling and its exit codes. An arm
# that exercised a copy of the logic would be the mutation proof that does not mutate.
#
# Hermetic: temp fixtures only. No container runtime, no network, no credentials.
if [ "${1:-}" = "--self-test" ]; then
    st_tmp="$(mktemp -d)" || { echo "cosmos-liveness-gate self-test: cannot create a temp dir" >&2; exit 2; }
    trap 'rm -rf "$st_tmp"' EXIT
    st_ran=0; st_verdict=0; st_fail=0
    st_out="$st_tmp/arm.out"

    # arm <label> <expected-exit> <must-contain|-> <gate args...>
    arm() {
        local label="$1" want="$2" needle="$3"; shift 3
        st_ran=$((st_ran + 1))
        bash "$0" "$@" >"$st_out" 2>&1
        local rc=$?
        # 126/127 mean the gate could not be EXECUTED at all — an unreadable or absent script. That
        # arm RAN and reached NO verdict, and the tally must say so: a battery that reaches green by
        # failing to run its subject prints the same zero as a healthy one.
        if [ "$rc" -eq 126 ] || [ "$rc" -eq 127 ]; then
            printf '  [FAIL] %s — the gate could not be EXECUTED (exit %s); this arm reached NO verdict\n' "$label" "$rc" >&2
            st_fail=$((st_fail + 1)); return
        fi
        st_verdict=$((st_verdict + 1))
        if [ "$rc" -ne "$want" ]; then
            printf '  [FAIL] %s — expected exit %s, got %s\n' "$label" "$want" "$rc" >&2
            sed 's/^/         /' "$st_out" >&2
            st_fail=$((st_fail + 1)); return
        fi
        if [ "$needle" != "-" ] && ! grep -qF -- "$needle" "$st_out"; then
            printf '  [FAIL] %s — exit %s was correct but the message never says "%s"\n' "$label" "$rc" "$needle" >&2
            sed 's/^/         /' "$st_out" >&2
            st_fail=$((st_fail + 1)); return
        fi
        printf '  [PASS] %s (exit %s)\n' "$label" "$rc"
    }

    # ── fixtures ────────────────────────────────────────────────────────────────────────────────
    good="$st_tmp/good.txt"
    cat >"$good" <<'EOF'
PlantedEmulatorShould.FirstProbe 2026-01-01T00:00:00Z emulator=reached
PlantedEmulatorShould.SecondProbe 2026-01-01T00:00:01Z emulator=reached
PlantedEmulatorShould.ThirdProbe 2026-01-01T00:00:02.500Z emulator=reached endpoint=loopback note=unknown-future-field
EOF

    extras="$st_tmp/extras.txt"
    printf '%s\n' 'PlantedEmulatorShould.OnlyProbe 2026-01-01T00:00:00Z a=1 b=2 c=3 d=4 e=5' >"$extras"

    empty="$st_tmp/empty.txt"; : >"$empty"
    absent="$st_tmp/never-written.txt"

    malformed="$st_tmp/malformed.txt"
    printf '%s\n' 'not a record at all' '<<<truncated' '{"partial":' >"$malformed"

    ts_only="$st_tmp/timestamp-only.txt"
    printf '%s\n' '2026-01-01T00:00:00Z' >"$ts_only"

    class_only="$st_tmp/class-only.txt"
    printf '%s\n' 'PlantedEmulatorShould.FirstProbe' >"$class_only"

    # PERMANENT FIXTURE — the pre-fix shape, kept forever.
    #
    # This is what the defect looked like: the runner reported a complete, entirely passing run while
    # every test had skipped, so the only artifact left behind was a success summary and not one
    # execution record. It must be REJECTED. If a future edit ever weakens the record shape to
    # "any non-blank line", this arm goes red before any other.
    reported_pass="$st_tmp/reported-pass-executed-nothing.txt"
    cat >"$reported_pass" <<'EOF'
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 2 s
EOF

    echo "cosmos-liveness-gate --self-test"

    # ── SAFETY: scope 'in' — the emulator was admitted, so silence is a refusal ─────────────────
    arm "safety:   no evidence file at all -> REFUSE"                3 "REFUSE" "$absent"        in
    arm "safety:   an empty evidence file -> REFUSE"                 3 "REFUSE" "$empty"         in
    arm "safety:   only unrecognised content -> REFUSE"              3 "REFUSE" "$malformed"     in
    arm "safety:   fewer records than required -> REFUSE"            3 "REFUSE" "$good"          in 5
    arm "safety:   PERMANENT FIXTURE — reported passing, executed nothing -> REFUSE" \
                                                                     3 "REFUSE" "$reported_pass" in
    arm "safety:   a timestamp with no class name is not a record"   3 "REFUSE" "$ts_only"       in
    arm "safety:   a class name with no timestamp is not a record"   3 "REFUSE" "$class_only"    in

    # ── SAFETY: scope 'out' — the inversion. Records here mean a class drifted onto a path that
    #    declares itself emulator-free, and that must fire rather than be waved through.
    arm "safety:   records present on an emulator-free path -> REFUSE" 3 "REFUSE" "$good"        out

    # ── SAFETY: the caller must state the scope, and a minimum of zero is refused ───────────────
    arm "safety:   no arguments -> usage error, never a pass"        1 "-"
    arm "safety:   an omitted scope is never defaulted"              1 "-"       "$good"
    arm "safety:   an unknown scope is refused, never defaulted"     1 "-"       "$good" sideways
    arm "safety:   a minimum of zero is refused (it accepts nothing)" 1 "-"      "$good" in 0
    arm "safety:   a non-numeric minimum is refused"                 1 "-"       "$good" in many

    # ── LIVENESS: the gate must be able to PASS. Without these arms a gate that refuses every input
    #    satisfies all eight safety arms above and is exactly as broken as one that passes everything
    #    — and it is the direction nobody thinks to check.
    arm "liveness: genuine records -> PASS"                          0 "LIVENESS OK" "$good"   in
    arm "liveness: genuine records meeting an explicit minimum -> PASS" 0 "LIVENESS OK" "$good" in 3
    arm "liveness: unknown extra fields do not break parsing -> PASS" 0 "LIVENESS OK" "$extras" in
    arm "liveness: scope out with no evidence file -> PASS"          0 "-"       "$absent"      out
    arm "liveness: scope out with an empty evidence file -> PASS"    0 "-"       "$empty"       out

    echo
    printf 'cosmos-liveness-gate self-test: %s arm(s) EXECUTED, %s reached a VERDICT, %s FAILED.\n' \
        "$st_ran" "$st_verdict" "$st_fail"
    # Three conditions, because the failure count alone cannot tell a healthy battery from an empty
    # one: nothing failed, every arm that ran actually reached a verdict, and at least one ran.
    if [ "$st_fail" -eq 0 ] && [ "$st_verdict" -eq "$st_ran" ] && [ "$st_ran" -gt 0 ]; then
        echo "SELF-TEST PASS (safety + liveness, non-vacuous)"
        exit 0
    fi
    echo "SELF-TEST FAIL" >&2
    exit 2
fi

# --- real run ---------------------------------------------------------------------------------------
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
esac

EVIDENCE="${1:-}"
SCOPE="${2:-}"
MIN="${3:-1}"

[ -n "$EVIDENCE" ] || usage_error "missing <evidence-file>."
[ -n "$SCOPE" ]    || usage_error "missing <emulator-scope>. It is never defaulted — 'in' and 'out' have opposite verdicts."
case "$SCOPE" in
    in|out) ;;
    *) usage_error "unknown emulator scope '$SCOPE' (expected 'in' or 'out')." ;;
esac
case "$MIN" in
    ''|*[!0-9]*) usage_error "expected-min must be a whole number, got '$MIN'." ;;
esac
# A minimum of zero would be satisfied by an empty file, which is the exact state this gate exists to
# refuse. Rejecting it at the argument keeps the gate from being disarmed by its own knob.
[ "$MIN" -ge 1 ] || usage_error "expected-min must be at least 1 (a minimum of zero is satisfied by an empty file)."

present=0
if [ -e "$EVIDENCE" ]; then
    # An existing-but-unusable path is an ENVIRONMENT problem, and it is a different condition from
    # "nothing was written". Reporting it as a refusal would send someone to investigate a test suite
    # when the fault is a permission or a directory in the way.
    [ -f "$EVIDENCE" ] || usage_error "'$EVIDENCE' exists but is not a regular file."
    [ -r "$EVIDENCE" ] || usage_error "'$EVIDENCE' exists but is not readable."
    present=1
fi

valid=0
malformed=0
if [ "$present" -eq 1 ]; then
    read -r valid malformed <<<"$(count_records "$EVIDENCE")"
fi

echo "evidence file      = $EVIDENCE ($([ "$present" -eq 1 ] && echo present || echo absent))"
echo "emulator scope     = $SCOPE"
echo "genuine records    = $valid"
echo "unrecognised lines = $malformed"

if [ "$SCOPE" = "out" ]; then
    if [ "$valid" -gt 0 ]; then
        echo "::error::the first few records:" >&2
        head -5 "$EVIDENCE" | sed 's/^/    /' >&2
        refuse "$valid execution record(s) were produced on a path declared emulator-free. A class that starts an emulator has drifted onto this path — exclude it by trait so the filter can leave it out, or move it to the job that provisions an emulator."
    fi
    if [ "$malformed" -gt 0 ]; then
        echo "::warning::$malformed unrecognised line(s) in the evidence file on an emulator-free path. No execution record among them, so this is not a refusal — but nothing should be writing there at all."
    fi
    echo "LIVENESS OK: no Cosmos class reached an emulator on a path declared emulator-free, which is the required outcome here."
    echo "NOT PROVEN by this step: that the Cosmos suite passes anywhere. It does not run on this path by design; the job that provisions an emulator owns that claim."
    exit 0
fi

# scope 'in' — the emulator was admitted, so silence is the defect.
if [ "$present" -eq 0 ]; then
    refuse "no evidence file was produced at all ($EVIDENCE). Cosmos classes were admitted on this path, so a run that reports success while leaving no record of reaching the emulator executed nothing against it — which a passing test count cannot distinguish, and is precisely why this check does not read one."
fi

if [ "$valid" -eq 0 ]; then
    if [ "$malformed" -gt 0 ]; then
        echo "::error::the first few unrecognised lines:" >&2
        head -5 "$EVIDENCE" | sed 's/^/    /' >&2
        refuse "the evidence file holds $malformed line(s) and NOT ONE is an execution record (a record carries a UTC timestamp and a class name). The run reported success while producing no evidence of reaching the emulator."
    fi
    refuse "the evidence file exists and is empty. Every admitted Cosmos test skipped, or none ran: the run reported success while executing nothing against the emulator."
fi

if [ "$valid" -lt "$MIN" ]; then
    refuse "only $valid execution record(s), fewer than the $MIN required. Part of the admitted Cosmos suite reported success without reaching the emulator."
fi

if [ "$malformed" -gt 0 ]; then
    echo "::warning::$malformed line(s) in the evidence file are not execution records. The $valid genuine record(s) satisfy this gate, but something is writing content that was not expected."
fi

echo "LIVENESS OK: $valid execution record(s) prove the emulator was genuinely reached (minimum required: $MIN)."
echo "NOT PROVEN by this step: that those records describe a successful round trip, or that every admitted test ran. This asserts the emulator was reached, not that the suite is complete."
exit 0

# Unreachable by construction — both scopes above exit. It exists so that no future edit can create a
# path that falls off the end of this script, where the shell would return the status of the last
# command run and could hand back a zero nobody decided on.
echo "::error::cosmos-liveness-gate: reached an unhandled path; refusing rather than reporting a pass it did not earn." >&2
exit 1
