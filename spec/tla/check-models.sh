#!/usr/bin/env bash
# Run every TLA+ model, CHECK THE ANSWER, and report what the check is worth.
#
# Three states, and REFUSE is not a pass:
#   0  PASS    every arm gave the answer it must give
#   1  FAIL    an arm gave the wrong answer -- a model regressed, or stopped being able to fail
#   2  REFUSE  the toolchain is absent, so NOTHING was measured. Do not read this as clean.
#
# WHY THERE ARE ARMS THAT MUST FAIL. A model that only ever passes has not been shown capable of
# failing, and our ladder names "a TLA+ model whose invariant is implied by its own type constraints"
# as an artifact that discharges nothing. So the harness fails if a red arm turns green.
#
# WHY THE FOUR-WAY DECOMPOSITION. The event-store model has two INDEPENDENT mechanisms -- whether the
# counter row's lock is held to commit, and whether the increment rolls back with the transaction. An
# earlier version folded both into one flag, so no arm isolated either, and the counterexample it
# produced was about a different mechanism than the design document argues for. Each arm below fails
# for its own reason, and the (FALSE, TRUE) arm is the one that justifies InvUnique existing at all:
# InvariantJ reports that design CLEAN while two writers commit the same position.
#
# NOT WIRED TO CI, deliberately, and said out loud so nobody reads it as protection we have. CI has no
# Java runtime; wiring this would advertise a check that cannot run. Manual / pre-alpha instrument. If
# Java is added to CI, wire it in the same commit.
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

JAVA="${TLA_JAVA:-}"
if [ -z "$JAVA" ]; then
    JAVA="$(find /c/Users/*/tools/tla -name java.exe -type f 2>/dev/null | head -1)"
fi
JAR="${TLA_JAR:-$(ls /c/Users/*/tools/tla/tla2tools.jar 2>/dev/null | head -1)}"

if [ -z "$JAVA" ] || [ ! -x "$JAVA" ] || [ -z "$JAR" ] || [ ! -f "$JAR" ]; then
    echo "REFUSE: no TLA+ toolchain found (java='$JAVA' jar='$JAR')."
    echo "        NOTHING WAS CHECKED. This is not a pass."
    exit 2
fi

ARTIFACT_ROOT="${TLA_RUN_ARTIFACTS:-${TMPDIR:-/tmp}}"
if [ ! -d "$ARTIFACT_ROOT" ]; then
    echo "REFUSE: TLC artifact root is not an existing directory: '$ARTIFACT_ROOT'."
    exit 2
fi

TLC() {
    local metadir rc
    metadir="$(mktemp -d "$ARTIFACT_ROOT/tlc-arm.XXXXXX")" || return 2
    # TLC's default directory uses second-resolution timestamps. Independent fast arms can
    # otherwise collide and fail before checking an invariant. Retain each invocation's evidence.
    "$JAVA" -XX:+UseParallelGC -cp "$JAR" tlc2.TLC -deadlock -metadir "$metadir" "$@" 2>&1
    rc=$?
    printf 'TLC artifacts retained: %s\n' "$metadir" >&2
    return "$rc"
}

# Build a configured arm of the event-store model. Keeping the arms generated rather than committed
# means the four cannot drift apart from the module they are meant to be configurations of.
mkarm() { # name serialised rollsback writers
    cp "$HERE/EventStoreGlobalPosition.tla" "$HERE/$1.tla"
    sed -i "s/MODULE EventStoreGlobalPosition/MODULE $1/" "$HERE/$1.tla"
    cat > "$HERE/$1.cfg" <<EOF
SPECIFICATION Spec
CONSTANTS
    Writers = $4
    MaxAppends = 4
    SerialisedToCommit = $2
    CounterRollsBack = $3
INVARIANT TypeOK
INVARIANT InvariantJ
INVARIANT InvUnique
INVARIANT InvA
EOF
}

