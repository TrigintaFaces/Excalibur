# Excalibur.Data.OpenSearch

OpenSearch data provider for Excalibur event sourcing projections.

## Usage

```csharp
services.AddOpenSearchProjectionStore<OrderSummary>(options =>
{
    options.ConnectionUri = new Uri("https://localhost:9200");
    options.IndexPrefix = "projections";
});
```

Each projection type gets a dedicated OpenSearch index (`{prefix}-{typename}`).

## Index state management

Opt in with `AddOpenSearchIndexManagement()` after registering a client. It makes
`IIndexLifecycleManager` (ISM policies), `IIndexTemplateManager`, `IIndexOperationsManager` and
`IIndexAliasManager` resolvable from the container.

```csharp
services.AddOpenSearchServices("https://localhost:9200");
services.AddOpenSearchIndexManagement();
```

