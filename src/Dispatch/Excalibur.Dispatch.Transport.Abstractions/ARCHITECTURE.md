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

**The settlement contract is stated and it picks one of the two possible shapes.** `ITransportReceiver`
declares `AcknowledgeAsync` and `RejectAsync` returning plain `Task`, and its remarks say why:

> *Returning normally means the broker accepted the acknowledgement. An implementation that cannot
> complete the settlement **throws** rather than returning, because a caller cannot distinguish a
> settled message from an unsettled one by any other means.*

**Stated falsifiably: a settlement call that returns normally means the broker accepted it. A
settlement call that throws means it did not.** Every provider MUST keep that; a provider that
swallows a failed settle and returns normally reports a message as settled that the broker will
redeliver, and the consumer sees an unexplained duplicate.

### The gap this contract does not close

**`Task` has room for two outcomes and settlement has three.** An exception says *"not settled"*. It
cannot say *"I do not know whether this settled"* — and that third outcome is reachable on every
broker: a token cancelled between the handler succeeding and the ack being sent, a connection lost
mid-settle, a receipt that expired while the handler ran.

**Those two cases require different consumer behaviour**, which is why collapsing them matters:

| the receiver observed | the message will be | what the consumer must do |
|---|---|---|
| the broker REFUSED the settle | redelivered | may retry the settle; the broker is reachable |
| the outcome is UNKNOWN | redelivered **or not** | must be idempotent; retrying the settle may double-settle |

Today both arrive as an exception and the consumer cannot tell them apart. **Consumers must therefore
treat every settlement exception as the UNKNOWN case — the conservative reading — and be idempotent
regardless.** That is a consumer obligation created by the contract's shape, and it is stated here
rather than left to be discovered.

### Cancellation during ack is the adversarial case

**A token cancelled between "handler succeeded" and "ack sent" is the single most common way an
at-least-once system silently becomes at-most-once.** Any conformance arm for settlement MUST include
it. A provider that treats that cancellation as a successful settle has converted a delivery
guarantee into its opposite, silently.

### A provider that cannot support an outcome must say so at registration

**Not emulate it.** An emulated settlement is a guarantee this framework advertises and the broker
does not keep — and it fails in the direction the consumer cannot see.

### UNVERIFIED — the per-provider table is NOT in this document

**Whether each transport actually keeps the contract above is UNMEASURED.** No per-provider
settlement table exists here and none should be inferred from this section. One provider is known to
carry a two-valued settle internally (`GrpcTransportSubscriber.SettleAsync` returns `bool`), which
means a conversion to the throw-or-return contract happens at its boundary and has not been reviewed.

**There is no conformance arm binding any of the above.** Until one exists, this section states the
intended contract and the measured interface shape — not observed provider behaviour.

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
