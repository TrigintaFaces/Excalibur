---
sidebar_position: 4
title: Reliability Guarantees
description: Delivery, ordering, deduplication, and dead-letter guarantees by execution path
---

# Reliability Guarantees

Use this matrix to understand what behavior is guaranteed in each mode.

## Dispatch-Level Matrix

| Capability | Local Dispatch | Transport Dispatch |
|---|---|---|
| Delivery | Single in-process invocation | At-least-once baseline |
| Ordering | Deterministic in-process | Provider/partition scoped |
| Duplicate handling | Not expected in single invocation path | Requires idempotency/inbox strategy |
| Cancellation propagation | Required | Required |
| Retry behavior | Optional policy | Required for production transports |
| Dead-letter handling | Optional | Required for production transports |

## Provider Family Baselines

| Provider Family | Delivery | Ordering | Dead-Letter |
|---|---|---|---|
| Kafka | At-least-once | Partition-ordered | Supported via policy |
| RabbitMQ | At-least-once | Queue-order best effort under competing consumers | Supported via reject/dead-letter policy |
| Azure Service Bus | At-least-once | Entity/session scoped ordering | Supported via dead-letter subqueue |
| AWS SQS/SNS/EventBridge adapters | At-least-once | Profile-dependent | Supported via adapter policy |
| Google Pub/Sub | At-least-once | Ordering-key scoped when enabled | Supported via provider policy |

## Failure-Path Guarantees

Release tests verify:

1. retry requeue increments delivery attempt,
2. poison flow routes to dead-letter when requeue is disabled,
3. timeout windows return no phantom message,
4. cancellation tokens are honored.

## Non-Guarantees

The framework does not promise:

- global ordering across all partitions/queues,
- exactly-once distributed delivery without external idempotency controls,
- zero-loss under infrastructure outage without durable persistence patterns.

## See Also

- [Runtime Contract](runtime-contract.md)
- [Incident Runbooks](incident-runbooks.md)
