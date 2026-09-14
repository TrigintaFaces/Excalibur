# CM-8: Software Bill of Materials (SBOM)

**Control:** NIST 800-53 Rev 5 CM-8 - Information System Component Inventory
**Framework:** Excalibur
**Last Updated:** 2026-09-12
**Status:** IMPLEMENTED
**Implementation Date:** 2026-01-01

---

## Control Description

**CM-8 Information System Component Inventory**

The organization:
- Develops and documents an inventory of information system components that:
  - Accurately reflects the current information system;
  - Includes all components within the authorization boundary of the information system;
  - Is at the level of granularity deemed necessary for tracking and reporting; and
  - Includes the following information to achieve effective information system component accountability: description, type, location, manufacturer, supplier, owner, responsible individual, unique identifier.

---

## Implementation Summary

Excalibur satisfies CM-8 through **automated Software Bill of Materials (SBOM) generation** using the CycloneDX standard in the continuous integration pipeline.

**Key Implementation Details:**
- **SBOM Format:** CycloneDX (OWASP standard)
- **Scope:** All NuGet packages (Excalibur.*)
- **Generation:** Automated via GitHub Actions CI/CD pipeline
- **Retention:** 90 days artifact retention
- **Validation:** Automated completeness checks
- **Accessibility:** Published with every CI build

---

## SBOM Generation

### Automation

SBOM generation is automated in the framework's GitHub Actions CI workflow, in the [framework repository](https://github.com/TrigintaFaces/Excalibur):

```yaml
sbom-generation:
  name: SBOM Generation (CycloneDX)
  runs-on: ubuntu-latest
  needs: build
  steps:
    - name: Checkout code
      uses: actions/checkout@v4
      with:
        fetch-depth: 0

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Restore dependencies
      run: dotnet restore

    - name: Generate CycloneDX SBOM for all packages
      run: |
        dotnet tool restore   # the generator is version-pinned in .config/dotnet-tools.json
        dotnet CycloneDX ./src -o ./src -F Json -t

    - name: Upload SBOM artifacts
      uses: actions/upload-artifact@v4
      with:
        name: cyclonedx-sbom
        path: |
          **/bom.json
          **/bom.xml
        retention-days: 90
```

### SBOM Contents

Each generated SBOM includes:

**Component Information:**
- Package name and version
- Package description
- Package type (library, framework)
- Publisher/author
- License information (SPDX identifiers)
- Package hashes (SHA-256)
- External references (repository, documentation)

**Dependency Graph:**
- Direct dependencies
- Transitive dependencies
- Dependency versions and constraints

**Metadata:**
- Generation timestamp
- Tool information (CycloneDX version)
- Framework target (.NET 10.0)

---

## Validation

### Automated Validation

The CI pipeline validates SBOM completeness:

```yaml
- name: Validate SBOM completeness
  run: |
    # Check that SBOM files were generated
    sbom_count=$(find . -name "bom.json" -o -name "bom.xml" | wc -l)

    if [ "$sbom_count" -eq 0 ]; then
      echo "No SBOM files generated"
      exit 1
    fi

    # List all SBOM files for verification
    echo "Generated SBOM files:"
    find . -name "bom.json" -o -name "bom.xml"
```

**Validation Criteria (what CI actually enforces):**
- At least one SBOM file is produced; a run that produces none fails the job.

The check counts SBOM files across the whole run, not per package, so it cannot distinguish
complete coverage from a single package emitting a document. Per-package coverage, CycloneDX
schema validity, dependency completeness and license presence are **not** asserted by the
pipeline — verify those from the artifact before submitting it.

---

## CM-8 Control Mapping

| CM-8 Requirement | Excalibur Implementation |
|------------------|-----------------------------------|
| **Inventory Development** | Automated SBOM generation via CycloneDX |
| **Current System Reflection** | SBOM generated on every CI build (reflects latest state) |
| **Authorization Boundary Components** | Every NuGet package the framework publishes |
| **Appropriate Granularity** | Package-level granularity with dependency graph |
| **Component Description** | Package name, version, description included |
| **Component Type** | Library/framework type metadata |
| **Location** | Source repository reference in SBOM metadata |
| **Manufacturer** | Publisher/author metadata |
| **Supplier** | NuGet package source |
| **Owner** | Repository owner (TrigintaFaces) |
| **Responsible Individual** | Tracked via Git commit metadata |
| **Unique Identifier** | Package ID + version + hash (SHA-256) |

---

## Artifact Retention

**Retention Policy:**
- SBOM artifacts retained for **90 days** per GitHub Actions artifact policy
- Downloadable from GitHub Actions workflow runs
- Accessible via GitHub API