# expect: "hold" | an invariant name that must be violated
run_arm() {
    local module="$1" expect="$2" label="$3" out rc viol
    out="$(TLC "$HERE/$module.tla")"; rc=$?

    case "$rc" in
        0|12) ;;
        *)  echo "FAIL  $label -- TLC exited $rc (neither 'no error' nor 'violated')."
            echo "$out" | tail -12; return 1 ;;
    esac

    # [A-Za-z0-9_]+, not [A-Za-z]+. The narrower class cannot match an invariant whose name contains
    # a digit -- InvS1 came back as "no violation" while TLC had exited 12 (violated), so a HEALTHY
    # red arm reported FAIL with a misleading reason. It failed in the safe direction, which is why it
    # went unnoticed: every invariant that existed when this was written happened to be letters only.
    viol="$(echo "$out" | grep -oE 'Invariant [A-Za-z0-9_]+ is violated' | head -1 | awk '{print $2}')"

    if [ "$expect" = "hold" ]; then
        if [ "$rc" -eq 0 ]; then echo "  ok  $label -- all invariants HOLD"; return 0; fi
        echo "FAIL  $label -- expected HOLD, TLC found a violation of ${viol:-?}:"
        echo "$out" | sed -n '/is violated/,$p' | head -20; return 1
    fi

    if [ "$viol" = "$expect" ]; then
        echo "  ok  $label -- $expect VIOLATED, as required"; return 0
    fi
    echo "FAIL  $label -- expected $expect to be violated; got '${viol:-no violation}' (exit $rc)."
    echo "      An arm that can no longer fail proves nothing, and every green result that leans on"
    echo "      it is now unsupported. Do not 'fix' this by deleting the arm."
    return 1
}

echo "TLA+ model check -- $(TLC 2>&1 | head -1)"
echo

fails=0
refused=0

echo "-- control: can this TLC invocation report a violation at all?"
run_arm _SelfTest NeverThree "_SelfTest (not a model of anything)" || fails=$((fails+1))

echo
echo "-- event store, global position: 2x2 over the two independent mechanisms"
mkarm _Arm_Ser_Roll     TRUE  TRUE  "{w1, w2, w3}"
mkarm _Arm_Ser_NoRoll   TRUE  FALSE "{w1, w2, w3}"
mkarm _Arm_NoSer_Roll   FALSE TRUE  "{w1, w2, w3}"
mkarm _Arm_NoSer_NoRoll FALSE FALSE "{w1, w2, w3}"

run_arm _Arm_Ser_Roll     hold       "(serialised, rolls back)  <- SHIPS"                || fails=$((fails+1))
run_arm _Arm_Ser_NoRoll   InvariantJ "(serialised, burns value) <- hole from an abort"   || fails=$((fails+1))
run_arm _Arm_NoSer_Roll   InvUnique  "(racing,     rolls back)  <- DUPLICATE position"   || fails=$((fails+1))
run_arm _Arm_NoSer_NoRoll InvariantJ "(racing,     burns value) <- hole from interleave" || fails=$((fails+1))

# ---------------------------------------------------------------------------
# ADEQUACY INSTRUMENT, not a pass/fail arm.
#
# A model in which the process count does not change the diameter has not modelled concurrency
# BETWEEN the processes -- the extra states are name permutations of the same behaviour. That is a
# mechanical tell for a whole class of near-vacuous models, and it is cheap, so it runs every time
# rather than living in a comment somebody stops reading.
# ---------------------------------------------------------------------------
echo
echo "-- adequacy: does the shipped arm actually exercise concurrency?"
diam_for() {
    local out rc diameter
    mkarm _Arm_Diam TRUE TRUE "$1"
    out="$(TLC "$HERE/_Arm_Diam.tla")"; rc=$?
    if [ "$rc" -ne 0 ]; then
        printf 'Diameter probe unavailable: TLC exited %s.\n%s\n' "$rc" "$out" >&2
        return 1
    fi
    diameter="$(printf '%s\n' "$out" | grep -oE 'depth of the complete state graph search is [0-9]+' | grep -oE '[0-9]+$')"
    if [ -z "$diameter" ]; then
        printf 'Diameter probe unavailable: TLC reported no diameter.\n%s\n' "$out" >&2
        return 1
    fi
    printf '%s\n' "$diameter"
}
d1="$(diam_for '{w1}')"; d1_rc=$?
d3="$(diam_for '{w1, w2, w3}')"; d3_rc=$?
echo "     diameter with 1 writer:  ${d1:-?}"
echo "     diameter with 3 writers: ${d3:-?}"
if [ "$d1_rc" -ne 0 ] || [ "$d3_rc" -ne 0 ]; then
    echo "     >> UNAVAILABLE. A failed probe is not evidence that concurrency was exercised."
    fails=$((fails+1))
