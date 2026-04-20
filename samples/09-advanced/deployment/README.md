# 09-advanced/deployment — Background Services, Leader Election, and Production Pipelines

Patterns for long-running workers, distributed coordination, job scheduling, and production-grade middleware stacks.

## Background Services

| Sample | What You Learn | Infrastructure |
|--------|----------------|----------------|
| [BackgroundServices](BackgroundServices/) | 4 hosting patterns: at-least-once inbox, transactional, minimized window, basic polling | Varies by sub-project |
| [DataProcessingBackgroundService](DataProcessingBackgroundService/) | Background data processing with retry and error handling | None |

## Distributed Coordination

| Sample | What You Learn | Infrastructure |
|--------|----------------|----------------|
| [LeaderElection](LeaderElection/) | Redis-based leader election, TTL leases, failover callbacks | Docker (Redis) |

## Job Scheduling

| Sample | What You Learn | Infrastructure |
|--------|----------------|----------------|
| [JobWorkerSample](JobWorkerSample/) | Quartz scheduling, persistent store, Redis coordination, CDC/outbox/data-processing jobs, health checks | Docker (SQL Server, Redis) |

> **Looking for Quartz-scheduled CDC specifically?** See [09-advanced/cdc/CdcJobQuartz](../cdc/CdcJobQuartz/).

## Production Stacks

| Sample | What You Learn | Infrastructure |
|--------|----------------|----------------|
| [ProductionPipeline](ProductionPipeline/) | Full middleware stack: security, validation, transactions, observability | None (in-memory) |
| [Testing](Testing/) | Test-harness patterns, fake pipelines, deterministic clocks | None |

## Learning Path

1. **[BackgroundServices](BackgroundServices/)** — understand the 4 basic hosting patterns
2. **[LeaderElection](LeaderElection/)** — add singleton guarantees across replicas
3. **[JobWorkerSample](JobWorkerSample/)** — production-grade scheduled work with Quartz
4. **[ProductionPipeline](ProductionPipeline/)** — wire everything together with security, validation, and observability
5. **[Testing](Testing/)** — ensure your pipeline is reliably testable

## Related

- [04-reliability/](../../04-reliability/) — outbox, retry, circuit breaker, saga (compose with these deployment patterns)
- [07-observability/](../../07-observability/) — telemetry infrastructure used by `ProductionPipeline`
- [11-real-world/](../../11-real-world/) — samples that combine these deployment patterns end-to-end
