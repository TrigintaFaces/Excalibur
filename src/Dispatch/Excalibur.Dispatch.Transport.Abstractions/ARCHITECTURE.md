# Transport subscriber reconnection

## Guarantee

**There is no framework-level reconnection guarantee for a transport subscriber unless you opt in.**

`ReconnectingTransportSubscriber` is a decorator, applied only by calling `UseReconnect(...)` on a
`TransportSubscriberBuilder`. Nothing applies it for you. A subscriber built without that call has
exactly the recovery behaviour of the client SDK underneath it, and for several transports that is
none.

Stated falsifiably: **if the decorator is applied, a subscription whose underlying subscribe call
fails is retried on the caller-supplied backoff schedule until the supplied cancellation token is
signalled.** It does not deduplicate, does not replay messages delivered before the failure, and
does not bound total retry time — the schedule and the token are the only limits.

## Why this is opt-in rather than default

Most client SDKs already reconnect. Wrapping one that does layers two independent backoff loops
over the same broker: the SDK is retrying while the decorator is also retrying, so the effective
retry rate is the product rather than either schedule, and a broker restart is met with a
reconnect storm from every instance at once. Applying the decorator by default would introduce
that on the majority of transports to fix a minority.

So the decision is per transport, and it turns on a distinction coarser descriptions miss:
**re-establishing the connection is not the same as resuming the subscription.** A client that
restores its connection but not its consumer looks healthy — the connection is up, nothing throws —
while delivering no messages. That silent-but-idle state is worse than a clean failure, because
nothing surfaces for an operator to act on. The table below answers the two separately.

## Per-transport behaviour

Read from each client's own documentation or source at the version this repository references.
"Subscription resumed" is the load-bearing column: a *no* there means a subscriber can sit on a
healthy connection delivering nothing.

> **The last column is a recommendation to you, not a description of what this framework does.**
> No transport applies the decorator for you — not even the two that need it. If your transport's
> row says **yes**, you have no reconnection until you call `UseReconnect(...)` yourself.

| Transport | Client library | Connection restored | Subscription resumed | On by default | Apply `UseReconnect` |
|---|---|---|---|---|---|
| RabbitMQ | `RabbitMQ.Client` 7.2.1 | yes | yes | yes | **no** |
| Azure Service Bus | `Azure.Messaging.ServiceBus` 7.20.1 | yes | yes | yes | **no** |
| Google Pub/Sub | `Google.Cloud.PubSub.V1` 3.33.0 | yes | yes | yes | **no** |
| Kafka | `Confluent.Kafka` 2.14.0 | yes | yes | yes | **no** |
| Pulsar | `DotPulsar` 5.3.0 | yes | yes | yes | **no** |
| MQTT | `MQTTnet` 5.2.0.1603 | no | no | — | **yes** |
| gRPC | `Grpc.Net.Client` 2.76.0 | yes | **no** | — | **yes** |
| IBM MQ | `IBMMQDotnetClient` 10.0.0 | opt-in | unestablished | no | **undecided** |
| Amazon SQS | `AWSSDK.SQS` 4.0.2.25 | not applicable | not applicable | — | **no** |

**MQTT** — `MQTTnet` ships two clients with different recovery behaviour. The managed client
maintains its connection and re-subscribes; the plain client does neither. This package constructs
the plain one (`MqttConnectionProvider.cs:38`, `_factory.CreateMqttClient()`), so nothing recovers a
dropped subscription today.

**gRPC** — the channel transparently reconnects for *new* calls, but a server-streaming call that
has already yielded a message is not retried by the client's own retry policy, and the subscriber
here is exactly that shape (`GrpcTransportSubscriber.cs:84`, `AsyncServerStreamingCall`, consumed
through `ResponseStream.MoveNext`). This is the connection-restored-but-subscription-dead case.

**IBM MQ** — reconnection is off unless the connection is opened with the reconnect option, and
the documentation states that open object handles are restored. Whether a non-durable topic
subscription is restored on the same terms as a queue handle **could not be established from the
documentation**, and it is not inferred here in either direction: wrapping a client that already
restores the subscription re-introduces the double-backoff problem, and not wrapping one that does
not leaves a dead subscriber. The decision is therefore recorded as undecided rather than defaulted.

**Amazon SQS** — there is no persistent connection or server-side subscription to lose. Receiving
is a discrete long-poll request, and the SDK retries each request internally. The decorator has
nothing to recover, and adding it would place a second retry loop around one that already exists.

## Settlement semantics

`ITransportReceiver.AcknowledgeAsync` and `RejectAsync` return `Task`. Normal completion means the
provider completed its supported operation without an observed failure or refusal through its available
response. Confirmation depends on the provider: completion does not universally prove durable broker
acceptance or prevent redelivery. An observed failure, refusal, or unsupported disposition must throw.

An exception also does not prove that settlement failed at the broker. A request can be accepted before
its response is lost. `TransportSettlementException` carries two independent axes:

| Property | Meaning |
|---|---|
| `RedeliveryExpectation` | What the receiver knows about delivery of the message. `Unspecified` means redelivery remains possible; it does not prove the broker refused settlement. |
| `Retryability` | Whether repeating this settlement call could succeed. `Permanent` includes a consumed local handle; `Unspecified` gives no basis for an unbounded retry loop. |

Provider exceptions remain available as `InnerException`. Already classified portable exceptions retain
their identity. Callers must remain idempotent and distinguish retrying a settlement from processing a
later redelivery.

### Bounded source observations

These rows describe the inspected receiver paths, not a complete implementation census or a live-broker
certification. Deterministic tests of client seams establish local exception and ownership behavior;
they cannot establish broker durability, failover, or eventual redelivery.

