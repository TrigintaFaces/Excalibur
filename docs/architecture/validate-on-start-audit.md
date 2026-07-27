# ValidateOnStart Coverage Audit (Sprint 681, updated Sprint 746)

## Summary

- **Total DI registration methods** (`Add*` on `IServiceCollection`): ~130 public Add methods
- **Files with ValidateOnStart**: All registration methods now include ValidateOnStart
- **Gap**: **0** -- fully cleared as of Sprint 746 (ADR-202)

The original audit (Sprint 681) identified 28 DI extensions (21.5%) missing ValidateOnStart. This gap was progressively closed across Sprints 740-746, with the final P1 gaps (OutboxDeliveryOptions, ExcaliburOptions) fixed in Sprint 746 Phase 2.

## Top 10 Candidates for ValidateOnStart

These packages have options that are most likely to cause runtime failures if misconfigured:

| # | Package | Options Class | Risk | Reason |
|---|---------|--------------|------|--------|
| 1 | `Excalibur.Hosting` | `ExcaliburOptions` | High | Core framework options; misconfiguration breaks everything |
| 2 | `Excalibur.Security` | Multiple auth/crypto options | High | Security misconfig = silent bypass or runtime crash |
| 3 | `Excalibur.Data.ElasticSearch` | `ElasticsearchOptions`, `SecurityOptions` | High | 9+ DI methods, complex multi-option setup |
| 4 | `Excalibur.Data.CosmosDb` | `CosmosDbOptions` | Medium | Connection string, consistency level, RU config |
| 5 | `Excalibur.EventSourcing` | `EventSourcingOptions` | Medium | Event store, snapshot, migration config |
| 6 | `Excalibur.Outbox.*` | Provider options | Medium | Outbox misconfiguration = message loss |
| 7 | `Excalibur.LeaderElection.*` | Provider options | Medium | LE misconfiguration = split brain |
| 8 | `Excalibur.Compliance` | GDPR, Erasure, Encryption options | High | Compliance misconfig = regulatory risk |
| 9 | `Excalibur.AuditLogging.*` | Exporter options | Medium | Audit trail gaps |
| 10 | `Excalibur.Data.MongoDB` | `MongoDbOptions` | Medium | Connection, projection, auth config |

## Packages with Good Coverage (Reference)

These packages already have comprehensive ValidateOnStart:

- `Excalibur.Data.DataProcessing` — 9 ValidateOnStart registrations
- `Excalibur.Compliance` — GDPR, erasure, encryption all validated
- `Excalibur.Dispatch` Core — delivery, scheduling, serialization validated
- `Excalibur.Cdc.*` — CDC processing, DynamoDB, CosmosDB all validated
- `Excalibur.Data.Postgres` — persistence options validated
- `Excalibur.Saga.*` — saga options validated across all providers

## Packages with No ValidateOnStart

These packages register options but have no ValidateOnStart:

- `Excalibur.A3` — Authorization services
- `Excalibur.Caching` — Projection caching
- `Excalibur.Application` — Application services
- `Excalibur.Data` — Base data services
- `Excalibur.Jobs.SqlServer` — Job coordination

## Migration Plan (Completed)

1. **Sprint 681**: This audit document (done)
2. **Sprints 560-564**: ValidateOnStart phases 1-5 covered ~100 registrations
3. **Sprint 740**: ValidateOnStart for 16 additional sites
4. **Sprint 746**: Final sweep -- OutboxDeliveryOptions, ExcaliburOptions, Jobs AWS/Azure/GoogleCloud, PollyRetryOptions
5. **Status: COMPLETE** -- All `Add*` methods now include ValidateOnStart for their options
6. **Convention**: All new `Add*` methods MUST include ValidateOnStart for their options

## Pattern for Adding ValidateOnStart

```csharp
// 1. Create validator
internal sealed class MyOptionsValidator : IValidateOptions<MyOptions>
{
    public ValidateOptionsResult Validate(string? name, MyOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            failures.Add("ConnectionString is required.");
        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

// 2. Register in DI extension
services.AddSingleton<IValidateOptions<MyOptions>, MyOptionsValidator>();
services.AddOptionsWithValidateOnStart<MyOptions>();
```
