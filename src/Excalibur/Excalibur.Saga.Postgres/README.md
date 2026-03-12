# Excalibur.Saga.Postgres

PostgreSQL implementation of saga state persistence for the Excalibur framework.

## Features

- JSONB storage for saga state with atomic upserts
- Configurable schema and table names
- Connection factory support for advanced scenarios
- ISagaBuilder.UsePostgres() fluent API
- ValidateOnStart with DataAnnotations

## Usage

```csharp
// Simple registration
services.AddPostgresSagaStore("Host=localhost;Database=myapp;");

// Via ISagaBuilder
services.AddExcaliburSaga(saga =>
{
    saga.UsePostgres("Host=localhost;Database=myapp;");
});

// With options
services.AddPostgresSagaStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=myapp;";
    options.Schema = "dispatch";
    options.TableName = "sagas";
    options.CommandTimeoutSeconds = 30;
});
```
