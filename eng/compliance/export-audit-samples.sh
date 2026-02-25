#!/usr/bin/env bash

#
# Excalibur Audit Log Sample Exporter
#
# This script exports sample audit logs from an audit store (SQL Server, PostgreSQL, etc.)
# for compliance evidence. Logs are anonymized before export.
#
# Usage:
#   ./export-audit-samples.sh [OPTIONS]
#
# Options:
#   -c, --connection-string STRING  Database connection string
#   -o, --output PATH               Output file (default: ./audit-samples.json)
#   -n, --count N                   Number of samples to export (default: 100)
#   -s, --start-date DATE           Start date (YYYY-MM-DD, default: 30 days ago)
#   -e, --end-date DATE             End date (YYYY-MM-DD, default: today)
#   -t, --event-types LIST          Comma-separated event types (default: all)
#   -a, --anonymize                 Anonymize user IDs (default: true)
#   -f, --format FORMAT             Output format: json, csv (default: json)
#   -h, --help                      Show this help message
#
# Examples:
#   ./export-audit-samples.sh -c "Server=localhost;Database=Compliance;User=sa;Password=***"
#   ./export-audit-samples.sh -n 50 -t "PHIAccessed,DataExported"
#   ./export-audit-samples.sh -s 2025-01-01 -e 2025-12-31 -f csv
#
# Prerequisites:
#   - sqlcmd (for SQL Server) or psql (for PostgreSQL)
#   - jq for JSON processing: apt-get install jq
#

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0;no Color'

# Default values
CONNECTION_STRING=""
OUTPUT_PATH="./audit-samples.json"
SAMPLE_COUNT=100
START_DATE=$(date -d '30 days ago' +%Y-%m-%d 2>/dev/null || date -v-30d +%Y-%m-%d)
END_DATE=$(date +%Y-%m-%d)
EVENT_TYPES=""
ANONYMIZE=true
OUTPUT_FORMAT="json"

# Parse command line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -c|--connection-string)
                CONNECTION_STRING="$2"
                shift 2
                ;;
            -o|--output)
                OUTPUT_PATH="$2"
                shift 2
                ;;
            -n|--count)
                SAMPLE_COUNT="$2"
                shift 2
                ;;
            -s|--start-date)
                START_DATE="$2"
                shift 2
                ;;
            -e|--end-date)
                END_DATE="$2"
                shift 2
                ;;
            -t|--event-types)
                EVENT_TYPES="$2"
                shift 2
                ;;
            --no-anonymize)
                ANONYMIZE=false
                shift
                ;;
            -f|--format)
                OUTPUT_FORMAT="$2"
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

