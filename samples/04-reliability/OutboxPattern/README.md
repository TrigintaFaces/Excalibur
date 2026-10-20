# Outbox Pattern Sample

This sample demonstrates framework-owned, eventually consistent event delivery with in-memory stores.
It is for developers learning how producer handlers, outbox staging and the hosted processor fit together.

Events declare stable identities with the framework's `MessageName` attribute. Preserve those values
across code moves; register message types in a separate worker, and use `MessageNameAlias` if a declared
identity changes. This sample stages and consumes in one host.

## Run

Requires the repository's .NET SDK. No database, broker, Docker or cloud account is needed.

```bash
dotnet run --project samples/04-reliability/OutboxPattern -- --once
```

The process dispatches five `PlaceOrderCommand` actions, checks their typed order IDs, and waits up to
30 seconds for the actual outbox statistics to report five sent messages. It exits after verified delivery.
Omit `--once` to keep the host running; stop it with Ctrl+C.

## Framework flow

```text
PlaceOrderCommand
  -> PlaceOrderHandler validates input and writes OrderPlacedEvent through IOutboxWriter
  -> UseOutbox middleware stages the buffered event after successful handling
  -> OutboxBackgroundService resolves the framework IOutboxProcessor
  -> the processor claims an eligible row and dispatches the event
  -> OrderPlacedHandler runs; the store records Sent
```

The handler returns an order ID after staging. Event delivery happens asynchronously. Each producer
dispatch uses a fresh service scope and context. The sample leaves claim acquisition, retry timing,
attempt budgets and terminal transitions to the framework instead of implementing a publishing loop.

`OutboxSample.CreateBuilder` contains the executable composition: `UseOutbox`, `UseInbox`, registered
handlers, in-memory inbox/outbox providers and opt-in background processing. The sample has no business
database or shared business transaction; it does not demonstrate atomic business-data persistence.

## Processing settings

The builder requests batches of 50, a two-second polling interval, three retries after the initial
attempt, and a ten-second fixed retry delay. The in-memory provider's failure floor can defer a retry
longer than the requested delay. Eligibility is the later of the calculated retry time and that floor.

Builder defaults map to the processor's delivery and background options. Explicit `Configure` or
`PostConfigure` callbacks override them regardless of registration order. A registered
`IBackoffCalculator` takes precedence over the default fixed calculator; the
[Retry and Circuit Breaker sample](../RetryAndCircuitBreaker/) demonstrates the framework's Polly integration.

Delivery can repeat after ambiguous transport acceptance or a failure recording Sent. Consumers should
be idempotent. Inbox middleware is configured here, but this console run does not simulate broker
redelivery or certify exactly-once business effects.

## Verification and limits

Successful output includes five staged producer outcomes and `Completed 5 real outbox deliveries`.
The wait checks store statistics rather than treating a fixed sleep as evidence of delivery.

Native regression controls exercise the actual sample composition and verify typed outcomes, real
staging, stored payloads, processor delivery and no replay of sent rows. Framework tests cover attempt
budgets, retained failures, backoff floors, caller schedules and consumer calculator overrides.

In-memory inbox/outbox contents disappear when the process stops. There is no infrastructure cost or
external cleanup; restarting creates fresh stores. This example does not prove recovery after restart,
remote broker behavior, or an atomic transaction spanning business data and the outbox.

## Durable deployment

Select a durable provider and provision its schema before starting the application. Use the maintained
provider scripts instead of copying a second schema into this sample:

- [SQL Server outbox schema](../../../src/Excalibur/Excalibur.Outbox.SqlServer/Scripts/001_CreateOutboxSchema.sql)
- [SQL Server single-tenant inbox schema](../../../src/Excalibur/Excalibur.Inbox.SqlServer/Scripts/001_CreateInboxSchema.sql)
- [SQL Server multi-tenant inbox schema](../../../src/Excalibur/Excalibur.Inbox.SqlServer/Scripts/001_CreateInboxSchema.MultiTenant.sql)

See the [outbox documentation](../../../docs-site/docs/patterns/outbox.md) for transaction integration,
storage, routing, retention and operational guidance. Select the inbox schema matching the registered
tenancy mode. Sent-row retention requires an explicit cleanup policy.

## Source and ownership

- `OutboxSample.cs`: framework registrations and processing defaults.
- `Program.cs`: producer dispatch and bounded delivery verification.
- `Handlers/OrderHandlers.cs`: producer and event consumers.
- `Messages/OrderEvents.cs`: typed action and event contracts.

Maintainer: [@TrigintaFaces](https://github.com/TrigintaFaces), as recorded in repository CODEOWNERS.

Related: [Saga Orchestration](../SagaOrchestration/) and
[Retry and Circuit Breaker](../RetryAndCircuitBreaker/).
