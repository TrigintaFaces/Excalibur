# Tests.Shared

Shared test infrastructure for Excalibur.Dispatch. Provides base classes, fixtures, and utilities for consistent testing across the solution.

## Base Class Hierarchy

```
UnitTestBase
    │
    └── IntegrationTestBase (IAsyncLifetime)
            │
            ├── FunctionalTestBase
            ├── DatabaseIntegrationTestBase
            ├── CacheIntegrationTestBase
            └── MessageBrokerIntegrationTestBase

ContainerFixtureBase (IAsyncLifetime)
    │
    ├── SqlServerContainerFixture
    ├── PostgresContainerFixture
    ├── RedisContainerFixture
    ├── KafkaContainerFixture
    ├── RabbitMqContainerFixture
    ├── MongoDbContainerFixture
    └── ElasticsearchContainerFixture
```

### When to Use Each Base Class

| Base Class | Use When | Docker Required |
|------------|----------|-----------------|
| `UnitTestBase` | Testing business logic in isolation, no external dependencies | No |
| `IntegrationTestBase` | Testing with real databases, services, or async lifecycle | Yes* |
| `FunctionalTestBase` | End-to-end workflow tests, full host testing | Yes |
| `DatabaseIntegrationTestBase` | Testing database operations with specific container | Yes |
| `CacheIntegrationTestBase` | Testing cache operations (Redis, etc.) | Yes |
| `MessageBrokerIntegrationTestBase` | Testing message broker operations (Kafka, RabbitMQ) | Yes |

\* Depends on test requirements; some integration tests may not need containers.

---

## Base Classes

### UnitTestBase

For fast, isolated tests with no external dependencies.

```csharp
using Tests.Shared;

public class MyUnitTests : UnitTestBase
{
    [Fact]
    public void Should_do_something()
    {
        // Use Services to configure DI
        Services.AddSingleton<IMyService, MyService>();
        BuildServiceProvider();

        var service = GetRequiredService<IMyService>();
        service.DoSomething().ShouldBe(expected);
    }
}
```

**Features:**
- `Services` - IServiceCollection for DI configuration
- `ServiceProvider` - Built service provider
- `BuildServiceProvider()` - Rebuild after modifying services
- `GetRequiredService<T>()` / `GetService<T>()` - Service resolution
- `NullLoggerFactory` - For tests that don't need logging

### IntegrationTestBase

For tests requiring TestContainers or real dependencies.

```csharp
using Tests.Shared;

public class MyIntegrationTests : IntegrationTestBase
{
    public override async Task InitializeAsync()
    {
        // Start containers or async setup
        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        // Cleanup
        await base.DisposeAsync();
    }

    [Fact]
    public async Task Should_integrate_with_database()
    {
        // Test with real dependencies
    }
}
```

### FunctionalTestBase

For end-to-end workflow tests.

```csharp
using Tests.Shared;

public class MyFunctionalTests : FunctionalTestBase
{
    [Fact]
    public async Task Should_complete_workflow()
    {
        // Full workflow test with timeout handling
    }
}
```

---

## Container Fixtures

All container fixtures extend `ContainerFixtureBase`, which provides:
- Consistent Docker availability detection (`DockerAvailable` property)
- Graceful error handling (`InitializationError` property)
- Configurable timeouts (affected by `TEST_TIMEOUT_MULTIPLIER`)

### Available Fixtures

| Fixture | Collection Name | Connection Property |
|---------|-----------------|---------------------|
| `SqlServerContainerFixture` | `SqlServer` | `ConnectionString` |
| `PostgresContainerFixture` | `Postgres` | `ConnectionString` |
| `MongoDbContainerFixture` | `MongoDB` | `ConnectionString` |
| `RedisContainerFixture` | `Redis` | `ConnectionString` |
| `RabbitMqContainerFixture` | `RabbitMQ` | `ConnectionString` |
| `KafkaContainerFixture` | `Kafka` | `BootstrapServers` |
| `ElasticsearchContainerFixture` | `Elasticsearch` | `Uri` |

### Usage

```csharp
using Tests.Shared.Fixtures;

[Collection(ContainerCollections.SqlServer)]
public class SqlServerTests
{
    private readonly SqlServerContainerFixture _fixture;

    public SqlServerTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Should_query_database()
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        // Test with real SQL Server
    }
}
```

### Docker Requirement

Docker is required for all container-based tests. GitHub Actions provides Docker
out of the box. If Docker is unavailable, container fixture initialization fails
and tests report as errors (not skipped).

---

## TestTimeouts

Centralized timeout constants with CI-configurable multiplier support.

### Environment Variable

Set `TEST_TIMEOUT_MULTIPLIER` to scale all timeouts for slower CI environments:

```bash
# Double all timeouts (useful for CI runners with limited resources)
export TEST_TIMEOUT_MULTIPLIER=2.0

# Run tests with multiplier
dotnet test
```

