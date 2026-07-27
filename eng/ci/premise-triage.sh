#!/usr/bin/env bash
# premise-triage — decide the fate of a carried bug premise against a history written under a freeze.
#
# THE PROBLEM. An operator stop order landed at STOP_EPOCH. Commits landed after it anyway. So the
# naive question "does the premise still reproduce at HEAD?" is UNSOUND: a single boolean read of one
# mutable place (HEAD) cannot distinguish
#     (a) the premise was never real, from
#     (b) a post-stop commit silently FIXED it, from
#     (c) a post-stop commit silently MASKED it.
# Closing a bead on "no longer reproduces at HEAD" therefore closes real bugs. It nearly closed the
# bead which reported that the mandatory rule corpus was untracked -- a post-stop commit had repaired
# it, and a revert of that commit would drop every rule out of git again.
#
# THE METHOD. Git is an append-only log of immutable values; HEAD is a fold over it. Nothing was
# destroyed. So do not read the fold: read the last CLEAN value, then read the events between it and
# HEAD. Four terminals result, and "CLOSE -- never real" is emitted from exactly ONE branch, guarded
# by a predicate evaluation at the clean value. There is no reachable path from a HEAD-only reading
# to a close. The unsound conclusion is not discouraged; it is inexpressible.
#
#   1  premise absent at V0 ............................. CLOSE -- never real
#   2  present at V0, no post-stop commit touched it .... KEEP OPEN (admissible)
#   3  present at V0, touched post-stop, still present .. KEEP OPEN (breach did not fix it)
#   4  present at V0, touched post-stop, now absent ..... CANNOT DETERMINE -- ESCALATE
#
# Terminal 4 is the honest answer, not a failure. Distinguishing "fixed" from "masked" requires a
# human reading the diff. This script will not guess, and it will never auto-close.
#
# INSTRUMENT DISCIPLINE (each of these cost this project a real hour):
#   * Compare EPOCHS (%ct), never `--date=format:` -- that prints the COMMIT'S OWN offset and stamps
#     whatever literal you typed, so a hand-written "Z" is a lie.
#   * Compute the anchor. Never name a SHA: the plan, the bead, and the task spec all named a
#     "pre-stop baseline" that was 25 seconds POST-stop.
#   * Never cite a cached count. It was 36 post-stop commits, then 39. It is a function of when you ask.
#
# Usage:
#   premise-triage.sh --subject <path> [--subject <path>...] --predicate <command> [--bead <id>]
#
#   --predicate  A command run with CWD inside a detached worktree. It MUST exit 0 if the premise
#                REPRODUCES there (i.e. the bug is present), non-zero if it does not. Any other
#                failure mode (crash, missing tool) must be made to exit non-zero by the caller, or
#                the premise will be judged absent. Prefer a predicate that greps for the defect.
#
# Exit codes mirror the terminals: 1=CLOSE 2=KEEP-OPEN 3=KEEP-OPEN-BREACH 4=ESCALATE 64=usage 65=hard error
#
# gate-wiring: not-a-ci-gate (decision tool)
#   This is a triage DECISION PROCEDURE invoked by an agent with --subject/--predicate, NOT a
#   repo-state CI gate: its exit codes are terminals (CLOSE/KEEP-OPEN/ESCALATE), not pass/fail, and
#   a no-arg invocation is a usage error, so it cannot be wired as a `run:` step. It is therefore
#   exempt from gate-wiring.sh ARM2 (the "gate must run in an automated env" check). Its regression
#   lock premise-triage.test.sh is STILL required to have a caller (gate-wiring ARM1) — the tool's
#   own correctness stays locked. Exempt at the authoring site with a greppable declaration; never
#   loosen the meta-gate heuristically.

set -euo pipefail

# The operator stop order for the 2026-07-09 freeze. Override only to triage a different freeze.
STOP_EPOCH="${PREMISE_TRIAGE_STOP_EPOCH:-1783600577}"

