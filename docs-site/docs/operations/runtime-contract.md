---
sidebar_position: 3
title: Runtime Contract
description: Canonical runtime semantics for Excalibur execution paths
---

# Runtime Contract

This guide defines the runtime guarantees that Excalibur provides during message execution.

## Execution Modes

Dispatch runs in one of two modes:

- **Local mode**: message is handled in-process.
- **Transport mode**: message is routed through configured transport adapters.

Routing is resolved before pipeline execution.

## Pipeline Contract

Execution order is:

1. Dispatcher receives message + caller cancellation token.
2. Route decision is resolved.
3. Middleware executes in registration order.
4. Final handler/transport execution occurs.
5. Result is returned to caller.

## Cancellation Contract

- Cancellation tokens are part of the API contract and must propagate end-to-end.
- A `DispatchAsync` call from within a handler inherits correlation and cancellation budget.
- Canceled requests must not continue work in the same request pipeline.

## Context Contract

When context is materialized, these fields are expected:

- `CorrelationId`
- `CausationId`
- message identity/type metadata

Lean local paths can defer full context creation, but correlation/causation semantics must remain correct whenever context is requested.

## Retry and Poison Contract

- Retry behavior is policy/profile based.
- Exhausted retries route to poison/dead-letter handling when configured.
- Dead-letter records must retain actionable failure metadata.

## Payload Size Contract

- Oversized messages are rejected at the boundary — measured **before** the body is deserialized — so a
  single large payload cannot exhaust memory, strand a batch, or poison a queue. Ingress fails **closed**:
  the message is rejected, never truncated and never silently passed.
- **Outbox publish** enforces a maximum serialized payload size (`OutboxDeliveryOptions.MaxPayloadBytes`,
  default 4 MiB); an over-limit message is rejected before staging rather than written and retried forever.
- **Every transport receive/subscribe path** enforces its own maximum inbound payload size before
  deserialization. An over-limit delivery is rejected using the transport's native negative-acknowledgement
  (nacked / dead-lettered / abandoned) and logged (a `…PayloadTooLarge` event), and the rest of the batch
  keeps processing — no poison-loop, no stranded batch:

  | Surface | Configure via | Default limit |
  |---|---|---|
  | Outbox publish | `OutboxDeliveryOptions.MaxPayloadBytes` | 4 MiB |
  | AWS SQS | `IAwsSqsTransportBuilder.UseMaxPayloadBytes(int?)` | 256 KiB (SQS message ceiling) |
  | Azure Service Bus | `AzureServiceBusProcessorOptions.MaxPayloadBytes` | 256 KiB |
  | Google Pub/Sub | `GooglePubSubOptions.Subscriber.MaxPayloadBytes` | 10 MiB |
  | gRPC | `GrpcTransportOptions.MaxPayloadBytes` | 4 MiB |
  | Kafka | `KafkaConsumerTuningOptions.MaxPayloadBytes` | 4 MiB |
  | RabbitMQ | `RabbitMqConsumptionOptions.MaxPayloadBytes` | 4 MiB (nacked `requeue: false`) |

- The default is deliberately **bounded** (never unbounded) so the guard is never inert for a consumer who
  never configures one. Set `MaxPayloadBytes` to `null` to opt out (unbounded) for larger legitimate
  payloads, or raise it to a specific ceiling. A non-positive value is rejected at startup
  (`ValidateOnStart`) rather than silently bricking delivery.

## Release Enforcement

This contract is release-blocked by:

- transport conformance tests,
- release-blocking CI test governance gate,
- architecture/governance validation.

## See Also

- [Reliability Guarantees](reliability-guarantees.md)
- [SLO, SLI, and Telemetry](slo-sli-telemetry.md)
- [Incident Runbooks](incident-runbooks.md)
