# MongoDB Data Provider Sample

Demonstrates all capabilities of the `Excalibur.Data.MongoDB` package.

## Capabilities Shown

| # | Capability | API |
|---|-----------|-----|
| 1 | DI Registration | `AddExcaliburMongoDb(Action<MongoDbProviderOptions>)` |
| 2 | CRUD Operations | `MongoDbPersistenceProvider.GetCollection<T>()` + MongoDB driver |
| 3 | Aggregation Pipeline | `MongoAggregationBuilder<T>` with `Match`, `Group`, `Sort`, `Limit` |
| 4 | Transaction Support | `IPersistenceProviderTransaction.CreateTransactionScope()` |
| 5 | Connection Pooling | `MongoDbPoolingOptions` (MaxPoolSize, MinPoolSize) |
| 6 | Health Check | `IPersistenceProviderHealth.TestConnectionAsync()` |

## Prerequisites

### MongoDB via Docker (single node)

```bash
docker run -d --name mongo -p 27017:27017 mongo:7
```

### MongoDB via Docker (replica set, required for transactions)

```bash
docker run -d --name mongo -p 27017:27017 mongo:7 --replSet rs0
docker exec mongo mongosh --eval "rs.initiate()"
```

## Running

```bash
dotnet run --project samples/14-data-providers/MongoDB/MongoDB.csproj
```

## Configuration

The sample reads from `appsettings.json`:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "ExcaliburSample"
  }
}
```

You can also configure options inline via `AddExcaliburMongoDb`:

```csharp
builder.Services.AddExcaliburMongoDb(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "ExcaliburSample";
    options.UseSsl = false;
    options.RetryCount = 3;
    options.Pooling = new MongoDbPoolingOptions
    {
        MaxPoolSize = 50,
        MinPoolSize = 5,
    };
});
```

Alternatively, bind from an `IConfiguration` section:

```csharp
builder.Services.AddExcaliburMongoDb(builder.Configuration.GetSection("MongoDB"));
```

## Aggregation Pipeline Example

The `MongoAggregationBuilder<T>` provides a fluent API for constructing MongoDB aggregation pipelines:

```csharp
var pipeline = new MongoAggregationBuilder<BsonDocument>(collection)
    .Match(new BsonDocument("stock", new BsonDocument("$gt", 0)))
    .Group(new BsonDocument
    {
        { "_id", "$category" },
        { "avgPrice", new BsonDocument("$avg", "$price") },
        { "totalStock", new BsonDocument("$sum", "$stock") },
    })
    .Sort(new BsonDocument("avgPrice", -1))
    .Limit(10)
    .Build();

var results = await pipeline.ExecuteAsync<BsonDocument>(cancellationToken);
```

Available stages: `Match`, `Group`, `Project`, `Sort`, `Limit`, `Skip`, `Unwind`, `AddRawStage`.

## Key Types

| Type | Purpose |
|------|---------|
| `MongoDbProviderOptions` | Connection string, database name, SSL, retry, read-only |
| `MongoDbPoolingOptions` | MaxPoolSize, MinPoolSize |
| `MongoDbPersistenceProvider` | Core provider implementing `IDocumentPersistenceProvider` |
| `MongoAggregationBuilder<T>` | Fluent aggregation pipeline builder |
| `IMongoAggregationPipeline<T>` | Executable aggregation pipeline |
| `MongoAggregationOptions` | AllowDiskUse, MaxTime, Collation, BatchSize |
| `ITransactionScope` | Transaction commit/rollback abstraction |
