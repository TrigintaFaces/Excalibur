#!/usr/bin/env bash
# premise-triage.test.sh -- the independent lock for eng/ci/premise-triage.sh.
#
# AUTHORSHIP: written by FrontendDeveloper. The implementation is SoftwareArchitect's.
# author != impl, by dispatch. Reassigned from ProjectReviewer (terminated session).
#
# WHAT THIS LOCK BINDS, and why each arm exists.
#
# The script's whole claim is: "CLOSE -- never real" is emitted from EXACTLY ONE branch, guarded by a
# predicate evaluation at the computed clean value V0. A HEAD-only reading cannot reach a close.
#
#   SAFETY  -- the unsound close is unreachable. A premise real at V0 but absent at HEAD (because a
#              post-stop commit touched it) must ESCALATE, never CLOSE.
#   LIVENESS -- the sound close is still REACHABLE. Without this arm, a script that returned 4 for
#              every input on earth would pass the safety arm perfectly, while triaging nothing.
#              (testing-patterns section 3: "a guard asserted only on its safety half is satisfied by
#              a component that does nothing at all.")
#
# Both arms, or the lock is decoration.
#
# NON-VACUITY: arm 8 mutates the implementation to re-introduce the exact bug the script exists to
# prevent -- it evaluates the predicate against the working tree instead of the V0 worktree -- and
# asserts the mutant CLOSES (exit 1) where the real script ESCALATES (exit 4). If that mutant ever
# stops being caught, this lock has rotted into a badge and says so out loud.
#
# Exit: 0 all arms pass. 1 an arm failed (the lock is RED). 2 the environment is unusable.
#
# Usage:  bash eng/ci/premise-triage.test.sh

set -uo pipefail

# ── Git-env isolation (xy3hze) — MUST precede the first git call ────────────────────────────────
# git EXPORTS GIT_INDEX_FILE / GIT_DIR / GIT_WORK_TREE into every hook and every child process.
# This script `git init`s its own throwaway fixture repos — but an inherited GIT_INDEX_FILE is an
# ABSOLUTE PATH and WINS over the repo you are standing in, so `git add` inside the fixture writes
# the CALLER'S index instead. `git init` does not rescue you; neither does `cd`.
#
# Measured consequence, S890: run from a normal shell this script passed; run from pre-commit (where
# git had exported GIT_INDEX_FILE) every arm failed AND it staged its own fixtures — including an
# AWS-shaped token and an RSA private-key header — into the real repo's index, one arm at a time.
# The standalone GREEN is the disguise: the only environment that reproduces it is the one the gate
# actually runs in, and that is the one nobody tested.
#
# Unset before the first git call, NOT inside the subshells (they inherit it either way).
unset GIT_INDEX_FILE GIT_DIR GIT_WORK_TREE GIT_OBJECT_DIRECTORY GIT_COMMON_DIR

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
IMPL="$SCRIPT_DIR/premise-triage.sh"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

pass=0
fail=0
TMPROOT=""

cleanup() { [ -n "$TMPROOT" ] && rm -rf "$TMPROOT" 2>/dev/null || true; }
trap cleanup EXIT

red()  { printf '  FAIL: %s\n' "$*" >&2; fail=$((fail + 1)); }
green(){ printf '  ok  : %s\n' "$*";     pass=$((pass + 1)); }

# Assert an exact exit code. An arm that accepts "non-zero" is the fixture bug this project already
# shipped once: a missing dependency also exits non-zero, and the arm certifies a gate that never ran.
expect_exit() {
    local want="$1" got="$2" what="$3"
    if [ "$got" -eq "$want" ]; then green "$what (exit $got)"
    else red "$what -- expected exit $want, got $got"; fi
}

[ -f "$IMPL" ] || { printf 'premise-triage.test: implementation not found at %s\n' "$IMPL" >&2; exit 2; }
command -v git >/dev/null 2>&1 || { printf 'premise-triage.test: git not on PATH\n' >&2; exit 2; }

