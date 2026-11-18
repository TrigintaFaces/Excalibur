#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
#
# Non-vacuous self-test for assert-tests-executed.sh.
#
# Both arms, because a zero-match detector that only proves it PASSES a real run is
# satisfied by a script that passes everything â€” the exact vacuous-green it exists to
# catch, one level up. The SAFETY arm proves it REFUSES a zero-match; the LIVENESS arm
# proves it does NOT refuse a run where tests executed. A break in either fails this test.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
gate="${here}/assert-tests-executed.sh"

fail() { echo "SELF-TEST FAIL: $1" >&2; exit 1; }

# Fixtures â€” verbatim shapes of real `dotnet test` console output.

# A multi-project run where the filter matched NOTHING anywhere: every project prints the
# "No test matches" line and dotnet exits 0. This is the empty-filter false green.
zero_match_output='Test run for ProjA.dll (.NETCoreApp,Version=v10.0)
No test matches the given testcase filter `FullyQualifiedName~Typo_zzz` in ProjA.dll
Test run for ProjB.dll (.NETCoreApp,Version=v10.0)
No test matches the given testcase filter `FullyQualifiedName~Typo_zzz` in ProjB.dll'

# A multi-project run where ONE project matched and passed while a sibling matched nothing.
# The per-project "No test matches" line is present, but the aggregate Total >= 1 IS the
# signal that the run was real. A naive "No test matches -> refuse" detector would WRONGLY
# refuse this â€” the false positive the aggregate check exists to avoid.
mixed_real_output='Test run for ProjA.dll (.NETCoreApp,Version=v10.0)
No test matches the given testcase filter `Category=X` in ProjA.dll
Test run for ProjB.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 80 ms - ProjB.dll'

# A vacuous "Total: 0" must count as zero-match, not as a real run.
total_zero_output='Passed!  - Failed: 0, Passed: 0, Skipped: 0, Total: 0, Duration: 1 ms - ProjA.dll'

# A zero-match run whose text merely CONTAINS the substring "Subtotal: 5" (contains "total: 5")
# must still REFUSE â€” the word "Total" must be anchored, not substring-matched.
subtotal_trap_output='Test run for ProjA.dll (.NETCoreApp,Version=v10.0)
No test matches the given testcase filter `Category=X` in ProjA.dll
Summary Subtotal: 5 (unrelated line, no real executed Total)'

# â”€â”€ SAFETY arm: a zero-match run is REFUSED (non-zero exit) â”€â”€
if printf '%s' "$zero_match_output" | bash "$gate" --filter "Typo_zzz" >/dev/null 2>&1; then
    fail "a zero-match run was NOT refused (safety arm) â€” the false green passes"
fi

# â”€â”€ SAFETY arm 2: 'Total: 0' is treated as zero-match, not a real run â”€â”€
if printf '%s' "$total_zero_output" | bash "$gate" --filter "empty" >/dev/null 2>&1; then
    fail "a 'Total: 0' run was NOT refused â€” vacuous total read as a real run"
fi

# â”€â”€ SAFETY arm 3: 'Subtotal: 5' does NOT false-pass a zero-match run (word-anchor, not substring) â”€â”€
if printf '%s' "$subtotal_trap_output" | bash "$gate" --filter "Category=X" >/dev/null 2>&1; then
    fail "a 'Subtotal: 5' zero-match run was NOT refused â€” 'Total' substring-matched instead of word-anchored"
fi

# â”€â”€ LIVENESS arm: a run where tests executed is ALLOWED, even with a sibling zero-match â”€â”€
if ! printf '%s' "$mixed_real_output" | bash "$gate" --filter "Category=X" >/dev/null 2>&1; then
    fail "a real multi-project run WAS refused (liveness arm) â€” the per-project 'No test matches' line caused a false positive"
fi

# â”€â”€ executed == expected, and the abort arm â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
# The gate's original property was `Total >= 1`, which certifies a TRUNCATED run: five arms of
# sixteen is not zero. These fixtures are the measured shape of that false green.

