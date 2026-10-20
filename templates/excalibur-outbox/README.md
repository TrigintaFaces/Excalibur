# Framework Outbox API

This scaffold exposes `POST /orders` through the framework's JSON body endpoint helper. The producer
returns a typed order ID and buffers an `OrderPlacedEvent` with `IOutboxWriter`; framework middleware
stages the event and the opt-in hosted processor delivers eligible rows.

The event declares its durable identity with the framework's `MessageName` attribute. Keep that value
stable when moving or renaming the type; use `MessageNameAlias` when changing a declared identity so
registered workers can read rows under the previous name.

The endpoint uses framework result-to-HTTP conversion and the shared global exception handler.
Invalid product, quantity, price or amount overflow returns HTTP 400 Problem Details with field errors.
Production responses omit internal diagnostics; Development includes diagnostic exception text.

## Run locally

The default processor requires an explicitly configured `IDeadLetterQueue`. The SQL Server
variant registers the framework's SQL recovery queue using `ConnectionStrings:OutboxStore`;
apply the packaged outbox/dead-letter schema before starting it. Queue registration supports
inspection and retention; automatic replay additionally needs a replay handler.

The PostgreSQL and in-memory variants need an explicit recovery queue before their hosted
processor can start. Their outbox store alone does not provide one. Do not treat broker DLQ
managers as this queue interface or silently choose discard. The in-memory command below
therefore requires queue configuration first; it is not a complete recovery composition.

For a host needing no database or broker, generate with both in-memory choices:

```bash
dotnet new excalibur-outbox --name OrdersDemo --Transport inmemory --Database inmemory --IncludeTests true
dotnet run --project OrdersDemo -- --urls http://localhost:5080
```

Submit a JSON command:

```bash
curl http://localhost:5080/orders -H "Content-Type: application/json" -d '{"productId":"PRODUCT-1","quantity":2,"unitPrice":12.50}'
```

A successful request returns the buffered event's GUID. The in-memory transport includes
`OrderPlacedHandler`, which logs the eventual event receipt. It is excluded from broker variants so
they can route events to their configured transport. In-memory data disappears on process restart.

The scaffold explicitly selects the framework's System.Text.Json payload serializer. Producer handlers
use the public outbox writer rather than a custom publishing loop. The tests cover exact buffered
payloads, typed identifiers, validation before writes, and preservation of writer failures.

## Delivery configuration

The builder requests batch size 100 and a five-second poll interval. Retry defaults come from the outbox
builder. A retry count excludes the initial attempt; a registered `IBackoffCalculator`, including the
Polly adapter, overrides the processor's default fixed calculator. Backoff-capable providers preserve
the later of the requested schedule and their failure floor.

Configure the selected durable provider's connection, schema and retention policy before deployment.
Broker variants route `OrderPlacedEvent` through the named `outbox-events` transport using framework
transport and endpoint routing. Configure the selected broker before starting the host:

| Transport | Configuration | Destination |
|---|---|---|
| Kafka | `Kafka:BootstrapServers`, optional `Kafka:Topic` | Topic defaults to `orders` |
| RabbitMQ | Required `RabbitMQ:ConnectionString` with non-default credentials | Declared `orders` exchange and queue, bound by `orders.placed` |
| Azure Service Bus | Required `AzureServiceBus:ConnectionString`, optional `AzureServiceBus:EntityName` | Existing queue or topic defaults to `orders` |
| AWS SQS | Required `AWS:QueueUrl`, optional `AWS:Region` | Existing queue URL |
| Google Pub/Sub | Required `GooglePubSub:ProjectId`, optional `GooglePubSub:TopicId` | Existing topic defaults to `orders`; no subscription is required for this publisher |

Development permits plaintext Kafka and RabbitMQ connections explicitly. Other environments require
TLS; Kafka uses SSL and RabbitMQ uses the configured TLS connection. Provision broker credentials,
certificates and existing cloud destinations for your environment. Connectivity and remote consumption
require separate runtime verification; successful scaffolding alone does not establish either.

This example has no order database and no shared business-data transaction. HTTP success confirms
staging, while event delivery happens asynchronously. Delivery can repeat after ambiguous acceptance
or bookkeeping failure, so business consumers should be idempotent. See the framework outbox and
resilience documentation for transaction integration and Polly policies.
