#!/usr/bin/env bash
# aot-publish-validation-exit.test.sh — the AOT validator must not report a PASS it did not earn.
#
# THE DEFECT THIS LOCKS (3ridgy): the verdict read `$publishExitCode -gt 1`. A failed `dotnet publish`
# exits **1** — the ordinary MSBuild failure code — so every real publish failure slipped the guard.
# The script then found zero IL2xxx/IL3xxx warnings (a publish that FAILED emits none, having never got
# far enough to emit any) and printed "AOT validation PASSED - zero warnings" and exit 0.
#
# It is a safety property satisfied by INACTION: "no AOT warnings were found" is trivially true when
# nothing was analysed. The summary printed "Publish: FAILED (exit 1)" one screen earlier and the
# verdict discarded it.
#
# METHOD — behavioural, not structural. A `dotnet` shim on PATH returns the exit code under test, so the
# REAL script runs its REAL verdict against it. A grep for `-ne 0` would pass against a script whose
# logic had been broken some other way; this asserts the exit code a caller actually receives.
#
#   SAFETY   publish exits 1 (the regression)  -> validator MUST NOT exit 0
#   SAFETY   publish exits 2                   -> validator MUST NOT exit 0
#   LIVENESS the guard is not "fail always"    -> asserted structurally, see note at the arm
#
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VALIDATOR="$SCRIPT_DIR/Invoke-AotPublishValidation.ps1"

pass=0; fail=0
note() { echo "  $1"; }

if ! command -v pwsh >/dev/null 2>&1; then
    echo "[aot-publish-validation-exit] CANNOT EVALUATE — pwsh not on PATH." >&2
    echo "  A skipped lock is not a passing lock; reporting REFUSE so this is not read as green." >&2
    exit 2
fi
[ -f "$VALIDATOR" ] || { echo "[aot-publish-validation-exit] CANNOT EVALUATE — validator not found: $VALIDATOR" >&2; exit 2; }

echo "[aot-publish-validation-exit] running..."

run_with_shimmed_dotnet() { # $1 = exit code the fake dotnet returns ; echoes the validator's exit code
    local code="$1"
    local shim out
    shim="$(mktemp -d)"
    out="$(mktemp -d)"
    # Both forms: pwsh on Windows resolves dotnet.cmd; on POSIX it resolves the extensionless file.
    cat > "$shim/dotnet" <<EOF
#!/usr/bin/env bash
echo "fake dotnet: simulated publish failure"
exit $code
EOF
    chmod +x "$shim/dotnet"
    cat > "$shim/dotnet.cmd" <<EOF
@echo off
echo fake dotnet: simulated publish failure
exit /b $code
EOF
    PATH="$shim:$PATH" pwsh -NoProfile -File "$VALIDATOR" -OutputPath "$out" >/dev/null 2>&1
    local rc=$?
    rm -rf "$shim" "$out"
    printf '%s' "$rc"
}

# ── SAFETY: exit 1 is the regression that shipped ───────────────────────────────────────────────────
rc="$(run_with_shimmed_dotnet 1)"
if [ "$rc" -ne 0 ]; then
    note "ok   SAFETY: publish exit 1 does NOT yield a PASS (validator exit $rc)"; pass=$((pass + 1))
else
    note "FAIL SAFETY: publish exit 1 yielded exit 0 — the false GREEN is back (3ridgy regression)"; fail=$((fail + 1))
fi

# ── SAFETY: exit 2 was already caught; assert it stays caught ───────────────────────────────────────
rc="$(run_with_shimmed_dotnet 2)"
if [ "$rc" -ne 0 ]; then
    note "ok   SAFETY: publish exit 2 does NOT yield a PASS (validator exit $rc)"; pass=$((pass + 1))
else
    note "FAIL SAFETY: publish exit 2 yielded exit 0"; fail=$((fail + 1))
fi

# ── LIVENESS ────────────────────────────────────────────────────────────────────────────────────────
# The honest liveness arm — "a SUCCEEDING publish still yields exit 0" — cannot be run here: a real
# successful AOT publish takes several minutes and needs the full toolchain, and a shim returning 0
# produces no publish artifacts, so the script fails downstream for an unrelated reason. Asserting the
# guard is not "fail always" is therefore done structurally, which is weaker and is labelled as such
# rather than dressed up as behavioural.
if grep -q 'publishExitCode -ne 0' "$VALIDATOR" && ! grep -q 'publishExitCode -gt 1' "$VALIDATOR"; then
    note "ok   LIVENESS (structural): verdict tests -ne 0 and the -gt 1 regression is absent"; pass=$((pass + 1))
else
    note "FAIL LIVENESS (structural): verdict does not test '-ne 0', or '-gt 1' has returned"; fail=$((fail + 1))
fi

echo "[aot-publish-validation-exit] $pass passed, $fail failed"
[ "$fail" -eq 0 ] || exit 1
exit 0