### Available Timeouts

```csharp
using Tests.Shared.Infrastructure;

// Timeout constants (all scaled by TEST_TIMEOUT_MULTIPLIER)
TestTimeouts.Unit             // 5 seconds - for unit tests
TestTimeouts.Integration      // 30 seconds - for integration tests
TestTimeouts.Functional       // 60 seconds - for functional tests
TestTimeouts.ContainerStart   // 120 seconds - for container startup
TestTimeouts.HealthCheck      // 10 seconds - for health checks
TestTimeouts.DatabaseOperation // 5 seconds - for database operations
TestTimeouts.ContainerDispose // 30 seconds - for container disposal
```

### Helper Methods

```csharp
// Execute task with timeout
var result = await TestTimeouts.WithTimeout(
    myAsyncOperation(),
    TestTimeouts.Integration,
    "Database query");

// Create cancellation token source
using var cts = TestTimeouts.CreateCancellationTokenSource(TestTimeouts.Functional);
```

---

## WaitHelpers

Async polling utilities for tests that need to wait for conditions.

### WaitUntilAsync

```csharp
using Tests.Shared.Infrastructure;

// Wait for synchronous condition
var isHealthy = await WaitHelpers.WaitUntilAsync(
    () => service.IsHealthy,
    TimeSpan.FromSeconds(30));

if (!isHealthy)
{
    Assert.Fail("Service did not become healthy in time");
}

// Wait for async condition
var found = await WaitHelpers.WaitUntilAsync(
    async () => await repository.ExistsAsync(id),
    TimeSpan.FromSeconds(10));

// Wait with custom poll interval
var ready = await WaitHelpers.WaitUntilAsync(
    () => cache.IsWarmed,
    TimeSpan.FromSeconds(60),
    pollInterval: TimeSpan.FromMilliseconds(500));
```

### WaitForValueAsync

```csharp
// Wait for a value to become available
var message = await WaitHelpers.WaitForValueAsync(
    () => queue.TryDequeue(out var msg) ? msg : null,
    TimeSpan.FromSeconds(5));

// Async version
var record = await WaitHelpers.WaitForValueAsync(
    async () => await repository.FindAsync(id),
    TimeSpan.FromSeconds(10));
```

### RetryUntilSuccessAsync

```csharp
// Retry action until success or timeout
var success = await WaitHelpers.RetryUntilSuccessAsync(
    async () => await service.ConnectAsync(),
    timeout: TimeSpan.FromSeconds(30),
    retryDelay: TimeSpan.FromMilliseconds(500),
    maxRetries: 10);
```

---

## Test Categories

### Using Attributes (Recommended)

```csharp
using Tests.Shared.Categories;

[UnitTest]
public class MyUnitTests { }

[IntegrationTest]
public class MyIntegrationTests { }

[FunctionalTest]
public class MyFunctionalTests { }

[ArchitectureTest]
public class MyArchTests { }

[PerformanceTest]
public class MyPerfTests { }
```

### Using Constants

```csharp
using Tests.Shared.Categories;

[Trait("Category", TestCategories.Unit)]
public class MyTests { }
```

### Running by Category

```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Functional"
```

---

## Helpers

### TestLogger

Custom logger for capturing log output in tests.

```csharp
var logger = new TestLogger<MyService>();
var service = new MyService(logger);

service.DoSomething();

logger.LogEntries.ShouldContain(e => e.Message.Contains("expected"));
```

### ServiceCollectionExtensions

DI helpers for common test scenarios.

```csharp
Services.AddTestServices();
Services.ReplaceWithFake<IExternalService>();
```

### MessageBuilder

Fluent builder for test messages.

```csharp
var message = new MessageBuilder()
    .WithCorrelationId(Guid.NewGuid())
    .WithPayload(new MyCommand())
    .Build();
```

---

## Best Practices

1. **Inherit from base classes** - Get automatic category traits and DI setup
2. **Use collections for containers** - Share expensive resources across tests
3. **Keep unit tests fast** - Target <30 seconds for all unit tests
4. **Isolate integration tests** - Use unique data per test to enable parallelism
5. **Use Shouldly for assertions** - Consistent, readable assertions
6. **Use FakeItEasy for mocking** - Simple, expressive mocks
7. **Check DockerAvailable** - Skip gracefully when Docker is unavailable
8. **Use TestTimeouts** - Consistent, configurable timeouts across all tests
9. **Use WaitHelpers** - Clean polling without busy-waiting

---

## Adding New Fixtures

1. Create fixture class extending `ContainerFixtureBase`
2. Implement `InitializeContainerAsync` and `DisposeContainerAsync`
3. Add collection definition to `ContainerCollections.cs`
4. Document in this README

Example:

