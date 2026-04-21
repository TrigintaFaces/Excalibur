# ElasticSearch Inbox/Outbox Sample

Demonstrates using Elasticsearch as the backing store for the **Inbox** (idempotent message processing) and **Outbox** (reliable message publishing) patterns.

## Patterns

### Inbox Pattern (Idempotent Consumer)

The Inbox pattern ensures **at-least-once** delivery with **at-most-once** processing semantics per handler. When a message arrives, the inbox store records it before the handler executes. If the same message arrives again (a duplicate), the store detects it and skips processing.

Key operations:
- `CreateEntryAsync` -- Record an incoming message before handling
- `IsProcessedAsync` -- Check if a message was already processed
- `MarkProcessedAsync` -- Mark a message as successfully handled
- `TryMarkAsProcessedAsync` -- Atomic check-and-mark (preferred for concurrent scenarios)

### Outbox Pattern (Transactional Outbox)

The Outbox pattern ensures **reliable exactly-once** message publishing. Messages are staged in the outbox within the same transaction as business state changes, then a background processor publishes them to the message broker.

Key operations:
- `StageMessageAsync` -- Stage a message for later delivery
- `GetUnsentMessagesAsync` -- Retrieve pending messages for publishing
- `MarkSentAsync` -- Confirm successful delivery
- `GetStatisticsAsync` -- Monitor outbox health
- `CleanupSentMessagesAsync` -- Remove old sent messages

## When to Use Elasticsearch vs SQL Server

| Criteria | Elasticsearch | SQL Server |
|----------|--------------|------------|
| **Search requirements** | Full-text search on message payloads | Simple key-based lookup |
| **Scale** | Horizontal scaling, high throughput | Vertical scaling, ACID transactions |
| **Existing infrastructure** | Already using ES for logging/search | Already using SQL Server |
| **Transactional guarantees** | Eventual consistency | Strong consistency with DB transactions |
| **Retention & analytics** | Built-in ILM policies, time-based indices | Manual cleanup, archival strategies |

Choose Elasticsearch when you already have an ES cluster for logging or search and want to co-locate inbox/outbox data. Choose SQL Server when you need strong transactional guarantees with your business data in the same database.

## Prerequisites

Elasticsearch running on `http://localhost:9200`:

```bash
docker run -d --name es -p 9200:9200 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  elasticsearch:8.15.0
```

## Running

```bash
dotnet run
```

## Configuration

See `appsettings.json` for the Elasticsearch connection URL. The sample creates two indices:

- `sample-inbox` -- Stores inbox entries for idempotent processing
- `sample-outbox` -- Stores outbox messages for reliable publishing