elif [ "$d1" = "$d3" ]; then
    echo "     >> UNCHANGED. The shipped arm does NOT exercise concurrency between writers:"
    echo "        serialisation is GRANTED by the model, so three writers behave as one and the"
    echo "        extra states are name permutations. What this arm establishes is the implication"
    echo "        'given mutual exclusion, advancing the counter only at commit yields a prefix'."
    echo "        The premise does the work, and the premise is what the code could get wrong."
    echo "        DO NOT cite this arm as concurrency evidence, and do not claim R4 on it."
else
    echo "     >> diameter moved with the writer count; concurrency is being exercised."
fi

# ---------------------------------------------------------------------------
# KEY-DESTRUCTION LEDGER. Written at DESIGN time, before the implementation exists, which is the
# phase the ladder makes load-bearing: a model written during TEST restates the code.
#
# Each red arm below is a design the architect actually proposed and adversarial review broke. They
# are kept as arms rather than as prose because a design that was refuted once gets re-proposed, and
# an arm refuses it mechanically.
# ---------------------------------------------------------------------------
mklarm() { # name stagefirst recordall recordbeforedestroy
    cp "$HERE/KeyDestructionLedger.tla" "$HERE/$1.tla"
    sed -i "s/MODULE KeyDestructionLedger/MODULE $1/" "$HERE/$1.tla"
    cat > "$HERE/$1.cfg" <<EOF
SPECIFICATION Spec
CONSTANTS
    Gens = {g1, g2}
    Current = g2
    StageFirst = $2
    RecordAll = $3
    RecordBeforeDestroy = $4
INVARIANT TypeOK
INVARIANT InvS1
INVARIANT InvJ
INVARIANT InvRepairable
EOF
}

echo
echo "-- key-destruction ledger: the shipped design, and the three refuted ones"
mklarm _L_Ships       TRUE  TRUE  FALSE
mklarm _L_NoStage     FALSE TRUE  FALSE
mklarm _L_OnlyCurrent TRUE  FALSE FALSE
mklarm _L_RecordFirst TRUE  TRUE  TRUE

run_arm _L_Ships       hold           "(stage, destroy, record)   <- SHIPS"                      || fails=$((fails+1))
run_arm _L_NoStage     InvRepairable  "(destroy, record)          <- subject lost PERMANENTLY"    || fails=$((fails+1))
run_arm _L_OnlyCurrent InvRepairable  "(stage only current gen)   <- pre-rotation envelope lost"  || fails=$((fails+1))
run_arm _L_RecordFirst InvS1          "(record, destroy)          <- TOMBSTONE OVER LIVE MATERIAL" || fails=$((fails+1))

# ---------------------------------------------------------------------------
# SUBSCRIPTION ROSTER. The seam binds ONE roster row per PROJECTION, and the
# attestation "every member of this subscription is caught up" is honest only while
# the quantifier's domain is the roster read from the DURABLE STORE.
#
# WHY THE ARMS ARE SHAPED THIS WAY. Two hosts here INTERLEAVE at the grain the store
# actually provides: the per-member claim is one atomic statement (ON CONFLICT DO
# NOTHING on the composite key), but the read-back is a SECOND statement in no
# transaction, and the attestation is a whole page fold later still. None of that is
# granted -- which is why the arm modelling what SHIPS violates the strongest
# invariant in that module rather than printing a green over an assumed premise.
#
# READ SubscriptionRoster.tla's SCOPE AND LIMITS BLOCKS BEFORE CITING ANY ARM BELOW.
# Two of these arms are configurations the shipped code does NOT have.
# ---------------------------------------------------------------------------
mkrarm() { # name numhosts topology divergent local upsert relclr atomic reread token [invariants...]
    local name="$1" dir="$HERE/subscription-roster"
    cp "$dir/SubscriptionRoster.tla" "$dir/$name.tla"
    sed -i "s/MODULE SubscriptionRoster/MODULE $name/" "$dir/$name.tla"
    cat > "$dir/$name.cfg" <<EOF
SPECIFICATION Spec
CONSTANTS
    NumHosts = $2
    Topology = "$3"
    DivergentVersions = $4
    MaxPos = 2
    LocalQuantifier = $5
    UpsertBinding = $6
    ReleaseClearsSubscription = $7
    AtomicRosterRead = $8
    AllowReRead = $9
    TokenMode = "${10}"
    NoCursor = NoCursor
INVARIANT TypeOK
EOF
    shift 10
    local inv
    for inv in "$@"; do echo "INVARIANT $inv" >> "$dir/$name.cfg"; done
}