```csharp
public sealed class MyContainerFixture : ContainerFixtureBase
{
    private MyContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not initialized");

    protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
    {
        _container = new MyContainerBuilder().Build();
        await _container.StartAsync(cancellationToken);
    }

    protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
```

---

## Project Structure

```
tests/Shared/Tests.Shared/
├── Base Classes
│   ├── UnitTestBase.cs
│   ├── IntegrationTestBase.cs
│   ├── FunctionalTestBase.cs
│   ├── DatabaseIntegrationTestBase.cs
│   ├── CacheIntegrationTestBase.cs
│   └── MessageBrokerIntegrationTestBase.cs
│
├── Fixtures/
│   ├── ContainerFixtureBase.cs       (unified base class)
│   ├── ContainerCollections.cs       (xUnit collection definitions)
│   ├── ContainerConnectionStringProvider.cs
│   ├── IDatabaseContainerFixture.cs  (database interface)
│   ├── DatabaseEngine.cs
│   ├── SqlServerContainerFixture.cs
│   ├── PostgresContainerFixture.cs
│   ├── RedisContainerFixture.cs
│   ├── KafkaContainerFixture.cs
│   ├── RabbitMqContainerFixture.cs
│   ├── MongoDbContainerFixture.cs
│   └── ElasticsearchContainerFixture.cs
│
├── Infrastructure/
│   ├── TestTimeouts.cs               (CI-configurable timeouts)
│   └── WaitHelpers.cs                (async polling utilities, 328 lines)
│
├── TestTypes/                        (consolidated from Extra/)
│   ├── CloudMessage.cs               (cloud messaging abstractions)
│   ├── MessagingTypes.cs             (RabbitMQ-style types, 318 lines)
│   ├── JsonMessageSerializer.cs      (IJsonSerializer + IMessageSerializer)
│   ├── TestEventData.cs              (serverless test data)
│   ├── TestMessages.cs               (message test types)
│   ├── ITestCacheInvalidator.cs      (cache invalidation stubs)
│   ├── TestEvent.cs                  (domain event types)
│   ├── TestDispatchAction.cs         (dispatch action types)
│   ├── CqrsTypes.cs                  (CQRS test types)
│   ├── PerformanceTypes.cs           (perf test types)
│   ├── TrafficTypes.cs               (load test types)
│   └── InboxMessage.cs               (inbox test types)
│
├── TestDoubles/
│   └── TestMessageContext.cs
│
├── Categories/
│   └── TestCategories.cs             (test category constants)
│
├── Helpers/
│   ├── TestLogger.cs
│   ├── TestOutputSink.cs
│   └── ServiceCollectionExtensions.cs
│
├── Builders/
│   └── MessageBuilder.cs
│
├── Conformance/                      (conformance test bases)
│   ├── DbConformanceTestBase.cs
│   ├── PersistenceProviderAssertions.cs
│   ├── PersistenceProviderConformanceTestBase.cs
│   ├── RetryPolicyConformanceTestBase.cs
│   ├── EventStore/EventStoreConformanceTestBase.cs
│   ├── Inbox/InboxStoreConformanceTestBase.cs
│   └── Outbox/OutboxStoreConformanceTestBase.cs
│
├── Stubs/                            (provider stubs)
│   ├── Alba/AlbaStubs.cs
│   ├── CloudStubs/AwsStubs.cs, AzureStubs.cs, GoogleCloudStubs.cs
│   ├── CachingStubs/CachingStubs.cs
│   ├── EventSourcingStubs/EventSourcingStubs.cs
│   ├── ObservabilityStubs/ObservabilityStubs.cs
│   ├── PoolingStubs/PoolingStubs.cs
│   ├── ResilienceStubs/ResilienceStubs.cs
│   └── RoutingStubs/RoutingStubs.cs
│
├── CQRS/
│   └── TestCommandBase.cs
│
└── GlobalUsings.cs
```

---

## Migration Notes

### Sprint 280: Major Test Infrastructure Cleanup (January 2026)

Sprint 280 performed a major cleanup of the test infrastructure:

1. **Removed 400+ obsolete test files** from `Excalibur.Tests.Functional`
2. **Removed 100+ obsolete test files** from `Excalibur.Tests.Integration`
3. **Reorganized integration tests** into proper project structure:
   - `Excalibur.Data.ElasticSearch.Tests.Integration`
   - `Excalibur.Data.Tests.Integration`
4. **Added shared test infrastructure** (`Alba/`, `CQRS/`) to Tests.Shared
5. **Build clean**: 0 errors, 198 tests passing

### Sprint 46-48: Test Infrastructure Consolidation

The test infrastructure was consolidated in Sprints 46-48:

