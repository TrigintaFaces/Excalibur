# Test Suite

This directory contains all tests for the Excalibur solution.

## Test Organization

```
tests/
├── unit/                    # Fast, isolated unit tests
├── integration/             # TestContainer-based integration tests
├── functional/              # End-to-end workflow tests
├── conformance/             # Transport conformance tests
├── requirements/            # Requirement verification tests
├── ArchitectureTests/       # Architecture enforcement tests
└── Tests.Shared/            # Shared utilities and fixtures
```

## Running Tests

### By Category

```bash
# Unit tests only (target: <30s)
dotnet test --filter "Category=Unit"

# Integration tests only (target: <2min)
dotnet test --filter "Category=Integration"

# Functional tests only
dotnet test --filter "Category=Functional"

# Architecture tests only
dotnet test --filter "Category=Architecture"
```

### By Project Type

```bash
# All unit test projects
dotnet test tests/unit

# All integration tests
dotnet test tests/integration
```

## Test Categorization

Tests inherit categories from their base class:

- `UnitTestBase` → Category=Unit
- `IntegrationTestBase` → Category=Integration
- `FunctionalTestBase` → Category=Functional

Or use attribute-based categorization:

```csharp
using Tests.Shared.Categories;

[UnitTest]
public class MyTests
{
    // tests automatically tagged as Unit
}
```

## TestContainers Sharing

For optimal performance, share containers across tests using collections:

```csharp
using Tests.Shared.Fixtures;

[Collection(ContainerCollections.Postgres)]
public class MyDatabaseTests
{
    private readonly PostgresContainerFixture _fixture;

    public MyDatabaseTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test()
    {
        var connectionString = _fixture.ConnectionString;
        // Use shared container
    }
}
```

Available collections: Postgres, SqlServer, Redis, MongoDB, Kafka, RabbitMQ, Elasticsearch

## Performance Targets

- **Unit tests**: Complete in <30 seconds total
- **Integration tests**: Complete in <2 minutes total
- **Functional tests**: Complete in <5 minutes total

## Parallel Execution

Parallel execution is enabled by default via `xunit.runner.json` and `test.runsettings`.

To disable for specific test classes:

```csharp
[Collection("Sequential")]
public class MySequentialTests { }
```

## Test Frameworks

- **xUnit** - Test runner
- **Shouldly** - Assertions
- **FakeItEasy** - Mocking
- **TestContainers** - Integration test infrastructure
