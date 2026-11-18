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

### Kafka: one lock serves both the poll and the offset commit

**PARTLY MEASURED, and the split matters: the mechanism below is read from the code, the
eviction's behaviour at the end of it was induced against a real broker, and the window's
reachability at production scale is UNVERIFIED.** On Kafka, a single
operation gate serializes the consume loop and the settlement path. A settlement that advances the
commit position performs a synchronous broker round-trip while holding that gate, so the consume
loop cannot run until it returns, and a host that settles from threads other than the one polling
serializes one round-trip per advancing settlement ahead of the next poll. The number of
settlements in flight is the host's, not the framework's: the receiver warns above ten thousand
unsettled deliveries and does not cap them.

**The cost is not only throughput.** The client dispatches its partitions-assigned and
partitions-revoked callbacks only as a side effect of a consume, close or dispose call, and those
callbacks are the only thing that advances this library's local view of which assignment generation
owns a partition. So delaying the poll delays the only mechanism that can learn a partition has
changed hands, and a settlement whose generation check passes during that delay can publish a
position for a partition this process no longer owns. A broker accepts an offset commit on the
strength of the group generation and member id; it does not check that the member still holds the
partition.

**IT DOES NOT LOSE A MESSAGE, AND THE REASON IS ARITHMETIC RATHER THAN A FENCE.** The committable
position is the minimum over the offsets a tenure still has in a handler, the offsets awaiting
replay, and one past the highest it fetched. So every offset below a published position was, *in
the tenure that computed it*, either carried to a terminal state or never fetched at all — a
property that is local, and that does not decay when the commit arrives late or under a later
generation. A stale position therefore cannot be published over work nobody processed.

**What it does instead is rewind.** If the partition's current tenure has already committed past
the stale position, the broker accepts the older one and that tenure redelivers work it had
already settled: duplicates, bounded by the distance between the two positions, and inside the
at-least-once guarantee this document states. Handlers must be idempotent regardless, so a consumer
who honours that obligation is not harmed by this; the failure mode is wasted work, not a wrong
answer.

**So read the trade honestly, because it is the opposite of what it looks like.** The generation
check is a duplicate-suppression heuristic, not a fence — no lock can make a local check and a
remote decision one atomic action, since the two sit on different machines. The stall is paid on
the one thread whose punctuality determines group membership, to suppress a bounded number of
duplicate redeliveries; and when the stall is long enough to cost the member its partitions, the
revoke redelivers *everything* that tenure had not committed. The mitigation can cost more of the
harm than the harm it prevents.

**AND THE SECOND CONSEQUENCE IS THE ONE THAT IS NOT BOUNDED, so do not read the paragraphs above as
the whole cost.** Losing the member's partitions is not a single event with a single redelivery. A
non-graceful eviction publishes no position at all — the partitions are already gone, so the
library records the loss and commits nothing — and the tenure that picks them up resumes from the
last *completed* commit and is handed the whole in-flight tail again. If the host's settlement
concurrency does not fall after an eviction, and nothing in this library signals that it should,
the same conditions reproduce: the poll is starved again, the member is evicted again, and the tail
is redelivered again. The redelivery count for a message at the tail of that window is then bounded
by nothing. Net progress is degraded rather than zero — commits do complete during each stall — so
what a consumer observes is throughput collapsing toward the rate at which single commits complete,
visible as group lag rather than as an error.

This is the same destination as the no-progress cycle described at the commit-on-revoke path for a
tenure that settled nothing, reached from the other direction: that cycle needs handlers slower than
the rebalance cadence, and this one shortens the cadence instead.

**MEASURED against a real broker, 2026-11-09, and it cuts both ways.** This paragraph replaces an
UNVERIFIED label: which path such an eviction takes was the pivot the severity turned on, and it has
now been induced directly rather than reasoned about.

**What holds.** The eviction takes the lost-partitions path, which publishes nothing, so the next
tenure does resume from the last completed commit and the whole in-flight window is redelivered.
That half of the cycle above is confirmed.

