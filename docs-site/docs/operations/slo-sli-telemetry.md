---
sidebar_position: 5
title: SLO, SLI, and Telemetry
description: Operational objectives, indicators, and telemetry schema for production Dispatch systems
---

# SLO, SLI, and Telemetry

This page defines baseline production objectives and the telemetry required to operate Excalibur safely.

## Suggested SLO Baseline

| Objective | Target |
|---|---|
| Dispatch success rate | &gt;= 99.9% over rolling 30 days |
| Local dispatch p95 latency | &lt;= 5 ms |
| Transport dispatch p95 latency | &lt;= 100 ms (provider dependent) |
| Dead-letter growth | no sustained growth in steady-state |
| Queue lag recovery | return to baseline within 15 minutes after spikes |

## Required SLIs

| SLI | Formula / Meaning |
|---|---|
| Success rate | `success_count / total_count` |
| Latency p95/p99 | percentile latency by message type and route |
| Error budget burn | failure trend against allowed SLO budget |
| Queue lag | age/depth of unprocessed transport messages |
| Dead-letter rate | dead-letter additions per minute |

## Required Telemetry Dimensions

Use consistent dimensions across metrics/traces/logs:

- `message.type`
- `route`
- `operation`
- `result`
- `error.type`
- `transport.name`
- `correlation.id`

## Minimum Alert Set

1. error rate above threshold (for example > 2% for 5m),
2. p95 latency over target (for example 10m),
3. dead-letter growth sustained,
4. queue lag sustained.

## See Also

- [Runtime Contract](runtime-contract.md)
- [Incident Runbooks](incident-runbooks.md)
- [Operational Resilience](resilience.md)