BEAD=""
PREDICATE=""
SUBJECTS=()

die() { printf 'premise-triage: %s\n' "$*" >&2; exit 65; }
usage() { sed -n '2,30p' "$0" | sed 's/^# \{0,1\}//'; exit 64; }

while [ $# -gt 0 ]; do
    case "$1" in
        --subject)   [ $# -ge 2 ] || usage; SUBJECTS+=("$2"); shift 2 ;;
        --predicate) [ $# -ge 2 ] || usage; PREDICATE="$2";   shift 2 ;;
        --bead)      [ $# -ge 2 ] || usage; BEAD="$2";        shift 2 ;;
        --stop-epoch)[ $# -ge 2 ] || usage; STOP_EPOCH="$2";  shift 2 ;;
        -h|--help)   usage ;;
        *) printf 'premise-triage: unknown arg %s\n' "$1" >&2; usage ;;
    esac
done

[ -n "$PREDICATE" ]        || { printf 'premise-triage: --predicate is required\n' >&2; usage; }
[ "${#SUBJECTS[@]}" -gt 0 ] || { printf 'premise-triage: at least one --subject is required\n' >&2; usage; }
case "$STOP_EPOCH" in (*[!0-9]*|'') die "stop epoch must be an integer epoch, got '$STOP_EPOCH'" ;; esac

# ---------------------------------------------------------------------------
# The anchor is COMPUTED. V0 = the maximal commit at or before the stop epoch.
# A baseline that is not maximal silently classifies post-stop commits as clean.
# ---------------------------------------------------------------------------
# Compute V0 by comparing %ct integers ourselves. Two rejected alternatives, both real footguns:
#
#   git log --until="@$STOP_EPOCH"   -- git does NOT reliably parse a bare "@<epoch>". Given a value it
#       cannot parse it emits no error and SILENTLY IGNORES THE FILTER, returning the newest commit.
#       A filter that does not filter, reporting success. Observed here with --stop-epoch 1.
#
#   git log --format='%ct %H' | awk '...{exit}'   -- awk exits at the first match, git log takes SIGPIPE,
#       and under `set -e -o pipefail` this script dies with 141 and NO OUTPUT. A failure signal
#       carrying no meaning.
#
# So: disable pipefail for exactly this pipeline, and compare the integers we asked for.
V0="$(set +o pipefail; git log --format='%ct %H' | awk -v s="$STOP_EPOCH" '$1<=s {print $2; exit}')"
[ -n "$V0" ] || die "no commit at or before stop epoch $STOP_EPOCH -- refusing to guess a baseline (EC-1)"

V0_EPOCH="$(git log -1 --format='%ct' "$V0")"
[ "$V0_EPOCH" -le "$STOP_EPOCH" ] || die "computed V0 $V0 is POST-stop -- the computation is broken, not the tree"

WT=""
cleanup() { [ -n "$WT" ] && git worktree remove --force "$WT" >/dev/null 2>&1 || true; }
trap cleanup EXIT

# ---------------------------------------------------------------------------
# Evidence, printed on EVERY run before any decision (AC-4). A terminal with no
# forward set printed is a terminal nobody can check.
# ---------------------------------------------------------------------------
FORWARD="$(git log --format='%h %ct %s' "$V0"..HEAD -- "${SUBJECTS[@]}" 2>/dev/null || true)"
FORWARD_N="$(printf '%s' "$FORWARD" | grep -c . || true)"

printf '=== premise-triage %s===\n' "${BEAD:+$BEAD }"
printf 'stop epoch : %s\n' "$STOP_EPOCH"
printf 'V0         : %s (%s) [computed, not named]\n' "$V0" "$V0_EPOCH"
printf 'HEAD       : %s\n' "$(git rev-parse --short HEAD)"
printf 'subjects   : %s\n' "${SUBJECTS[*]}"
printf 'forward set: %s commit(s) touching the subjects since V0\n' "$FORWARD_N"
[ "$FORWARD_N" -gt 0 ] && printf '%s\n' "$FORWARD" | sed 's/^/  | /'
printf '\n'

