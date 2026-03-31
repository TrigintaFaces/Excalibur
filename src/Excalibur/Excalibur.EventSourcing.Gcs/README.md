# Excalibur.EventSourcing.Gcs

Google Cloud Storage cold event store for Excalibur event sourcing tiered storage.

## Usage

```csharp
services.AddExcaliburEventSourcing(es =>
{
    es.UseTieredStorage(options =>
    {
        options.MaxAge = TimeSpan.FromDays(90);
    });
    es.UseGcsColdStore(options =>
    {
        options.BucketName = "my-cold-events";
        options.ProjectId = "my-gcp-project";
    });
});
```

Implements `IColdEventStore` for archiving old events to Google Cloud Storage with gzip+JSON serialization.
