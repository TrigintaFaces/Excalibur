# Excalibur.EventSourcing.AzureBlob

Azure Blob Storage cold event store for Excalibur event sourcing tiered storage.

## Usage

```csharp
services.AddExcalibur(x => x.AddEventSourcing(es =>
{
    es.UseTieredStorage(options =>
    {
        options.MaxAge = TimeSpan.FromDays(90);
    });
    es.UseAzureBlobColdEventStore(blob => blob
        .ConnectionString("UseDevelopmentStorage=true")
        .ContainerName("cold-events"));
}));
```

Implements `IColdEventStore` for archiving old events to Azure Blob Storage with gzip+JSON serialization.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
