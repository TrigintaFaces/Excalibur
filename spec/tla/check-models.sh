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

TLC() { "$JAVA" -XX:+UseParallelGC -cp "$JAR" tlc2.TLC -deadlock "$@" 2>&1; }

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
    mkarm _Arm_Diam TRUE TRUE "$1"
    TLC "$HERE/_Arm_Diam.tla" | grep -oE 'depth of the complete state graph search is [0-9]+' | grep -oE '[0-9]+$'
}
d1="$(diam_for '{w1}')"
d3="$(diam_for '{w1, w2, w3}')"
echo "     diameter with 1 writer:  ${d1:-?}"
echo "     diameter with 3 writers: ${d3:-?}"
if [ -n "$d1" ] && [ "$d1" = "$d3" ]; then
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

# Adequacy for this model is not a diameter question -- there is one process, so concurrency is not
# what it establishes. What it establishes is that the SET semantics and the post-destruction
# unreadability of the generation together make two proposed designs unrepairable. The premise doing
# the work is Record's `ledger' = ledger \cup intents`, stated in the module's SCOPE block. If that
# equation is wrong about any provider, this model is wrong for that provider.

# TLC leaves a trace binary per violated arm and a states/ directory. Seven of our nine arms
# violate BY DESIGN, so this is not incidental litter -- it accumulates every run.
rm -rf "$HERE"/_Arm_*.tla "$HERE"/_Arm_*.cfg "$HERE"/_L_*.tla "$HERE"/_L_*.cfg \
       "$HERE"/*_TTrace_*.tla "$HERE"/*_TTrace_*.bin "$HERE"/states 2>/dev/null

echo
if [ "$fails" -eq 0 ]; then
    echo "PASS -- 9 arms, 7 of which MUST fail and did."
    echo
    echo "  A pass means each arm gave the answer it was required to give. It does NOT mean a model"
    echo "  is ADEQUATE, and no exit code can carry that judgement. R4 for the event-sourcing seam"
    echo "  is UNVERIFIED in eng/governance/rigor-ladder.yaml; read the LIMITS block in"
    echo "  EventStoreGlobalPosition.tla before citing any of this."
    exit 0
fi
echo "FAIL -- $fails arm(s) gave the wrong answer."
exit 1
