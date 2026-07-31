#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
#
# Non-vacuous self-test for assert-tests-executed.sh (bead 885jxd).
#
# Both arms, because a zero-match detector that only proves it PASSES a real run is
# satisfied by a script that passes everything — the exact vacuous-green it exists to
# catch, one level up. The SAFETY arm proves it REFUSES a zero-match; the LIVENESS arm
# proves it does NOT refuse a run where tests executed. A break in either fails this test.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
gate="${here}/assert-tests-executed.sh"

fail() { echo "SELF-TEST FAIL: $1" >&2; exit 1; }

# Fixtures — verbatim shapes of real `dotnet test` console output.

# A multi-project run where the filter matched NOTHING anywhere: every project prints the
# "No test matches" line and dotnet exits 0. This is the 885jxd false green.
zero_match_output='Test run for ProjA.dll (.NETCoreApp,Version=v10.0)
No test matches the given testcase filter `FullyQualifiedName~Typo_zzz` in ProjA.dll
Test run for ProjB.dll (.NETCoreApp,Version=v10.0)
No test matches the given testcase filter `FullyQualifiedName~Typo_zzz` in ProjB.dll'

# A multi-project run where ONE project matched and passed while a sibling matched nothing.
# The per-project "No test matches" line is present, but the aggregate Total >= 1 IS the
# signal that the run was real. A naive "No test matches -> refuse" detector would WRONGLY
# refuse this — the false positive the aggregate check exists to avoid.
mixed_real_output='Test run for ProjA.dll (.NETCoreApp,Version=v10.0)
No test matches the given testcase filter `Category=X` in ProjA.dll
Test run for ProjB.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 80 ms - ProjB.dll'

# A vacuous "Total: 0" must count as zero-match, not as a real run.
total_zero_output='Passed!  - Failed: 0, Passed: 0, Skipped: 0, Total: 0, Duration: 1 ms - ProjA.dll'

# A zero-match run whose text merely CONTAINS the substring "Subtotal: 5" (contains "total: 5")
# must still REFUSE — the word "Total" must be anchored, not substring-matched.
subtotal_trap_output='Test run for ProjA.dll (.NETCoreApp,Version=v10.0)
No test matches the given testcase filter `Category=X` in ProjA.dll
Summary Subtotal: 5 (unrelated line, no real executed Total)'

# ── SAFETY arm: a zero-match run is REFUSED (non-zero exit) ──
if printf '%s' "$zero_match_output" | bash "$gate" --filter "Typo_zzz" >/dev/null 2>&1; then
    fail "a zero-match run was NOT refused (safety arm) — the false green passes"
fi

# ── SAFETY arm 2: 'Total: 0' is treated as zero-match, not a real run ──
if printf '%s' "$total_zero_output" | bash "$gate" --filter "empty" >/dev/null 2>&1; then
    fail "a 'Total: 0' run was NOT refused — vacuous total read as a real run"
fi

# ── SAFETY arm 3: 'Subtotal: 5' does NOT false-pass a zero-match run (word-anchor, not substring) ──
if printf '%s' "$subtotal_trap_output" | bash "$gate" --filter "Category=X" >/dev/null 2>&1; then
    fail "a 'Subtotal: 5' zero-match run was NOT refused — 'Total' substring-matched instead of word-anchored"
fi

# ── LIVENESS arm: a run where tests executed is ALLOWED, even with a sibling zero-match ──
if ! printf '%s' "$mixed_real_output" | bash "$gate" --filter "Category=X" >/dev/null 2>&1; then
    fail "a real multi-project run WAS refused (liveness arm) — the per-project 'No test matches' line caused a false positive"
fi

# ── executed == expected, and the abort arm ─────────────────────────────────────────────────────
# The gate's original property was `Total >= 1`, which certifies a TRUNCATED run: five arms of
# sixteen is not zero. These fixtures are the measured shape of that false green.

# The real one: dotnet aborted mid-run, printed a partial pass line, and exited non-zero. The
# summary line alone is indistinguishable from a complete 5-arm suite.
aborted_partial_output='Test run for ProjA.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 3 s - ProjA.dll
Test Run Aborted.'

# The same partial count WITHOUT an abort marker — proves --expect stands on its own and does not
# depend on the abort text being present. A hang-timeout or crash can truncate a run silently.
truncated_no_marker_output='Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 3 s - ProjA.dll'

