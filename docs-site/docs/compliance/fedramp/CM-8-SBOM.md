# CM-8: Software Bill of Materials (SBOM)

:::warning Not legal advice

This page describes technical features that can **support** your compliance work. It is not legal
advice, and it does not establish that any system is compliant with any law, regulation or standard.
You remain responsible for your own compliance assessment, independent testing and validation, and
review by qualified legal and compliance professionals. See the [Compliance Disclaimer](../../legal/compliance-disclaimer.md).
:::

**Control:** NIST 800-53 Rev 5 CM-8 - Information System Component Inventory
**Framework:** Excalibur
**Last Updated:** 2026-09-22
**Status:** PARTIAL — the framework inventories its own packages. Your component inventory is larger than
that, and the rest of it is yours to produce.

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

Excalibur **contributes to** CM-8 through automated Software Bill of Materials (SBOM) generation using
the CycloneDX standard in the continuous integration pipeline. The SBOM enumerates the framework's own
packages and their dependencies — one entry in your inventory, not the inventory.

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
  timeout-minutes: 20
  steps:
    # Actions are pinned by commit SHA, not by tag. A tag is mutable; a SHA is not.
    - name: Checkout code
      uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7
      with:
        fetch-depth: 1

    - name: Setup .NET build environment
      uses: ./.github/actions/setup-dotnet-build

    - name: Restore dependencies
      run: bash ./build.sh --restore

    - name: Generate CycloneDX SBOM for all packages
      run: |
        dotnet tool restore   # the generator is version-pinned in .config/dotnet-tools.json
        dotnet CycloneDX ./src -o ./src -F Json -t

    - name: Upload SBOM artifacts
      uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7
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
| **Authorization Boundary Components** | Every NuGet package the framework publishes. Your boundary also holds your own code, runtime and OS packages, which this SBOM does not cover |
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

**Access:** SBOM artifacts are produced in the framework's own pipeline and are not published to a
registry. If your assessor needs one, request it — and generate an SBOM for **your** application from
your own build, because that is the artifact your authorization boundary is assessed against.

---

## Compliance Evidence

### Evidence Artifacts

**Primary Evidence:**
1. The CI workflow's SBOM generation job, in the [framework repository](https://github.com/TrigintaFaces/Excalibur)
2. GitHub Actions workflow runs - SBOM artifacts (90-day retention)
3. This control documentation

**Supporting Evidence:**
1. The generated `bom.json` itself — the CycloneDX document a tool can parse and validate. The
   pipeline does not assert schema validity (see [Validation](#automated-validation)), so validate it
   yourself before submitting it.
2. The validation step's log from the CI run that produced the artifact.

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
- Combined SBOM + container scan **would** provide comprehensive inventory, once you add the scan

---

## FedRAMP Impact

**Status:** asserted only in the [FedRAMP checklist](../checklists/fedramp.md#control-mapping-table).

**CM-8 is PARTIAL, and this page is the reason why:** the SBOM described here covers the framework's
own packages. That is one entry in your component inventory, not the inventory. You still enumerate
your application's own components, your runtime and OS packages, and everything else inside your
authorization boundary.

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
- Any tool that reads CycloneDX 1.7. The SBOM is generated at spec version 1.7, so a tool that supports
  only an earlier version may reject it

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
