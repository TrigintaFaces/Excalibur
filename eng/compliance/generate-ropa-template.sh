#!/usr/bin/env bash

#
# Excalibur Records of Processing Activities (RoPA) Template Generator
#
# This script generates a Records of Processing Activities (RoPA) template
# required by GDPR Article 30. The template documents:
# - Processing purposes
# - Categories of data subjects
# - Categories of personal data
# - Recipients of personal data
# - Retention periods
# - Security measures
#
# Usage:
#   ./generate-ropa-template.sh [OPTIONS]
#
# Options:
#   -o, --output PATH          Output file (default: ./ropa-template.md)
#   -f, --format FORMAT        Output format: markdown, json, csv (default: markdown)
#   -t, --template-type TYPE   Template type: controller, processor (default: controller)
#   -h, --help                 Show this help message
#
# Examples:
#   ./generate-ropa-template.sh
#   ./generate-ropa-template.sh -f json -t processor
#   ./generate-ropa-template.sh -o docs/compliance/ropa.md
#
# Prerequisites:
#   - None (pure Bash)
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
OUTPUT_PATH="./ropa-template.md"
OUTPUT_FORMAT="markdown"
TEMPLATE_TYPE="controller"

# Parse command line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -o|--output)
                OUTPUT_PATH="$2"
                shift 2
                ;;
            -f|--format)
                OUTPUT_FORMAT="$2"
                shift 2
                ;;
            -t|--template-type)
                TEMPLATE_TYPE="$2"
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

