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
