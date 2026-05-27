# Samples Audit

This document records the current sample quality baseline and compositional-quality assessment.

## Scope

- `samples/**/*.csproj` build audit
- Orphan sample detection (`.cs` files without an associated `.csproj`)
- Sample documentation link consistency checks
- **Compositional-quality audit** (bd-dmsi02): Do samples demonstrate framework *composition* (IDispatcher/handlers/pipeline) rather than features in isolation?

## Results

- Buildable sample projects: **93**
- Build failures: **0**
- Orphan sample folders with source files and no project file: **0**
- Samples with README: **108** (100% coverage)
- Samples using IDispatcher/handler composition: **81/93** (87%)
- Samples with aspirational TODOs: **1** (`TransportBindings/README.md`)

## Compositional Quality Audit (Sprint 829)

Audit date: 2026-05-26. Rubric: bd-dmsi02 epic acceptance criteria.

### Level Classification

| Level | Category | Samples | Compositional? |
|-------|----------|---------|----------------|
| L1 | `01-getting-started` | 6 | 5/6 (83%) |
| L2 | `02-09` (messaging, cloud, reliability, serverless, security, observability, serialization, advanced) | 76 | 66/76 (87%) |
| L3 | `11-real-world` | 1 | 0/1 (0%) |

### Samples Lacking Dispatch Composition (Gaps)

| Sample | Level | Issue | Remediation |
|--------|-------|-------|-------------|
| `01-getting-started/BindConfigurationPatterns` | L1 | Infrastructure-only (IOptions demo) | Acceptable — demonstrates DI/config, not messaging |
| `01-getting-started/DataAccessIntro` | L1 | Data access only (IDataRequest) | Acceptable — scope is data layer, not dispatch |
| `01-getting-started/MetapackageQuickStart` | L1 | Wiring-only (AddExcalibur demo) | Acceptable — purpose is DI registration |
| `04-reliability/SagaOrchestration` | L2 | Uses saga primitives directly, no IDispatcher | **Gap**: Should dispatch commands via IDispatcher to trigger saga steps |
| `06-security/AccessReviews` | L2 | Direct service calls | **Gap**: Should dispatch review commands via pipeline |
| `06-security/AwsSecretsManager` | L2 | Infrastructure wiring only | Acceptable — demonstrates provider config |
| `06-security/AzureKeyVault` | L2 | Infrastructure wiring only | Acceptable — demonstrates provider config |
| `06-security/JitAccess` | L2 | Direct service calls | **Gap**: Should dispatch access requests via IDispatcher |
| `06-security/ProvisioningWorkflow` | L2 | Direct service calls | **Gap**: Should dispatch provisioning commands |
| `06-security/SeparationOfDuties` | L2 | Direct service calls | **Gap**: Should demonstrate policy via middleware/pipeline |
| `06-security/StandaloneA3` | L2 | A3 demo without dispatch | Acceptable — focused A3 authorization showcase |
| `11-real-world/ECommerceSample` | L3 | Direct OrderProcessingService, no dispatch | **Critical Gap**: L3 must show full composition |

### Gap Summary

| Priority | Count | Description |
|----------|-------|-------------|
| P1 (Critical) | 1 | L3 `ECommerceSample` — must demonstrate full pipeline composition |
| P1 (High) | 1 | L2 `SagaOrchestration` — saga steps should be triggered via dispatch |
| P2 (Medium) | 4 | L2 Security samples (`AccessReviews`, `JitAccess`, `ProvisioningWorkflow`, `SeparationOfDuties`) — should use dispatch pipeline |
| Acceptable | 6 | Infrastructure/config/wiring demos (L1 config, L2 vault/secrets) |

### P0 Checklist (All Samples)

| Criterion | Status |
|-----------|--------|
| CI/sln/slnf/manifest sync | **PASS** (93 projects in SamplesOnly.slnf) |
| Build green | **PASS** (verified) |
| No secrets in source | **PASS** |
| No `#if` conditional compilation | **PASS** |
| Public APIs only (no InternalsVisibleTo) | **PASS** |

### P1 Remaining Issues

| Criterion | Status | Details |
|-----------|--------|---------|
| No aspirational TODOs | 1 remaining | `TransportBindings/README.md` |
| README-code parity | **PASS** | All READMEs checked |
| Single `AddExcalibur` root (L3) | **FAIL** | ECommerceSample uses manual registration |
| Zero new public types | **PASS** | Samples define only internal/file-scoped types |

## Actions Completed (Historical)

1. Removed legacy/orphan sample folders that were not build-validated.
2. Updated sample documentation paths to the categorized folder layout (`samples/01-getting-started/...` etc.).
3. Removed links to non-existent/deprecated sample folders.
4. Added a `DataProcessing`-based CDC history replay path to `09-advanced/CdcAntiCorruption`.
5. **(Sprint 829)** Compositional-quality audit completed — 6 gaps tracked, 1 critical (ECommerceSample).

## Quality Standard Going Forward

1. Every runnable sample must have a `.csproj`.
2. Every sample listed in docs must point to an existing, buildable project path.
3. Legacy snippets without projects should live in docs, not in `samples/`.
4. Any new sample change must keep the full samples build audit green.
5. **L2/L3 samples MUST use IDispatcher/handler composition** — features must be reached via the dispatch pipeline, not direct service calls.
6. **L3 samples MUST use `AddExcalibur` builder root** — demonstrate the full framework composition pattern.
7. READMEs must match actual code — no aspirational feature descriptions.
