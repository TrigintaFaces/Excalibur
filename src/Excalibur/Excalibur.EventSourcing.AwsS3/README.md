# Excalibur.EventSourcing.AwsS3

AWS S3 cold event store for Excalibur event sourcing tiered storage.

## Usage

```csharp
services.AddExcaliburEventSourcing(es =>
{
    es.UseTieredStorage(options =>
    {
        options.MaxAge = TimeSpan.FromDays(90);
    });
    es.UseAwsS3ColdStore(options =>
    {
        options.BucketName = "my-cold-events";
        options.Region = "us-east-1";
    });
});
```

Implements `IColdEventStore` for archiving old events to AWS S3 with gzip+JSON serialization.