# WHY EACH ARM DECLARES ONLY THE INVARIANT IT ISOLATES. TLC reports the FIRST
# invariant violated in the first violating state, so declaring a weaker invariant
# alongside a stronger one lets the weaker one's shorter counterexample mask the arm's
# whole purpose. Measured: the local-quantifier arm with both attestation invariants
# declared reported InvNoFalseFresh, because the shortest path to a false attestation
# is the stale-read window and not the quantifier -- so the arm said nothing about the
# defect it exists to refute. The arms below are therefore one-variable-apart PAIRS,
# and the pairing is what makes each non-vacuous.
echo
echo "-- subscription roster: the quantifier, the claim, and the attestation window"
#      name                  NH topo    Div   Local Upsrt RelClr Atom  ReRd Token
mkrarm _SR_Ships         3 subset FALSE FALSE FALSE FALSE FALSE FALSE none \
       InvNoBlindFresh InvRosterStable InvSiblingProgress
mkrarm _SR_LocalQuant    3 subset FALSE TRUE  FALSE FALSE FALSE FALSE none \
       InvNoBlindFresh
mkrarm _SR_StaleWindow   3 subset FALSE FALSE FALSE FALSE FALSE FALSE none \
       InvNoFalseFresh
mkrarm _SR_ReReadNoHelp  3 subset FALSE FALSE FALSE FALSE FALSE TRUE  none \
       InvNoFalseFresh
mkrarm _SR_AtomicRead    3 subset FALSE FALSE FALSE FALSE TRUE  FALSE none \
       InvNoFalseFresh InvNoBlindFresh InvNoSilentFalseFresh InvRosterStable InvSiblingProgress
mkrarm _SR_NoToken       3 subset FALSE FALSE FALSE FALSE FALSE FALSE none \
       InvNoSilentFalseFresh
mkrarm _SR_Token         3 subset FALSE FALSE FALSE FALSE FALSE FALSE membership \
       InvNoSilentFalseFresh
mkrarm _SR_TokenLocal    3 subset FALSE TRUE  FALSE FALSE FALSE FALSE membership \
       InvNoSilentFalseFresh
mkrarm _SR_Upsert        3 subset TRUE  FALSE TRUE  FALSE FALSE FALSE none \
       InvRosterStable
mkrarm _SR_ReleaseClears 3 subset FALSE FALSE FALSE TRUE  FALSE FALSE none \
       InvSiblingProgress

run_arm subscription-roster/_SR_Ships hold \
  "(store-sourced domain)        <- HISTORICAL baseline guarantee"            || fails=$((fails+1))
run_arm subscription-roster/_SR_LocalQuant InvNoBlindFresh \
  "(LOCAL domain)                <- read the member, ignored it. THE DEFECT"      || fails=$((fails+1))
run_arm subscription-roster/_SR_StaleWindow InvNoFalseFresh \
  "(store-sourced, stale read)   <- HISTORICAL shape: overtaken between read+attest" || fails=$((fails+1))
run_arm subscription-roster/_SR_ReReadNoHelp InvNoFalseFresh \
  "(re-read before attesting)    <- a FRESHER read MOVES the window, not closes"  || fails=$((fails+1))
run_arm subscription-roster/_SR_AtomicRead hold \
  "(read ATOMIC with attest)     <- the premise the code does NOT have"           || fails=$((fails+1))
run_arm subscription-roster/_SR_NoToken InvNoSilentFalseFresh \
  "(no roster token)             <- the false attestation is SILENT"              || fails=$((fails+1))
