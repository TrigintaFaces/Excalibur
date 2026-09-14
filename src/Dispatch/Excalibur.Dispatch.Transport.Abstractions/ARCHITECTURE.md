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

## Consumer obligations

- On MQTT and gRPC, call `UseReconnect(...)` when building the subscriber, or accept that a dropped
  subscription is permanent for the life of the process.
- On the five transports marked **no**, do not call it — their clients already recover, and a second
  retry loop multiplies the reconnect rate rather than adding safety.
- The decorator retries the *subscribe* call. It makes no claim about messages in flight when the
  connection dropped, so handlers must remain idempotent regardless of which column applies.
