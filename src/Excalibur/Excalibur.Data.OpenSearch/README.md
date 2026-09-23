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


## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
