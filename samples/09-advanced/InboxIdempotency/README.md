# InboxIdempotency Sample

Demonstrates how `UseInbox()`/`UseIdempotency()` middleware prevents duplicate message processing.

## What This Shows

- **UseIdempotency()** middleware in the dispatch pipeline
- Duplicate message detection via `IInboxStore`
- `UseInbox()` and `UseIdempotency()` are aliases for the same middleware
- In-memory inbox store for development/testing

## Pipeline

```
Incoming Message (with MessageId)
     |
     v
UseIdempotency()  --> checks IInboxStore for MessageId
     |
     v (first time)  or  short-circuit (duplicate)
  Handler
```

## Running

```bash
dotnet run
```

## Production Usage

Replace the in-memory store with a durable provider:

```csharp
services.AddExcaliburInbox(inbox => inbox.UseSqlServer(opts => opts.ConnectionString = connectionString));
// or
services.AddExcaliburInbox(inbox => inbox.UsePostgres(pg => pg.ConnectionString = connectionString));
```

## See Also

- `samples/04-reliability/OutboxPattern` -- Outbox + Inbox together
- `samples/09-advanced/ProductionPipeline` -- Full middleware pipeline
- `docs-site/docs/patterns/inbox.md` -- Inbox pattern documentation