**What does not hold, and it was the cycle's load-bearing step.** The eviction is NOT quiet. The
client throws out of the poll — a typed consume fault whose message names the cause and the overshoot
("application maximum poll interval exceeded by …") — and this library lets it propagate to the
caller of a receive, after logging it and after handing back the in-flight deliveries of the batch
that failed. So a host DOES get a signal, at the moment it matters, naming the thing to fix. The
unbounded case therefore requires a host that catches that fault and immediately retries at the same
settlement concurrency; it is not the default outcome of a host that merely has no information.

**What this means for you as a consumer, and the awkward part is WHERE the fault arrives.** It is
raised from the poll, so it reaches whoever called receive -- the party that was *blocked*. The
settlement concurrency that caused the block lives on your other threads, in the acknowledge and
reject calls, and those never see it. **A poll loop that catches a transport exception and retries
the poll is the ordinary resilience shape, and it swallows this fault by default while leaving the
in-flight width untouched.** That is not a careless host; it is the common one.

So the obligation is a wiring one rather than a vigilance one: propagate a consume fault naming the
poll interval to whatever governs your in-flight settlement window, and narrow that window before
polling again. Treating it as a transient and retrying at the same width is what turns one eviction
into a cycle, and it is the one response this library cannot make on your behalf.

**What a consumer can do now.** Settle on the same flow that polls — the subscriber entry point
does this by construction, handing one message to the handler at a time — or keep the number of
in-flight unsettled deliveries small, so that at most a short queue of commits can precede a poll.
A host that polls on one thread and settles from many is the composition this gap is about.

## Consumer obligations

- On MQTT and gRPC, call `UseReconnect(...)` when building the subscriber, or accept that a dropped
  subscription is permanent for the life of the process.
- On the five transports marked **no**, do not call it — their clients already recover, and a second
  retry loop multiplies the reconnect rate rather than adding safety.
- The decorator retries the *subscribe* call. It makes no claim about messages in flight when the
  connection dropped, so handlers must remain idempotent regardless of which column applies.
- On Kafka, prefer settling on the flow that polls, or bound your in-flight unsettled deliveries.
  A settlement performs a synchronous offset commit under the same lock the consume loop needs, so
  a large in-flight window converts settlement latency into poll delay — and poll delay is what
  defers this library's view of a rebalance. An idempotent handler covers the single rewind this
  produces; it does NOT cover the eviction loop, where the same tail is redelivered once per cycle
  and throughput collapses toward the single-commit rate. **A receive that throws a consume fault
  naming the poll interval is your backpressure signal** — measured, the client raises it and this
  library propagates it — but it arrives on the RECEIVE path, while the concurrency that caused it
  lives in your acknowledge and reject calls on other threads. Wire one to the other: narrow the
  in-flight settlement window rather than retrying the poll at the same width. See the Kafka entry
  under Known gaps.

## Internal supplied-channel message pump (B08 row114)

The selected contract for internal `Excalibur.Dispatch.Transport.MessagePump` is one ingress:
its concrete `Reader` and `Writer` expose the supplied `Channel<MessageEnvelope>` that the processing
loop consumes. The caller creates that channel and selects its capacity, full-mode and reader/writer
options. There is no additional ingress buffer or forwarding loop. Retained `Configure` callbacks do
not resize or reconfigure an existing supplied channel. The ineffective public
`ChannelMessagePumpOptions.FullMode` property is withdrawn: it no longer has a shipped reader after
removing the orphan ingress, and setting it cannot configure the supplied channel. Select full-mode
behavior through the channel's own `BoundedChannelOptions` when creating that channel. No ignored
compatibility setter or extra queue is introduced. This decision does not change the separate public
`Excalibur.Dispatch.Channels.MessagePump<T>` event-based contract.

Disposal completes the single supplied ingress; stopping alone leaves it open:

| Operation | Effect on supplied writer | Processing termination |
|---|---|---|
| `StopAsync` | Leaves it open | Cancels, then awaits the processing task |
| `DisposeAsync` | Completes it after `StopAsync` returns | Cancels, then awaits the processing task |
| `Dispose` | Completes it | Cancels without awaiting the processing task |

