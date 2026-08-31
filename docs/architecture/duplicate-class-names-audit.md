# Duplicate Sealed Class Names Audit

**Sprint:** 682 (T.18)
**Date:** 2026-03-21
**Status:** Audit complete -- recommendations below

## Overview

This audit identifies sealed class names that appear multiple times across the codebase, along with their namespaces and a recommendation for each.

## High-Frequency Duplicates (6+ occurrences)

| Class Name | Count | Namespaces | Recommendation |
|------------|-------|-----------|----------------|
| `Resources` | 27 | Per-project auto-generated | **Keep** -- standard .NET resource class, one per project |
| `TagCardinalityGuard` | 1 | `Excalibur.Dispatch.Abstractions` | **Resolved** -- six copies collapsed to one public type in `Excalibur.Dispatch.Diagnostics` |
| `RunnerFactory` | 5 | Various CDC packages | **Keep** -- per-provider factory pattern |
| `Runner` | 5 | Various CDC packages | **Keep** -- per-provider runner implementations |
| `RetryPolicyOptions` | 4 | Various transport packages | **RESOLVED in S695** -- Transport.Abstractions → `TransportRetryPolicyOptions`, AwsSqs → `AwsSqsRetryPolicyOptions`, GooglePubSub → `PubSubRetryPolicyOptions`. ElasticSearch variant kept separate (different domain). See ADR-259 D.1. |

## Medium-Frequency Duplicates (3-4 occurrences)

| Class Name | Count | Namespaces | Recommendation |
|------------|-------|-----------|----------------|
| `RetryPolicy` | 1 | Resilience.Polly (executor) | **RESOLVED** -- config POCOs renamed: `SagaRetryOptions`, `RabbitMqRetryOptions`, `PubSubRetryOptions`. The `Dispatch/Configuration` and `Transport.Abstractions/BatchProcessing` copies have since been deleted as unread. |
| `ErrorMessages` | 4 | Various packages | **Keep** -- per-package error strings |
| `EncryptionOptions` | 4 | Security, Compliance, EventSourcing | **Review** -- potential consolidation |
| `CircuitBreakerOpenException` | 1 | Dispatch.Abstractions | **RESOLVED in S691** -- consolidated to single class in `Excalibur.Dispatch.Abstractions/Resilience/` with `InvalidOperationException` base. See ADR-255 D.26. |
| `TimeoutOptions` | 2 | Middleware (canonical), ElasticSearch (separate domain) | **RESOLVED in S694** -- `Options.Core.TimeoutOptions` deleted, `Options.Middleware.TimeoutOptions` is canonical. ElasticSearch variant is a different domain (kept). See ADR-258 D.10. |
| `OutboxOptions` | 1 | Excalibur.Outbox (canonical) | **RESOLVED in S687** -- 4 collisions renamed: `OutboxConfigurationOptions`, `OutboxDeliveryOptions`, `OutboxMiddlewareOptions`, `ExcaliburOutboxOptions`. See ADR-251 D.1. |
| `InboxOptions` | 3 | Various inbox packages | **Keep** -- per-provider configuration |
| `DeadLetterOptions` | 3 | Transport packages | **Review** -- candidates for shared Options |
| `MigrationOptions` | 3 | CDC/EventSourcing | **Keep** -- per-provider migration config |
| Snapshot request types | 3 each | EventSourcing packages | **Keep** -- per-provider request DTOs |

## Low-Frequency Duplicates (2 occurrences)

| Class Name | Count | Recommendation |
|------------|-------|----------------|
| `ValidationOptions` | 2 | **Keep** -- different validation domains |
| `TracingOptions` | 2 | **Review** -- may share common shape |
| `SqlServerConnectionOptions` | 1 | Resolved -- the `Excalibur.Data.SqlServer` copy was unread and has been deleted; `Excalibur.Data.SqlServer.Persistence` is canonical |
| `SqlServerPoolingOptions` | 1 | Resolved -- the `Excalibur.Data.SqlServer` copy was unread and has been deleted; `Excalibur.Data.SqlServer.Persistence` is canonical |
| `SessionOptions` | 1 | **Resolved** -- the transport copy went with the unreachable `SessionManagement` types; only Marten's own remains |

## Summary

- **99 duplicate names** across the codebase (matching task title)
- **27 are `Resources`** -- auto-generated, expected and correct
- **~20 are per-provider Options/Config** -- legitimate pattern for transport/persistence providers
- **~10 warrant review** for potential shared abstractions (RetryPolicy, DeadLetterOptions); `TagCardinalityGuard` is done
- **~~3 are bugs~~ RESOLVED** (CircuitBreakerOpenException consolidated in S691, see ADR-255)

## Recommendations

1. **No mass rename** -- most duplicates are legitimate per-provider implementations
2. **DONE** -- CircuitBreakerOpenException consolidated in Sprint 691 (ADR-255 D.26)
3. **DONE (S695)**: `RetryPolicyOptions` disambiguated with per-package prefixes. `DeadLetterOptions` still candidates for shared base in Transport.Abstractions
4. ~~**TagCardinalityGuard**: Evaluate if 6 copies can share a single implementation~~ -- **done**: one implementation now lives in `Excalibur.Dispatch.Abstractions`, which all six former carriers already reference
