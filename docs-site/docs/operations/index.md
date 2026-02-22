---
sidebar_position: 22
title: Operations
description: Operational guides for running Excalibur in production
---

# Operations

Operational guidance for running Excalibur in production environments, including resilience, recovery procedures, and maintenance runbooks.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A deployed Dispatch application
- Familiarity with [configuration](../configuration/index.md) and [observability](../observability/index.md)

## Guides

| Topic | Description |
|-------|-------------|
| [Runtime Contract](runtime-contract.md) | Canonical runtime semantics for dispatch ordering, cancellation, retries, and context propagation |
| [Reliability Guarantees](reliability-guarantees.md) | Delivery/ordering/deduplication/dead-letter guarantees by execution path and provider family |
| [SLO, SLI, and Telemetry](slo-sli-telemetry.md) | Production objectives and telemetry schema for release readiness and operations |
| [Incident Runbooks](incident-runbooks.md) | Escalation model and step-by-step response playbooks for common runtime incidents |
| [Operational Resilience](resilience.md) | Transient error handling, retry policies, and recovery strategies |
| [Recovery Runbooks](recovery-runbooks.md) | Step-by-step recovery procedures for common failure scenarios |

## Quick Reference

### Provider Resilience Matrix

| Provider | Retry Policy | Recovery Options | CDC Position Recovery |
|----------|--------------|------------------|----------------------|
| SQL Server | `SqlServerRetryPolicy` | Automatic reconnect | `CdcRecoveryOptions` |
| PostgreSQL | `PostgresRetryPolicy` | Automatic reconnect | `PostgresCdcRecoveryOptions` |
| CosmosDB | SDK-managed | Automatic | Continuation token |
| DynamoDB | SDK-managed | Automatic | Stream ARN |
| MongoDB | Driver pool | Automatic | Resume token |
| Redis | Manual reconnect | `ConnectionMultiplexer` | N/A |

### Key Error Codes

**SQL Server Transient Errors:**
- `596` - Session killed by backup/restore (critical for CDC)
- `9001`, `9002` - Transaction log unavailable
- `1205` - Deadlock victim
- `40613` - Database unavailable

**PostgreSQL Transient Errors:**
- `08xxx` - Connection errors
- `40001`, `40P01` - Serialization/deadlock
- `57Pxx` - Admin/crash shutdown
- `53xxx` - Insufficient resources

## Related Documentation

- [Observability](../observability/index.md) - Monitoring and alerting
- [Deployment](../deployment/index.md) - Deployment configurations
- [Event Sourcing](../event-sourcing/index.md) - Event store operations
- [Testing Overview](../testing/index.md) - Conformance and integration quality expectations

## See Also

- [Resilience with Polly](resilience-polly.md) — Polly-based retry policies, circuit breakers, and resilience pipelines
- [Performance Tuning](performance-tuning.md) — Optimize event store, outbox, and projection performance
- [Health Checks](../observability/health-checks.md) — Application health monitoring and diagnostics
