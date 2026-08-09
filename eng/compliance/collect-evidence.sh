#!/usr/bin/env bash

#
# Excalibur Compliance Evidence Collector (Bash)
#
# This script collects evidence from GitHub Actions workflow runs, including:
# - Test results (JUnit XML, coverage reports)
# - Security scan results (SAST, DAST, container scans, secrets)
# - SBOM artifacts (CycloneDX JSON/XML)
# - Audit log samples
# - Requirements Traceability Matrix (RTM)
#
# Evidence is organized by compliance framework (FedRAMP, GDPR, SOC 2, HIPAA).
#
# WHAT THE MANIFEST MAY ASSERT
#
#   Every compliance figure in MANIFEST.json is DERIVED from the evidence actually collected into the
#   package, or it is not emitted. There are no coverage constants in the manifest template. A run that
#   downloads nothing reports zero controls documented, because the number is computed from the files
#   present — not from a literal that no input can change.
#
#   Control identifiers and their evidence categories come from control-evidence-map.tsv, next to this
#   script; the manifest names that file as its source so a reader can check the arithmetic. Controls
#   whose evidence is documentation or a business process rather than a pipeline artifact are mapped
#   `none` and never count as documented, however full the package is.
#
#   A missing evidence directory is REFUSED, not reported as zero. "The directory is not there" and
#   "the directory is empty" are different facts, and only one of them is a measurement.
#
# Usage:
#   ./collect-evidence.sh [OPTIONS]
#
# Options:
#   -o, --output PATH          Output directory (default: ./compliance-evidence)
#   -r, --run-id ID            GitHub Actions run ID (default: latest successful)
#   -f, --frameworks LIST      Comma-separated frameworks (default: All)
#                              Valid: FedRAMP,GDPR,SOC2,HIPAA,All
#   -a, --no-audit-logs        Skip audit log samples
#   -m, --max-samples N        Maximum audit samples (default: 100)
#   -h, --help                 Show this help message
#
# Examples:
#   ./collect-evidence.sh
#   ./collect-evidence.sh -o /tmp/evidence -f FedRAMP,SOC2
#   ./collect-evidence.sh -r 123456789 -m 50
#
# Prerequisites:
#   - GitHub CLI (gh) installed and authenticated: gh auth login
#   - jq for JSON processing: apt-get install jq (or brew install jq)
#
# Exit codes (three states — a REFUSE is not a pass):
#   0  evidence collected and every reported figure was derived
#   1  error (missing prerequisite, no successful workflow run)
#   2  REFUSE — the package was written, but at least one figure could not be derived (a missing
#      evidence directory, a missing or malformed control map, an unknown framework). The manifest
#      says so in ManifestStatus/RefusalReasons rather than printing a number nobody measured.
#

set -euo pipefail

# Location of the control map (source of truth for control identifiers and their evidence categories).
# Overridable so the self-test can point at a fixture map without touching the committed one.
CONTROL_MAP="${CONTROL_EVIDENCE_MAP:-$(cd "$(dirname "${BASH_SOURCE[0]}")" 2>/dev/null && pwd)/control-evidence-map.tsv}"

# The evidence categories that exist in a package. A category in the map outside this set is a typo or
# a layout change, and scoring its control as "not met" would quietly under-report coverage while
# looking like a measurement — so an unrecognised category REFUSES instead.
EVIDENCE_CATEGORIES="test-results security-scans sbom audit-logs rtm"

# Reasons the manifest could not derive something. Non-empty => exit 2.
REFUSAL_REASONS=()

# Coverage the manifest derived, as `framework<TAB>in_scope<TAB>documented` lines. The README is
# rendered from this rather than from its own constants: two documents in one package that state
# coverage independently will eventually state it differently, and a reader has no way to tell which
# of them was computed.
COVERAGE_SUMMARY=""

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Default values
OUTPUT_PATH="./compliance-evidence"
RUN_ID=""
FRAMEWORKS="All"
INCLUDE_AUDIT_LOGS=true
MAX_AUDIT_SAMPLES=100

# Parse command line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -o|--output)
                OUTPUT_PATH="$2"
                shift 2
                ;;
            -r|--run-id)
                RUN_ID="$2"
                shift 2
                ;;
            -f|--frameworks)
                FRAMEWORKS="$2"
                shift 2
                ;;
            -a|--no-audit-logs)
                INCLUDE_AUDIT_LOGS=false
                shift
                ;;
            -m|--max-samples)
                MAX_AUDIT_SAMPLES="$2"
                shift 2
                ;;
            -h|--help)
                grep '^#' "$0" | sed 's/^# //g; s/^#//g'
                exit 0
                ;;
            *)
                echo -e "${RED}Unknown option: $1${NC}"
                exit 1
                ;;
        esac
    done
}

