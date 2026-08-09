#!/usr/bin/env bash
# collect-evidence.test.sh — regression lock for eng/compliance/collect-evidence.sh.
#
# THE DEFECT THIS LOCKS. Every compliance figure in the evidence MANIFEST.json used to be a literal
# inside the document template: 14 of 14 FedRAMP controls documented, 80 GDPR conformance tests, 17
# SOC 2 controls, 12 HIPAA technical controls. No input could make any of them print anything else, so
# a run that downloaded no artifacts at all still asserted complete control documentation — the manifest
# could state `TestResults: 0` and `ControlsDocumented: 14` in the same document. The audience for that
# document is an auditor, and this script is published, so a consumer could generate it against their own
# repository and be handed our numbers as if they were theirs.
#
# The arms are a safety/liveness pair on purpose. Reporting zero for everything would satisfy both
# safety arms and be useless, so C and E fail any version that has learned only to say nothing.
#
#   A  zero collected evidence          -> every framework reports ControlsDocumented 0   (SAFETY)
#   B  a missing evidence directory     -> REFUSE (exit 2), count null, never 0           (SAFETY)
#   C  genuine collected evidence       -> accurate NON-ZERO figures                      (LIVENESS)
#   D  the pre-fix hardcoded manifest   -> arms A and B REJECT it                         (NON-VACUITY)
#   E  a smaller control map            -> ControlsInScope follows the map, not a constant
#   F  a framework absent from the map  -> REFUSE, never "0 of 0 controls"
#   G  a placeholder template alone     -> documents nothing (a blank form is not evidence)
#
# Arm D is what keeps A and B honest. It replays the exact heredoc this script was written to remove and
# asserts the safety assertions fail against it. Without D, A and B could be weakened to nothing and
# still pass, which is the failure mode they exist to prevent.
#
# Hermetic: no network, no gh, no GitHub token, no workflow run. The collector is sourced and its
# manifest generator called directly against fixture directories.
#
# Run: bash eng/compliance/collect-evidence.test.sh   (exit 0 = all green; non-zero = a lock failed)

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COLLECTOR="${EVIDENCE_COLLECTOR:-$SCRIPT_DIR/collect-evidence.sh}"

FAILURES=0
pass() { printf '  [PASS] %s\n' "$1"; }
fail() { printf '  [FAIL] %s\n' "$1" >&2; FAILURES=$((FAILURES + 1)); }

[ -f "$COLLECTOR" ] || { printf 'FATAL: collector not found at %s\n' "$COLLECTOR" >&2; exit 3; }

WORK="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/collectevidencetest.$$")"
mkdir -p "$WORK"
cleanup() { rm -rf "$WORK" 2>/dev/null || true; }
trap cleanup EXIT

echo "collect-evidence.test.sh — locking $COLLECTOR"

# Source the collector, then restore our own shell options: the collector sets `-e`, under which a
# deliberate REFUSE from an arm would abort this test instead of being measured.
# shellcheck source=/dev/null
source "$COLLECTOR"
set +e
set +o pipefail
set +u

# Keep every arm off the network regardless of whether gh is installed and authenticated here.
export EVIDENCE_REPOSITORY="fixture/repo"

# ── helpers ────────────────────────────────────────────────────────────────────────────────────────

# new_package <name> [category ...] — a package root with the named categories present and empty.
# With no categories, all five are created.
new_package() {
    local name=$1; shift
    local root="$WORK/$name"
    local cats="$*"
    [ -n "$cats" ] || cats="test-results security-scans sbom audit-logs rtm"
    local c
    for c in $cats; do mkdir -p "$root/$c"; done
    printf '%s' "$root"
}

# generate <package_root> — runs the manifest generator, prints its REAL exit code.
# The exit is captured on the very next statement: a pipeline or a trailing echo would report its own
# status and mask the refusal this test exists to observe.
generate() {
    FRAMEWORKS="${FW_OVERRIDE:-All}" \
    generate_evidence_manifest "$1" "fixture-run" >/dev/null 2>&1
    local rc=$?
    printf '%s' "$rc"
}

# The reusable SAFETY assertions. Arms A/B call them on the real generator; arm D calls the same
# functions on the pre-fix manifest to prove they can fail.
#
# assert_no_documented_controls <manifest> — no framework may claim a documented control.
assert_no_documented_controls() {
    local manifest=$1
    grep -oE '"ControlsDocumented"[[:space:]]*:[[:space:]]*[0-9]+' "$manifest" 2>/dev/null \
        | grep -qvE ':[[:space:]]*0$' && return 1
    return 0
}

# assert_absent_dir_not_reported_zero <manifest> <category> — the category must be null, not 0.
assert_absent_dir_not_reported_zero() {
    local manifest=$1 category=$2
    grep -qE "\"$category\"[[:space:]]*:[[:space:]]*null" "$manifest" 2>/dev/null
}

json_int() {  # json_int <manifest> <key> — first integer value for a key
    grep -oE "\"$2\"[[:space:]]*:[[:space:]]*[0-9]+" "$1" 2>/dev/null | head -1 | grep -oE '[0-9]+$'
}

