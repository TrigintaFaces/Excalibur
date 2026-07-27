#!/usr/bin/env bash
# validate-sprint-plan.sh — the sprint plan must declare a DENOMINATOR and an OWNER for every bead
# before IMPLEMENT is dispatched.
#
# Promoted from an incident: the plan was 24 lines, declared "Target ~100 beads", enumerated none of them, and
# deferred the list to SPEC. 13 beads reached CLOSE with zero owners and the retro could not state a
# completion rate, because no denominator ever existed.
#
#   eng/ci/validate-sprint-plan.sh management/sprints/sprint-881-plan.md
#   eng/ci/validate-sprint-plan.sh --self-test     # prove this gate can FAIL
#
# Exit 0 = plan is dispatchable. Exit 1 = do not start IMPLEMENT.

set -uo pipefail

# CI has no tty. Any command that unexpectedly reads stdin would block FOREVER in Actions
# (a hang is worse than a wrong answer: the job just sits there). Detach stdin, once, here.
exec </dev/null

RED=$'\033[0;31m'; GRN=$'\033[0;32m'; YLW=$'\033[1;33m'; NC=$'\033[0m'

fail_count=0
note() { printf '  %s\n' "$1"; }
err()  { printf '%s✗ %s%s\n' "$RED" "$1" "$NC"; fail_count=$((fail_count + 1)); }
ok()   { printf '%s✓ %s%s\n' "$GRN" "$1" "$NC"; }
warn() { printf '%s! %s%s\n' "$YLW" "$1" "$NC"; }

# A bead id in this repo: 6 lowercase alnum chars in backticks, optionally prefixed.
BEAD_RX='`(Excalibur\.Dispatch-)?[a-z0-9]{6}`'