Awaited cancellation is not graceful draining. Publishing after stopping has no processing guarantee,
even though its writer remains open. Successful disposal refuses subsequent writes through either alias
of the supplied writer. The caller selects capacity/backpressure and grants the pump completion authority
when disposing it; the channel is not a shared ingress that another producer can keep using afterward.
This decision adds no drain, restart or concurrent lifecycle guarantee.

The existing test-first control preloads and seals both writers before starting; it observes the exact
callback before stopping, so a missing callback is not attributed to shutdown cancellation. The original
exposed-writer failure is retained. The earlier 29-case local pass did not include the separate legacy
`DisposeAsync_StopsPump_AndCompletesInternalWriter` test. The subsequent canonical Transport run exposed
that completion regression. The corrected contract retains that legacy test unchanged and updates only
the two newly authored disposal rows. Both existing fixture owners now pass all 40 pump cases with zero
skips, including unchanged legacy ID `1a62c1ec-4ae6-b8fe-f5cc-476e88630ce4`. The independent actual audit is
C4D2159B74EE4570EDB035E768E51263153FCFDF8DFA11581ACDFEB323431438
(`b08-pump-disposal-correction-independent-actual-audit-r1`). Full canonical Transport (expected 5200)
remains pending. Neither local evidence
nor this bounded decision certifies all B08 dispositions, the release matrix or NativeAOT. See the dated row114 addendum in
[ADR-240](../../../management/architecture/adr-240-sprint-676-disposal-data-safety-resource-leaks.md).

## Selected SQS send-attempt telemetry boundary

An SDK operation log is not a physical HTTP request count. In the pinned AWS SDK Core 4.0.3.30 implementation, `client.call.attempts` records retry decisions, excludes the initial attempt on the examined path, and can increment before another HTTP request is sent. Do not add it to logical attempts and label the result a universal wire total. `client.call.attempt_duration` observes the SDK HTTP-handler invocation boundary; it includes the first invocation and records entered error/cancellation paths, but failures before an actual send can also produce a duration observation.

For an explicit opt-in, configure the public AWS SDK `AmazonSQSConfig.TelemetryProvider` before constructing the client, using a provider implementing the SDK's public telemetry interfaces. The named `AddAwsSqsTransport` registration accepts a prior `AddKeyedSingleton<IAmazonSQS>` factory under the same transport name: its `TryAddKeyedSingleton` preserves that client and the bus resolves its logger through normal logging DI. Configure the observer on the actual selected send client; configuring an unrelated unkeyed or receive-only client is insufficient. For owned persisted sends, that client must retain `MaxErrorRetry = 0` and `MaxStaleConnectionRetries = 0`. Enable the SQS debug operation log and an Activity listener for the relevant operation: owned outbox work supplies its existing message activity, while an unowned call needs an application-owned ambient Activity for this opt-in attribution profile. Default unowned attribution is not established. See the [AWS SDK telemetry-provider documentation](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/observability-telemetry-providers.html).

Correlate actual callback trace/span context with the actual operation log's trace/span and message identity, and retain a count for each operation rather than only an aggregate. Message identities belong in correlated logs/traces, not metric dimensions. The selected managed controls compare an independent counting HTTP handler with SDK duration observations: owned two-acquisition terminal failure gives two operations and two requests; owned healthy and entered-cancellation paths each give one; an unowned HTTP 500 retry/exhaustion with one SDK retry gives one operation and two requests. These tests use a test-only provider and internal bus construction. The public registration route above is source-verified, not covered by those runtime controls. No turnkey exporter, default unowned correlation, all-failure physical counter, cloud delivery, or NativeAOT guarantee follows from this evidence.

## Retained RabbitMQ options validation

The RabbitMQ live connection input and retained message-consumption options have different validation owners. Removing the dead connection-copy validator does not exempt retained `RabbitMqOptions`. Both named and default registrations retain startup evaluation and explicitly require `Consumption.MaxPayloadBytes` to be null (intentional unbounded input) or positive; zero/negative values are rejected by the options pipeline. Root-only data-annotation validation would not traverse this nested limit. This does not construct a connection factory, open a broker connection, change URI/credential policy, or introduce a global options validator. The named/default options controls and existing Boundary coverage guard are the bounded verification; execution is pending at source proposal time.