TMPROOT="$(mktemp -d -t premise-triage-lock-XXXXXX)" || { printf 'cannot mktemp\n' >&2; exit 2; }

# ---------------------------------------------------------------------------
# A hermetic repo with commits at CONTROLLED epochs. The fixtures do not depend
# on this project's history, so the lock cannot rot when history moves.
# %ct is the COMMITTER date, so that is the one we pin.
# ---------------------------------------------------------------------------
STOP=2000000000

mkrepo() {
    # mkrepo <name> <v0-content> [<post-stop-content>]
    #   v0-content       written and committed at STOP-100 (pre-stop, so it becomes V0)
    #   post-stop-content if given, written and committed at STOP+100 (a breach commit)
    local name="$1" v0="$2" post="${3-}"
    local d="$TMPROOT/$name"
    mkdir -p "$d"
    (
        cd "$d" || exit 1
        git init --quiet -b main .
        git config user.email lock@test.local
        git config user.name  lock
        git config commit.gpgsign false
        printf 'seed\n' > seed.txt
        GIT_AUTHOR_DATE="@$((STOP - 200)) +0000" GIT_COMMITTER_DATE="@$((STOP - 200)) +0000" \
            git add seed.txt && \
        GIT_AUTHOR_DATE="@$((STOP - 200)) +0000" GIT_COMMITTER_DATE="@$((STOP - 200)) +0000" \
            git commit --quiet -m seed

        printf '%s\n' "$v0" > subject.txt
        GIT_AUTHOR_DATE="@$((STOP - 100)) +0000" GIT_COMMITTER_DATE="@$((STOP - 100)) +0000" \
            git add subject.txt && \
        GIT_AUTHOR_DATE="@$((STOP - 100)) +0000" GIT_COMMITTER_DATE="@$((STOP - 100)) +0000" \
            git commit --quiet -m 'v0: the last clean value'

        if [ -n "$post" ]; then
            printf '%s\n' "$post" > subject.txt
            GIT_AUTHOR_DATE="@$((STOP + 100)) +0000" GIT_COMMITTER_DATE="@$((STOP + 100)) +0000" \
                git add subject.txt && \
            GIT_AUTHOR_DATE="@$((STOP + 100)) +0000" GIT_COMMITTER_DATE="@$((STOP + 100)) +0000" \
                git commit --quiet -m 'POST-STOP: a commit that should not exist'
        fi
    ) || return 1
    printf '%s' "$d"
}

# The predicate: exit 0 iff the premise REPRODUCES (the defect is present).
PRED='grep -q BUG subject.txt'

run_impl() { # run_impl <repo-dir> <impl-path> [extra args...]
    local d="$1" impl="$2"; shift 2
    ( cd "$d" && bash "$impl" --stop-epoch "$STOP" --subject subject.txt --predicate "$PRED" "$@" ) \
        >/dev/null 2>&1
    printf '%s' "$?"
}

printf '=== premise-triage lock (author != impl) ===\n\n'

# ---------------------------------------------------------------------------
# LIVENESS. The sound close must still be reachable. Without this, "always exit 4"
# passes every safety arm below.
# ---------------------------------------------------------------------------
printf 'LIVENESS -- the sound close is reachable\n'
d="$(mkrepo never-real 'clean' '')" && \
    expect_exit 1 "$(run_impl "$d" "$IMPL")" 'arm 1: premise absent at V0 -> TERMINAL 1 CLOSE' \
    || red 'arm 1: fixture construction failed'

printf 'LIVENESS -- the non-close terminals are each reachable\n'
d="$(mkrepo keep-open 'BUG' '')" && \
    expect_exit 2 "$(run_impl "$d" "$IMPL")" 'arm 2: real at V0, untouched post-stop -> TERMINAL 2 KEEP OPEN'