# check_file_collisions <mini-specs-dir> — 527ciw. A plan can license two beads/waves in different lanes
# to edit the SAME file (3 such collisions were found at SPEC, not PLAN). The file→owner map does
# not live in the plan — it lives in the lane mini-specs' `File surface:` declarations (the one-file-one-
# owner enforcement surface per coordinate-before-parallel-work). This flags any file/glob token claimed
# by >=2 distinct lane mini-specs. Exact-token match (sound, no false positives); overlapping-but-different
# globs are deliberately out of scope (a harder problem — flag the exact collision, not a fuzzy overlap).
check_file_collisions() {
    local dir="$1"
    if [ ! -d "$dir" ]; then
        warn "no mini-specs dir at $dir — skipping file→owner collision check"; return 0
    fi
    local lanes; lanes=$(find "$dir" -maxdepth 1 -name 'lane-*.md' 2>/dev/null | LC_ALL=C sort)
    if [ -z "$lanes" ]; then
        warn "no lane-*.md mini-specs in $dir — skipping file→owner collision check"; return 0
    fi
    # "token<TAB>owner" for every backtick-quoted path on a File-surface line. The harm is one file with
    # TWO DIFFERENT OWNERS editing in parallel (coordinate-before-parallel-work); two lanes with the SAME
    # owner claiming a file is benign (one actor). So key on the lane's declared Owner, not the lane file —
    # otherwise a single owner who owns two lanes (e.g. Platform owning C+D) false-positives.
    local pairs
    pairs=$(
        while IFS= read -r lf; do
            [ -n "$lf" ] || continue
            local owner
            owner=$(grep -iE 'owner' "$lf" | head -1 | sed -E 's/.*[Oo]wner[^A-Za-z0-9]*//; s/[^A-Za-z0-9].*$//')
            [ -n "$owner" ] || owner="$(basename "$lf")"   # no declared owner → treat the lane as its own owner
            grep -iE 'File surface' "$lf" | grep -oE '`[^`]+`' | tr -d '`' \
              | while IFS= read -r tok; do
                    tok="$(printf '%s' "$tok" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//; s/,$//')"
                    [ -n "$tok" ] && printf '%s\t%s\n' "$tok" "$owner"
                done
        done <<< "$lanes" | sort -u
    )
    # A token owned by >=2 DISTINCT owners = a real one-file-two-owners collision.
    local collisions
    collisions=$(printf '%s\n' "$pairs" | awk -F'\t' '
        NF==2 { if (!seen[$1 SUBSEP $2]++) { c[$1]++; who[$1]=who[$1]" "$2 } }
        END   { for (t in c) if (c[t] > 1) printf "%s <- owners:%s\n", t, who[t] }')
    if [ -n "$collisions" ]; then
        while IFS= read -r line; do
            [ -n "$line" ] && err "file claimed by >1 lane (one-file-one-owner violation): $line"
        done <<< "$collisions"
        return 1
    fi
    ok "no file is claimed by more than one lane mini-spec (file→owner collision-free)"
    return 0
}

validate() {
    local plan="$1"
    fail_count=0

    [ -f "$plan" ] || { err "plan not found: $plan"; return 1; }
    printf 'Validating %s\n\n' "$plan"

    # ---- 1. Required sections -------------------------------------------------
    local missing=0
    for section in \
        "Hard Gate" \
        "Selected Scope" \
        "Bead.*Owner" \
        "Definition of Done" \
        "Exit Criteria" \
        "Freeze Protocol" \
        "Amendments"
    do
        if ! grep -qiE "^#+ .*${section}" "$plan"; then
            err "missing required section: ${section}"
            missing=$((missing + 1))
        fi
    done
    [ "$missing" -eq 0 ] && ok "all required sections present"

    # ---- 2. SPEC decomposition must be resolved -------------------------------
    if grep -q 'DECOMPOSE-PENDING' "$plan"; then
        err "DECOMPOSE-PENDING markers remain — SPEC has not replaced epics with children"
    else
        ok "no unresolved DECOMPOSE-PENDING markers"
    fi

    # ---- 3. The denominator must exist ---------------------------------------
    # Blockquote lines (`> ...`) inside Selected Scope are commentary -- typically a
    # "deliberately NOT in scope" list. Counting them inflates the denominator and lets the
    # ownership arm pass for the wrong reason. Strip them before extracting bead IDs.
    local scope_beads owner_beads
    scope_beads=$(sed -n '/^#\+ .*Selected Scope/,/^#\+ .*Bead.*Owner/p' "$plan" \
                  | grep -v '^[[:space:]]*>' \
                  | grep -oE "$BEAD_RX" | tr -d '`' | sed 's/^Excalibur\.Dispatch-//' | sort -u)
    local n_scope; n_scope=$(printf '%s\n' "$scope_beads" | grep -c . || true)

    if [ "$n_scope" -eq 0 ]; then
        err "Selected Scope enumerates ZERO bead IDs — the sprint has no denominator"
    else
        ok "Selected Scope enumerates $n_scope bead IDs"
    fi

    # A target without a list is exactly what the incident shipped.
    if grep -qiE '(target|~)\s*~?[0-9]+\s*beads' "$plan" && [ "$n_scope" -eq 0 ]; then
        err "plan states a bead TARGET but lists no bead IDs — a target is not a denominator"
    fi

    # ---- 4. EVERY in-scope bead needs its OWN owner row ----------------------
    # There is deliberately NO per-wave fallback. A wave-level owner is exactly what the incident had:
    # lanes carried owners, individual beads did not, and 13 beads reached CLOSE unowned. Adding
    # that fallback (to make a legacy plan pass) reopened the very hole this gate exists to close.
    # Legacy plans are grandfathered by the CI cutover, not by weakening the gate.
    #
    # The AUTHORITATIVE check is still --check-tracker: prose ownership that never reached
    # `bd update -a` does not start work, because poll-opcom wakes on an assigned bead.
    owner_beads=$(sed -n '/^#\+ .*Bead.*Owner/,/^#\+ .*Definition of Done/p' "$plan" \
                  | grep -oE "$BEAD_RX" | tr -d '`' | sed 's/^Excalibur\.Dispatch-//' | sort -u)

    local unowned=0
    while IFS= read -r b; do
        [ -n "$b" ] || continue
        printf '%s\n' "$owner_beads" | grep -qx "$b" || { err "bead '$b' is in Selected Scope but has NO owner row"; unowned=$((unowned + 1)); }
    done <<< "$scope_beads"
    if [ "$n_scope" -gt 0 ] && [ "$unowned" -eq 0 ]; then
        ok "every in-scope bead has its own owner row"
        warn "an owner row is prose. Run --check-tracker before IMPLEMENT: it is the assignee in \`bd\` that starts work."
    fi


    # ---- 5. Counting-rule guard ----------------------------------------------
    if ! grep -qi 'shard-sum' "$plan"; then
        warn "no shard-sum counting rule stated (an incident shipped '10 failures' as a commit title; it was 7)"
    else
        ok "shard-sum counting rule stated"
    fi

    # ---- 6. file→owner collision across lane mini-specs (527ciw) --------------
    local sprint_id specs_dir
    sprint_id=$(basename "$plan" | grep -oE 'sprint-[0-9]+' | head -1 | grep -oE '[0-9]+')
    if [ -n "$sprint_id" ]; then
        specs_dir="$(dirname "$plan")/../specs/mini/sprint-${sprint_id}"
        check_file_collisions "$specs_dir"
    else
        warn "could not derive a sprint id from the plan name — skipping file→owner collision check"
    fi

    printf '\n'
    if [ "$fail_count" -eq 0 ]; then
        printf '%s✓ sprint plan is dispatchable%s\n' "$GRN" "$NC"
        return 0
    fi
    printf '%s✗ %d problem(s): do NOT dispatch IMPLEMENT%s\n' "$RED" "$fail_count" "$NC"
    return 1
}

# ---------------------------------------------------------------------------
# --self-test: a gate that cannot fail is not a gate. Prove BOTH arms.
# ---------------------------------------------------------------------------
self_test() {
    local tmp; tmp=$(mktemp -d)
    trap 'rm -rf "$tmp"' RETURN
    local rc=0

    # ---- SAFETY arm: a bad plan (the incident shape) MUST fail --------------------
    cat > "$tmp/bad.md" <<'BAD'
# Sprint 880 Plan
Target ~100 beads, 6 disjoint single-owner lanes
## Lanes (disjoint, single-owner)
| Lane | Owner | Scope |
| A | BackendDeveloper | SPEC decomposes to the band |
BAD
    if validate "$tmp/bad.md" >/dev/null 2>&1; then
        printf '%s✗ SELF-TEST FAIL: the bad-shaped plan PASSED. Gate is vacuous.%s\n' "$RED" "$NC"; rc=1
    else
        printf '%s✓ safety arm: bad-shaped plan (target, no IDs, no owners) is REJECTED%s\n' "$GRN" "$NC"
    fi

    # ---- LIVENESS arm: a good plan MUST pass ---------------------------------
    # Without this arm, a gate that rejects everything would look correct.
    cat > "$tmp/good.md" <<'GOOD'
# Sprint 881 Plan
## Hard Gate (must-fix; blocks TEST)
- `tz2fks` (P0) — aggregate reload
## Selected Scope — the DENOMINATOR
### Wave A [2]
`tz2fks` (P0) — read-path version stamp · `hovqw1` (P1) — reflection tests
## Bead → Owner (FINALIZED before IMPLEMENT)
| Bead | Owner |
| `tz2fks` | BackendDeveloper |
| `hovqw1` | TestsDeveloper |
## Definition of Done (per bead)
- [ ] lock is RED on pre-fix code
## Exit Criteria
Report distinct failing tests, never a shard-sum.
## Freeze Protocol
The integrator is the most bound by it.
## Amendments (append-only)
GOOD
    if validate "$tmp/good.md" >/dev/null 2>&1; then
        printf '%s✓ liveness arm: a well-formed plan is ACCEPTED%s\n' "$GRN" "$NC"
    else
        printf '%s✗ SELF-TEST FAIL: a well-formed plan was REJECTED. Gate rejects everything.%s\n' "$RED" "$NC"
        validate "$tmp/good.md" || true
        rc=1
    fi

    # ---- SAFETY arm 2: scope bead with no owner row MUST fail -----------------
    sed '/| `hovqw1` | TestsDeveloper |/d' "$tmp/good.md" > "$tmp/unowned.md"
    if validate "$tmp/unowned.md" >/dev/null 2>&1; then
        printf '%s✗ SELF-TEST FAIL: an unowned in-scope bead PASSED.%s\n' "$RED" "$NC"; rc=1
    else
        printf '%s✓ safety arm: an in-scope bead with no owner is REJECTED%s\n' "$GRN" "$NC"
    fi

    # ---- SAFETY arm 3: same file, TWO DIFFERENT OWNERS MUST fail (527ciw) -----
    mkdir -p "$tmp/mini"
    printf '**Owner:** BackendDeveloper\n**File surface:** `eng/ci/spa-gate.sh`, `wwwroot/index.html`\n' > "$tmp/mini/lane-A.md"
    printf '**Owner:** FrontendDeveloper\n**File surface:** `eng/ci/spa-gate.sh`\n'                       > "$tmp/mini/lane-B.md"
    fail_count=0
    if check_file_collisions "$tmp/mini" >/dev/null 2>&1; then
        printf '%s✗ SELF-TEST FAIL: a file owned by two DIFFERENT owners PASSED (527ciw vacuous).%s\n' "$RED" "$NC"; rc=1
    else
        printf '%s✓ safety arm: a file claimed by two DIFFERENT-owner lanes is REJECTED (527ciw)%s\n' "$GRN" "$NC"
    fi

    # ---- LIVENESS arm 3a: same file, SAME OWNER is benign → MUST pass ---------
    # A single owner owning two lanes (e.g. Platform owning C+D) is one actor, not a collision.
    printf '**Owner:** BackendDeveloper\n**File surface:** `eng/ci/spa-gate.sh`\n' > "$tmp/mini/lane-B.md"
    fail_count=0
    if check_file_collisions "$tmp/mini" >/dev/null 2>&1; then
        printf '%s✓ liveness arm: same file, SAME owner (one actor) is ACCEPTED (527ciw)%s\n' "$GRN" "$NC"
    else
        printf '%s✗ SELF-TEST FAIL: same-owner overlap was REJECTED (527ciw false-positives).%s\n' "$RED" "$NC"; rc=1
    fi

    # ---- LIVENESS arm 3b: disjoint file surfaces MUST pass -------------------
    printf '**Owner:** FrontendDeveloper\n**File surface:** `.claude/hooks/**`\n' > "$tmp/mini/lane-B.md"
    fail_count=0
    if check_file_collisions "$tmp/mini" >/dev/null 2>&1; then
        printf '%s✓ liveness arm: disjoint lane file surfaces are ACCEPTED (527ciw)%s\n' "$GRN" "$NC"
    else
        printf '%s✗ SELF-TEST FAIL: disjoint lane file surfaces were REJECTED (527ciw over-fires).%s\n' "$RED" "$NC"; rc=1
    fi

    printf '\n'
    [ "$rc" -eq 0 ] && printf '%s✓ self-test GREEN — the gate can pass AND can fail%s\n' "$GRN" "$NC" \
                    || printf '%s✗ self-test RED%s\n' "$RED" "$NC"
    return "$rc"
}

# ---------------------------------------------------------------------------
# --check-tracker: the AUTHORITATIVE ownership arm.
# Prose in a plan does not start work. `poll-opcom` auto-resume only re-wakes an idle
# worker that holds an open ASSIGNED bead. The incident's lanes had owners in markdown and
# `bd list --assignee` was empty, so five agents sat idle on nine unowned P0s.
# ---------------------------------------------------------------------------
check_tracker() {
    local plan="$1"
    command -v bd >/dev/null 2>&1 || { err "bd not on PATH — cannot verify assignees"; return 1; }

    # Must match validate()'s extraction exactly: strip blockquote commentary (a "deliberately NOT
    # in scope" list), or deferred beads get reported as unassigned in-scope work. This extraction
    # is duplicated; that duplication is why the blockquote bug had to be fixed twice.
    local scope_beads
    scope_beads=$(sed -n '/^#\+ .*Selected Scope/,/^#\+ .*Bead.*Owner/p' "$plan" \
                  | grep -v '^[[:space:]]*>' \
                  | grep -oE "$BEAD_RX" | tr -d '`' | sed 's/^Excalibur\.Dispatch-//' | sort -u)
    [ -n "$scope_beads" ] || { err "no in-scope beads to check"; return 1; }

    printf 'Cross-checking tracker assignees for %s\n\n' "$plan"
    local unassigned=0 unreadable=0 total=0
    while IFS= read -r b; do
        [ -n "$b" ] || continue
        total=$((total + 1))
        # `bd` reads were non-deterministic under concurrent daemons (a bead returns its assignee,
        # then empty seconds later) — hence the retry. `` (svacnv) removes that source of
        # flap at the root by answering from the DB rather than a daemon; the retry stays as cheap
        # defence-in-depth against a transient lock.
        #
        # The load-bearing change is distinguishing FAILED-TO-READ from READ-AND-EMPTY. Previously
        # `2>/dev/null` collapsed them: a read that errored produced "" and this loop then ACCUSED the
        # bead of having no assignee — a false error, reported against a tracker we never read. An
        # unreadable tracker is not an unassigned bead.
        local a="" _rc=0 _read_ok=0 _out=""
        for _try in 1 2 3; do
            set +e
            # Pass the BARE short id: bd resolves it in either namespace. Hardcoding a prefix here
            # made this check unable to see beads in the other one — the tracker carries both
            # `Excalibur.Dispatch-` and `Excalibur_Dispatch-`, and a plan citing the latter failed
            # validation for a reason that had nothing to do with the plan.
            _out="$(bd show "$b" 2>&1)"
            _rc=$?
            set -e
            if [ "$_rc" -eq 0 ]; then
                _read_ok=1
                a=$(printf '%s\n' "$_out" | grep -m1 '^Assignee:' | sed 's/^Assignee:[[:space:]]*//')
                [ -n "$a" ] && break
            fi
        done
        if [ "$_read_ok" -eq 0 ]; then
            err "bead '$b': tracker UNREADABLE (\`bd show\` exited $_rc) — assignee UNKNOWN, not absent.${_out:+ Last output: $_out}"
            unreadable=$((unreadable + 1))
        elif [ -z "$a" ]; then
            err "bead '$b' is in scope but has NO assignee in the tracker (bd update $b -a <Agent>)"
            unassigned=$((unassigned + 1))
        fi
    done <<< "$scope_beads"

    printf '\n'

    # An UNREADABLE bead must never be counted as an assigned one. Before this, `unassigned` was the
    # only counter, so a run where every read FAILED scored unassigned=0 and printed the green
    # "all N in-scope beads are assigned — IMPLEMENT may dispatch" — a clean bill of health issued
    # against a tracker the gate never actually read. That is the inert-gate class (testing-patterns
    # §3): the safety half ("no unassigned beads") was satisfied by reading nothing at all.
    if [ "$unreadable" -gt 0 ]; then
        printf '%s✗ REFUSED: %d of %d in-scope beads could not be READ from the tracker.%s\n' \
            "$RED" "$unreadable" "$total" "$NC"
        printf '  Their assignees are UNKNOWN — this is not a report that they are unassigned, and it\n' >&2
        printf '  is emphatically not a report that they are fine. Fix the tracker read, then re-run.\n' >&2
        return 1
    fi

    if [ "$unassigned" -eq 0 ]; then
        printf '%s✓ all %d in-scope beads are assigned in the tracker — IMPLEMENT may dispatch%s\n' "$GRN" "$total" "$NC"
        return 0
    fi
    printf '%s✗ %d of %d in-scope beads unassigned. Workers will sit IDLE. Do NOT dispatch IMPLEMENT.%s\n' \
        "$RED" "$unassigned" "$total" "$NC"
    return 1
}

case "${1:-}" in
    --self-test)     self_test ;;
    --check-tracker) [ -n "${2:-}" ] || { printf 'usage: %s --check-tracker <plan.md>\n' "$0"; exit 2; }
                     check_tracker "$2" ;;
    "" ) printf 'usage: %s <plan.md> | --check-tracker <plan.md> | --self-test\n' "$0"; exit 2 ;;
    * ) validate "$1" ;;
esac
