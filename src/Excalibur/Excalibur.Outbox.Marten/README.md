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

## Enqueue payloads and transformations

`EnqueueAsync` serializes the runtime message type with the default `System.Text.Json`
payload options. This preserves its existing property names, numeric enum values and null
members; it does not switch to the framework's canonical event-payload format. The Marten
`IDocumentStore` still owns serialization of the surrounding persisted document.

Each enqueue creates a fresh message identity rather than using `context.MessageId`.
The message type is namespace-qualified. `context.SetDestination(...)` supplies the routing
destination; when absent, the namespace-qualified type name is the fallback. Tenant,
correlation, causation and explicitly propagated headers are retained. Request-local context
items are not persisted as headers.

The owning store supports `IOutboxPayloadTransformStore` for decorators such as
`AddOutboxEncryption`. It prepares the envelope before invoking the callback once with its
serialized payload, prepared tenant and cancellation token. Input memory is borrowed read-only
for that invocation. The provider copies the callback's returned bytes before handing the
document to Marten, so later changes to that returned array cannot change the staged payload.
Empty binary results remain valid. Callback faults, null results and cancellation detected
before persistence accept no document. Once persistence begins, existing cancellation and
commit uncertainty remain unchanged.

Register stores before adding encryption. The encryption decorator mediates the transform
capability rather than exposing a raw writer. Staging continues to use its own lightweight
Marten session, conditional `Insert` and `SaveChangesAsync`; this facet does not add a
caller-owned transaction overload or change retention, claims or polling prerequisites.
The injected `IDocumentStore` remains owned by its registering consumer or container.

These payload capabilities do not establish NativeAOT support for the framework provider's
internal document mapping; the runtime requirements below still apply.

## Runtime requirements

The pinned Marten 9 dependency supports NativeAOT configurations; that upstream capability does not
validate this provider's internal outbox-document mapping. The provider borrows the consumer's
`IDocumentStore` and currently supplies no generated metadata for its internal document or arbitrary
header values. Runtime metadata and generic-instantiation requirements can therefore remain even when
message payloads are preencoded. Provider NativeAOT compatibility is not claimed.

Direct construction and the `UseMarten`, `AddMartenOutboxStore` and `UseMartenOutboxStore` registration
methods retain trimming and dynamic-code warnings for this unverified provider path. Those warnings
are construction/registration boundaries, not a claim that every `IOutboxStore` read call emits a
warning. Legacy enqueue separately retains its runtime-payload-serialization warnings.

See the [pinned Marten 9.13 NativeAOT guide](https://github.com/JasperFx/marten/blob/9.13.0/docs/configuration/aot-publishing.md)
for upstream prerequisites; the framework provider still requires its own publish-and-run evidence.

## Known limitation — the polling outbox does not start against this store

**As of this version, a host that registers this store and runs the polling outbox drain fails at
startup**, including the registration shown above. The startup validation reports it by name and explains
why: this store does not implement the framework's terminal dead-lettering capability, which the polling
drain requires in order to move a retry-exhausted message to a terminal state. Without it a message that
exhausted its attempts would stay failed, be re-claimed when its lease expired, and be re-delivered and
re-dead-lettered indefinitely — so the framework refuses to start rather than run in that state.

The failure is immediate and loud; nothing is lost and no data is written in a bad state. But it means
the drain configuration is unavailable here today, and the package description does not yet say so.

**If you need the polling outbox drain now**, use a store that provides terminal dead-lettering — SQL
Server, PostgreSQL, Oracle, MongoDB, Redis or Elasticsearch.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.

## Claim-scoped completion

The public `IClaimScopedOutboxStore` capability accepts the opaque `DispatcherId` returned by acquisition. Capture it before sending; use that exact identity for sent, failed and scheduled-backoff completion. Each acquisition mints a new identity even within one store instance. A stale or expired acquisition returns `ClaimLost`; terminal and absent rows have explicit outcomes. The strong completion locks the claim row, loads a fresh Marten document and rechecks the exact live lease in the guarded update. Claim transition and document mutation commit in one enlisted PostgreSQL transaction. Legacy token-free settlement remains weaker but its tombstone and document write also commit together.

Acquisition excludes terminal tombstones and reloads won documents in a fresh enlisted session before returning them. Stale, deleted or newly ineligible candidates are withheld and only this acquisition is removed. Cleanup takes claim locks in id order and deletes document plus tombstone atomically. Retention and retry floor configuration are unchanged. This provider does not advertise distributed leadership fencing, a dead-letter capability or verified NativeAOT support. Its internal document-mapping requirements remain subject to the runtime warning boundaries described above.
