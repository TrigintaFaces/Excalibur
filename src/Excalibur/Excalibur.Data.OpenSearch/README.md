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