# Check prerequisites
check_prerequisites() {
    echo -e "${CYAN}Checking prerequisites...${NC}"

    # Check GitHub CLI
    if ! command -v gh &> /dev/null; then
        echo -e "${RED}✗ GitHub CLI (gh) is not installed${NC}"
        echo -e "${YELLOW}  Install from: https://cli.github.com/${NC}"
        exit 1
    fi

    # Check authentication
    if ! gh auth status &> /dev/null; then
        echo -e "${RED}✗ GitHub CLI is not authenticated${NC}"
        echo -e "${YELLOW}  Run: gh auth login${NC}"
        exit 1
    fi

    # Check jq
    if ! command -v jq &> /dev/null; then
        echo -e "${RED}✗ jq is not installed${NC}"
        echo -e "${YELLOW}  Install: apt-get install jq (or brew install jq)${NC}"
        exit 1
    fi

    echo -e "${GREEN}✓ Prerequisites satisfied${NC}"
}

# Get latest successful workflow run
get_latest_workflow_run() {
    echo -e "${CYAN}Finding latest successful CI workflow run...${NC}"

    local run_json
    run_json=$(gh run list --workflow=ci.yml --status=success --limit=1 --json databaseId,conclusion,createdAt)

    if [[ $(echo "$run_json" | jq '. | length') -eq 0 ]]; then
        echo -e "${RED}✗ No successful workflow runs found${NC}"
        exit 1
    fi

    local run_id created_at
    run_id=$(echo "$run_json" | jq -r '.[0].databaseId')
    created_at=$(echo "$run_json" | jq -r '.[0].createdAt')

    echo -e "${GREEN}✓ Found run: $run_id ($created_at)${NC}"
    echo "$run_id"
}

# Create evidence directory structure
initialize_evidence_directory() {
    local output_path=$1

    echo -e "${CYAN}Creating evidence directory structure...${NC}"

    mkdir -p "$output_path"/{test-results,security-scans/{sast,dast,container,secrets},sbom,audit-logs,rtm,metadata}

    echo -e "${GREEN}✓ Directory structure created${NC}"
}

# Download artifacts from GitHub Actions
download_workflow_artifacts() {
    local run_id=$1
    local output_path=$2

    echo -e "${CYAN}Downloading artifacts from run $run_id...${NC}"

    # Get list of artifacts
    local artifacts_json
    artifacts_json=$(gh run view "$run_id" --json artifacts)

    local artifact_count
    artifact_count=$(echo "$artifacts_json" | jq '.artifacts | length')

    if [[ $artifact_count -eq 0 ]]; then
        echo -e "${YELLOW}⚠ No artifacts found for run $run_id${NC}"
        return
    fi

    echo -e "${YELLOW}Found $artifact_count artifacts${NC}"

    # Download each artifact
    local artifact_names
    artifact_names=$(echo "$artifacts_json" | jq -r '.artifacts[].name')

    while IFS= read -r artifact_name; do
        echo -e "${GRAY}  Downloading: $artifact_name${NC}"

        # Determine target directory based on artifact type
        local target_dir
        case "$artifact_name" in
            *test-results*|*coverage*)
                target_dir="$output_path/test-results"
                ;;
            *sarif*|*codeql*)
                target_dir="$output_path/security-scans/sast"
                ;;
            *zap*|*dast*)
                target_dir="$output_path/security-scans/dast"
                ;;
            *trivy*|*container*)
                target_dir="$output_path/security-scans/container"
                ;;
            *gitleaks*|*secrets*)
                target_dir="$output_path/security-scans/secrets"
                ;;
            *sbom*|*cyclonedx*)
                target_dir="$output_path/sbom"
                ;;
            *)
                target_dir="$output_path/metadata"
                ;;
        esac

        # Download artifact
        if gh run download "$run_id" -n "$artifact_name" -D "$target_dir" 2>/dev/null; then
            echo -e "${GREEN}    ✓ Downloaded to: $target_dir${NC}"
        else
            echo -e "${YELLOW}  ⚠ Failed to download $artifact_name${NC}"
        fi
    done <<< "$artifact_names"
}

