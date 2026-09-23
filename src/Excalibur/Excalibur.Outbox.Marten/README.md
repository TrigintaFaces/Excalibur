# Excalibur.Outbox.Marten

Marten (PostgreSQL document store) implementation of the transactional outbox pattern for reliable message delivery.

The store composes Marten's `IDocumentSession` unit-of-work: staging uses `Insert` (a real conditional write that rejects a duplicate message id) rather than an upsert, so message staging is exactly-once. Reads, mark-sent/failed, cleanup, and statistics run through the same session seam.

## Usage

Register Marten and the outbox provider:

```csharp
services.AddMarten(options =>
{
    options.Connection(connectionString);
});

services.AddExcalibur(x => x.AddOutbox(outbox =>
{
    outbox.UseMarten();
}));
```

The consumer owns the Marten `IDocumentStore` (connection, schema, serialization); the outbox resolves it from the container.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