d="$(mkrepo breach-nofix 'BUG' 'BUG and some churn')" && \
    expect_exit 3 "$(run_impl "$d" "$IMPL")" 'arm 3: real at V0, touched post-stop, still reproduces -> TERMINAL 3'

# ---------------------------------------------------------------------------
# SAFETY. The unsound close is unreachable. This is the shape of 5pdhaw:
# real at V0, silently repaired by a post-stop commit, absent at HEAD.
# ---------------------------------------------------------------------------
printf 'SAFETY -- the unsound close is unreachable\n'
d="$(mkrepo breach-fixed 'BUG' 'clean')"
got="$(run_impl "$d" "$IMPL")"
expect_exit 4 "$got" 'arm 4: real at V0, absent at HEAD, touched post-stop -> TERMINAL 4 ESCALATE'
[ "$got" -eq 1 ] && red 'arm 4: CLOSED a premise that was REAL at V0 -- the unsound close is reachable'

# ---------------------------------------------------------------------------
# Hard errors: refuse to guess. Each of these was a real footgun.
# ---------------------------------------------------------------------------
printf 'HARD ERRORS -- refuse to guess\n'
d="$(mkrepo ec1 'BUG' 'clean')"
# --stop-epoch 1: no commit at or before it. `git log --until=@1` silently ignores the filter and
# returns the newest commit -- a filter that does not filter, reporting success. Must be a hard error.
got="$( ( cd "$d" && bash "$IMPL" --stop-epoch 1 --subject subject.txt --predicate "$PRED" ) >/dev/null 2>&1; printf '%s' "$?" )"
expect_exit 65 "$got" 'arm 5: EC-1 no commit at/before stop epoch -> hard error, not a guessed baseline'
[ "$got" -eq 1 ] && red 'arm 5: emitted CLOSE with no admissible baseline'

# EC-2: a subject that does not exist at V0 cannot be evaluated there. "never real" is not supported.
d="$TMPROOT/ec2"
mkdir -p "$d"
(
    cd "$d" || exit 1
    git init --quiet -b main .
    git config user.email lock@test.local; git config user.name lock; git config commit.gpgsign false
    printf 'seed\n' > seed.txt
    GIT_AUTHOR_DATE="@$((STOP - 100)) +0000" GIT_COMMITTER_DATE="@$((STOP - 100)) +0000" git add seed.txt
    GIT_AUTHOR_DATE="@$((STOP - 100)) +0000" GIT_COMMITTER_DATE="@$((STOP - 100)) +0000" git commit --quiet -m seed
    printf 'clean\n' > subject.txt   # subject BORN post-stop
    GIT_AUTHOR_DATE="@$((STOP + 100)) +0000" GIT_COMMITTER_DATE="@$((STOP + 100)) +0000" git add subject.txt
    GIT_AUTHOR_DATE="@$((STOP + 100)) +0000" GIT_COMMITTER_DATE="@$((STOP + 100)) +0000" git commit --quiet -m 'post-stop: subject born'
) >/dev/null 2>&1
got="$(run_impl "$d" "$IMPL")"
expect_exit 4 "$got" 'arm 6: EC-2 subject absent at V0 -> ESCALATE, never CLOSE'
[ "$got" -eq 1 ] && red 'arm 6: CLOSED on a subject it could not evaluate at V0'

# Usage: a missing --predicate must be a usage error, not a silent default that judges every premise absent.
got="$( ( cd "$d" && bash "$IMPL" --subject subject.txt ) >/dev/null 2>&1; printf '%s' "$?" )"
expect_exit 64 "$got" 'arm 7: missing --predicate -> usage error, not a silent absent-premise'

