#!/usr/bin/env bash
# staged-secret-scan.harness-lock.sh — INDEPENDENT lock (author != impl) for staged-secret-scan.sh.
# S886 / MS-A2 / 74nt46 (FR-A2 / GUIDE Ruling R3 — the real blocking staged secret scan).
#
# WHAT THIS LOCK BINDS (behavior, not line numbers)
#   Drives the REAL scanner inside throwaway temp git repos (a staged fixture per arm) and asserts:
#     * SAFETY   — a staged added line carrying an AWS key / GitHub PAT / PEM private-key header
#                  → scanner exits 1 (commit would be blocked). RED against the pre-fix hook where NO
#                  scan runs, and RED against a no-op scanner (the inert control never fires).
#     * LIVENESS — a clean staged tree → scanner exits 0 (a normal commit is allowed). This is the arm
#                  that catches a "block everything" scanner — the S880 inert-control class. Do NOT omit.
#     * ALLOWLIST — a staged secret on a line marked `# pragma: allowlist secret` → scanner exits 0
#                  (the exemption is honoured at the authoring site; the scanner is not weakened —
#                  the SAFETY arms prove it still catches a non-pragma key).
#
# The example tokens below are assembled by concatenation (never a contiguous literal in this file) and
# additionally carry a pragma, so the REAL scanner running on THIS lock's own commit does not trip.
#
# Override the scanner under test:  SCAN_BIN=/path/to/scanner bash <thislock>   (used to prove non-vacuity
#   against a no-op / block-all stub — the real gate uses the default path).
#
# Usage:  bash eng/ci/staged-secret-scan.harness-lock.sh
# Exit:   0 all arms pass · 1 an arm failed · 2 cannot evaluate (git/scanner missing)

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

HARNESS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCAN="${SCAN_BIN:-$HARNESS_DIR/staged-secret-scan.sh}"
[ -f "$SCAN" ] || { echo "scanner under lock not found: $SCAN" >&2; exit 2; }
command -v git >/dev/null 2>&1 || { echo "git not available — cannot evaluate" >&2; exit 2; }
SCAN_ABS="$(cd "$(dirname "$SCAN")" && pwd)/$(basename "$SCAN")"

PASS=0; FAIL=0
ok()  { PASS=$((PASS + 1)); printf '  PASS  %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  FAIL  %s\n     -> %s\n' "$1" "$2" >&2; }

# Assemble example tokens WITHOUT a contiguous literal in this source file.
AWS_P="AKIA"                                                          # pragma: allowlist secret
AWS_TOKEN="${AWS_P}IOSFODNN7EXAMPLE"                                  # pragma: allowlist secret  (AKIA + 16)
ANT_P="sk-"; ANT_TOKEN="${ANT_P}ant-api03-AbCdEfGhIjKlMnOpQrStUvWxYz0123"   # pragma: allowlist secret  (sk- + 20+)
GH_P="ghp_"                                                           # pragma: allowlist secret
GH_TOKEN="${GH_P}0123456789abcdefghij0123456789abcdef"               # pragma: allowlist secret  (ghp_ + 36)
PEM_TOKEN="-----BEGIN RSA PRIVATE ""KEY-----"                        # pragma: allowlist secret

# run_scan <rc-var-unused> <file-content...> — writes content to a staged fixture in a fresh temp repo,
# runs the scanner with cwd=repo, echoes the scanner's exit code.
run_scan() {
    local content="$1"
    local repo; repo="$(mktemp -d)"
    (
        cd "$repo" || exit 99
        git init -q . >/dev/null 2>&1 || exit 99
        printf '%s\n' "$content" > staged.txt
        git add staged.txt >/dev/null 2>&1 || exit 99
        bash "$SCAN_ABS" >/dev/null 2>&1
    )
    local rc=$?
    rm -rf "$repo"
    echo "$rc"
}

echo "staged-secret-scan — INDEPENDENT lock (FR-A2 / R3), scanner=$SCAN_ABS"
echo

# SAFETY — AWS access key id planted → blocked.
rc="$(run_scan "config aws_key = ${AWS_TOKEN}")"
[ "$rc" = "1" ] && ok "SAFETY: staged AWS access key → scanner exits 1 (blocked)" \
                || bad "SAFETY: staged AWS key must be blocked" "scanner exit $rc (0=inert no-op / not wired)"

# SAFETY — GitHub PAT planted → blocked.
rc="$(run_scan "token: ${GH_TOKEN}")"
[ "$rc" = "1" ] && ok "SAFETY: staged GitHub PAT → scanner exits 1 (blocked)" \
                || bad "SAFETY: staged GitHub PAT must be blocked" "scanner exit $rc"

# SAFETY — PEM private-key header planted → blocked.
rc="$(run_scan "$PEM_TOKEN")"
[ "$rc" = "1" ] && ok "SAFETY: staged PEM private-key header → scanner exits 1 (blocked)" \
                || bad "SAFETY: staged PEM header must be blocked" "scanner exit $rc"

# LIVENESS — a clean staged tree → allowed. Catches a block-everything (inert) scanner.
rc="$(run_scan "public sealed class Foo { public int Bar => 42; }")"
[ "$rc" = "0" ] && ok "LIVENESS: clean staged tree → scanner exits 0 (commit allowed)" \
                || bad "LIVENESS: a clean commit MUST be allowed (inert block-all scanner?)" "scanner exit $rc"

# SAFETY — the Anthropic/OpenAI key shape is blocked. This pattern requires a leading token boundary,
# so it needs its own arm: the three arms above all use prefixes that cannot occur inside a word, and
# a control on those cannot say whether THIS one still fires.
rc="$(run_scan "var key = \"${ANT_TOKEN}\";")"
[ "$rc" = "1" ] && ok "SAFETY: staged Anthropic/OpenAI key -> scanner exits 1 (blocked)"                 || bad "SAFETY: an Anthropic/OpenAI key MUST be blocked" "scanner exit $rc"

# LIVENESS — a hyphenated identifier CONTAINING 'sk-' is not a key and must not block a commit.
# 'task-delay-syncwait-baseline' puts twenty-three word characters after 'sk-', and without the
# boundary it matched: a gate artifact named after a task was rejected as an Anthropic key. This arm
# fails against the unbounded pattern, which is what makes it a real check rather than a restatement.
rc="$(run_scan "BASELINE=eng/ci/task-delay-syncwait-baseline.txt")"
[ "$rc" = "0" ] && ok "LIVENESS: 'sk-' inside a word (task-delay-...) -> scanner exits 0 (not a key)"                 || bad "LIVENESS: an identifier containing 'sk-' MUST NOT be read as a key" "scanner exit $rc"

# ALLOWLIST — a real secret on a pragma-marked line → exempted (scanner not weakened; SAFETY arms hold).
rc="$(run_scan "aws_key = ${AWS_TOKEN}   # pragma: allowlist secret")"
[ "$rc" = "0" ] && ok "ALLOWLIST: pragma-marked example token → scanner exits 0 (exempted at authoring site)" \
                || bad "ALLOWLIST: a pragma-marked line must be exempted" "scanner exit $rc"

echo
printf 'passed %d · failed %d\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ] || exit 1