# ---------------------------------------------------------------------------
# Step 1 -- evaluate the premise at the CLEAN value. This is the only gate that
# can ever authorise a close.
# ---------------------------------------------------------------------------
WT="$(mktemp -d -t premise-triage-v0-XXXXXX)"
git worktree add --detach --quiet "$WT" "$V0" || die "could not materialise V0 worktree at $V0"

# EC-2: a subject that does not exist at V0 cannot be evaluated there. Never close on it.
for s in "${SUBJECTS[@]}"; do
    if ! git cat-file -e "$V0:$s" 2>/dev/null && [ ! -e "$WT/$s" ]; then
        printf 'TERMINAL 4: CANNOT DETERMINE -- ESCALATE\n'
        printf 'reason: subject %s does not exist at V0 (%s); the premise cannot be evaluated on clean\n' "$s" "$V0"
        printf '        ground, so "never real" is not a conclusion this evidence supports (EC-2).\n'
        exit 4
    fi
done

REPRODUCES_AT_V0=0
if ( cd "$WT" && eval "$PREDICATE" ) >/dev/null 2>&1; then REPRODUCES_AT_V0=1; fi
printf 'premise at V0   : %s\n' "$([ "$REPRODUCES_AT_V0" -eq 1 ] && echo REPRODUCES || echo absent)"

# TERMINAL 1 -- the ONLY close. Reachable solely from a V0 evaluation. HEAD is not consulted, and is
# not in scope in this branch. Moving this block below the HEAD evaluation would reintroduce the bug.
if [ "$REPRODUCES_AT_V0" -eq 0 ]; then
    printf '\nTERMINAL 1: CLOSE -- never real\n'
    printf 'reason: the premise does not reproduce at the last clean value V0 (%s). It was never true\n' "$V0"
    printf '        on admissible ground, independent of anything the %s post-stop commit(s) did.\n' "$FORWARD_N"
    exit 1
fi

# ---------------------------------------------------------------------------
# The premise was REAL at V0. From here, HEAD may inform, but can no longer close.
# ---------------------------------------------------------------------------
if [ "$FORWARD_N" -eq 0 ]; then
    printf '\nTERMINAL 2: KEEP OPEN (admissible)\n'
    printf 'reason: real at V0, and no post-stop commit touched the subjects. HEAD is admissible\n'
    printf '        evidence here, and it agrees with V0. No breach involved.\n'
    exit 2
fi

REPRODUCES_AT_HEAD=0
if ( eval "$PREDICATE" ) >/dev/null 2>&1; then REPRODUCES_AT_HEAD=1; fi
printf 'premise at HEAD : %s\n' "$([ "$REPRODUCES_AT_HEAD" -eq 1 ] && echo REPRODUCES || echo absent)"

if [ "$REPRODUCES_AT_HEAD" -eq 1 ]; then
    printf '\nTERMINAL 3: KEEP OPEN (breach did not fix it)\n'
    printf 'reason: real at V0, %s post-stop commit(s) touched the subjects, and it STILL reproduces.\n' "$FORWARD_N"
    printf '        Flag those commits: they modified the subject without fixing the defect.\n'
    exit 3
fi

printf '\nTERMINAL 4: CANNOT DETERMINE -- ESCALATE\n'
printf 'reason: real at V0, absent at HEAD, and %s post-stop commit(s) touched the subjects. A commit\n' "$FORWARD_N"
printf '        that should not exist changed the observable behaviour. Whether it FIXED the defect or\n'
printf '        merely MASKED it cannot be decided by a predicate -- it requires a human reading the\n'
printf '        diff above. If those commits are ever reverted, this premise returns.\n'
printf '        This script will not auto-close it, and neither should you.\n'
exit 4
