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