# framework_documented <manifest> <framework> — ControlsDocumented within one framework's block
framework_documented() {
    awk -v fw="\"$2\"" '
        index($0, fw) { inblk = 1 }
        inblk && /"ControlsDocumented"/ { gsub(/[^0-9]/, "", $0); print; exit }
    ' "$1"
}
framework_in_scope() {
    awk -v fw="\"$2\"" '
        index($0, fw) { inblk = 1 }
        inblk && /"ControlsInScope"/ { gsub(/[^0-9]/, "", $0); print; exit }
    ' "$1"
}

# ── A. zero collected evidence -> ControlsDocumented 0 everywhere (SAFETY) ─────────────────────────
PKG_A="$(new_package a)"
RC_A="$(generate "$PKG_A")"
if [ "$RC_A" -eq 0 ] && assert_no_documented_controls "$PKG_A/MANIFEST.json"; then
    pass "A: an empty package documents 0 controls (all frameworks)"
else
    fail "A: empty package claimed documented controls, or did not complete (rc=$RC_A): $(grep -o '"ControlsDocumented"[^,]*' "$PKG_A/MANIFEST.json" 2>/dev/null | tr '\n' ' ')"
fi

# The stale fabricated figures must be gone entirely, not merely recomputed to something else.
if grep -qE '"(ConformanceTests|TechnicalControls)"' "$PKG_A/MANIFEST.json" 2>/dev/null; then
    fail "A2: manifest still emits a fabricated figure (ConformanceTests/TechnicalControls)"
else
    pass "A2: no undeliverable fabricated figures remain in the manifest"
fi

# ── B. a missing evidence directory REFUSES rather than reporting 0 (SAFETY) ───────────────────────
PKG_B="$(new_package b test-results security-scans audit-logs rtm)"   # sbom deliberately absent
RC_B="$(generate "$PKG_B")"
if [ "$RC_B" -eq 2 ]; then
    pass "B: a missing evidence directory REFUSES (exit 2)"
else
    fail "B: missing evidence directory did not REFUSE (got rc=$RC_B, expected 2)"
fi
if assert_absent_dir_not_reported_zero "$PKG_B/MANIFEST.json" "sbom"; then
    pass "B2: the absent category is null, not 0 ('never looked' is not 'found none')"
else
    fail "B2: absent category was reported as a number: $(grep -o '"sbom"[^,]*' "$PKG_B/MANIFEST.json" 2>/dev/null)"
fi
if grep -q '"ManifestStatus": "REFUSED"' "$PKG_B/MANIFEST.json" 2>/dev/null; then
    pass "B3: the manifest itself states it was REFUSED"
else
    fail "B3: manifest does not record the refusal a reader needs to see"
fi

# ── C. genuine evidence -> accurate NON-ZERO figures (LIVENESS) ────────────────────────────────────
# Without this arm, a collector that reported zero for everything would pass A and B and be useless.
PKG_C="$(new_package c)"
echo x > "$PKG_C/test-results/junit.xml"
echo x > "$PKG_C/security-scans/codeql.sarif"
echo x > "$PKG_C/sbom/bom.cdx.json"
echo x > "$PKG_C/audit-logs/audit-export.json"
echo x > "$PKG_C/rtm/rtm.csv"
RC_C="$(generate "$PKG_C")"
C_FED="$(framework_documented "$PKG_C/MANIFEST.json" FedRAMP)"
C_SCOPE="$(framework_in_scope "$PKG_C/MANIFEST.json" FedRAMP)"
if [ "$RC_C" -eq 0 ] && [ "${C_FED:-0}" -gt 0 ]; then
    pass "C: a populated package documents a non-zero control count (FedRAMP $C_FED/$C_SCOPE)"
else
    fail "C: populated package reported no documented controls (rc=$RC_C, FedRAMP=${C_FED:-unset}) — reporting zero always is not a fix"
fi
# Accuracy, not merely non-zero: controls mapped to categories with no evidence must stay uncounted,
# so a full package must still be short of its in-scope total.
if [ -n "$C_FED" ] && [ -n "$C_SCOPE" ] && [ "$C_FED" -lt "$C_SCOPE" ]; then
    pass "C2: controls whose evidence is not a pipeline artifact stay uncounted ($C_FED < $C_SCOPE)"
else
    fail "C2: a full download was reported as total control coverage ($C_FED of $C_SCOPE)"
fi
# Removing one category must move the number. A figure nothing can change is the original defect.
rm -rf "${PKG_C:?}/sbom" && mkdir -p "$PKG_C/sbom"
RC_C2="$(generate "$PKG_C")"
C_FED2="$(framework_documented "$PKG_C/MANIFEST.json" FedRAMP)"
if [ "${C_FED2:-0}" -lt "${C_FED:-0}" ]; then
    pass "C3: emptying an evidence category lowers the reported coverage ($C_FED -> $C_FED2)"
else
    fail "C3: coverage did not respond to removing evidence ($C_FED -> ${C_FED2:-unset}) — the figure is not derived"
fi

