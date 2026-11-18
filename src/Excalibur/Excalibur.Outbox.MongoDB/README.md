# Excalibur.Outbox.MongoDB

MongoDB persistence and builder integration for the Excalibur outbox pattern.

## Installation

```bash
dotnet add package Excalibur.Outbox.MongoDB
```

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;

public static class MongoOutboxRegistration
{
    public static void Configure(IServiceCollection services)
    {
        services.AddExcalibur(x => x.AddOutbox(outbox => outbox.UseMongoDB(options =>
        {
            options.ConnectionString("mongodb://localhost:27017")
                   .DatabaseName("myapp");
        })));
    }
}
```

This bridge package provides:
- `IOutboxBuilder.UseMongoDB()` and `MongoDbOutboxStore`, implemented by this package
- Transitive dependency on `Excalibur.Outbox` and `Excalibur.Data.MongoDB`

## Runtime requirements

The provider's default MongoDB BSON document mapping uses runtime member metadata and constructs
generic serializers at runtime. Trimming may remove required metadata, and required native generic
instantiations may be unavailable under NativeAOT. This provider path has not been validated for
NativeAOT; the package does not claim compatibility.

Direct construction and `UseMongoDB` registration retain corresponding analyzer warnings, including
registrations that borrow an existing client or resolve one through `ClientFactory`. These are
construction/registration warning boundaries, not a claim that every `IOutboxStore` read call emits
a warning. Preencoded payloads do not remove document-mapping requirements. This limitation does not
establish that every fixed-schema MongoDB configuration requires a JIT runtime.

## Legacy enqueue and payload transformation

`EnqueueAsync` keeps MongoDB's canonical JSON serialization (camel-case property names, omitted nulls,
and string enums), destination routing and propagated context values. It creates a fresh outbox row ID;
`context.MessageId` is not that ID or a MongoDB-specific header frame. Unused context IDs do not replace
or restrict the provider's fresh identity. Explicit preencoded `StageMessageAsync` identities retain
that API's separate admission behavior.

Discover the optional `IOutboxPayloadTransformStore` facet through its owning `IOutboxStore.GetService`.
Encryption uses it to transform the exact provider-serialized bytes before accepting an insert. The
complete envelope is prepared before awaiting the callback. Input memory is a stable read-only borrow
for that invocation; output is copied before schema admission, BSON mapping and insertion. Empty output
is supported as BSON binary. Callback faults, null results and pre-accept callback cancellation produce
no accepting insert or callback retry. Cancellation is checked before preparation and after the callback;
once persistence starts, existing MongoDB driver error and cancellation uncertainty remains.

The existing simple identity-collection admission, BSON date representation, indexes, sent TTL and
single-document insertion remain unchanged; this adds no caller session or replica-set transaction
requirement. Invariant decorators must mediate the capability; encryption hides the raw transform writer
to prevent bypassing encryption. Borrowed `IMongoClient` ownership remains with the caller.

This is not a configurable serializer-options route or a new worker/tenant/fencing capability. Legacy
serialization warnings and default driver document-mapping warnings remain. Preencoded staging or other
providers' NativeAOT controls do not certify MongoDB driver mapping for NativeAOT.

## Acquisition-scoped completion

Each atomic claim mints a new opaque acquisition identity and returns it as `DispatcherId`.
Use `IClaimScopedOutboxStore` for success/failure/backoff and
`IClaimScopedDeadLetterableOutboxStore` for dead-letter completion. Exact identity, a live server-clock
lease and terminal state are evaluated in the same `findAndModify` action as the mutation.
The command returns a computed preimage outcome; no receipt field is persisted and no later read
guesses whether an earlier mutation applied. Standalone MongoDB remains supported; no replica-set
transaction prerequisite is added. Sent TTL, stored payloads and headers, retry floors and staged
backoff semantics remain unchanged. Legacy process-only overloads remain weaker direct APIs.

The managed MongoDB driver mapping requirements remain explicit. This provider is not advertised
as NativeAOT compatible. Real-server command/lease controls are required to verify these semantics;
a mocked driver or source review cannot substitute for them.

## Dedicated binary identity collection

The outbox requires a dedicated ordinary collection with simple (binary) collation and its
simple unique `_id` index. Message IDs and opaque acquisition receipts are case-sensitive identity,
not language-aware text. A case-insensitive collection can collapse different message IDs at insertion
and authorize a different receipt during completion. An additional secondary index or overriding only
one query's collation does not repair that identity namespace.

On first use, both constructors inspect the effective collection namespace before provider row actions
or automatic index creation. An absent collection is explicitly created with simple collation; a
concurrent creator's resulting schema is inspected again. Existing linguistic, view, time-series,
capped or incompatible `_id` schemas fail with an actionable `InvalidOperationException`; existing
rows and indexes are not rewritten. Metadata inspection requires permission to list the target
collection and its indexes, in addition to normal outbox operations. Failures and cancellation do not
cache successful initialization.

This requirement applies only to the outbox collection, not other application collections or a global
database collation policy. Existing incompatible installations need a separately planned migration to
a fresh simple collection with producers/workers quiesced and message/tenant provenance preserved.
The provider performs no automatic migration or data deletion. Admission is cached after successful
initialization: do not drop or replace the namespace underneath active store instances. Schema changes
require quiescing them and creating new instances. This does not add NativeAOT support to the driver.

## Sequence metadata preservation

Explicit `SequenceNumber` values round-trip as BSON `Int64`. Older documents without this field
read as zero (unsequenced). The provider does not allocate sequence numbers or enforce delivery order.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
