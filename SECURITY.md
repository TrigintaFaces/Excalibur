# Security Policy

## Supported Versions

Excalibur.Dispatch is currently in pre-release development. Security updates are applied to the latest version on the `main` branch.

| Version | Supported |
|---------|-----------|
| main (pre-release) | Yes |

Once stable releases are published to NuGet, this table will be updated with specific version support windows.

## Reporting a Vulnerability

We take security vulnerabilities seriously. If you discover a security issue in Excalibur.Dispatch, please report it responsibly.

### How to Report

**Do NOT open a public GitHub issue for security vulnerabilities.**

Instead, please use one of the following methods:

1. **GitHub Security Advisories (Preferred):** Use the [GitHub Security Advisory](../../security/advisories/new) feature to report vulnerabilities privately. This allows us to collaborate on a fix before public disclosure.

2. **Email:** Send a detailed report to the repository maintainers via the email address listed in the repository contact information.

### What to Include

Please provide as much of the following information as possible:

- **Description** of the vulnerability and its potential impact
- **Steps to reproduce** the issue, including any proof-of-concept code
- **Affected packages** (e.g., `Excalibur.Dispatch`, `Excalibur.EventSourcing.SqlServer`)
- **Affected versions** or commits
- **Suggested fix** (if you have one)

### What to Expect

- **Acknowledgment:** We will acknowledge receipt of your report within **3 business days**.
- **Assessment:** We will assess the severity and impact within **7 business days**.
- **Resolution:** We aim to provide a fix or mitigation within **30 days** for confirmed vulnerabilities, depending on severity and complexity.
- **Disclosure:** We follow coordinated disclosure. We will work with you to agree on a disclosure timeline after the fix is available.

### Severity Classification

We use the following severity levels aligned with [CVSS v3.1](https://www.first.org/cvss/):

| Severity | CVSS Score | Response Target |
|----------|------------|-----------------|
| Critical | 9.0 - 10.0 | Fix within 7 days |
| High | 7.0 - 8.9 | Fix within 14 days |
| Medium | 4.0 - 6.9 | Fix within 30 days |
| Low | 0.1 - 3.9 | Fix in next release |

## Security Practices

### Dependencies

- NuGet dependencies are audited for known vulnerabilities via `dotnet list package --vulnerable` in CI.
- Dependency updates are tracked and applied regularly.
- A CVE allowlist is maintained at `management/security/cve-allowlist.yaml` for evaluated and accepted risks.

### Static Analysis

- Security SAST scanning runs on every CI build.
- Results are published as SARIF artifacts for review.

### Code Quality

- Banned API scanning prevents use of known-insecure patterns (e.g., `Newtonsoft.Json`, blocking async).
- SQL injection prevention uses `[GeneratedRegex]` whitelist validation and bracket-escape defense-in-depth.
- PII protection uses `ITelemetrySanitizer` with SHA-256 hashing for data subject identifiers in telemetry.
- Serialization policy enforcement prevents unauthorized serializer usage in core packages.

## Scope

This security policy covers the Excalibur.Dispatch framework packages published from this repository:

- `Excalibur.Dispatch` and `Excalibur.Dispatch.Abstractions`
- `Excalibur.Domain`, `Excalibur.Data.*`, `Excalibur.EventSourcing.*`
- All transport, middleware, and infrastructure packages
- CI/CD scripts and workflow definitions

Third-party dependencies are covered by their own security policies.
