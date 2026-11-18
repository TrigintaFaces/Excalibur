# Excalibur.Outbox.Redis

Redis implementation of the outbox pattern for Excalibur.

## Legacy enqueue and payload ownership

`EnqueueAsync` retains Redis's canonical JSON serialization (camel-case property names, omitted nulls,
and string enums), routing destination and propagated context values. It creates a fresh outbox row ID;
`context.MessageId` is not the row ID or a required Redis framing field. Explicit preencoded
`StageMessageAsync` IDs must be nonblank so the row can later be completed.

The optional `IOutboxPayloadTransformStore` facet is discovered through the owning `IOutboxStore`'s
`GetService`. Encryption uses it to transform the provider's exact serialized payload before Redis
accepts the write. The complete envelope is prepared before the callback awaits; borrowed input remains
stable during the callback, and callback-owned output is copied before constructing Redis persistence
arguments. Empty output is supported. A null callback/result, callback fault or callback cancellation
accepts no row; cancellation is checked before preparation and after the callback. Once persistence
starts, Redis retains its existing error/cancellation uncertainty; Lua execution is not a promise to
roll back every command after a runtime error.

This facet does not add a serializer-options route, worker admission, tenant authority or fencing.
The encryption decorator hides the raw transform writer so it cannot bypass encryption. The legacy
runtime-serialization warnings remain: this path is not certified for NativeAOT by preencoded staging
or typed-constructor Native controls. Existing Lua staging/dedup, scheduling, retry and retention remain
unchanged; staging adds no TTL and configured sent-message TTL still applies only on completion.

## Lease ownership

Each claim mints a new `encoded-process-prefix:acquisition-id` and returns it as `DispatcherId`.
The process prefix is UTF-8/base64 encoded before the separator, so configured IDs such as
`A` and `A:peer` cannot collide with the acquisition separator. This is an opaque lease identity,
not a secret or security token; consumers must not parse its storage representation.
Capture it before any handler runs. The claim Lua script returns the identity and message hash together,
so `RetryCount`, `LastError`, payload and other persisted fields describe that same acquisition. The
store does not fetch a later hash and attach the earlier identity to a successor's retry evidence.
Returned envelopes own their decoded values independently of later claims.

This is a snapshot at acquisition, not a promise the lease is still live when dispatch begins. Expiry
can permit redelivery; completion must still present the captured identity and handlers remain
idempotent. The entire reply is validated before any row is returned. After validation, cancellation
retains the existing prefix-selection behavior: an accumulated prefix, possibly empty, may be returned.
It does not roll back the Lua claim or release the rows omitted from the returned prefix; those rows
remain subject to normal lease recovery. Malformed replies fail without fetching current hashes to
repair them. Existing retry budgets, failure floors, final-attempt disposition and retention are unchanged.

Use `IClaimScopedOutboxStore` for success/failure/backoff and
`IClaimScopedDeadLetterableOutboxStore` for dead-letter completion. One Lua script checks existence,
terminal state, exact current identity and live server-clock lease before applying the transition.
Expired/released/replaced claims return `ClaimLost`; other owned rows remain usable. Refusals are
reported from the script's own outcome, never inferred by a later read. Sent TTL, failure floors,
the existing staged-backoff behavior, payloads and retention are preserved.

The older overloads carry no acquisition evidence. Their process-level guard remains weaker:
same-process acquisitions are indistinguishable. Configured process prefixes should remain unique
per independent processor. Framework workers use the strong capabilities. Redis does not advertise
distributed fencing, and these guards do not promise exactly-once external delivery.

## Sequence metadata preservation

Staging and reloading preserve the caller's exact signed 64-bit `SequenceNumber`, including values
above `2^53`. The Redis hash stores its invariant decimal spelling; it is not converted through a
Lua floating-point number. A legacy hash without this field reads as zero (unsequenced). This
preserves metadata only: it does not assign sequence numbers or certify partition delivery order
across concurrent dispatch, retries or multiple workers. Payload framing, Lua write boundaries and
sent-message retention remain unchanged.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