# The real one: dotnet aborted mid-run, printed a partial pass line, and exited non-zero. The
# summary line alone is indistinguishable from a complete 5-arm suite.
aborted_partial_output='Test run for ProjA.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 3 s - ProjA.dll
Test Run Aborted.'

# The same partial count WITHOUT an abort marker â€” proves --expect stands on its own and does not
# depend on the abort text being present. A hang-timeout or crash can truncate a run silently.
truncated_no_marker_output='Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 3 s - ProjA.dll'

# A complete run, split across two projects: 9 + 7 = 16. Proves the count is the AGGREGATE, not
# any single project's line â€” a per-line check would wrongly refuse this.
complete_split_output='Test run for ProjA.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9, Duration: 40 ms - ProjA.dll
Test run for ProjB.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 30 ms - ProjB.dll'

# MORE than expected: a stale --expect. Must refuse rather than pass, or the count check tolerates
# its own drift and silently stops being a count check.
overrun_output='Passed!  - Failed: 0, Passed: 20, Skipped: 0, Total: 20, Duration: 90 ms - ProjA.dll'

# â”€â”€ SAFETY arm 4: the measured false green â€” aborted, 5 of 16 â€” is REFUSED â”€â”€
if printf '%s' "$aborted_partial_output" | bash "$gate" --filter "Category=Integration" --expect 16 >/dev/null 2>&1; then
    fail "an ABORTED 5-of-16 run was NOT refused â€” the measured false green still passes"
fi

# â”€â”€ SAFETY arm 5: the abort arm fires WITHOUT --expect, so legacy callers are covered too â”€â”€
if printf '%s' "$aborted_partial_output" | bash "$gate" --filter "Category=Integration" >/dev/null 2>&1; then
    fail "an ABORTED run was NOT refused when --expect was omitted â€” legacy callers still inherit it"
fi

# â”€â”€ SAFETY arm 6: a silent truncation (no abort marker) is caught by the count alone â”€â”€
if printf '%s' "$truncated_no_marker_output" | bash "$gate" --filter "Category=Integration" --expect 16 >/dev/null 2>&1; then
    fail "a truncated 5-of-16 run with NO abort marker was NOT refused â€” --expect is not standing alone"
fi

# â”€â”€ SAFETY arm 7: MORE tests than expected is a stale expectation, not a pass â”€â”€
if printf '%s' "$overrun_output" | bash "$gate" --filter "Category=X" --expect 16 >/dev/null 2>&1; then
    fail "a run executing MORE than --expect was NOT refused â€” the count check tolerates its own drift"
fi

# â”€â”€ SAFETY arm 8: a malformed --expect REFUSES rather than degrading to the weaker >= 1 check â”€â”€
if printf '%s' "$complete_split_output" | bash "$gate" --filter "Category=X" --expect "sixteen" >/dev/null 2>&1; then
    fail "a malformed --expect was silently ignored â€” a typo would permanently weaken the gate"
fi

# â”€â”€ LIVENESS arm 2: an exactly-matching run is ALLOWED, counted as the aggregate across projects â”€â”€
if ! printf '%s' "$complete_split_output" | bash "$gate" --filter "Category=X" --expect 16 >/dev/null 2>&1; then
    fail "a complete 9+7=16 run WAS refused (liveness) â€” the count is not aggregating across projects"
fi

# â”€â”€ LIVENESS arm 3: omitting --expect preserves the ORIGINAL >= 1 contract verbatim â”€â”€
# Without this, the strengthening could silently break every existing caller and the suite would
# still look green on the new arms alone.
if ! printf '%s' "$truncated_no_marker_output" | bash "$gate" --filter "Category=X" >/dev/null 2>&1; then
    fail "a real run WAS refused when --expect was omitted â€” backward compatibility is broken"
fi