# A complete run, split across two projects: 9 + 7 = 16. Proves the count is the AGGREGATE, not
# any single project's line — a per-line check would wrongly refuse this.
complete_split_output='Test run for ProjA.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9, Duration: 40 ms - ProjA.dll
Test run for ProjB.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 30 ms - ProjB.dll'

# MORE than expected: a stale --expect. Must refuse rather than pass, or the count check tolerates
# its own drift and silently stops being a count check.
overrun_output='Passed!  - Failed: 0, Passed: 20, Skipped: 0, Total: 20, Duration: 90 ms - ProjA.dll'

# ── SAFETY arm 4: the measured false green — aborted, 5 of 16 — is REFUSED ──
if printf '%s' "$aborted_partial_output" | bash "$gate" --filter "Category=Integration" --expect 16 >/dev/null 2>&1; then
    fail "an ABORTED 5-of-16 run was NOT refused — the measured false green still passes"
fi

# ── SAFETY arm 5: the abort arm fires WITHOUT --expect, so legacy callers are covered too ──
if printf '%s' "$aborted_partial_output" | bash "$gate" --filter "Category=Integration" >/dev/null 2>&1; then
    fail "an ABORTED run was NOT refused when --expect was omitted — legacy callers still inherit it"
fi

# ── SAFETY arm 6: a silent truncation (no abort marker) is caught by the count alone ──
if printf '%s' "$truncated_no_marker_output" | bash "$gate" --filter "Category=Integration" --expect 16 >/dev/null 2>&1; then
    fail "a truncated 5-of-16 run with NO abort marker was NOT refused — --expect is not standing alone"
fi

# ── SAFETY arm 7: MORE tests than expected is a stale expectation, not a pass ──
if printf '%s' "$overrun_output" | bash "$gate" --filter "Category=X" --expect 16 >/dev/null 2>&1; then
    fail "a run executing MORE than --expect was NOT refused — the count check tolerates its own drift"
fi

# ── SAFETY arm 8: a malformed --expect REFUSES rather than degrading to the weaker >= 1 check ──
if printf '%s' "$complete_split_output" | bash "$gate" --filter "Category=X" --expect "sixteen" >/dev/null 2>&1; then
    fail "a malformed --expect was silently ignored — a typo would permanently weaken the gate"
fi

# ── LIVENESS arm 2: an exactly-matching run is ALLOWED, counted as the aggregate across projects ──
if ! printf '%s' "$complete_split_output" | bash "$gate" --filter "Category=X" --expect 16 >/dev/null 2>&1; then
    fail "a complete 9+7=16 run WAS refused (liveness) — the count is not aggregating across projects"
fi

# ── LIVENESS arm 3: omitting --expect preserves the ORIGINAL >= 1 contract verbatim ──
# Without this, the strengthening could silently break every existing caller and the suite would
# still look green on the new arms alone.
if ! printf '%s' "$truncated_no_marker_output" | bash "$gate" --filter "Category=X" >/dev/null 2>&1; then
    fail "a real run WAS refused when --expect was omitted — backward compatibility is broken"
fi

# ── The refuse code stays the documented 3 for the NEW refusal paths too ──
set +e
printf '%s' "$aborted_partial_output" | bash "$gate" --filter "X" --expect 16 >/dev/null 2>&1
rc_abort=$?
printf '%s' "$truncated_no_marker_output" | bash "$gate" --filter "X" --expect 16 >/dev/null 2>&1
rc_count=$?
set -e
[ "$rc_abort" -eq 3 ] || fail "abort refuse exit code was ${rc_abort}, expected 3"
[ "$rc_count" -eq 3 ] || fail "count-mismatch refuse exit code was ${rc_count}, expected 3"

# ── The exit code on refuse is the documented distinct code, not a generic 1 ──
set +e
printf '%s' "$zero_match_output" | bash "$gate" --filter "Typo_zzz" >/dev/null 2>&1
rc=$?
set -e
[ "$rc" -eq 3 ] || fail "refuse exit code was ${rc}, expected the documented 3 (distinct from a test failure)"

echo "assert-tests-executed self-test: PASS (safety + liveness, non-vacuous)"