run_arm subscription-roster/_SR_Token hold \
  "(roster snapshot carried)     <- staleness always WITNESSED. The lead."        || fails=$((fails+1))
run_arm subscription-roster/_SR_TokenLocal InvNoSilentFalseFresh \
  "(token + LOCAL domain)        <- a token does NOT rescue the quantifier"       || fails=$((fails+1))
run_arm subscription-roster/_SR_Upsert InvRosterStable \
  "(bind UPSERTS)                <- roster describes the LAST WRITER"             || fails=$((fails+1))
run_arm subscription-roster/_SR_ReleaseClears InvSiblingProgress \
  "(release clears SUBSCRIPTION) <- every sibling's progress destroyed"           || fails=$((fails+1))

# ---------------------------------------------------------------------------
# ADEQUACY INSTRUMENT for the roster model. Same question the event-store probe asks,
# plus the confound that probe cannot control for: there, adding a writer adds nothing
# but name permutations. Here, adding a HOST would also add KEYS it is the only host
# able to bind, so a diameter change could be bought by coverage rather than by
# concurrency.
#
# So the primary probe holds KEY COVERAGE CONSTANT (Topology = "all", every host hosts
# every key) and varies only the host count. A diameter that moves there is
# attributable to interleaving between hosts and nothing else. The shipped topology is
# reported second, labelled confounded, because it is what the arms above actually run.
# ---------------------------------------------------------------------------
echo
echo "-- adequacy: do the roster arms exercise concurrency BETWEEN hosts?"
rdiam_for() { # numhosts topology
    local out rc diameter
    mkrarm _SR_Diam "$1" "$2" FALSE FALSE FALSE FALSE FALSE FALSE none InvNoBlindFresh
    out="$(TLC "$HERE/subscription-roster/_SR_Diam.tla")"; rc=$?
    if [ "$rc" -ne 0 ]; then
        printf 'Roster diameter probe unavailable: TLC exited %s.\n%s\n' "$rc" "$out" >&2
        return 1
    fi
    diameter="$(printf '%s\n' "$out" | grep -oE 'depth of the complete state graph search is [0-9]+' | grep -oE '[0-9]+$')"
    if [ -z "$diameter" ]; then
        printf 'Roster diameter probe unavailable: TLC reported no diameter.\n%s\n' "$out" >&2
        return 1
    fi
    printf '%s\n' "$diameter"
}
ra1="$(rdiam_for 1 all)";    ra1_rc=$?
ra3="$(rdiam_for 3 all)";    ra3_rc=$?
rs1="$(rdiam_for 1 subset)"; rs1_rc=$?
rs3="$(rdiam_for 3 subset)"; rs3_rc=$?
echo "     COVERAGE-CONTROLLED (every host hosts every key; only the host count varies)"
echo "       diameter with 1 host:  ${ra1:-?}"
echo "       diameter with 3 hosts: ${ra3:-?}"
echo "     SHIPPED TOPOLOGY (overlapping worksets -- CONFOUNDED by key coverage, stated)"
echo "       diameter with 1 host:  ${rs1:-?}"
echo "       diameter with 3 hosts: ${rs3:-?}"
if [ "$ra1_rc" -ne 0 ] || [ "$ra3_rc" -ne 0 ] || [ "$rs1_rc" -ne 0 ] || [ "$rs3_rc" -ne 0 ]; then
    echo "     >> UNAVAILABLE. A failed probe is not evidence that concurrency was exercised."
    fails=$((fails+1))
elif [ "$ra1" = "$ra3" ]; then
    echo "     >> UNCHANGED under the COVERAGE-CONTROLLED probe. The roster arms do NOT"
    echo "        exercise concurrency between hosts: the extra states would be name"
    echo "        permutations, so some premise in the model is GRANTING the mutual"
    echo "        exclusion these arms were written to test. Treat every roster arm"
    echo "        above as unsupported and do not claim R4 on any of them."
    fails=$((fails+1))
else
    echo "     >> diameter moved with the host count under the COVERAGE-CONTROLLED probe;"
    echo "        interleaving between hosts is being exercised."
fi

