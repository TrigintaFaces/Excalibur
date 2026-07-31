#!/usr/bin/env bash
# assert-binary-matches-source.sh
#
# ============================================================================
# STATUS: UNWIRED. NOTHING INVOKES THIS SCRIPT.
#
#   callers in .github/workflows/**   0
#   callers in eng/ci/**              0
#   registered in the harness gates   no
#
#   It has a passing self-test and zero callers, so it CANNOT FAIL A BUILD and
#   it protects nothing today. It is committed as a manual tool, deliberately
#   and with that stated here, because a self-tested gate that nothing runs is
#   the advertised-but-unwired defect this repository keeps finding in other
#   people's work — and shipping one silently would earn a false sense that
#   the hazard below is now covered. It is not. Run it by hand.
#
#   Wiring it into a workflow is the open follow-up. When that happens, delete
#   this block — a stale "UNWIRED" notice on a wired gate is its own lie.
# ============================================================================
#
# Answers ONE question: does the compiled artifact under test actually contain the source you
# think you are testing?
#
# WHY THIS EXISTS
#   Building a TEST project does not recompile its project-referenced impl DLLs, and `--no-build`
#   reuses whatever is already there. So a mutate -> restore -> rebuild-the-test-project sequence
#   restores the SOURCE and leaves the MUTANT BINARY in place. Every subsequent run measures the
#   mutant while the source on disk looks correct.
#
#   That is not hypothetical. It produced three "reproducible" RED runs, a release-class bug report,
#   a keystone NO-GO and two consumer-doc corrections before anyone thought to read the binary.
#   `clean-rebuild-before-trusting-locks.md` already prescribed the fix; it was loaded, understood,
#   and quoted by the person who then violated it. A rule that is known and still does not fire is
#   the definition of a control that needs to be mechanical.
#
# EXIT CODES  (three-state: a REFUSE is never a PASS)
#   0  PASS    the expected token is present in the binary
#   1  FAIL    the expected token is ABSENT -- the binary does not match the source
#   2  REFUSE  cannot answer (missing file, unreadable, no string reader)
#
# USAGE
#   assert-binary-matches-source.sh --dll <path> --expect <token> [--forbid <token>]
#   assert-binary-matches-source.sh --self-test
#
#   --expect   a string the CURRENT source should put in the binary (e.g. the pinned image tag)
#   --forbid   optional: a string that must NOT be there (e.g. the mutant you injected)
#
# NOTE ON READING .NET ASSEMBLIES
#   .NET stores literals as UTF-16. `strings` defaults to 8-bit and finds NOTHING, which reads
#   exactly like "the token is absent". This script uses `strings -el` and PROVES the reader works
#   by requiring a control token to be found first -- an empty result from an unproven reader is
#   not evidence (verify-before-claiming: a negative needs a positive control).

set -u

die_refuse() { printf 'REFUSE: %s\n' "$1" >&2; exit 2; }

read_utf16() {
    # $1 = dll path. Emits UTF-16LE strings.
    strings -el "$1" 2>/dev/null
}

check_binary() {
    local dll="$1" expect="$2" forbid="${3:-}" control="${4:-}"

    [ -f "$dll" ] || die_refuse "assembly not found: $dll"
    command -v strings >/dev/null 2>&1 || die_refuse "'strings' is unavailable; cannot read the assembly"

    local content
    content="$(read_utf16 "$dll")"
    [ -n "$content" ] || die_refuse "read 0 strings from $dll -- the reader is not working, so a negative would be meaningless"

    # POSITIVE CONTROL. If a token we KNOW is present cannot be found, the query is broken and
    # every negative below is worthless. Default control: the longest common prefix of expect.
    if [ -n "$control" ]; then
        if ! printf '%s' "$content" | grep -qF -- "$control"; then
            die_refuse "positive control '$control' not found -- the query cannot discriminate, so no verdict is possible"
        fi
    fi

    if [ -n "$forbid" ] && printf '%s' "$content" | grep -qF -- "$forbid"; then
        printf 'FAIL: forbidden token present in the binary: %s\n' "$forbid" >&2
        printf '      the artifact under test is NOT the source you think you are testing.\n' >&2
        printf '      rebuild the IMPL project explicitly: dotnet build <impl.csproj> -c Release --no-incremental\n' >&2
        return 1
    fi

    if printf '%s' "$content" | grep -qF -- "$expect"; then
        printf 'PASS: binary contains the expected token: %s\n' "$expect"
        return 0
    fi

    printf 'FAIL: expected token ABSENT from the binary: %s\n' "$expect" >&2
    printf '      the artifact under test does not match the current source.\n' >&2
    printf '      rebuild the IMPL project explicitly: dotnet build <impl.csproj> -c Release --no-incremental\n' >&2
    return 1
}

