# Test Infrastructure Architecture

This document provides an overview of the test infrastructure in Excalibur.Dispatch.

## Quick Reference

The authoritative documentation for test infrastructure is in [`tests/Shared/Tests.Shared/README.md`](../../tests/Shared/Tests.Shared/README.md).

## Architecture Overview

### Test Base Class Hierarchy

```
UnitTestBase
    |
    +-- IntegrationTestBase (IAsyncLifetime)
            |
            +-- FunctionalTestBase
            +-- DatabaseIntegrationTestBase
            +-- CacheIntegrationTestBase
            +-- MessageBrokerIntegrationTestBase

ContainerFixtureBase (IAsyncLifetime)
    |
    +-- SqlServerContainerFixture
    +-- PostgresContainerFixture
    +-- RedisContainerFixture
    +-- KafkaContainerFixture
    +-- RabbitMqContainerFixture
    +-- MongoDbContainerFixture
    +-- ElasticsearchContainerFixture
```

### Design Principles

1. **Single Inheritance Path** - All test bases derive from `UnitTestBase` for DI support
2. **Docker Graceful Degradation** - Container fixtures detect Docker availability via `DockerAvailable` property
3. **CI-Configurable Timeouts** - All timeouts respect `TEST_TIMEOUT_MULTIPLIER` environment variable
4. **No Framework-Specific Variants** - One base class per test type, not per framework

### Key Components

| Component | Purpose | Location |
|-----------|---------|----------|
| `UnitTestBase` | Fast, isolated tests with DI | `Tests.Shared/UnitTestBase.cs` |
| `IntegrationTestBase` | Tests with real dependencies | `Tests.Shared/IntegrationTestBase.cs` |
| `FunctionalTestBase` | End-to-end workflow tests | `Tests.Shared/FunctionalTestBase.cs` |
| `ContainerFixtureBase` | Unified Docker container lifecycle | `Tests.Shared/Fixtures/ContainerFixtureBase.cs` |
| `TestTimeouts` | CI-configurable timeout constants | `Tests.Shared/Infrastructure/TestTimeouts.cs` |
| `WaitHelpers` | Async polling utilities | `Tests.Shared/Infrastructure/WaitHelpers.cs` |
| `TestTypes/` | Reusable test types (messages, stubs) | `Tests.Shared/TestTypes/` |

### Container Collection Serialization (Sprint 703)

Transport integration tests use xUnit `[Collection]` attributes to serialize test class execution within each transport provider. This prevents resource exhaustion from parallel container creation on CI runners.

**Available collection constants** (`Tests.Shared/Fixtures/ContainerCollections.cs`):

| Constant | Value | Purpose |
|----------|-------|---------|
| `ContainerCollections.Postgres` | `"Postgres"` | Serialize Postgres tests |
| `ContainerCollections.SqlServer` | `"SQL Server"` | Serialize SQL Server tests |
| `ContainerCollections.Redis` | `"Redis"` | Serialize Redis tests |
| `ContainerCollections.MongoDB` | `"MongoDB"` | Serialize MongoDB tests |
| `ContainerCollections.Kafka` | `"Kafka"` | Serialize Kafka tests |
| `ContainerCollections.RabbitMQ` | `"RabbitMQ"` | Serialize RabbitMQ tests |
| `ContainerCollections.Elasticsearch` | `"Elasticsearch"` | Serialize Elasticsearch tests |
| `ContainerCollections.AzureServiceBus` | `"Azure Service Bus"` | Serialize Azure ServiceBus tests |
| `ContainerCollections.AwsSqs` | `"AWS SQS"` | Serialize AWS SQS tests |

**Usage pattern:**

```csharp
[Collection(ContainerCollections.RabbitMQ)]
[Trait("Category", "Integration")]
[Trait("Provider", "RabbitMQ")]
public sealed class MyRabbitMqIntegrationShould : IAsyncLifetime
{
    // Tests run sequentially with other classes in the same collection
}
```

**Rule:** All transport integration test classes MUST use `[Collection(ContainerCollections.X)]`. Without collection serialization, parallel container creation can exhaust CI runner resources and crash the test host.

### RabbitMQ.Client 7.x Buffer Safety (Sprint 703)

RabbitMQ.Client 7.x uses `ReadOnlyMemory<byte>` backed by pooled `PipeReader` buffers for `BasicDeliverEventArgs.Body`. The buffer is recycled when the consumer callback completes. Any test that stores `BasicDeliverEventArgs` for later inspection must copy the body inside the callback:

```csharp
// CORRECT: Copy body inside callback
consumer.ReceivedAsync += (sender, args) =>
{
    receivedBody = args.Body.ToArray();
    return Task.CompletedTask;
};
```

### Consolidation History

| Phase | Achievement |
|-------|-------------|
| Phase 1 | Unified ContainerFixtureBase (3->1), created TestTimeouts |
| Phase 2 | Created WaitHelpers (328 lines), enhanced base classes, removed 8 framework-specific bases |
| Phase 3 | Deleted Tests.Shared.Extra, consolidated TestTypes, documentation |

## Related Documentation

- [Tests.Shared README](../../tests/Shared/Tests.Shared/README.md) - Comprehensive usage guide
- [Test Infrastructure Consolidation](../../management/architecture/adr-071-test-infrastructure-consolidation.md) - Consolidation rationale
- [testcontainers-setup.md](../../docs/testcontainers-setup.md) - Container configuration

---

*Last updated: Test Infrastructure Consolidation Complete*
