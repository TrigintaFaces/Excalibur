---
title: Threat Model Baseline
description: Release-blocking threat categories and control expectations for Excalibur.
---

# Threat Model Baseline

Excalibur uses a baseline threat model to keep releases security-ready by default.

## Covered Threat Areas

| Area | Risks | Baseline Mitigations |
|---|---|---|
| Supply chain | Dependency/package tampering | SBOM + vulnerability scanning + package governance gates |
| Message integrity | Tampering/replay/header spoofing | signing/encryption options, correlation checks, idempotency patterns |
| Privilege boundaries | Unauthorized execution paths | authorization + audit + policy middleware |
| Data protection | PII/secret exposure | encryption and logging redaction controls |
| Availability | retry storms, poison loops, queue lag | retry caps, dead-letter handling, operational SLO alerts |

## Release Requirement

Security readiness is release-blocking and tied to CI gates (security scans, governance checks, and conformance tests).

## Operational Follow-Up

- Sev1/Sev2 incidents must produce security remediation tasks and regression tests.
- Threat model updates are reviewed in the architecture review board cadence.

## See Also

- [Encryption Architecture](./encryption-architecture.md)
- [Audit Logging](./audit-logging.md)
- [Operations Runbooks](../operations/incident-runbooks.md)
