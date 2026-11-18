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
# THE TALLY, and it exists because this script's own PASS line used to lie about it. That line was a
# hardcoded literal claiming "7 obligation-partition arms" while eleven were declared and eleven ran --
# a count with a denominator nobody had measured, in the one artifact whose whole job is to establish
# that each arm gave its required answer. A battery that goes green by DROPPING an arm reports exactly
# what a healthy one reports, so the summary now counts what it evaluated rather than asserting it.
arms_run=0

run_arm() {
    local module="$1" expect="$2" label="$3" out rc viol
    arms_run=$((arms_run+1))
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

# ---------------------------------------------------------------------------
# LEADER-ELECTION FENCE TOKENS
#
# The manifest declares leader-election blast_radius catastrophic and says why:
# "Split brain. Two holders each believe they are sole leader; fencing tokens are the only
# thing between that and double-processing." Three of the seven providers shipped a fence
# that admitted EVERY token, so the thing standing between us and double-processing was
# absent in three substrates at once.
#
# The four arms below are those shipped defects, as configurations rather than as prose.
# They are the model's non-vacuity proof and they are stronger than a self-test: one
# invariant set, four independently-written defects, four different substrates, each
# caught and each naming which invariant it broke.
#
# The first version of this model could not fail on two of them -- it let a candidate
# write only its own PINNED token, so neither the fabricated-token path nor the
# never-minted state was reachable. Both arms reported "no violation" against code known
# to be broken. That is the generator-cannot-produce-the-failing-shape defect, found by
# running the arms rather than by trusting the model, and it is why these four exist.
mkfence() { # name shared zeroIsToken canRewind unsetReportsStart
    cp "$HERE/LeaderFenceTokens.tla" "$HERE/$1.tla"
    sed -i "s/MODULE LeaderFenceTokens/MODULE $1/" "$HERE/$1.tla"
    cat > "$HERE/$1.cfg" <<EOF
SPECIFICATION Spec
CONSTANTS
    Candidates = {cA, cB}
    MaxTenures = 3
    SharedMark = $2
    ZeroIsToken = $3
    MarkCanRewind = $4
    UnsetReportsStart = $5
INVARIANT TypeOK
INVARIANT InvNoSupersededWrite
INVARIANT InvTokensUniquePerTenure
INVARIANT InvUnsetRefusesEverything
EOF
}

echo "-- leader-election fence: the shipped relation, and the four shipped defects"
mkfence _F_Unshared   FALSE FALSE FALSE FALSE
mkfence _F_ZeroToken  TRUE  TRUE  FALSE FALSE
mkfence _F_Rewind     TRUE  FALSE TRUE  FALSE
mkfence _F_UnsetStart TRUE  FALSE FALSE TRUE

run_arm LeaderFenceTokens hold                       "(fence: shipped at-or-above relation) <- SHIPS"           || fails=$((fails+1))
run_arm _F_Unshared   InvTokensUniquePerTenure       "(fence: per-process mark)   <- in-memory provider shape"  || fails=$((fails+1))
run_arm _F_Rewind      InvTokensUniquePerTenure      "(fence: mark rewinds to 0)  <- Consul unreadable payload" || fails=$((fails+1))
run_arm _F_ZeroToken   InvUnsetRefusesEverything     "(fence: 0 counts as a mark) <- Kubernetes zero Lease"      || fails=$((fails+1))
run_arm _F_UnsetStart  InvUnsetRefusesEverything     "(fence: unset reports 1)    <- SqlServer never-drawn"      || fails=$((fails+1))
echo

echo "-- control: can this TLC invocation report a violation at all?"
# ---------------------------------------------------------------------------
# DATA-SUBJECT TOKEN FINGERPRINT LEDGER
#
# The manifest declares compliance-erasure blast_radius catastrophic. The defect this
# models shipped and was fixed at 7ce9400aca: a changed data-subject hashing key silently
# orphaned every token-keyed compliance record, and -- the consequence a regulator cares
# about most -- every legal-hold lookup too, so an erasure proceeded over data the law
# required kept.
#
# BOTH SWITCHES ARE REGRESSION LOCKS, NOT DESCRIPTIONS OF PRESENT CODE. Each is ON in
# exactly one pair of arms and OFF in the shipped configuration, so an arm going green
# would mean the fix regressed.
#
# The two defects are the two rulings adversarial review REVERSED, and each arm pair
# separates the MECHANISM from the HARM, because an arm that catches only the mechanism
# cannot say what it costs a consumer:
#
#   UpdateAllowed               mechanism -> InvRecordedNeverChanges   (write-once breaks)
#                               harm      -> InvProceedersAgreeOnBehaviour (two token
#                                            functions wrote into one store)
#   FingerprintIsConfiguration  harm      -> InvProceedersAgreeOnBehaviour (a check that
#                                            PASSES while the invariant is violated)
#                               harm      -> InvNoFalseRefusal         (and it refuses a
#                                            CORRECTLY configured deployment, which is the
#                                            second, opposite harm of the same design)
#
# Each defect cfg checks exactly ONE invariant beyond TypeOK, deliberately. The fence
# generator above lists every invariant in every arm, which works only while the defects
# happen to violate distinct ones in a stable order -- so an arm there could silently start
# passing for the wrong reason. These cannot.
mkfp() { # name updateAllowed fingerprintIsConfiguration invariant
    cp "$HERE/TokenFingerprintLedger.tla" "$HERE/$1.tla"
    sed -i "s/MODULE TokenFingerprintLedger/MODULE $1/" "$HERE/$1.tla"
    cat > "$HERE/$1.cfg" <<EOF
SPECIFICATION Spec
CONSTANTS
    Processes = {p1, p2}
    Fingerprints = {f1, f2}
    UpdateAllowed = $2
    FingerprintIsConfiguration = $3
INVARIANT TypeOK
INVARIANT $4
EOF
}

echo "-- data-subject token fingerprint: the shipped claim, and the two reversed rulings"
mkfp _T_UpdateMech  TRUE  FALSE InvRecordedNeverChanges
mkfp _T_UpdateHarm  TRUE  FALSE InvProceedersAgreeOnBehaviour
mkfp _T_ConfigHarm  FALSE TRUE  InvProceedersAgreeOnBehaviour
mkfp _T_ConfigRefus FALSE TRUE  InvNoFalseRefusal

run_arm TokenFingerprintLedger hold                            "(token fp: write-once claim over the hasher's OUTPUT) <- SHIPS"   || fails=$((fails+1))
run_arm _T_UpdateMech  InvRecordedNeverChanges                  "(token fp: claim may overwrite)   <- write-once breaks"           || fails=$((fails+1))
run_arm _T_UpdateHarm  InvProceedersAgreeOnBehaviour            "(token fp: claim may overwrite)   <- two functions, one store"    || fails=$((fails+1))
run_arm _T_ConfigHarm  InvProceedersAgreeOnBehaviour            "(token fp: fingerprint the CONFIG) <- passes while violated"      || fails=$((fails+1))
run_arm _T_ConfigRefus InvNoFalseRefusal                        "(token fp: fingerprint the CONFIG) <- refuses a CORRECT deploy"   || fails=$((fails+1))

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
# COMPLIANCE ERASURE OWNER. Two tenants hold the same data-subject identifier. The
# questions are whether ONE tenant's erasure can reach ANOTHER tenant's key material,
# and whether a read can be served to a caller that does not own what it is reading.
#
# THIS MODEL EXISTED AND WAS INVOKED BY NOTHING. The rung manifest cited it as
# covering this seam's bounded safety obligations, so a reader of the manifest
# inherited confidence in an artifact no checker ever ran -- the shape this runner's
# own self-test exists to prevent, arriving one level up, in the manifest instead of
# in a model. Wired here with its refutation arms. Only the shipped configuration is
# required to hold; each red arm is a design that loses consumer data, and the first
# of them is the one that reached published packages.
#
# READ ComplianceErasureOwner.tla's SCOPE BLOCK BEFORE CITING ANY ARM BELOW. It is
# bounded greenfield safety: two tenants, one subject, one erased handle. It models
# no provider implementation effects, no parallel caches, no crash recovery and no
# legacy deployment ordering, and a green here carries none of those.
# ---------------------------------------------------------------------------
mkcarm() { # name tenantqualified enforceowner pairledger
    cp "$HERE/ComplianceErasureOwner.tla" "$HERE/$1.tla"
    sed -i "s/MODULE ComplianceErasureOwner/MODULE $1/" "$HERE/$1.tla"
    cat > "$HERE/$1.cfg" <<EOF
SPECIFICATION Spec
CONSTANTS
    Tenants = {tenantA, tenantB}
    A = tenantA
    B = tenantB
    TenantQualified = $2
    EnforceOwner = $3
    PairLedger = $4
INVARIANT TypeOK
INVARIANT InvOtherOwner
INVARIANT InvRetained
INVARIANT InvReadOwner
INVARIANT InvNoFalseTombstone
INVARIANT InvLedgerSound
INVARIANT InvRepairable
INVARIANT InvRecordedOldRead
EOF
}

echo
echo "-- compliance erasure owner: the shipped design, and three that lose consumer data"
mkcarm _C_Ships        TRUE  TRUE  TRUE
mkcarm _C_SharedKey    FALSE TRUE  TRUE
mkcarm _C_NoOwnerCheck TRUE  FALSE TRUE
mkcarm _C_HandleLedger TRUE  TRUE  FALSE

run_arm _C_Ships        hold                "(tenant handle, owner enforced, pair ledger) <- SHIPS"        || fails=$((fails+1))
run_arm _C_SharedKey    InvOtherOwner       "(handle omits the tenant)  <- A's erasure DESTROYS B's key"   || fails=$((fails+1))
run_arm _C_NoOwnerCheck InvReadOwner        "(owner not enforced)       <- read served to a NON-OWNER"     || fails=$((fails+1))
run_arm _C_HandleLedger InvNoFalseTombstone "(ledger keyed by handle)   <- TOMBSTONE over live material"   || fails=$((fails+1))

# ---------------------------------------------------------------------------
# RETENTION DECLARATION VIEW. Retention declarations live in each instance's own CODE,
# so during a rolling deploy that ADDS one, an instance on the previous version believes
# nothing is retained and destroys key material the deploy declares kept -- then
# certifies it Completed, because by its own view it was right. A deployment-wide record
# plus a comparison immediately before the destruction is what lets a stale instance
# discover it is behind and withhold the irreversible act.
#
# WRITTEN AT DESIGN TIME, WHICH IS WHAT MAKES IT WORTH ANYTHING: the predicate it checks
# was RULED WRONG ONCE already and corrected by a counterexample found by hand. The
# _R_Subset arm is that counterexample, mechanised, so the refuted predicate cannot come
# back quietly. One arm per mechanism, because they fail for different reasons.
#
# READ RetentionDeclarationView.tla's SCOPE AND LIMITS BEFORE CITING ANY ARM BELOW. It
# models one deploy generation reaching a fleet, not a sequence of deploys, and it models
# the destruction as ATOMIC with its own gate check -- the split check-then-act across N
# keys is refuted separately by _P_LoopPerKey above and a green here does not cover it.
mkrdv() { # name strictsubset latchonprefix invariant
    cp "$HERE/RetentionDeclarationView.tla" "$HERE/$1.tla"
    sed -i "s/MODULE RetentionDeclarationView/MODULE $1/" "$HERE/$1.tla"
    cat > "$HERE/$1.cfg" <<EOF
SPECIFICATION Spec
CONSTANTS
    Types = {tA, tB, tC}
    Instances = {i1, i2}
    UseStrictSubset = $2
    LatchOnPrefix = $3
INVARIANT TypeOK
INVARIANT $4
EOF
}

echo
echo "-- retention declaration view: the shipped predicate, the refuted one, and the latch"
mkrdv _R_Ships    FALSE FALSE InvNoRetainedDestroyed
mkrdv _R_Subset   TRUE  FALSE InvNoRetainedDestroyed
mkrdv _R_Prefix   FALSE TRUE  InvNoRetainedDestroyed
mkrdv _R_Witness  FALSE FALSE InvNothingIsEverDestroyed

run_arm _R_Ships   hold                       "(containment, latch on full success) <- SHIPS"                   || fails=$((fails+1))
run_arm _R_Subset  InvNoRetainedDestroyed     "(local set a STRICT SUBSET of the record) <- REFUTED: destroys it" || fails=$((fails+1))
run_arm _R_Prefix  InvNoRetainedDestroyed     "(latch closes on a PREFIX) <- an unrecorded pair has NO TELL"     || fails=$((fails+1))
run_arm _R_Witness InvNothingIsEverDestroyed  "(an erasure is still REACHABLE -- non-vacuity)"                   || fails=$((fails+1))

# ---------------------------------------------------------------------------
# SUBJECT KEY OBLIGATION PARTITION. A key handle must determine the OBLIGATION
# CLASS of everything encrypted under it, because an erasure decides per handle
# while the law binds per class. The shipped derivation widens onto a per-type
# handle only for a type declared AT WRITE TIME, so every undeclared type and
# every non-aggregate field share one handle -- and once a retention is declared
# later, that one handle must be both destroyed and spared.
#
# EACH REFUTED ARM CHECKS ONLY THE INVARIANT IT MUST VIOLATE. The shipped
# derivation breaks more than one property at once, and an arm that could report
# either of two violations would flap between them; naming one invariant per arm
# makes each claim precise and each failure attributable.
# ---------------------------------------------------------------------------
mkparm() { # name tenants widen writeahead enumledger claim refusespare fence tworow atomicattest  invariant...
    local name="$1" tn="$2" widen="$3" wa="$4" enum="$5" claim="$6" rsd="$7" fence="$8"
    local tworow="$9"; shift 9
    local atomic="$1" whole="$2"; shift 2
    cp "$HERE/SubjectKeyObligationPartition.tla" "$HERE/$name.tla"
    sed -i "s/MODULE SubjectKeyObligationPartition/MODULE $name/" "$HERE/$name.tla"
    {
        echo "SPECIFICATION Spec"
        echo "CONSTANTS"
        echo "    Tenants = $tn"
        # An arm variable for the same reason ARM_REQUESTS is one: the per-subject-claim arms
        # need ONE type to stay tractable at two requests, and every other arm needs two.
        echo "    Types = ${ARM_TYPES:-{typeT, typeU\}}"
        echo "    Bot = classNone"
        echo "    Owner = tenantA"
        echo "    Requests = $ARM_REQUESTS"
        echo "    WidenAlways = $widen"
        echo "    WriteAhead = $wa"
        echo "    EnumerateFromLedger = $enum"
        echo "    SubjectClaim = $claim"
        echo "    RefuseSpareOfDestroyed = $rsd"
        echo "    RegistryFence = $fence"
        echo "    TwoStateRow = $tworow"
        echo "    AtomicAttestation = $atomic"
        echo "    AttestWholeLedger = $whole"
        # Defaults to the GRANT, so every arm written before the destruction was splittable keeps
        # its exact meaning. Only the two per-key arms turn it off.
        echo "    AtomicDestruction = ${ARM_ATOMIC_DESTROY:-TRUE}"
        echo "INVARIANT TypeOK"
        for inv in "$@"; do echo "INVARIANT $inv"; done
    } > "$HERE/$name.cfg"
}

echo
echo "-- subject key obligation partition: the proposed design, and six that lose consumer data"
#        name                tenants              widen wa    enum  claim rsd     invariants
# Requests per arm. Two concurrent requests by default; the two-tenant arms narrow it to one,
# because two tenants x two requests x the history variables does not terminate (see below).
ARM_REQUESTS='{e1, e2}'

# The shipped design: all SIX mechanisms on, every safety invariant checked, exhaustive.
# NOTE ON InvNoRetainedLoss WHEREVER IT APPEARS BELOW: it is ENTAILED by mechanism 5's Certify
# guard (see the retired-arm note further down), so its presence in a HOLD arm's invariant list
# adds nothing. It is kept only because it still DISCRIMINATES when mechanism 5 is off, which is
# what _P_NoClaimNoCheck and _P_NoDestroyedCheck use it for. Do not read it as evidence in a
# green arm.
mkparm _P_Ships             "{tenantA}"           TRUE  TRUE  TRUE  TRUE  TRUE  TRUE    TRUE  TRUE    TRUE    InvPartition InvNoFalseErasure InvNoRetainedLoss InvEnumComplete InvNoSpareDestroyed InvNoRetainedDestruction
# Each arm below turns exactly ONE mechanism off and names the invariant that must then FAIL.
# An arm that cannot refute the design without its mechanism is not evidence the mechanism is needed.
mkparm _P_HeadAlgo          "{tenantA, tenantB}"  FALSE FALSE FALSE TRUE  TRUE  TRUE    TRUE  TRUE    TRUE    InvPartition
# MECHANISM 1 ALONE off, every other mechanism ON, checked against SAFETY-2'. This arm did not
# exist, and its absence is why the shipped derivation was refuted only on InvPartition -- which
# needs TWO classes to share the plain handle. With ONE declared class under the plain handle,
# nothing fired at all. This is the arm that refutes the shipped algorithm on the property the
# guarantee actually makes.
mkparm _P_NoWiden           "{tenantA}"           FALSE TRUE  TRUE  TRUE  TRUE  TRUE    TRUE  TRUE    TRUE    InvNoRetainedDestruction
mkparm _P_MintFirst         "{tenantA}"           TRUE  FALSE TRUE  TRUE  TRUE  TRUE    TRUE  TRUE    TRUE    InvEnumComplete
# MECHANISM 8 IS OFF HERE TOO, AND THAT IS A FINDING RATHER THAN A CONVENIENCE. With it on, this arm
# HOLDS: the whole-ledger attestation catches an omitted handle even when the enumeration never had it,
# so mechanism 3's necessity for THIS invariant stops being demonstrable. Mechanism 8 subsumes mechanism
# 3 for false-erasure safety -- it does not subsume it for COMPLETENESS, because an omitted handle is
# still never destroyed, only detected, and no invariant here separates destroyed from detected. So the
# arm turns both off and attributes the violation to the pair, which is the honest claim. Leaving it with
# 8 on and flipping it to expect a hold would have recorded the rescue and lost the refutation.
mkparm _P_NoLedger          "{tenantA}"           TRUE  TRUE  FALSE TRUE  TRUE  TRUE    TRUE  TRUE    FALSE   InvNoFalseErasure
mkparm _P_NoDestroyedCheck  "{tenantA}"           TRUE  TRUE  TRUE  TRUE  FALSE TRUE    TRUE  TRUE    TRUE    InvNoSpareDestroyed
# MECHANISM 6. The registry is not versioned, so a stale instance cannot know it is stale.
# This is what EVERY shipped version does, and what every earlier revision of this design did.
mkparm _P_NoFence           "{tenantA}"           TRUE  TRUE  TRUE  TRUE  TRUE  FALSE   TRUE  TRUE    TRUE    InvNoRetainedDestruction
# _P_NoClaimOnly WAS HERE AND IS RETIRED AS UNSATISFIABLE. Recorded rather than deleted
# silently, because the next person will want to add it back.
#
# It set mechanism 4 off with 5 and 6 on and expected InvNoRetainedLoss to be VIOLATED. That
# invariant CANNOT be violated under any setting of claim, fence, widening or ledger: Certify is
# guarded by `spared[r] \cap destroyed = {}` and the SAME action sets dac'[r] = destroyed from the
# same unprimed state, so spared[r] \cap dac[r] = {} in the successor and neither changes again for
# a certified request. The invariant is ENTAILED BY THE ENABLING CONDITION of the only action that
# can make its antecedent true -- the "invariant implied by its own constraints" case, one level
# over from a type constraint.
#
# The arm was briefly flipped to expect `hold`. That was WRONG and is the error this comment
# exists to prevent: an arm expecting a hold on an entailed invariant is a permanent false green.
# A correct FAIL is worth more than a green that cannot fail.
#
# MECHANISM 4'S SAFETY NECESSITY IS NOW MEASURED, AND THE ANSWER IS THAT IT IS NOT NECESSARY --
# see the _P_ClaimTwoReq* arms below. This paragraph previously read "UNMEASURED, not disproved. The
# arm that can settle it is the TWO-REQUEST per-key destruction arm (_P_LoopTwoReq below)", and that
# forward reference was to an arm THAT DOES NOT EXIST ANYWHERE IN THIS FILE -- a promise a reader
# would go looking for and not find.
#
# It also named the wrong experiment. A two-request arm with the destruction SPLIT violates
# InvNoRetainedDestructionPerKey, which is the N=1 check-then-act the one-request arms already
# attribute to the split; TLC reports the first violated invariant, so that violation MASKS anything
# the arm could say about the claim. Measured: with the split and two requests the arm goes red at
# depth 8 regardless of the claim setting. The claim question is therefore asked with the destruction
# ATOMIC, which excludes the known defect and leaves the cross-request interference as the only
# hazard left in the arm.
#
# ---------------------------------------------------------------------------
# THE PER-KEY ARM EXISTS NOW. ITS FIRST WRITE-UP ATTRIBUTED THE FINDING TO THE FENCE AND THAT WAS
# WRONG -- CORRECTED BY MEASUREMENT, AND THE CORRECTION IS THE INTERESTING PART.
#
# Superseded, quoted so anyone who absorbed it recognises it: "_P_LoopPerKey answers the FENCE
# question ... and the answer is NO", and "a retention declared mid-loop is invisible to the keys
# that have not gone yet".
#
# BOTH ARE WRONG, FOR THE SAME REASON. Measured: RegistryFence = FALSE with the split violates
# IDENTICALLY, at the same trace length. The fence excludes a declaration arriving between
# ReadRegistry and BeginDestroy and excludes nothing arriving after BeginDestroy, and the violation
# uses the second window. So the fence is IRRELEVANT to this property, and the first write-up could
# not have known either way -- NEITHER of its arms varied the fence. An arm set that holds a
# variable fixed measures nothing about it, however confident the prose.
#
# THE DISCRIMINATOR IS THE ATOMICITY OF THE DESTRUCTION, and _P_LoopNoFence below measures the
# fence's irrelevance rather than asserting it.
#
# AND THE VIOLATION IS A CHECK-THEN-ACT AT N=1. The counterexample contains ONE key: the window is
# between the fence check in BeginDestroy and the FIRST DestroyOne, not between two keys. The defect
# does not scale with N; only the window does. That distinction decides the fix, which is why it is
# laboured here.
#
# MECHANISM 4's SAFETY NECESSITY IS STILL UNMEASURED and this arm does not measure it: SubjectClaim
# enters only through Enumerate's guard SubjectClaim => ActiveRequests = {}, and with one request
# that guard holds on both settings, so it cannot affect any one-request trace. Independence by
# construction. Do not read a red _P_LoopPerKey as evidence about the per-subject claim.
#
# NO FENCE-SHAPED FIX EXISTS, and this was measured BEFORE anyone built one. Re-checking the
# registry inside DestroyOne's own GUARD holds over ~200,000 states -- and it is a GRANT, the same
# one AtomicDestruction exists to prevent: a TLA+ guard is evaluated atomically with its action, so
# it makes the in-process registry read and the external key destruction one indivisible step,
# which the implementation cannot do. Written honestly, as its own action before the destruction,
# it VIOLATES again one step later. The obligation read and the irreversible destruction are two
# actions in any faithful model, so a declaration arriving between them is reachable at N=1.
#
# WHAT THE DESIGN OWES IS THEREFORE A BOUND AND AN OUTCOME, NOT AN INVARIANT: detect the conflict at
# the next key, stop the loop, keep a Completed certificate unreachable, and record how many keys
# had already gone. The implementation already says exactly this, in prose, for LEGAL HOLDS --
# Erasure/ErasureService.cs:1194-1201, "some window is irreducible ... bound the window to a SINGLE
# key ... and make the conflict LOUD". The retention obligation has the same shape and is read once
# from a frozen snapshot OUTSIDE the loop. One obligation bounded and loud, its sibling unbounded
# and silent.
#
# REACHABILITY. The weakest sufficient premise is that an operator declared a retention while an
# erasure was in flight: `decl` is the WORLD's obligation state, not the destroying process's view
# of it, and a compliance property conditioned on what the actor happened to believe is
# self-certifying -- which is the objection this model's own SCOPE raises against spared[r]. The
# shipped registry being frozen from IOptions means a stale process cannot OBSERVE the change,
# which is why the rolling-deploy case is an illustration here and not the load-bearing clause.
# ---------------------------------------------------------------------------

# Mechanisms 4 AND 5 both off -- the configuration that ships today.
mkparm _P_NoClaimNoCheck    "{tenantA}"           TRUE  TRUE  TRUE  FALSE FALSE TRUE    TRUE  TRUE    TRUE    InvNoRetainedLoss
# Requirement #2 of the spec demands two tenants and two types; before this, only _P_HeadAlgo
# honoured it, so the shipped arm never reached a declaration held by a NON-OWNING tenant.
#
# SCOPED TO ONE REQUEST, DELIBERATELY. Two tenants x two requests x the writtenAtDestroy history
# variable does not terminate -- and an arm that cannot finish makes this whole suite unusable,
# which is worse than not having it. Nothing needs the cross-product: the tenant-blind axis is
# about whether a declaration held by a NON-OWNING tenant moves the handle, which one request
# exercises fully, and concurrency between requests is exercised by the one-tenant arms above.
# Measured: HOLDS, 333,567 distinct states, depth 14.
ARM_REQUESTS='{e1}'
mkparm _P_TwoTenants1Req    "{tenantA, tenantB}"  TRUE  TRUE  TRUE  TRUE  TRUE  TRUE    TRUE  TRUE    TRUE    InvPartition InvNoFalseErasure InvNoRetainedLoss InvEnumComplete InvNoSpareDestroyed InvNoRetainedDestruction
# Its refuting partner at two tenants: a declaration held by the OTHER tenant must not be what
# decides this subject's handle. Measured: VIOLATED, depth 7, 6,912 states.
mkparm _P_NoWiden2Ten       "{tenantA, tenantB}"  FALSE TRUE  TRUE  TRUE  TRUE  TRUE    TRUE  TRUE    TRUE    InvNoRetainedDestruction
ARM_REQUESTS='{e1, e2}'
# Required VIOLATED: without it every safety invariant is satisfied by a design that always
# reports a concurrent write and erases nothing -- safe, useless, and green.
mkparm _P_Witness           "{tenantA}"           TRUE  TRUE  TRUE  TRUE  TRUE  TRUE    TRUE  TRUE    TRUE    InvNeverUsefulCertify

# PER-KEY DESTRUCTION. Set and restored explicitly, following this file's own idiom for
# ARM_REQUESTS rather than an inline `VAR=x mkparm`, whose scoping across a function call is a
# bash version question nobody should have to answer while reading an arm set.
#
# ALL THREE HOLD THE REQUEST SET AT ONE. The first version of this pair compared a one-request
# split against a TWO-request atomic arm, so it varied the grant AND the request count and
# attributed the hold to neither. Corrected after a reviewer ran the one-variable version.
ARM_REQUESTS='{e1}'
ARM_ATOMIC_DESTROY=FALSE
mkparm _P_LoopPerKey        "{tenantA}"           TRUE  TRUE  TRUE  TRUE  TRUE  TRUE    TRUE  TRUE    TRUE    InvNoRetainedDestructionPerKey
# The fence OFF, everything else identical to the arm above. It exists to MEASURE that the fence is
# irrelevant to this property rather than to leave that as a sentence: both arms violate, so no
# reader can attribute the violation to the fence, and no future edit can quietly make the fence
# load-bearing without one of these two changing answer.
mkparm _P_LoopNoFence       "{tenantA}"           TRUE  TRUE  TRUE  TRUE  TRUE  FALSE   TRUE  TRUE    TRUE    InvNoRetainedDestructionPerKey
ARM_ATOMIC_DESTROY=TRUE
mkparm _P_LoopAtomicGrant   "{tenantA}"           TRUE  TRUE  TRUE  TRUE  TRUE  TRUE    TRUE  TRUE    TRUE    InvNoRetainedDestructionPerKey
unset ARM_ATOMIC_DESTROY
ARM_REQUESTS='{e1, e2}'

# ---------------------------------------------------------------------------
# THE PER-SUBJECT CLAIM (mechanism 4). Two requests on one subject, destruction ATOMIC so the
# split's own defect cannot mask the answer, and ONE type so the arm terminates: at two types the
# claim-off arm reaches 12,866,388 distinct states, which is tractable but too slow to put in front
# of every reader. Both bounds were measured and AGREE, which is why the cheap one is wired.
#
# THE ANSWER IS THAT THE CLAIM IS NOT WHAT PROTECTS THIS PROPERTY. Both settings HOLD; the claim
# prunes the state space about 2.8x and changes no answer. What protects it is mechanism 5, and the
# CONTROL arm is what establishes that rather than asserting it: with mechanism 5 off and the claim
# still off, the same invariant is VIOLATED. Without that arm the two holds would be a green that
# cannot fail, which is the artifact the rigor rule refuses.
#
# So an implementer must NOT treat the per-subject claim as a safety mechanism for a spared handle.
# Whatever else it buys -- duplicate work, certificate clarity, liveness -- is not measured here.
ARM_TYPES='{typeT}'
#        name                  tenants      widen wa   enum claim rsd   fence tworow atomic whole
mkparm _P_ClaimTwoReqOn     "{tenantA}"     TRUE  TRUE TRUE TRUE  TRUE  TRUE  TRUE   TRUE   TRUE   InvNoSpareDestroyed
mkparm _P_ClaimTwoReqOff    "{tenantA}"     TRUE  TRUE TRUE FALSE TRUE  TRUE  TRUE   TRUE   TRUE   InvNoSpareDestroyed
mkparm _P_ClaimTwoReqCtl    "{tenantA}"     TRUE  TRUE TRUE FALSE FALSE TRUE  TRUE   TRUE   TRUE   InvNoSpareDestroyed
unset ARM_TYPES

# THE THREE ARMS THIS MODULE OWED, and the first of them is the one that was asked for by name.
#
# _P_WriteDuringErasure -- every mechanism 1-6 ON and mechanism 7 OFF, which is the shipped shape
# before the two-state row. It MUST VIOLATE InvNoFalseErasure: Step1 writes the row, the erasure
# enumerates and destroys, Step2 then mints the key, and the certificate attests an erasure over
# live material. This is the arm the two removed grants were suppressing -- grant 1 re-read the
# ledger at Certify so the late row was inside the coverage check, and grant 2 barred certification
# while any write was mid-flight. Neither is something the code does. If this arm ever comes back
# GREEN, the model has reacquired a grant and the arm is the evidence, not the defect.
mkparm _P_WriteDuringErasure "{tenantA}"          TRUE  TRUE  TRUE  TRUE  TRUE  TRUE    FALSE TRUE    TRUE    InvNoFalseErasure

# _P_SplitAttest -- mechanism 7 ON, but the attestation re-read and the certificate are TWO actions,
# which is what the implementation actually does (two awaited calls with nothing between them).
# It MUST VIOLATE InvNoFalseErasure: a row written between the re-read and the write is observed by
# neither. So mechanism 7 DETECTS a concurrent write and does not EXCLUDE one, and the gap is this
# arm rather than a sentence. The shipped arm holds only under AtomicAttestation = TRUE, and that
# constant exists so the grant is visible instead of being built into Certify's shape.
mkparm _P_SplitAttest       "{tenantA}"           TRUE  TRUE  TRUE  TRUE  TRUE  TRUE    TRUE  FALSE   TRUE    InvNoFalseErasure

# _P_PendingLiveness -- the liveness partner, and without it the two arms above are satisfied by a
# design that reports a concurrent write for every request and erases nothing: safe, useless, green.
# Mechanism 7 ON, atomicity ON, and InvNeverUsefulCertify REQUIRED VIOLATED, which means a USEFUL
# certificate is still reachable once no write is pending. A pending row must block certification;
# it must not make certification unreachable.
mkparm _P_PendingLiveness   "{tenantA}"           TRUE  TRUE  TRUE  TRUE  TRUE  TRUE    TRUE  TRUE    TRUE    InvNeverUsefulCertify

# MECHANISM 8 OFF, every other mechanism ON. THIS WAS THE SHIPPED CONFIGURATION AND NO LONGER IS -- the
# attestation now probes the ledger -- so read it as the refutation of the design WITHOUT mechanism 8,
# which is what it has always measured, and not as a description of the current code. It MUST
# VIOLATE InvNoFalseErasure: a write whose row and mint BOTH land between the enumeration and the
# attestation is in no set the implementation examines -- not enumerated, so the coverage check misses
# it; not destroyed, so the re-mint probe misses it; not pending, because its mint completed, so the
# two-state row misses it too. This arm is what the two removed grants were suppressing, and it is the
# reason the shipped-design arms went red the moment they came off.
mkparm _P_AttestEnumOnly    "{tenantA}"           TRUE  TRUE  TRUE  TRUE  TRUE  TRUE    TRUE  TRUE    FALSE   InvNoFalseErasure

run_arm _P_Ships            hold                   "(per-class, write-ahead, ledger, claim, check) <- PROPOSED"        || fails=$((fails+1))
run_arm _P_HeadAlgo         InvPartition           "(widen only when declared)  <- SHIPS: ONE HANDLE, TWO OBLIGATIONS" || fails=$((fails+1))
run_arm _P_NoWiden          InvNoRetainedDestruction "(widen only when declared) <- SHIPS: RETAINED DATA DESTROYED"   || fails=$((fails+1))
run_arm _P_MintFirst        InvEnumComplete        "(mint, then record)         <- a key NOTHING ENUMERATES"           || fails=$((fails+1))
run_arm _P_NoLedger         InvNoFalseErasure      "(re-derive from inventory)  <- an omitted handle CERTIFIED ERASED" || fails=$((fails+1))
run_arm _P_NoClaimNoCheck   InvNoRetainedLoss      "(per-REQUEST claim)         <- SHIPS: RETAINED DATA DESTROYED"     || fails=$((fails+1))
run_arm _P_NoDestroyedCheck InvNoSpareDestroyed    "(spare without checking)    <- SPARES WHAT IS ALREADY GONE"        || fails=$((fails+1))
run_arm _P_NoFence          InvNoRetainedDestruction "(registry not versioned)  <- SHIPS: A STALE INSTANCE DESTROYS IT" || fails=$((fails+1))
run_arm _P_TwoTenants1Req   hold                   "(the design, TWO tenants)   <- requirement #2, 1 request (scoped)" || fails=$((fails+1))
run_arm _P_NoWiden2Ten      InvNoRetainedDestruction "(widen when declared, 2 ten) <- a FOREIGN declaration decides it"  || fails=$((fails+1))
run_arm _P_Witness          InvNeverUsefulCertify  "(a USEFUL certificate is reachable -- non-vacuity)"                || fails=$((fails+1))
run_arm _P_WriteDuringErasure InvNoFalseErasure   "(no two-state row)         <- WRITE AFTER THE ENUMERATION, CERTIFIED" || fails=$((fails+1))
run_arm _P_SplitAttest      InvNoFalseErasure      "(re-read and write split)  <- SHIPS: DETECTS, DOES NOT EXCLUDE"     || fails=$((fails+1))
run_arm _P_PendingLiveness  InvNeverUsefulCertify  "(a pending row blocks, and does not DEADLOCK -- liveness)"          || fails=$((fails+1))
run_arm _P_AttestEnumOnly   InvNoFalseErasure      "(attest only what was enumerated) <- A WHOLE WRITE MISSED"       || fails=$((fails+1))
run_arm _P_LoopPerKey       InvNoRetainedDestructionPerKey "(split destruction, fence ON)  <- CHECK-THEN-ACT, AT ONE KEY"   || fails=$((fails+1))
run_arm _P_LoopNoFence      InvNoRetainedDestructionPerKey "(split destruction, fence OFF) <- IDENTICAL: the fence is irrelevant" || fails=$((fails+1))
# NOT a non-vacuity control, and it must not be read as one: retainedKill is written only by
# DestroyOne, which is guarded ~AtomicDestruction, so under the grant the set is empty BY
# CONSTRUCTION and this arm is a green that cannot fail. It is the differential partner that
# isolates the split -- one variable apart from _P_LoopPerKey. The non-vacuity evidence for the
# property is that _P_LoopPerKey goes RED; nothing else is needed, and claiming more of this arm
# than it can carry is the artifact the rigor rule refuses.
run_arm _P_LoopAtomicGrant  hold                   "(destruction atomic) <- vacuous by construction; isolates the split" || fails=$((fails+1))
run_arm _P_ClaimTwoReqOn    hold                   "(per-subject claim ON,  2 requests)  <- holds"                        || fails=$((fails+1))
run_arm _P_ClaimTwoReqOff   hold                   "(per-subject claim OFF, 2 requests)  <- ALSO holds: not safety-bearing" || fails=$((fails+1))
run_arm _P_ClaimTwoReqCtl   InvNoSpareDestroyed    "(claim OFF, mechanism 5 OFF) <- CONTROL: the two holds above can fail"  || fails=$((fails+1))

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
rm -rf "$HERE"/_F_*.tla "$HERE"/_F_*.cfg "$HERE"/_Arm_*.tla "$HERE"/_Arm_*.cfg "$HERE"/_L_*.tla "$HERE"/_L_*.cfg "$HERE"/_C_*.tla "$HERE"/_C_*.cfg "$HERE"/_P_*.tla "$HERE"/_P_*.cfg "$HERE"/_T_*.tla "$HERE"/_T_*.cfg "$HERE"/_R_*.tla "$HERE"/_R_*.cfg \
       "$HERE"/*_TTrace_*.tla "$HERE"/*_TTrace_*.bin "$HERE"/states \
       "$HERE"/subscription-roster/_SR_*.tla "$HERE"/subscription-roster/_SR_*.cfg \
       "$HERE"/subscription-roster/*_TTrace_*.tla "$HERE"/subscription-roster/*_TTrace_*.bin \
       "$HERE"/subscription-roster/states 2>/dev/null

# ---------------------------------------------------------------------------
# ORPHAN-MODEL CHECK. A model this runner never invokes is indistinguishable from
# protection we have -- it sits in the tree, it is cited by the rung manifest, and
# nothing ever asks it a question. That happened: a model of the compliance erasure
# owner lived here for weeks, was named in the manifest's justification for that
# seam's safety coverage, and was referenced by no arm at all. A reader of the
# manifest inherited confidence in an artifact no checker ran.
#
# So this runner now refuses to report PASS while any committed model is unreferenced.
# It is deliberately a NAME check and not a semantic one: it cannot tell whether an
# arm asks a model the RIGHT question, only that something asks it one. That is the
# judgement, and it stays with the architect.
# ---------------------------------------------------------------------------
orphans=""
# The runner surface is NOT just this script: check-models.sh DELEGATES the cooperative protocol
# arms to a python runner inside that model's own directory, so a name check greping only "$0"
# reports those models as orphans. It did, on its first run -- correct that nothing in THIS file
# names them, wrong about what counts as being invoked.
orphan_runners="$(find "$HERE" -type f \( -name 'check-*.sh' -o -name 'check-*.py' \) -not -path '*/evidence-*/*' | tr '\n' ' ')"
while IFS= read -r model; do
    # evidence-*/ holds ARCHIVED COPIES a previous run wrote out. They are output, not models, and
    # they owe no reference -- counting them reported one real model three times over.
    case "$model" in */evidence-*/*|*/states/*) continue ;; esac
    base="$(basename "$model" .tla)"
    case "$base" in _F_*|_Arm_*|_L_*|_C_*|_P_*|_R_*|_T_*|_SR_*|*_TTrace_*) continue ;; esac
    # shellcheck disable=SC2086 -- the word splitting is deliberate: several runner files.
    grep -qF "$base" $orphan_runners 2>/dev/null || orphans="$orphans $base"
done <<EOF
$(find "$HERE" -name '*.tla' -type f | sort)
EOF

if [ -n "$orphans" ]; then
    echo
    echo "FAIL  orphan model(s) --$orphans"
    echo "      Each is a .tla that no runner under spec/tla names. An unchecked model is not"
    echo "      evidence, and the rung manifest may already cite it as though it were. Wire it"
    echo "      with its refutation arms, or delete it AND the manifest sentence citing it."
    fails=$((fails+1))
fi

echo
if [ "$fails" -eq 0 ] && [ "$refused" -eq 0 ]; then
    echo "PASS -- $arms_run arms EVALUATED, each giving the answer it was required to give, and no"
    echo "        committed model is unreferenced. The number is counted by the runner, not stated by"
    echo "        it: a dropped arm lowers it rather than hiding behind a literal."
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