# Export audit log samples
export_audit_log_samples() {
    local output_path=$1
    local max_samples=$2

    if [[ "$INCLUDE_AUDIT_LOGS" != "true" ]]; then
        echo -e "${YELLOW}Skipping audit log samples (disabled)${NC}"
        return
    fi

    echo -e "${CYAN}Exporting audit log samples...${NC}"

    # NOTE: In production, this would connect to your IAuditStore implementation
    # For now, create a sample template showing the expected format

    # The `.template.json` suffix is load-bearing, not cosmetic. This file is a blank form the script
    # writes on every run, including a run that downloaded no artifacts at all. Counting it as evidence
    # would make an empty package report audit-log coverage — the manifest asserting a control on the
    # strength of a document that contains no audit records. Everything the collector counts skips
    # `*.template.json`, so a placeholder can never substantiate a control.
    local sample_path="$output_path/audit-logs/sample-audit-logs.template.json"

    cat > "$sample_path" <<'EOF'
{
  "Metadata": {
    "ExportedAt": "TIMESTAMP",
    "SampleCount": MAX_SAMPLES,
    "Anonymized": true,
    "Note": "Replace with actual audit log query: SELECT TOP MAX_SAMPLES * FROM AuditLog ORDER BY Timestamp DESC"
  },
  "Samples": [
    {
      "EventId": "00000000-0000-0000-0000-000000000001",
      "EventType": "PHIAccessed",
      "UserId": "[REDACTED]",
      "Timestamp": "TIMESTAMP",
      "Outcome": "Success",
      "CorrelationId": "cor-123",
      "Metadata": {
        "Action": "Read",
        "Resource": "PatientRecord"
      }
    },
    {
      "EventId": "00000000-0000-0000-0000-000000000002",
      "EventType": "DataExported",
      "UserId": "[REDACTED]",
      "Timestamp": "TIMESTAMP",
      "Outcome": "Success",
      "CorrelationId": "cor-124",
      "Metadata": {
        "Action": "Export",
        "Format": "PDF"
      }
    }
  ],
  "Instructions": "To include real audit logs, implement IDataInventoryService and query your audit store. Ensure data is anonymized before export."
}
EOF

    # Replace placeholders
    local timestamp
    timestamp=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
    sed -i "s/TIMESTAMP/$timestamp/g" "$sample_path"
    sed -i "s/MAX_SAMPLES/$max_samples/g" "$sample_path"

    echo -e "${GREEN}✓ Sample audit log template created: $sample_path${NC}"
    echo -e "${YELLOW}  NOTE: Replace with actual audit log queries in production${NC}"
}

# Record a reason the manifest could not derive a figure. A refusal is never fatal to writing the
# package — the manifest states it — but it is always fatal to the exit code.
refuse() {
    REFUSAL_REASONS+=("$1")
    echo -e "${YELLOW}⚠ REFUSE: $1${NC}" >&2
}

# count_evidence_files <package_root> <category>
#
# Prints the number of collected evidence files in the category, and returns 0. Returns 2 WITHOUT
# printing when the category directory does not exist.
#
# The distinction is the whole point. The previous form was
#     find "$dir" -type f 2>/dev/null | wc -l
# which prints 0 for a directory that is absent exactly as it does for one that is empty, with find's
# complaint discarded. So a package that had lost — or never created — its test-results directory
# reported "0 test results" in the same characters as one that had genuinely collected none, and the
# reader could not tell a measurement from a failure to measure.
#
# Placeholders (`*.template.json`) are blank forms this script writes on every run; they are not
# evidence and are excluded here so an empty package cannot count one toward a control.
count_evidence_files() {
    local package_root=$1 category=$2
    local dir="$package_root/$category"

    [[ -d "$dir" ]] || return 2

    find "$dir" -type f ! -name '*.template.json' 2>/dev/null | wc -l | tr -d '[:space:]'
}