# ── D. NON-VACUITY: the safety assertions must REJECT the pre-fix manifest ─────────────────────────
# This is the exact heredoc the collector shipped before the repair, reproduced verbatim in the shape
# that matters: file counts computed, coverage hardcoded. If A and B are ever weakened into assertions
# that cannot fail, this arm goes red first.
LEGACY="$WORK/legacy-MANIFEST.json"
cat > "$LEGACY" <<'LEGACYEOF'
{
  "GeneratedAt": "1970-01-01T00:00:00Z",
  "RunId": "fixture-run",
  "FileCounts": {
    "TestResults": 0,
    "SecurityScans": 0,
    "SBOM": 0,
    "AuditLogs": 0
  },
  "Compliance": {
    "FedRAMP": {
      "Controls": 14,
      "ControlsDocumented": 14,
      "EvidenceTypes": ["SBOM", "SecurityScans", "TestResults", "RTM"]
    },
    "GDPR": {
      "Articles": [17, "17(3)", 25, 30, 32],
      "ConformanceTests": 80,
      "EvidenceTypes": ["AuditLogs", "ErasureCertificates", "DataInventory"]
    },
    "SOC2": {
      "Categories": ["Security", "Availability", "ProcessingIntegrity", "Confidentiality"],
      "Controls": 17,
      "EvidenceTypes": ["ControlValidation", "AuditLogs", "Monitoring"]
    },
    "HIPAA": {
      "Safeguards": ["Technical", "Administrative", "Physical"],
      "TechnicalControls": 12,
      "EvidenceTypes": ["AccessLogs", "EncryptionVerification", "AuditTrail"]
    }
  }
}
LEGACYEOF

if assert_no_documented_controls "$LEGACY"; then
    fail "D: the SAFETY assertion PASSED the pre-fix manifest (14 documented controls, 0 evidence) — arm A is vacuous"
else
    pass "D: arm A's assertion REJECTS the pre-fix manifest (14 documented on 0 evidence)"
fi
if assert_absent_dir_not_reported_zero "$LEGACY" "sbom"; then
    fail "D2: the SAFETY assertion PASSED a manifest with no null-vs-zero distinction — arm B is vacuous"
else
    pass "D2: arm B's assertion REJECTS a manifest that cannot distinguish absent from empty"
fi

# ── E. the control map is the source of truth, not a constant ──────────────────────────────────────
FIXTURE_MAP="$WORK/fixture-map.tsv"
printf '# fixture\nFedRAMP\tAC-3\tnone\nFedRAMP\tCM-8\tsbom\n' > "$FIXTURE_MAP"
PKG_E="$(new_package e)"
echo x > "$PKG_E/sbom/bom.cdx.json"
CONTROL_MAP="$FIXTURE_MAP" FW_OVERRIDE="FedRAMP" generate "$PKG_E" >/dev/null
E_SCOPE="$(framework_in_scope "$PKG_E/MANIFEST.json" FedRAMP)"
E_DOC="$(framework_documented "$PKG_E/MANIFEST.json" FedRAMP)"
if [ "${E_SCOPE:-0}" -eq 2 ] && [ "${E_DOC:-0}" -eq 1 ]; then
    pass "E: figures follow the control map (2 in scope, 1 documented), not a baked-in 14"
else
    fail "E: figures did not follow the fixture map (in-scope=${E_SCOPE:-unset} documented=${E_DOC:-unset}, expected 2/1)"
fi

# ── F. an unmapped framework REFUSES rather than reporting "0 of 0" ────────────────────────────────
PKG_F="$(new_package f)"
RC_F="$(FW_OVERRIDE="NotAFramework" generate "$PKG_F")"
if [ "$RC_F" -eq 2 ] && grep -q '"Status": "REFUSED"' "$PKG_F/MANIFEST.json" 2>/dev/null; then
    pass "F: an unmapped framework REFUSES ('0 of 0 controls' would read as a clean bill of health)"
else
    fail "F: unmapped framework was not refused in BOTH places (rc=$RC_F, expected 2; and the framework block must carry Status REFUSED)"
fi

# ── G. a placeholder template is not evidence ──────────────────────────────────────────────────────
# The collector writes an audit-log template on every run, including a run that downloaded nothing.
# If that file counted, an empty package would document audit-log controls on the strength of a blank
# form — the original defect, rebuilt out of a file the script creates itself.
PKG_G="$(new_package g)"
echo '{}' > "$PKG_G/audit-logs/sample-audit-logs.template.json"
RC_G="$(generate "$PKG_G")"
if [ "$RC_G" -eq 0 ] && assert_no_documented_controls "$PKG_G/MANIFEST.json"; then
    pass "G: a placeholder template documents no control"
else
    fail "G: a placeholder template was counted as evidence (rc=$RC_G) — an empty package would claim coverage"
fi

# ── summary ────────────────────────────────────────────────────────────────────────────────────────
echo
if [ "$FAILURES" -eq 0 ]; then
    echo "collect-evidence.test.sh: GREEN — all arms passed."
    exit 0
fi
echo "collect-evidence.test.sh: $FAILURES arm(s) FAILED." >&2
exit 1