# Generate markdown RoPA template
generate_markdown_template() {
    local output_path=$1
    local template_type=$2

    local title
    if [[ "$template_type" == "controller" ]]; then
        title="Records of Processing Activities (Data Controller)"
    else
        title="Records of Processing Activities (Data Processor)"
    fi

    cat > "$output_path" <<EOF
# $title

**Framework:** Excalibur.Dispatch
**Standard:** GDPR Article 30
**Template Type:** $(tr '[:lower:]' '[:upper:]' <<< ${template_type:0:1})${template_type:1}
**Last Updated:** $(date +"%Y-%m-%d")

---

## Purpose of this Document

This document fulfills the requirements of GDPR Article 30 "Records of Processing Activities."
Every data controller or processor must maintain a record of processing activities under their responsibility.

**Article 30 Requirements:**
- Processing purposes
- Categories of data subjects
- Categories of personal data
- Recipients of personal data
- Transfers to third countries
- Retention periods
- Security measures

**Instructions:** Fill out the tables below for each processing activity.

---

## Processing Activity 1: [NAME - e.g., "Patient Record Management"]

### 1.1 Processing Details

| Field | Description |
|-------|-------------|
| **Activity Name** | [e.g., "Patient Record Management"] |
| **Activity ID** | [e.g., "PROC-001"] |
| **Purpose of Processing** | [e.g., "Provide healthcare services, maintain medical records, billing"] |
| **Legal Basis (Article 6)** | [e.g., "Consent (6(1)(a))", "Legal obligation (6(1)(c))", "Vital interests (6(1)(d))"] |
| **Special Category Basis (Article 9)** | [e.g., "Explicit consent (9(2)(a))", "Health/social care (9(2)(h))"] |
| **Responsible Person** | [Name, Title, Email] |
| **DPO Contact** | [Data Protection Officer contact if appointed] |

### 1.2 Data Subjects

| Category | Description | Examples |
|----------|-------------|----------|
| **Patients** | Individuals receiving healthcare services | Current patients, former patients |
| **Healthcare Providers** | Physicians, nurses, specialists | Employees, contractors |
| **Administrative Staff** | Non-clinical staff | Receptionists, billing clerks |
| **Other** | [Specify] | [Examples] |

### 1.3 Categories of Personal Data

| Category | Data Elements | Sensitivity |
|----------|---------------|-------------|
| **Identity Data** | Name, date of birth, address, SSN | Standard |
| **Contact Data** | Email, phone number | Standard |
| **Health Data** | Diagnosis, treatment plan, medications, test results | Special Category |
| **Financial Data** | Insurance details, payment information | Standard |
| **Technical Data** | IP address, device ID, login credentials | Standard |

**Sensitivity Levels:**
- **Standard:** Regular personal data (GDPR Article 6)
- **Special Category:** Sensitive data (GDPR Article 9) - health, biometric, genetic, etc.

### 1.4 Recipients of Personal Data

| Recipient | Purpose | Legal Basis |
|-----------|---------|-------------|
| **Healthcare Providers** | Provide medical care | Legitimate interest |
| **Insurance Companies** | Process claims | Contract |
| **Lab Services** | Process diagnostic tests | Contract |
| **IT Service Providers** | System maintenance | Contract (DPA required) |
| **Government Agencies** | Legal compliance (e.g., reporting) | Legal obligation |

**Data Processing Agreements (DPA):** All processors MUST have a signed DPA per Article 28.

### 1.5 Transfers to Third Countries

| Country | Recipient | Safeguard | Legal Basis |
|---------|-----------|-----------|-------------|
| [e.g., "United States"] | [e.g., "Cloud Provider"] | [e.g., "Standard Contractual Clauses"] | [e.g., "Article 46(2)(c)"] |
| **None** | N/A | N/A | N/A |

**Safeguards (if applicable):**
- Standard Contractual Clauses (SCCs) - Article 46(2)(c)
- Adequacy Decision - Article 45
- Binding Corporate Rules (BCRs) - Article 46(2)(b)

### 1.6 Retention Periods

| Data Category | Retention Period | Justification |
|---------------|------------------|---------------|
| **Patient Records** | 7 years after last treatment | Legal obligation (state law) |
| **Billing Records** | 7 years | Tax law requirement |
| **Audit Logs** | 6 years | HIPAA requirement |
| **Consent Records** | Indefinite (or data deletion) | Proof of consent |

**Deletion Policy:** Data is deleted after retention period via cryptographic erasure (IErasureService).

### 1.7 Security Measures (Article 32)

| Measure | Implementation |
|---------|----------------|
| **Encryption at Rest** | AES-256-GCM via IEncryptionProvider |
| **Encryption in Transit** | TLS 1.2+ for all communications |
| **Access Control** | Role-based (RBAC) with [RequirePermission] attribute |
| **Audit Logging** | Tamper-evident hash chain via IAuditLogger |
| **Pseudonymization** | User IDs hashed in audit logs |
| **Backup & Recovery** | Daily backups with 30-day retention, geo-redundant storage |
| **Vulnerability Scanning** | SAST, DAST, container scanning in CI/CD |
| **Incident Response** | 72-hour breach notification procedure |

**Risk Assessment:** [Reference risk assessment document]

---

## Processing Activity 2: [NAME]

[Repeat sections 1.1-1.7 for each processing activity]

---

## Review and Approval

| Role | Name | Signature | Date |
|------|------|-----------|------|
| **Data Protection Officer** | | | |
| **Security Official** | | | |
| **Management Representative** | | | |

**Next Review Date:** $(date -d "1 year" +%Y-%m-%d || date -v+1y +%Y-%m-%d)

---

## Appendix: Framework Capabilities

**Excalibur provides:**

| Capability | GDPR Article | Implementation |
|------------|--------------|----------------|
| **Data Inventory** | Article 30 | IDataInventoryService with [PersonalData] attribute |
| **Erasure** | Article 17 | IErasureService (cryptographic erasure) |
| **Audit Logging** | Article 32 | IAuditLogger (tamper-evident hash chain) |
| **Encryption** | Article 32 | IEncryptionProvider (AES-256-GCM) |
| **Access Control** | Article 32 | [RequirePermission] RBAC |

---

**Generated by:** Excalibur RoPA Template Generator
**Version:** 1.0.0
**Date:** $(date +"%Y-%m-%d")
EOF

    echo -e "${GREEN}✓ Markdown RoPA template created: $output_path${NC}"
}