1. **Unified ContainerFixtureBase** - Three duplicate implementations merged into one in `Tests.Shared/Fixtures/`
2. **Standardized Fixtures** - All container fixtures now extend `ContainerFixtureBase`
3. **TestTimeouts** - Centralized with `TEST_TIMEOUT_MULTIPLIER` environment variable support
4. **WaitHelpers** - Consolidated async polling utilities
5. **Removed Framework-Specific Variants** - No more DispatchTestBase, ExcaliburTestBase, etc.
6. **Deleted Tests.Shared.Extra** - All content consolidated into Tests.Shared

### Migration Guide

If you have tests using old base classes:

```csharp
// Old (removed)
public class MyTest : DispatchTestBase { }
public class MyTest : ExcaliburTestBase { }
public class MyTest : PostgresHostTestBase { }

// New
public class MyTest : UnitTestBase { }
public class MyTest : IntegrationTestBase { }
public class MyTest : HostTestBase<PostgresContainerFixture> { }
```

See ADR-071 for full consolidation rationale and patterns.

### Sprint 420-422: Test Infrastructure Consolidation Initiative

The Test Infrastructure Consolidation initiative (Sprints 420-422) cleaned up and unified all test infrastructure:

#### Sprint 420: Foundation (Epic 1 + Epic 2)
- **Unified ContainerFixtureBase** - Three duplicate implementations merged into one
- **Created TestTimeouts** - CI-configurable timeout constants with `TEST_TIMEOUT_MULTIPLIER` support

#### Sprint 421: Base Classes (Epic 3 + Epic 4)
- **WaitHelpers** - Created 328-line async polling utility class
- **IntegrationTestBase/FunctionalTestBase** - Enhanced with timeout support
- **Removed 8 framework-specific test bases** (DispatchTestBase, ExcaliburTestBase, etc.)

#### Sprint 422: Cleanup (Epic 5 + Epic 6)
- **Deleted Tests.Shared.Extra folder** - All 11 files migrated or removed
- **Consolidated test types** - All reusable types now in `TestTypes/`
- **Updated documentation** - This README fully reflects current state

#### Migration from Tests.Shared.Extra

If you have tests referencing `Tests.Shared.Extra`:

```csharp
// Old (deleted)
using Tests.Shared.Extra;
using Tests.Shared.Extra.Handlers;
using Tests.Shared.Extra.Serialization;

// New
using Tests.Shared.TestTypes;
```

**Migrated Types:**

| Old Location | New Location |
|--------------|--------------|
| `Extra/CloudMessage.cs` | `TestTypes/CloudMessage.cs` |
| `Extra/Handlers/BasicProperties.cs` | `TestTypes/MessagingTypes.cs` |
| `Extra/Handlers/ConnectionFactory.cs` | `TestTypes/MessagingTypes.cs` |
| `Extra/Common/IBasicProperties.cs` | `TestTypes/MessagingTypes.cs` |
| `Extra/Events/TestEventData.cs` | `TestTypes/TestEventData.cs` |
| `Extra/Stubs/ICacheInvalidator.cs` | `TestTypes/ITestCacheInvalidator.cs` |
| `Extra/ChannelMessagePumpOptions.cs` | `TestTypes/MessagingTypes.cs` |
| `Extra/SessionOptions.cs` | `TestTypes/MessagingTypes.cs` |

**Deleted (duplicates):**
- `Extra/Serialization/JsonMessageSerializer.cs` - Use `TestTypes/JsonMessageSerializer.cs`
- `Extra/TestStubs/CloudMessage.cs` - Use `TestTypes/CloudMessage.cs`

---

## TestTypes Reference

The `TestTypes/` folder contains reusable test types for integration and functional tests:

| File | Contents |
|------|----------|
| `CloudMessage.cs` | `CloudMessage`, `ICloudMessageAdapter` - cloud messaging abstractions |
| `MessagingTypes.cs` | `IBasicProperties`, `BasicProperties`, `ConnectionFactory`, `IConnection`, `IModel`, mock implementations, options classes |
| `JsonMessageSerializer.cs` | `JsonMessageSerializer` implementing `IJsonSerializer` and `IMessageSerializer` |
| `TestEventData.cs` | `TestEventData` for serverless tests |
| `TestMessages.cs` | `SimpleTestMessage`, `TestMessageWithPayload`, `VersionedTestMessage`, `ComplexTestMessage`, `GenericMessage<T>` |
| `ITestCacheInvalidator.cs` | `ITestCacheInvalidator`, `NoOpCacheInvalidator` |
| `TestEvent.cs` | Domain event test types |
| `TestDispatchAction.cs` | Dispatch action test types |
| `CqrsTypes.cs` | CQRS pattern test types |
| `PerformanceTypes.cs` | Performance testing types |
| `TrafficTypes.cs` | Traffic/load testing types |
| `InboxMessage.cs` | Inbox pattern test types |

---

*Last updated: January 2026 (Sprint 422 - Test Infrastructure Consolidation Complete)*