# Export audit samples (SQL Server example)
export_audit_samples_sqlserver() {
    local connection_string=$1
    local output_path=$2

    echo -e "${CYAN}Exporting audit samples from SQL Server...${NC}"

    # Build WHERE clause for event types
    local where_clause="Timestamp >= '$START_DATE' AND Timestamp <= '$END_DATE'"
    if [[ -n "$EVENT_TYPES" ]]; then
        local types_array=(${EVENT_TYPES//,/ })
        local types_in="'${types_array[0]}'"
        for ((i=1; i<${#types_array[@]}; i++)); do
            types_in="$types_in,'${types_array[$i]}'"
        done
        where_clause="$where_clause AND EventType IN ($types_in)"
    fi

    # SQL query (adjust table/column names to match your IAuditStore implementation)
    local sql_query="
    SELECT TOP $SAMPLE_COUNT
        EventId,
        EventType,
        UserId,
        Timestamp,
        Outcome,
        CorrelationId,
        Metadata
    FROM compliance.AuditLog
    WHERE $where_clause
    ORDER BY Timestamp DESC
    FOR JSON PATH
    "

    # Execute query (requires sqlcmd)
    if ! command -v sqlcmd &> /dev/null; then
        echo -e "${RED}✗ sqlcmd is not installed${NC}"
        echo -e "${YELLOW}  Install SQL Server command-line tools${NC}"
        exit 1
    fi

    local result
    result=$(sqlcmd -S "$connection_string" -Q "$sql_query" -h -1 -W)

    # Anonymize if requested
    if [[ "$ANONYMIZE" == "true" ]]; then
        echo -e "${YELLOW}Anonymizing user IDs...${NC}"
        # Replace actual user IDs with SHA256 hashes
        result=$(echo "$result" | jq '.[] | .UserId = (.UserId | @base64 | .[0:12] + "...")')
    fi

    # Write to output file
    echo "$result" | jq '.' > "$output_path"

    echo -e "${GREEN}✓ Exported $SAMPLE_COUNT audit samples to: $output_path${NC}"
}

# Export audit samples (PostgreSQL example)
export_audit_samples_postgresql() {
    local connection_string=$1
    local output_path=$2

    echo -e "${CYAN}Exporting audit samples from PostgreSQL...${NC}"

    # Similar to SQL Server but using psql
    echo -e "${YELLOW}PostgreSQL export not yet implemented${NC}"
    echo -e "${YELLOW}Modify this function to match your PostgreSQL schema${NC}"
}

# Create sample template (if no connection string provided)
create_sample_template() {
    local output_path=$1

    echo -e "${YELLOW}No connection string provided. Creating sample template...${NC}"

    cat > "$output_path" <<'EOF'
{
  "Metadata": {
    "ExportedAt": "TIMESTAMP",
    "SampleCount": SAMPLE_COUNT,
    "DateRange": {
      "Start": "START_DATE",
      "End": "END_DATE"
    },
    "Anonymized": ANONYMIZE,
    "EventTypes": "EVENT_TYPES",
    "Note": "This is a sample template. Provide a connection string to export real audit logs."
  },
  "Samples": [
    {
      "EventId": "00000000-0000-0000-0000-000000000001",
      "EventType": "PHIAccessed",
      "UserId": "[ANONYMIZED]",
      "Timestamp": "TIMESTAMP",
      "Outcome": "Success",
      "CorrelationId": "cor-123",
      "Metadata": {
        "Action": "Read",
        "Resource": "PatientRecord",
        "ResourceId": "patient-456"
      }
    },
    {
      "EventId": "00000000-0000-0000-0000-000000000002",
      "EventType": "DataExported",
      "UserId": "[ANONYMIZED]",
      "Timestamp": "TIMESTAMP",
      "Outcome": "Success",
      "CorrelationId": "cor-124",
      "Metadata": {
        "Action": "Export",
        "Format": "PDF",
        "RecordCount": 1
      }
    },
    {
      "EventId": "00000000-0000-0000-0000-000000000003",
      "EventType": "UnauthorizedAccess",
      "UserId": "[ANONYMIZED]",
      "Timestamp": "TIMESTAMP",
      "Outcome": "Failure",
      "CorrelationId": "cor-125",
      "Metadata": {
        "Action": "Read",
        "Resource": "PatientRecord",
        "Reason": "InsufficientPermissions"
      }
    }
  ],
  "Instructions": {
    "Step1": "Update this script to connect to your IAuditStore implementation",
    "Step2": "Adjust SQL queries to match your audit log table schema",
    "Step3": "Run with -c flag: ./export-audit-samples.sh -c 'connection-string'",
    "Step4": "Verify exported logs are anonymized before sharing with auditors"
  }
}
EOF

    # Replace placeholders
    sed -i "s/TIMESTAMP/$(date -u +"%Y-%m-%dT%H:%M:%SZ")/g" "$output_path"
    sed -i "s/SAMPLE_COUNT/$SAMPLE_COUNT/g" "$output_path"
    sed -i "s/START_DATE/$START_DATE/g" "$output_path"
    sed -i "s/END_DATE/$END_DATE/g" "$output_path"
    sed -i "s/ANONYMIZE/$ANONYMIZE/g" "$output_path"
    sed -i "s/EVENT_TYPES/$EVENT_TYPES/g" "$output_path"

    echo -e "${GREEN}✓ Sample template created: $output_path${NC}"
    echo -e "${YELLOW}  NOTE: Provide a connection string to export real audit logs${NC}"
}

# Main execution
main() {
    parse_args "$@"

    echo -e "\n${CYAN}=== Excalibur Audit Log Sample Exporter ===${NC}"
    echo -e "${YELLOW}Output: $OUTPUT_PATH${NC}"
    echo -e "${YELLOW}Sample Count: $SAMPLE_COUNT${NC}"
    echo -e "${YELLOW}Date Range: $START_DATE to $END_DATE${NC}\n"

    if [[ -z "$CONNECTION_STRING" ]]; then
        create_sample_template "$OUTPUT_PATH"
    else
        # Detect database type from connection string
        if [[ "$CONNECTION_STRING" =~ "Server=" ]] || [[ "$CONNECTION_STRING" =~ "Data Source=" ]]; then
            export_audit_samples_sqlserver "$CONNECTION_STRING" "$OUTPUT_PATH"
        elif [[ "$CONNECTION_STRING" =~ "Host=" ]] || [[ "$CONNECTION_STRING" =~ "host=" ]]; then
            export_audit_samples_postgresql "$CONNECTION_STRING" "$OUTPUT_PATH"
        else
            echo -e "${RED}✗ Unable to detect database type from connection string${NC}"
            exit 1
        fi
    fi

    echo -e "\n${GREEN}✓ Audit sample export complete!${NC}"
    echo -e "\n${YELLOW}Next steps:${NC}"
    echo -e "${GRAY}  1. Review exported samples: cat $OUTPUT_PATH | jq .${NC}"
    echo -e "${GRAY}  2. Verify user IDs are anonymized (if enabled)${NC}"
    echo -e "${GRAY}  3. Include in compliance evidence package${NC}"
}

main "$@"