# Generate JSON RoPA template
generate_json_template() {
    local output_path=$1
    local template_type=$2

    cat > "$output_path" <<'EOF'
{
  "Metadata": {
    "Framework": "Excalibur",
    "Standard": "GDPR Article 30",
    "TemplateType": "TEMPLATE_TYPE",
    "LastUpdated": "TIMESTAMP"
  },
  "ProcessingActivities": [
    {
      "ActivityId": "PROC-001",
      "ActivityName": "[e.g., Patient Record Management]",
      "Purpose": "[e.g., Provide healthcare services, maintain medical records, billing]",
      "LegalBasis": {
        "Article6": "[e.g., Consent (6(1)(a)), Legal obligation (6(1)(c))]",
        "Article9": "[e.g., Explicit consent (9(2)(a)), Health/social care (9(2)(h))]"
      },
      "ResponsiblePerson": {
        "Name": "[Name]",
        "Title": "[Title]",
        "Email": "[Email]"
      },
      "DataSubjects": [
        {
          "Category": "Patients",
          "Description": "Individuals receiving healthcare services",
          "Examples": ["Current patients", "Former patients"]
        },
        {
          "Category": "Healthcare Providers",
          "Description": "Physicians, nurses, specialists",
          "Examples": ["Employees", "Contractors"]
        }
      ],
      "PersonalDataCategories": [
        {
          "Category": "Identity Data",
          "DataElements": ["Name", "Date of birth", "Address", "SSN"],
          "Sensitivity": "Standard"
        },
        {
          "Category": "Health Data",
          "DataElements": ["Diagnosis", "Treatment plan", "Medications", "Test results"],
          "Sensitivity": "Special Category"
        }
      ],
      "Recipients": [
        {
          "Recipient": "Healthcare Providers",
          "Purpose": "Provide medical care",
          "LegalBasis": "Legitimate interest"
        },
        {
          "Recipient": "IT Service Providers",
          "Purpose": "System maintenance",
          "LegalBasis": "Contract (DPA required)"
        }
      ],
      "ThirdCountryTransfers": [
        {
          "Country": "United States",
          "Recipient": "Cloud Provider",
          "Safeguard": "Standard Contractual Clauses",
          "LegalBasis": "Article 46(2)(c)"
        }
      ],
      "RetentionPeriods": [
        {
          "DataCategory": "Patient Records",
          "RetentionPeriod": "7 years after last treatment",
          "Justification": "Legal obligation (state law)"
        },
        {
          "DataCategory": "Audit Logs",
          "RetentionPeriod": "6 years",
          "Justification": "HIPAA requirement"
        }
      ],
      "SecurityMeasures": [
        {
          "Measure": "Encryption at Rest",
          "Implementation": "AES-256-GCM via IEncryptionProvider"
        },
        {
          "Measure": "Access Control",
          "Implementation": "Role-based (RBAC) with [RequirePermission] attribute"
        },
        {
          "Measure": "Audit Logging",
          "Implementation": "Tamper-evident hash chain via IAuditLogger"
        }
      ]
    }
  ]
}
EOF

    # Replace placeholders
    sed -i "s/TIMESTAMP/$(date -u +"%Y-%m-%dT%H:%M:%SZ")/g" "$output_path"
    sed -i "s/TEMPLATE_TYPE/$(tr '[:lower:]' '[:upper:]' <<< ${template_type:0:1})${template_type:1}/g" "$output_path"

    echo -e "${GREEN}✓ JSON RoPA template created: $output_path${NC}"
}

# Main execution
main() {
    parse_args "$@"

    echo -e "\n${CYAN}=== Excalibur RoPA Template Generator ===${NC}"
    echo -e "${YELLOW}Output: $OUTPUT_PATH${NC}"
    echo -e "${YELLOW}Format: $OUTPUT_FORMAT${NC}"
    echo -e "${YELLOW}Type: $TEMPLATE_TYPE${NC}\n"

    case "$OUTPUT_FORMAT" in
        markdown)
            generate_markdown_template "$OUTPUT_PATH" "$TEMPLATE_TYPE"
            ;;
        json)
            generate_json_template "$OUTPUT_PATH" "$TEMPLATE_TYPE"
            ;;
        csv)
            echo -e "${YELLOW}CSV format not yet implemented${NC}"
            echo -e "${YELLOW}Use markdown or json format${NC}"
            exit 1
            ;;
        *)
            echo -e "${RED}✗ Unknown format: $OUTPUT_FORMAT${NC}"
            echo -e "${YELLOW}Valid formats: markdown, json${NC}"
            exit 1
            ;;
    esac

    echo -e "\n${GREEN}✓ RoPA template generation complete!${NC}"
    echo -e "\n${YELLOW}Next steps:${NC}"
    echo -e "${GRAY}  1. Fill out the template with your processing activities${NC}"
    echo -e "${GRAY}  2. Review with Data Protection Officer (DPO)${NC}"
    echo -e "${GRAY}  3. Update annually or when processing activities change${NC}"
    echo -e "${GRAY}  4. Make available to supervisory authority on request${NC}"
}

main "$@"