# read_control_map <framework>
#
# Prints the map rows for one framework as `ControlId<TAB>categories`, and returns 0. Returns 2 when
# the map is unreadable or names no control for that framework — an empty control set would otherwise
# score as "0 of 0 controls", which reads as a clean bill of health for a framework we never assessed.
read_control_map() {
    local framework=$1

    [[ -r "$CONTROL_MAP" ]] || return 2

    local rows
    rows=$(awk -F'\t' -v fw="$framework" '
        /^[[:space:]]*#/ || /^[[:space:]]*$/ { next }
        $1 == fw { print $2 "\t" $3 }
    ' "$CONTROL_MAP")

    [[ -n "$rows" ]] || return 2

    printf '%s\n' "$rows"
}

# frameworks_requested — resolves the -f/--frameworks selection against the map.
# "All" expands to every framework the map defines, so adding one to the map adds it here.
frameworks_requested() {
    if [[ "${FRAMEWORKS,,}" == "all" ]]; then
        awk -F'\t' '!/^[[:space:]]*#/ && NF >= 3 && $1 != "" { print $1 }' "$CONTROL_MAP" 2>/dev/null | awk '!seen[$0]++'
        return 0
    fi
    printf '%s\n' "${FRAMEWORKS//,/$'\n'}" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' | grep -v '^$' || true
}

# Generate evidence manifest — every compliance figure derived from the collected package.
generate_evidence_manifest() {
    local output_path=$1
    local run_id=$2

    echo -e "${CYAN}Generating evidence manifest...${NC}"

    # Reset the derived state, so a second call reports this package rather than this package plus
    # whatever the last one refused.
    REFUSAL_REASONS=()
    COVERAGE_SUMMARY=""

    local manifest_path="$output_path/MANIFEST.json"
    local repo_name
    repo_name="${EVIDENCE_REPOSITORY:-$(gh repo view --json nameWithOwner --jq '.nameWithOwner' 2>/dev/null || echo "Unknown")}"

    # ── Evidence counts, per category, refusing on an absent directory ─────────────────────────────
    # COUNTS[category] is set only when the category was measurable; an unset entry means REFUSED, and
    # every downstream consumer of it must treat "unset" as "unknown", never as zero.
    declare -A COUNTS=()
    local category count
    local counts_json="" sep=""
    for category in $EVIDENCE_CATEGORIES; do
        if count=$(count_evidence_files "$output_path" "$category"); then
            COUNTS["$category"]=$count
            counts_json+="${sep}
    \"$category\": $count"
        else
            refuse "evidence directory missing: $output_path/$category — cannot distinguish 'no evidence collected' from 'never looked'"
            counts_json+="${sep}
    \"$category\": null"
        fi
        sep=","
    done

    # ── Control coverage, per framework, derived from the map and the counts above ─────────────────
    local frameworks_json="" fw_sep=""
    local framework rows control categories cat in_scope documented documented_ids id_sep satisfied
    local mapped_categories mapped_json

    while IFS= read -r framework; do
        [[ -n "$framework" ]] || continue

        if ! rows=$(read_control_map "$framework"); then
            refuse "no controls mapped for framework '$framework' in $CONTROL_MAP — cannot derive its coverage"
            frameworks_json+="${fw_sep}
    \"$framework\": {
      \"Status\": \"REFUSED\",
      \"Reason\": \"no controls for this framework in the control map; no coverage figure can be derived\"
    }"
            fw_sep=","
            continue
        fi

        in_scope=0
        documented=0
        documented_ids=""
        id_sep=""
        mapped_categories=""

        while IFS=$'\t' read -r control categories; do
            [[ -n "$control" ]] || continue
            in_scope=$((in_scope + 1))

            # `none` — evidence lives outside the pipeline. Counted in scope, never documented.
            [[ "$categories" == "none" ]] && continue

            # ALL mapped categories must be present. A control needing a test result and a scan is
            # not substantiated by whichever one happens to be there.
            satisfied=1
            for cat in ${categories//,/ }; do
                case " $EVIDENCE_CATEGORIES " in
                    *" $cat "*) ;;
                    *)  refuse "control $framework/$control maps to unknown evidence category '$cat' — scoring it unmet would under-report coverage while looking measured"
                        satisfied=0
                        continue 2
                        ;;
                esac
                mapped_categories+="$cat "
                # An unset count is REFUSED, not zero: an unmeasurable category cannot satisfy anything.
                if [[ -z "${COUNTS[$cat]:-}" ]] || [[ "${COUNTS[$cat]}" -eq 0 ]]; then
                    satisfied=0
                fi
            done

            if [[ $satisfied -eq 1 ]]; then
                documented=$((documented + 1))
                documented_ids+="${id_sep}\"$control\""
                id_sep=", "
            fi
        done <<< "$rows"

        mapped_json=$(printf '%s\n' $mapped_categories | awk 'NF' | sort -u | awk '{printf "%s\"%s\"", (NR>1 ? ", " : ""), $0}')

        COVERAGE_SUMMARY+="$framework"$'\t'"$in_scope"$'\t'"$documented"$'\n'

        frameworks_json+="${fw_sep}
    \"$framework\": {
      \"ControlsInScope\": $in_scope,
      \"ControlsDocumented\": $documented,
      \"ControlsDocumentedIds\": [$documented_ids],
      \"EvidenceCategoriesMapped\": [$mapped_json]
    }"
        fw_sep=","
    done <<< "$(frameworks_requested)"

    # ── Refusals ──────────────────────────────────────────────────────────────────────────────────
    local status reasons_json="" r_sep=""
    local reason
    if [[ ${#REFUSAL_REASONS[@]} -eq 0 ]]; then
        status="COMPLETE"
    else
        status="REFUSED"
        for reason in "${REFUSAL_REASONS[@]}"; do
            reasons_json+="${r_sep}
    \"${reason//\"/\\\"}\""
            r_sep=","
        done
    fi

    cat > "$manifest_path" <<EOF
{
  "GeneratedAt": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")",
  "GeneratedBy": "${USER:-unknown}",
  "RunId": "$run_id",
  "Repository": "$repo_name",
  "Frameworks": "$FRAMEWORKS",
  "ManifestStatus": "$status",
  "RefusalReasons": [$reasons_json
  ],
  "EvidenceCounts": {$counts_json
  },
  "EvidenceCountBasis": "Files collected into this package, excluding *.template.json placeholders. null means the category directory was absent and the count is unknown -- it does not mean zero.",
  "ControlCoverage": {
    "Source": "eng/compliance/control-evidence-map.tsv",
    "Basis": "ControlsDocumented counts in-scope controls whose every mapped evidence category has at least one collected file in this package. Controls whose evidence is documentation or a business process are mapped 'none' and are never counted as documented. A package with no collected evidence therefore reports zero.",$frameworks_json
  }
}
EOF

    echo -e "${GREEN}✓ Evidence manifest created: $manifest_path${NC}"

    [[ ${#REFUSAL_REASONS[@]} -eq 0 ]] || return 2
    return 0
}

# Render the coverage table the package README shows, from the figures the manifest derived.
# Emits an explicit "not derived" notice rather than a table of zeros when nothing was computed —
# a table of zeros looks like a measurement of a package with no evidence, which is a different claim.
render_coverage_table() {
    if [[ -z "$COVERAGE_SUMMARY" ]]; then
        printf 'No control coverage was derived for this package. See `MANIFEST.json` -> `RefusalReasons`.\n'
        return 0
    fi

    printf '| Framework | Controls in scope | Documented by evidence in this package |\n'
    printf '|---|---:|---:|\n'
    printf '%s' "$COVERAGE_SUMMARY" | awk -F'\t' 'NF >= 3 { printf "| %s | %s | %s |\n", $1, $2, $3 }'
}

# Generate README
generate_evidence_readme() {
    local output_path=$1
    local run_id=$2

    local repo_name
    repo_name="${EVIDENCE_REPOSITORY:-$(gh repo view --json nameWithOwner --jq '.nameWithOwner' 2>/dev/null || echo "Unknown")}"

    cat > "$output_path/README.md" <<EOF
# Compliance Evidence Package

**Generated:** $(date +"%Y-%m-%d %H:%M:%S")
**Repository:** $repo_name
**Run ID:** $run_id
**Frameworks:** $FRAMEWORKS

---

## Directory Structure

\`\`\`
compliance-evidence/
├── test-results/           # Unit, integration, functional test results
│   ├── junit-xml/          # JUnit XML test results
│   └── coverage/           # Code coverage reports
├── security-scans/         # Security scan results
│   ├── sast/               # Static Application Security Testing (CodeQL, etc.)
│   ├── dast/               # Dynamic Application Security Testing (OWASP ZAP)
│   ├── container/          # Container vulnerability scanning (Trivy)
│   └── secrets/            # Secrets scanning (Gitleaks)
├── sbom/                   # Software Bill of Materials (CycloneDX)
├── audit-logs/             # Sample audit logs (anonymized)
├── rtm/                    # Requirements Traceability Matrix
├── metadata/               # Additional metadata and artifacts
├── MANIFEST.json           # Evidence inventory manifest
└── README.md               # This file
\`\`\`

---

## Control Coverage in This Package

Counted from the files actually collected here, not asserted. A control is **documented** when every
evidence category mapped to it in \`eng/compliance/control-evidence-map.tsv\` has at least one collected
file in this package.

$(render_coverage_table)

**What the remainder means.** A control that is in scope but not documented here is not thereby
non-compliant — most are substantiated by documentation, configuration, or a business process that a
CI pipeline does not produce, and those are mapped so that they can never be counted as documented
however complete the download was. Read \`MANIFEST.json\` for the per-control identifiers and for any
figure the collector refused to derive.

---

## Using This Evidence

### For External Audits

1. Provide this entire directory to your auditor
2. Provide access to GitHub Actions workflow runs (90-day retention)

### For Internal Reviews

1. Review MANIFEST.json for evidence inventory
2. Check test-results/ for coverage metrics (≥60% enforced)
3. Review security-scans/ for vulnerability findings
4. Verify SBOM completeness in sbom/

### For Certification

1. **FedRAMP:** Provide to 3PAO for Security Assessment Report (SAR)
2. **GDPR:** Reference for Data Protection Impact Assessment (DPIA)
3. **SOC 2:** Provide to auditor for Type I or Type II report
4. **HIPAA:** Reference for Risk Assessment and Security Rule compliance

---

## Next Steps

### Customize Evidence Collection

Edit eng/compliance/collect-evidence.sh to:
- Add custom evidence types
- Connect to production audit store
- Include additional metadata

### Automate Collection

Add to CI/CD pipeline:

\`\`\`yaml
- name: Collect Compliance Evidence
  run: |
    ./eng/compliance/collect-evidence.sh -o artifacts/evidence

- name: Upload Evidence Package
  uses: actions/upload-artifact@v4
  with:
    name: compliance-evidence
    path: artifacts/evidence
    retention-days: 365
\`\`\`

### Generate Evidence Package

Run:

\`\`\`bash
./eng/compliance/generate-evidence-package.sh
\`\`\`

Outputs: compliance-evidence-v1.0.0.tar.gz

---

## Contact

**Questions:**
- Compliance: Contact Security Official
- Evidence Access: Contact Project Manager

---

**Generated by:** Excalibur Compliance Evidence Collector
**Version:** 1.0.0
**Date:** $(date +"%Y-%m-%d")
EOF

    echo -e "${GREEN}✓ README created: $output_path/README.md${NC}"
}

# Main execution
main() {
    parse_args "$@"

    echo -e "\n${CYAN}=== Excalibur Compliance Evidence Collector ===${NC}"
    echo -e "${YELLOW}Frameworks: $FRAMEWORKS${NC}"
    echo -e "${YELLOW}Output: $OUTPUT_PATH${NC}\n"

    # Check prerequisites
    check_prerequisites

    # Get run ID
    if [[ -z "$RUN_ID" ]]; then
        RUN_ID=$(get_latest_workflow_run)
    fi

    # Create directory structure
    initialize_evidence_directory "$OUTPUT_PATH"

    # Download artifacts
    download_workflow_artifacts "$RUN_ID" "$OUTPUT_PATH"

    # Export audit logs
    export_audit_log_samples "$OUTPUT_PATH" "$MAX_AUDIT_SAMPLES"

    # Generate manifest. Capture the REAL exit directly on the next statement: a pipeline or a
    # trailing command reports its own status, and a refusal that is swallowed here becomes a package
    # that exits 0 while telling the reader it could not measure itself.
    local manifest_rc=0
    generate_evidence_manifest "$OUTPUT_PATH" "$RUN_ID" || manifest_rc=$?

    # Generate README
    generate_evidence_readme "$OUTPUT_PATH" "$RUN_ID"

    echo -e "\n${GREEN}✓ Evidence collection complete!${NC}"
    echo -e "${CYAN}Location: $OUTPUT_PATH${NC}"
    echo -e "\n${YELLOW}Next steps:${NC}"
    echo -e "${GRAY}  1. Review MANIFEST.json for evidence inventory${NC}"
    echo -e "${GRAY}  2. Verify security scan results in security-scans/${NC}"
    echo -e "${GRAY}  3. Generate evidence package: ./eng/compliance/generate-evidence-package.sh${NC}"

    if [[ $manifest_rc -ne 0 ]]; then
        echo -e "\n${YELLOW}⚠ REFUSED: at least one figure could not be derived. The package was written and${NC}"
        echo -e "${YELLOW}  states which figures are unknown. This is not a clean evidence package.${NC}"
        return 2
    fi
    return 0
}

# Run only when executed, not when sourced. The self-test sources this file to exercise manifest
# generation directly, without a GitHub token, a network, or a workflow run.
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi
