# Excalibur.Testing.Containers

Opt-in [TestContainers](https://dotnet.testcontainers.org/) fixtures for testing Excalibur
infrastructure implementations against **real** backends via Docker.

This package is deliberately **separate** from `Excalibur.Testing.Conformance` so that the core
conformance toolkit never forces a Docker/TestContainers dependency. Reference this package only when
you want to run conformance kits (or your own integration tests) against a real database.

## Installation

```bash
dotnet add package Excalibur.Testing.Containers
```

## What's included

| Type | Purpose |
|------|---------|
| `ContainerFixtureBase` | Base class with Docker lifecycle, timeout handling, transient-failure retry, and optional graceful degradation. |
| `IDatabaseContainerFixture` | Contract for database container fixtures (connection string, engine, `CreateDbConnection`). |
| `SqlServerContainerFixture` | Ready SQL Server fixture (`Testcontainers.MsSql`). |
| `PostgresContainerFixture` | Ready PostgreSQL fixture (`Testcontainers.PostgreSql`). |

Container timeouts scale with the `TEST_TIMEOUT_MULTIPLIER` environment variable via `ContainerTimeouts`.

## Quick start — run a conformance kit against a real database

```csharp
public sealed class MySqlServerOutboxConformanceTests
    : OutboxStoreConformanceTestKit, IClassFixture<SqlServerContainerFixture>
{
    private readonly SqlServerContainerFixture _fixture;

    public MySqlServerOutboxConformanceTests(SqlServerContainerFixture fixture) => _fixture = fixture;

    protected override IOutboxStore CreateStore() =>
        new MySqlServerOutboxStore(_fixture.ConnectionString);

    [Fact] public Task Append() => AppendAsync_Persists();
}
```

## License

This package is part of the Excalibur framework. See [LICENSE](..\..\..\LICENSE) for license details.