# Adequacy for this model is not a diameter question -- there is one process, so concurrency is not
# what it establishes. What it establishes is that the SET semantics and the post-destruction
# unreadability of the generation together make two proposed designs unrepairable. The premise doing
# the work is Record's `ledger' = ledger \cup intents`, stated in the module's SCOPE block. If that
# equation is wrong about any provider, this model is wrong for that provider.

# Check the current bounded lifetime/read protocol independently of the historical models.
# The PowerShell runner retains model/configuration hashes and all expected counterexamples.
if ! command -v pwsh >/dev/null 2>&1; then
    echo "REFUSE: pwsh unavailable for current binding-lifetime checks."
    refused=$((refused+1))
elif ! pwsh -NoProfile -File "$HERE/subscription-roster/Check-BindingLifetime.ps1" -Java "$JAVA" -Jar "$JAR"; then
    fails=$((fails+1))
fi
# Historical page/visibility protocol: the manifest binder correspondence is withdrawn.
# Keep its checker in the canonical entry point without turning timeout into a pass or failure.
echo
echo "-- historical cooperative page/visibility protocol (not current roster admission)"
PYTHON="${TLA_PYTHON:-python}"
if ! command -v "$PYTHON" >/dev/null 2>&1; then
    echo "REFUSE: Python unavailable for cooperative projection checks."
    refused=$((refused+1))
else
    cooperative_root="$(mktemp -d "$ARTIFACT_ROOT/cooperative-projection.XXXXXX")"
    java_python="$JAVA"; jar_python="$JAR"
    if command -v cygpath >/dev/null 2>&1; then
        java_python="$(cygpath -w "$JAVA")"; jar_python="$(cygpath -w "$JAR")"
    fi
    "$PYTHON" "$HERE/cooperative-projection/check-cooperative-projection.py" \
        --java "$java_python" --jar "$jar_python" --output "$cooperative_root/evidence" \
        --timeout "${TLA_COOPERATIVE_TIMEOUT:-1800}" \
        --heap-mb "${TLA_COOPERATIVE_HEAP_MB:-4096}"
    cooperative_rc=$?
    case "$cooperative_rc" in
        0) ;;
        2) refused=$((refused+1)) ;;
        *) fails=$((fails+1)) ;;
    esac
fi

# TLC leaves a trace binary per violated arm and a states/ directory. Seven of our nine arms
# violate BY DESIGN, so this is not incidental litter -- it accumulates every run.
rm -rf "$HERE"/_Arm_*.tla "$HERE"/_Arm_*.cfg "$HERE"/_L_*.tla "$HERE"/_L_*.cfg \
       "$HERE"/*_TTrace_*.tla "$HERE"/*_TTrace_*.bin "$HERE"/states \
       "$HERE"/subscription-roster/_SR_*.tla "$HERE"/subscription-roster/_SR_*.cfg \
       "$HERE"/subscription-roster/*_TTrace_*.tla "$HERE"/subscription-roster/*_TTrace_*.bin \
       "$HERE"/subscription-roster/states 2>/dev/null

echo
if [ "$fails" -eq 0 ] && [ "$refused" -eq 0 ]; then
    echo "PASS -- 19 historical arms, 14 current lifetime/control arms, 9 cooperative protocol arms and the checker self-test gave their required answers."
    echo
    echo "  A pass means each arm gave the answer it was required to give. It does NOT mean a model"
    echo "  is ADEQUATE, and no exit code can carry that judgement. R4 for the event-sourcing seam"
    echo "  is UNVERIFIED in eng/governance/rigor-ladder.yaml; read the LIMITS block in"
    echo "  EventStoreGlobalPosition.tla before citing any of this."
    echo
    echo "  AND READ subscription-roster/SubscriptionRoster.tla's LIMITS BLOCK. Two of its arms are"
    echo "  historical configurations; the old no-witness shape violates"
    echo "  the strongest invariant in that module BY DESIGN. A green here therefore includes an arm"
    echo "  whose required answer preserves a historical finding. Current lifetime scope is in BindingLifetime.tla."
    exit 0
fi
if [ "$fails" -eq 0 ] && [ "$refused" -gt 0 ]; then
    echo "REFUSE -- $refused checker(s) could not complete; this is not a pass."
    exit 2
fi
echo "FAIL -- $fails arm(s) gave the wrong answer."
exit 1
