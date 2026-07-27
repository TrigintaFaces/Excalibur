#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
#
# Non-vacuous self-test for run-filtered-tests.sh.
#
# The wrapper's whole purpose is that a zero-executed run must NOT report success, so the arms below
# drive a STUB `dotnet` (via DOTNET_BIN) that reproduces each real console-logger shape. A stub is the
# only way to exercise the plumbing deterministically: a real filtered run cannot be made to match
# nothing on demand without depending on the test tree staying exactly as it is today.
#
# Every arm captures the exit code DIRECTLY. Not through a pipe, not through a trailing echo.

set -uo pipefail

GATE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="$GATE_DIR/run-filtered-tests.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

pass=0
fail=0

note() { printf '%s\n' "$1"; }

check() {  # check <label> <expected-exit> <actual-exit>
    if [ "$2" = "$3" ]; then
        note "  ok   $1 (exit $3)"; pass=$((pass + 1))
    else
        note "  FAIL $1: expected exit $2, got $3"; fail=$((fail + 1))
    fi
}

make_stub() {  # make_stub <exit-code> <stdout-body>
    printf '#!/usr/bin/env bash\ncat <<'"'"'STUBEOF'"'"'\n%s\nSTUBEOF\nexit %s\n' "$2" "$1" > "$WORK/dotnet"
    chmod +x "$WORK/dotnet"
}

note "[run-filtered-tests.test] running..."

# ── SAFETY: a zero-executed run must not pass ───────────────────────────────────────────────────────
make_stub 0 "No test matches the given testcase filter 'Category=Nope' in /x/y.dll"
DOTNET_BIN="$WORK/dotnet" bash "$GATE" --filter "Category=Nope" --log "$WORK/a.log" -- proj.slnf >/dev/null 2>&1
check "SAFETY: filter matched nothing, dotnet exited 0 -> REFUSE not pass" 3 $?

# ── LIVENESS: a real run that executed tests and passed must still pass ─────────────────────────────
make_stub 0 "Passed!  - Failed: 0, Passed: 412, Skipped: 0, Total: 412, Duration: 3 s"
DOTNET_BIN="$WORK/dotnet" bash "$GATE" --filter "Category=Unit" --log "$WORK/b.log" -- proj.slnf >/dev/null 2>&1
check "LIVENESS: 412 executed and green -> pass" 0 $?

# ── SAFETY: a real test failure must surface as the TEST's exit, not as a refusal ───────────────────
make_stub 1 "Failed!  - Failed: 3, Passed: 409, Skipped: 0, Total: 412, Duration: 3 s"
DOTNET_BIN="$WORK/dotnet" bash "$GATE" --filter "Category=Unit" --log "$WORK/c.log" -- proj.slnf >/dev/null 2>&1
check "SAFETY: tests ran and failed -> the test exit wins over the refusal" 1 $?

# ── SAFETY: the pipe must not swallow the test's exit (the tee footgun this wrapper exists to fix) ──
make_stub 7 "Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 1 s"
DOTNET_BIN="$WORK/dotnet" bash "$GATE" --filter "Category=Unit" --log "$WORK/d.log" -- proj.slnf >/dev/null 2>&1
check "SAFETY: non-zero test exit survives the tee pipeline" 7 $?

# ── ENV: refuse to run without a filter, rather than silently becoming an unguarded plain run ───────
DOTNET_BIN="$WORK/dotnet" bash "$GATE" --log "$WORK/e.log" -- proj.slnf >/dev/null 2>&1
check "ENV: no --filter -> cannot evaluate" 2 $?

DOTNET_BIN="$WORK/dotnet" bash "$GATE" --filter "Category=Unit" >/dev/null 2>&1
check "ENV: no dotnet args -> cannot evaluate" 2 $?

# ── LIVENESS: the log is written where the caller asked, so CI can upload it ────────────────────────
make_stub 0 "Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9, Duration: 1 s"
DOTNET_BIN="$WORK/dotnet" bash "$GATE" --filter "Category=Unit" --log "$WORK/f.log" -- proj.slnf >/dev/null 2>&1
if [ -s "$WORK/f.log" ]; then
    note "  ok   LIVENESS: run output captured to the requested log"; pass=$((pass + 1))
else
    note "  FAIL LIVENESS: requested log is empty or missing"; fail=$((fail + 1))
fi

note "[run-filtered-tests.test] $pass passed, $fail failed"
[ "$fail" -eq 0 ] || exit 1
exit 0