# â”€â”€ The refuse code stays the documented 3 for the NEW refusal paths too â”€â”€
set +e
printf '%s' "$aborted_partial_output" | bash "$gate" --filter "X" --expect 16 >/dev/null 2>&1
rc_abort=$?
printf '%s' "$truncated_no_marker_output" | bash "$gate" --filter "X" --expect 16 >/dev/null 2>&1
rc_count=$?
set -e
[ "$rc_abort" -eq 3 ] || fail "abort refuse exit code was ${rc_abort}, expected 3"
[ "$rc_count" -eq 3 ] || fail "count-mismatch refuse exit code was ${rc_count}, expected 3"

# â”€â”€ The exit code on refuse is the documented distinct code, not a generic 1 â”€â”€
set +e
printf '%s' "$zero_match_output" | bash "$gate" --filter "Typo_zzz" >/dev/null 2>&1
rc=$?
set -e
[ "$rc" -eq 3 ] || fail "refuse exit code was ${rc}, expected the documented 3 (distinct from a test failure)"

for invalid in \
    'Skipped! - Failed: 0, Passed: 0, Skipped: 3, Total: 3, Duration: 1 ms - A.dll' \
    'Passed! - Failed: 0, Passed: 1, Skipped: 2, Total: 3, Duration: 1 ms - A.dll' \
    'Failed! - Failed: 1, Passed: 0, Skipped: 0, Total: 1, Duration: 1 ms - A.dll' \
    'Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 3, Duration: 1 ms - A.dll' \
    'Passed! - Failed: 0, Passed: missing, Skipped: 0, Total: 3, Duration: 1 ms - A.dll' \
    'Passed! - Failed: 0, Passed: 18446744073709551617, Skipped: 0, Total: 18446744073709551617, Duration: 1 ms - A.dll' \
    'Total: 10'; do
    if bash "$gate" --filter X <<<"$invalid" >/dev/null 2>&1; then
        fail "invalid summary was accepted: $invalid"
    fi
done
if bash "$gate" --filter X <<<"${mixed_real_output}"$'\n''Skipped! - Failed: 0, Passed: 0, Skipped: 1, Total: 1, Duration: 1 ms - C.dll' >/dev/null 2>&1; then
    fail 'a passing sibling concealed a skipped assembly'
fi

for invalid in 'Failed! - nope' 'Failed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 1 ms - A.dll'; do
    if bash "$gate" <<<"${mixed_real_output}"$'\n'"$invalid" >/dev/null 2>&1; then
        fail "passing sibling concealed invalid summary: $invalid"
    fi
done
for arg in '--expect=' '--unknown' '--expect'; do
    if bash "$gate" "$arg" <<<"$mixed_real_output" >/dev/null 2>&1; then
        fail "invalid option accepted: $arg"
    fi
done
if bash "$gate" --expect '' <<<"$mixed_real_output" >/dev/null 2>&1; then
    fail 'empty expectation silently disabled comparison'
fi
# Same positive result must pass at the declared minimum and refuse above it.
positive='Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2, Duration: 1 ms - A.dll'
bash "$gate" --minimum-executed-tests 2 --expect 2 <<<"$positive" >/dev/null 2>&1 || fail 'minimum equal actual refused'
if bash "$gate" --minimum-executed-tests 3 <<<"$positive" >/dev/null 2>&1; then fail 'below minimum accepted'; fi
for minimum in '' 0 -1 01 +1 1.0 2147483648 abc $'1\n' $'1\t' '1 '; do
    if bash "$gate" --minimum-executed-tests "$minimum" <<<"$positive" >/dev/null 2>&1; then fail "invalid minimum accepted: $minimum"; fi
done
if bash "$gate" --minimum-executed-tests 5 --minimum-executed-tests 1 <<<"$positive" >/dev/null 2>&1; then fail 'duplicate minimum downgraded expectation'; fi

echo "assert-tests-executed self-test: PASS (safety + liveness, non-vacuous)"