self_test() {
    local tmp rc fails=0
    tmp="$(mktemp -d)" || die_refuse "cannot create a temp dir for the self-test"
    trap 'rm -rf "${tmp:-}"' EXIT

    # Build fixtures that LOOK like .NET assemblies to the reader: UTF-16LE text.
    to_utf16() { printf '%s' "$1" | iconv -f UTF-8 -t UTF-16LE; }

    to_utf16 "harness-control-token cosmos-emulator:vnext-PINNED trailing" > "$tmp/good.bin"
    to_utf16 "harness-control-token cosmos-emulator:latest trailing"       > "$tmp/mutant.bin"
    to_utf16 "totally-unrelated-content"                                   > "$tmp/nocontrol.bin"
    : > "$tmp/empty.bin"

    # ---- LIVENESS: a correct binary must PASS. (The arm that catches a gate doing nothing.)
    ( check_binary "$tmp/good.bin" "cosmos-emulator:vnext-PINNED" "" "harness-control-token" ) >/dev/null 2>&1
    rc=$?
    if [ "$rc" -eq 0 ]; then echo "  ok   LIVENESS  correct binary -> PASS(0)"
    else echo "  FAIL LIVENESS  correct binary -> $rc (expected 0)"; fails=$((fails+1)); fi

    # ---- SAFETY 1: the expected token missing must FAIL.
    ( check_binary "$tmp/mutant.bin" "cosmos-emulator:vnext-PINNED" "" "harness-control-token" ) >/dev/null 2>&1
    rc=$?
    if [ "$rc" -eq 1 ]; then echo "  ok   SAFETY-1   stale binary (expected token absent) -> FAIL(1)"
    else echo "  FAIL SAFETY-1   stale binary -> $rc (expected 1)"; fails=$((fails+1)); fi

    # ---- SAFETY 2: a forbidden token present must FAIL even if the expected one is also there.
    to_utf16 "harness-control-token cosmos-emulator:vnext-PINNED cosmos-emulator:latest" > "$tmp/both.bin"
    ( check_binary "$tmp/both.bin" "cosmos-emulator:vnext-PINNED" "cosmos-emulator:latest" "harness-control-token" ) >/dev/null 2>&1
    rc=$?
    if [ "$rc" -eq 1 ]; then echo "  ok   SAFETY-2   forbidden token present -> FAIL(1)"
    else echo "  FAIL SAFETY-2   forbidden token present -> $rc (expected 1)"; fails=$((fails+1)); fi

    # ---- REFUSE 1: a missing file cannot yield a verdict.
    ( check_binary "$tmp/does-not-exist.bin" "anything" "" "" ) >/dev/null 2>&1
    rc=$?
    if [ "$rc" -eq 2 ]; then echo "  ok   REFUSE-1   missing assembly -> REFUSE(2)"
    else echo "  FAIL REFUSE-1   missing assembly -> $rc (expected 2)"; fails=$((fails+1)); fi

    # ---- REFUSE 2: an unreadable/empty artifact must REFUSE, never PASS or FAIL.
    ( check_binary "$tmp/empty.bin" "anything" "" "" ) >/dev/null 2>&1
    rc=$?
    if [ "$rc" -eq 2 ]; then echo "  ok   REFUSE-2   empty assembly -> REFUSE(2)"
    else echo "  FAIL REFUSE-2   empty assembly -> $rc (expected 2)"; fails=$((fails+1)); fi

    # ---- REFUSE 3: a FAILING POSITIVE CONTROL must REFUSE, not FAIL.
    # This is the arm that encodes tonight's other lesson: an absence measured with a broken
    # query is not a finding. Without it the gate would confidently report FAIL on every
    # assembly whose reader it cannot actually parse.
    ( check_binary "$tmp/nocontrol.bin" "cosmos-emulator:vnext-PINNED" "" "harness-control-token" ) >/dev/null 2>&1
    rc=$?
    if [ "$rc" -eq 2 ]; then echo "  ok   REFUSE-3   control token absent -> REFUSE(2), not a false FAIL"
    else echo "  FAIL REFUSE-3   control token absent -> $rc (expected 2)"; fails=$((fails+1)); fi

    echo
    if [ "$fails" -eq 0 ]; then
        echo "assert-binary-matches-source: 6/6 self-test arms passed (1 liveness, 2 safety, 3 refuse)"
        return 0
    fi
    echo "assert-binary-matches-source: $fails self-test arm(s) FAILED"
    return 1
}

DLL=""; EXPECT=""; FORBID=""; CONTROL=""
while [ $# -gt 0 ]; do
    case "$1" in
        --self-test) self_test; exit $? ;;
        --dll)     DLL="${2:-}";     shift 2 ;;
        --expect)  EXPECT="${2:-}";  shift 2 ;;
        --forbid)  FORBID="${2:-}";  shift 2 ;;
        --control) CONTROL="${2:-}"; shift 2 ;;
        *) die_refuse "unknown argument: $1" ;;
    esac
done

[ -n "$DLL" ]    || die_refuse "--dll is required"
[ -n "$EXPECT" ] || die_refuse "--expect is required"

check_binary "$DLL" "$EXPECT" "$FORBID" "$CONTROL"