| Receiver | Supported operation and observed failure classification |
|---|---|
| RabbitMQ | Calls native ack/nack with an independent timeout. Its receipt claim is consumed before I/O; native failures report `Unspecified` redelivery and `Permanent` retryability. Callback completion supplies no separate broker acceptance response. |
| Azure Service Bus | Retains the native receipt for every returned delivery and calls complete, abandon, or dead-letter with an independent timeout. The local claim is consumed; failures report `Unspecified` / `Permanent`. More than 10,000 outstanding receipts produces a warning, not a settlement cutoff or memory bound. Retained native messages keep their payload and metadata alive; consumers must control outstanding processing and settle deliveries promptly. |
| SQS | Calls delete or changes visibility to zero with an independent timeout. Native failures report `Unspecified` / `Unspecified`; missing or malformed receipt data is `Unspecified` / `Permanent`. The receipt remains on the message. |
| gRPC | Checks the settlement RPC response. Explicit server refusal reports `Expected` / `Unspecified`; native RPC failures report `Unspecified` / `Unspecified`. It uses an independent token and the configured RPC deadline. |
| Pulsar | Serializes settlement for the original delivery receipt and forwards the caller token; a retired receipt cannot settle a later delivery with the same native message ID. Native failures report `Unspecified` / `Unspecified`; invalid or already settled receipts are permanent. Exact requested caller cancellation remains cancellation. |
| Google Pub/Sub | Calls acknowledge or modifies the ack deadline to zero with an independent timeout. Native failures report `Unspecified` / `Unspecified`; missing or malformed ack IDs are permanent. |
| Kafka | Settles partition offsets through shared progress state and binds settlement to the original delivery receipt; retired receipts cannot settle a repeated offset. Native failures report `Unspecified` / `Unspecified`; stale or invalid delivery state is permanent. A shared operation gate serializes local progress and native operations, with bounded subscriber polling. Receiver and subscriber regressions pass; these controls do not certify remote ownership fencing. |
| MQTT | Completes the native acknowledgement callback, or withholds acknowledgement for requeue when the delivery's captured connection settings request a retained session. Clean sessions allow acknowledgment and rejection without requeue; unavailable requeue is refused before claiming the receipt (`Unspecified` / `Permanent`), leaving those alternatives usable. Callback failures report `Unspecified` / `Permanent` after the local claim is consumed. Exact requested caller cancellation remains cancellation and does not restore the claim. Captured settings do not prove broker retention; redelivery requires reconnecting to the retained session. |
| IBM MQ | Uses a unique delivery receipt, preserving the public MQMD message ID, to commit or back out the claimed unit of work; reject without requeue moves to a backout queue before commit. Native failures consume the local handle and are permanent. Commit or backout failure leaves redelivery unspecified; only an observed successful rollback before any commit attempt supports `Expected`. Cleanup attempts are independent of settlement classification. |

Cancellation behavior follows each provider's operation. A canceled caller token does not identify an
unrelated internal timeout, particularly where settlement uses an independent token. An implementation
must not report a failed or unsupported disposition as successful completion.

## Evidence

- `UseReconnectShould.UseReconnect_AddsReconnectingDecorator` — the builder extension actually wraps
  the inner subscriber, rather than returning it unchanged.
- `UseReconnectShould.UseReconnect_WiresBackoffScheduleIntoDecorator` — the caller-supplied schedule
  reaches the decorator and governs the retry pace, so the backoff is the caller's and not a
  hard-coded one.
- `ReconnectingTransportSubscriberShould` — the decorator's own behaviour: retry pacing, zero,
  negative and unbounded delays, and cancellation.

## Known gaps

**The reconnection guarantee is UNVERIFIED against a real broker.** Every test above drives a test
double. No test drops a live broker connection mid-subscription and asserts that delivery resumes,
so the guarantee stated at the top of this file is established by construction and by reading the
decorator, not by observation. Treat it accordingly.

**No transport applies the decorator by default, including the two that need it.** The table's
last column records what each transport *should* do; it does not describe current wiring. MQTT and
gRPC ship today with no reconnection at all unless the consumer calls `UseReconnect` themselves —
so for those two the table names a gap, not a feature.

**The IBM MQ row is undecided, not pending.** It stays undecided until its subscription-recovery
behaviour is established against a real queue manager rather than from documentation.

**The per-transport settlement table above is largely UNEVIDENCED, and the Evidence section does not
cover it.** A shared conformance kit supplies six settlement arms — refusal, native failure, repeated
settlement, cancellation, competing settlements, and batch retry — but each transport's suite chooses
which of them to call, and nothing required the full set. Measured 2026-10-19: of the eight transports
with a settlement suite, **one (RabbitMQ) exercises all six; Pulsar exercises one**. Every other row in
that table therefore states redelivery and retryability axes that no arm checks for that transport. The
rows are not known to be wrong — they are read from each provider's implementation rather than observed,
which is the same standing as the reconnection guarantee above. A structural guard now records the
uncovered arms per transport and fails if that set grows, so the gap can shrink but not silently widen.

## Consumer obligations

- On MQTT and gRPC, call `UseReconnect(...)` when building the subscriber, or accept that a dropped
  subscription is permanent for the life of the process.
- On the five transports marked **no**, do not call it — their clients already recover, and a second
  retry loop multiplies the reconnect rate rather than adding safety.
- The decorator retries the *subscribe* call. It makes no claim about messages in flight when the
  connection dropped, so handlers must remain idempotent regardless of which column applies.