**Access:**
- Project maintainers: Full access
- Security auditors: Read access via GitHub Security tab
- Consumers: Available on request for compliance audits

---

## Compliance Evidence

### Evidence Artifacts

**Primary Evidence:**
1. The CI workflow's SBOM generation job, in the [framework repository](https://github.com/TrigintaFaces/Excalibur)
2. GitHub Actions workflow runs - SBOM artifacts (90-day retention)
3. This control documentation

**Supporting Evidence:**
1. CycloneDX specification conformance (OWASP standard)
2. Automated validation logs in CI pipeline
3. GitHub Security tab (dependency graph integration)

### Audit Trail

**Traceability:**
- Every CI build generates fresh SBOM
- Git commit SHA links SBOM to exact codebase state
- GitHub Actions run ID provides unique audit identifier
- CycloneDX metadata includes generation timestamp and tool version

---

## Continuous Monitoring

### Automated Updates

SBOMs are automatically regenerated on:
- Every push to `main` or `develop` branches
- Every pull request (for validation)
- Manual workflow dispatch (on-demand)

### Staleness Prevention

- SBOM regenerated with every code change
- No manual SBOM maintenance required
- Automated validation catches generation failures
- 90-day artifact retention ensures recent inventory availability

---

## Security Integration

### GitHub Security Tab

CycloneDX SBOMs integrate with GitHub's dependency graph:
- Vulnerability scanning (Dependabot alerts)
- Dependency review (pull request checks)
- Security advisories (CVE matching)

### Container Scanning

SBOM data would complement container scanning. **No container-scan job exists in this pipeline**; if your assessment requires one, add it — do not cite it as inherited:
- Runtime dependency validation
- OS package vulnerability scanning
- Combined SBOM + container scan provides comprehensive inventory

---

## FedRAMP Impact

**Status:** 12 of 14 controls satisfied; 2 partial (SI-7, PM-11)

**CM-8 Closure:**
- CM-8 was the last of the twelve controls the framework satisfies outright
- SI-7 remains partial (packages ship unsigned) and PM-11 is a business process the consumer owns

**Related Controls:**
- **SA-4:** Acquisition Process (SBOM provided to consumers)
- **SA-15:** Development Process and Standards (automated SBOM generation)
- **SR-3:** Supply Chain Risk Management (dependency transparency)
- **SR-4:** Provenance (package source and hash verification)

---

## Consumer Usage

### Accessing SBOMs

**For Framework Consumers:**

1. **GitHub Actions:**
   - Navigate to repository Actions tab
   - Select latest CI workflow run
   - Download `cyclonedx-sbom` artifact

2. **GitHub API:**
   ```bash
   gh run download <run-id> -n cyclonedx-sbom
   ```

3. **On Request:**
   - Contact project maintainers for specific SBOM versions
   - Available for compliance audit purposes

### SBOM Formats

**Available Formats:**
- `bom.json` - CycloneDX JSON (machine-readable)

**Tools Compatible:**
- OWASP Dependency-Track
- GitHub Security tab
- an SBOM scanner of your choice (none ships here)
- Any CycloneDX 1.4+ compatible tool

---

## Review and Updates

**Last Review:** 2026-01-01

**Change Log:**
- 2026-01-01: Initial CM-8 implementation
- Future updates tracked via Git history

---

## References

**Standards:**
- [NIST SP 800-53 Rev 5 - CM-8](https://csrc.nist.gov/projects/risk-management/sp800-53-controls/release-search#!/control?version=5.1&number=CM-8)
- [CycloneDX Specification](https://cyclonedx.org/specification/overview/)
- [OWASP Software Component Verification Standard (SCVS)](https://owasp.org/www-project-software-component-verification-standard/)

**Implementation:**
- [CycloneDX GitHub Action](https://github.com/CycloneDX/gh-dotnet-generate-sbom)
- [GitHub Actions Artifacts](https://docs.github.com/en/actions/using-workflows/storing-workflow-data-as-artifacts)

**Related Documentation:**
- [FedRAMP overview](README.md) - Control status, evidence package, and inheritance model
- [Development process (SA-15)](README.md#development-process-sa-15) - CI/CD pipeline and quality gates

---

## See Also

- [FedRAMP Overview](./README.md) - All FedRAMP control implementations
- [Compliance Checklists](../checklists/fedramp.md) - FedRAMP checklist
- [Security](../../security/index.md) - Security implementation guides

---

**Last Updated:** 2026-09-12
**Status:** IMPLEMENTED
**Compliance:** CM-8 SATISFIED
