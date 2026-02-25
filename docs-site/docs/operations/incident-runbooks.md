---
sidebar_position: 6
title: Incident Runbooks
description: Escalation model and response playbooks for Excalibur runtime incidents
---

# Incident Runbooks

Use this guide for production incident response across Excalibur workloads.

## Severity Model

| Severity | Definition | Initial Response Target |
|---|---|---|
| Sev 1 | outage, data-loss risk, or critical message processing halt | 15 minutes |
| Sev 2 | major degradation without total outage | 30 minutes |
| Sev 3 | localized degradation or non-critical impact | 1 business day |

## Ownership

| Area | Primary Owner | Escalation |
|---|---|---|
| Dispatch core runtime | Platform team | Architecture lead |
| Transport/provider failures | Platform + provider owner | Release engineer |
| Security/compliance incidents | Compliance/security owner | Security lead |

## Standard Response Flow

1. Declare incident and assign incident commander.
2. Identify blast radius (message types, transports, tenants).
3. Stabilize (pause/scale/reroute as needed).
4. Roll back or patch.
5. Verify recovery using health metrics and queue/DLQ indicators.
6. Publish post-incident actions.

## Common Playbooks

## Transport backlog surge

- check queue depth and lag,
- verify consumer health and recent config/deploy changes,
- scale consumers or throttle producers,
- rollback recent runtime/transport changes if no recovery trend.

## Dead-letter spike

- identify dominant failure reason,
- validate retry/poison policy config,
- patch root cause and replay DLQ in controlled batches.

## Cancellation/timeout regression

- verify token propagation from HTTP/job trigger to dispatcher and transport calls,
- compare behavior with last known-good release,
- rollback if leaked work continues after cancellation.

## Post-Incident Requirements

For Sev 1 and Sev 2 incidents:

- create and track remediation issues,
- add regression tests for uncovered failure mode,
- update runbooks and reliability docs if behavior changed.

## See Also

- [Runtime Contract](runtime-contract.md)
- [Reliability Guarantees](reliability-guarantees.md)
