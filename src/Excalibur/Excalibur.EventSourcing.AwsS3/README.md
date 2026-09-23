# Excalibur.EventSourcing.AwsS3

AWS S3 cold event store for Excalibur event sourcing tiered storage.

## Usage

```csharp
services.AddExcalibur(x => x.AddEventSourcing(es =>
{
    es.UseTieredStorage(options =>
    {
        options.MaxAge = TimeSpan.FromDays(90);
    });
    es.UseAwsS3ColdEventStore(s3 => s3
        .BucketName("my-cold-events")
        .Region("us-east-1"));
}));
```

Implements `IColdEventStore` for archiving old events to AWS S3 with gzip+JSON serialization.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