# ---------------------------------------------------------------------------
# NON-VACUITY. Re-introduce the bug and prove this lock catches it.
#
# The mutant is one token of intent: evaluate the predicate against the WORKING TREE
# instead of the V0 worktree. That is precisely "read the fold, not the last clean value" --
# the unsound HEAD-only reading the script exists to make inexpressible.
# ---------------------------------------------------------------------------
printf 'NON-VACUITY -- the lock is RED on the reintroduced bug\n'
MUTANT="$TMPROOT/premise-triage.MUTANT.sh"
# NOTE: `&` in a sed REPLACEMENT means "the whole match". The `2>&1` below MUST be written `2>\&1`
# or sed splices the entire matched line back in and the mutant becomes a bash SYNTAX ERROR that
# exits 2 without running a single terminal. That is not a mutant; it is a broken file, and an arm
# asserting merely "mutant != real" or "mutant is non-zero" would print ok while proving NOTHING.
# This is why arm 8 asserts the EXACT exit codes 4 and 1. It caught precisely this, once.
sed 's|if ( cd "$WT" \&\& eval "$PREDICATE" ) >/dev/null 2>\&1; then REPRODUCES_AT_V0=1; fi|if ( eval "$PREDICATE" ) >/dev/null 2>\&1; then REPRODUCES_AT_V0=1; fi|' \
    "$IMPL" > "$MUTANT"

# The mutant must differ from the impl AND still be a runnable script. A syntax-error "mutant" proves
# nothing: it cannot reach a terminal, so it cannot demonstrate that the terminal is load-bearing.
if cmp -s "$IMPL" "$MUTANT"; then
    red 'arm 8: the mutation did not apply -- this lock proves NOTHING. Fix the sed, do not delete the arm.'
elif ! bash -n "$MUTANT" 2>/dev/null; then
    red 'arm 8: the mutant is a SYNTAX ERROR, not a behavioural mutant -- it never runs, so it proves NOTHING.'
else
    d="$(mkrepo mutant-check 'BUG' 'clean')"          # the 5pdhaw shape: real at V0, absent at HEAD
    real_exit="$(run_impl "$d" "$IMPL")"
    mut_exit="$(run_impl "$d" "$MUTANT")"
    if [ "$real_exit" -eq 4 ] && [ "$mut_exit" -eq 1 ]; then
        green "arm 8: mutant CLOSES (exit 1) where the real script ESCALATES (exit 4) -- lock is non-vacuous"
    else
        red "arm 8: mutant not distinguished -- real=$real_exit mutant=$mut_exit (want real=4 mutant=1)"
    fi
fi

# ---------------------------------------------------------------------------
# INTEGRATION. The real bead, against this repo's real history. 5pdhaw is the fixture and
# 449897ac9 -- a POST-STOP commit that armed the docs gate -- is the answer key.
# Skipped only if the anchor commit is absent (shallow clone), and it says so.
# ---------------------------------------------------------------------------
printf 'INTEGRATION -- the real bead, the real history\n'
if git -C "$REPO_ROOT" cat-file -e 449897ac9^{commit} 2>/dev/null; then
    got="$( ( cd "$REPO_ROOT" && bash "$IMPL" --bead 5pdhaw \
                --subject .github/workflows/ci.yml \
                --predicate '! grep -q "this step enforces zero" .github/workflows/ci.yml' ) >/dev/null 2>&1; printf '%s' "$?" )"
    expect_exit 4 "$got" 'arm 9: 5pdhaw -> ESCALATE (FIXED-BY 449897ac9, post-stop), never CLOSE'
    [ "$got" -eq 1 ] && red 'arm 9: would have CLOSED 5pdhaw -- a defect real for months, repaired 1h49m after a stop order'
else
    printf '  SKIP: 449897ac9 not present (shallow clone) -- integration arm did not run\n'
    printf '        This is NOT a pass. The unit arms above still bind the terminals.\n'
fi

printf '\n=== %d passed, %d failed ===\n' "$pass" "$fail"
[ "$fail" -eq 0 ] || { printf 'premise-triage lock is RED\n' >&2; exit 1; }
printf 'premise-triage lock is GREEN\n'
exit 0
