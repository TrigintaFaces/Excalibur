# Excalibur.Outbox.ElasticSearch

Elasticsearch implementation of the outbox pattern for Excalibur.

## Source serialization and NativeAOT

Framework-owned `NodeUri`, `NodeUris`, and `CloudId` clients include generated metadata for
the complete persisted outbox document. The SDK still owns request/response serialization,
converters, camel-case names, null omission, and numeric-string reads. Payload bytes,
claim/CAS queries, lease timing and retention are unchanged.

**Migration:** framework-owned clients no longer implicitly reflect arbitrary header types.
Primitive values, JSON values and framework document fields have generated metadata. Register
application metadata for custom POCO header values:

```csharp
using System;
using Excalibur.Outbox;
using Microsoft.Extensions.DependencyInjection;

public static class FrameworkOwnedOutboxRegistration
{
    public static void Configure(IOutboxBuilder outbox) =>
        outbox.UseElasticSearch(es => es.NodeUri(new Uri("http://localhost:9200"))
            .SourceSerializer(ApplicationJsonContext.Default));
}
```

For example, define the generated metadata used above:

```csharp
using System.Text.Json.Serialization;

internal sealed class AuditHeader
{
    [JsonPropertyName("actor")]
    public string Actor { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AuditHeader))]
internal sealed partial class ApplicationJsonContext : JsonSerializerContext;
```

A staged header may then contain `new AuditHeader { Actor = "worker-1" }`.
Register each custom runtime header type. The reader retains it as a structured
`JsonElement`, rather than reconstructing an `AuditHeader`.

The [public NativeAOT regression harness](../../../tests/harnesses/ElasticsearchOutboxAotProbe/README.md)
checks prepared-byte staging/draining, routing and nonzero sequence preservation,
generated custom headers and missing-metadata refusal against an isolated server.
Each RID requires its own strict publish and actual native execution; these checks
do not certify legacy POCO enqueue, polling-host startup or all provider features.

Include every custom runtime header type with `JsonSerializable`. Missing metadata fails
serialization; values are not dropped, stringified or silently sent through reflection.
Header reads retain their existing `JsonElement` representation. Managed applications that
need the previous arbitrary-object behavior can explicitly call
`.SourceSerializer(new DefaultJsonTypeInfoResolver())`; constructing that resolver carries
the platform trimming/dynamic-code warnings and is not the NativeAOT configuration.

For a borrowed client, configure the source serializer when creating the client:

```csharp
using System;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Excalibur.Outbox;
using Excalibur.Outbox.ElasticSearch;
using Microsoft.Extensions.DependencyInjection;

public static class BorrowedOutboxClientRegistration
{
    public static ElasticsearchClient CreateAndRegister(IOutboxBuilder outbox, Uri nodeUri)
    {
        var settings = new ElasticsearchClientSettings(new SingleNodePool(nodeUri),
            sourceSerializer: (_, settings) => ElasticsearchOutboxSerializer.Create(
                settings, ApplicationJsonContext.Default));
        var client = new ElasticsearchClient(settings);
        outbox.UseElasticSearch(es => es.Client(client));
        // Return the actual instance: its lifetime remains with the application, not the outbox.
        return client;
    }
}
```

`Client`, `ClientFactory`, and pre-registered clients retain their exact instance and custom
serializer. Builder `SourceSerializer` configures only framework-owned clients; it cannot
retrofit a client that has already been constructed. Shared clients must also supply metadata
for their other source document types. Authenticated Elastic Cloud connections should use a
preconfigured client/factory with SDK credentials; `CloudId(string)` continues to supply no
credentials and now parses the Cloud ID using the SDK cloud pool rather than treating it as
a URI. These configuration paths do not by themselves certify native execution or real-server
CAS behavior; those require the corresponding verification.

## Acquisition-scoped completion

Each successful lease CAS returns its locally minted opaque acquisition identity as `DispatcherId`.
Capture it before delivery. `IClaimScopedOutboxStore` guards success/failure/backoff;
`IClaimScopedDeadLetterableOutboxStore` guards dead-letter completion. A current GET can refuse a
missing, terminal or different claim at that read. Any actual mutation presents that version's
sequence number and primary term plus the captured claim to a script that checks exact live ownership
on the node. A version conflict retries without laundering the newer owner into the caller's identity.
Unknown responses or exhausted conflicts are not success. There is no post-write lookup to guess
whether the guarded transition applied.

Explicit `SequenceNumber` values round-trip as `Int64`. Older documents without this field read as
zero (unsequenced). The provider does not allocate sequence numbers or enforce delivery order.

Legacy process-only methods remain weaker. Process prefixes are encoded without the claim separator
so distinct configured IDs remain distinct; the opaque storage spelling is not a public parsing
contract. Retention, payload/header metadata and CAS semantics are retained. This does not advertise
distributed fencing or exactly-once external delivery. Native SDK metadata and real-server ownership
tests prove different scopes; both are required where applicable.

## Legacy enqueue and encrypted payload ownership

Legacy `IOutboxStore.EnqueueAsync` keeps Elasticsearch's canonical JSON payload serialization,
fresh row identity and propagated context headers. A destination set with `context.SetDestination(...)`
is now honored; otherwise the runtime message type name remains the destination. The context's
message ID does not replace the fresh outbox document ID. Tenant, correlation, causation and the
propagated header container are prepared before an awaited payload transform; request-local items
are not persisted.

This provider exposes `IOutboxPayloadTransformStore` through the owning store's `GetService`.
`AddOutboxEncryption` uses that mediated path for legacy enqueue, so the provider constructs its own
envelope and changes only payload bytes. The callback receives the exact canonical payload, prepared
tenant and supplied cancellation token once. Null output, callback faults, callback cancellation and
cancellation detected before acceptance do not issue an accepting index request. An empty transformed
payload is supported. Once indexing starts, cancellation or transport failure retains the existing
uncertain-outcome semantics; it is not a database transaction or a guarantee of rollback.

Before its first index await, staging encodes the payload into an owned immutable Base64 string.
That persisted snapshot keeps later callback-owned buffer mutations from changing stored data;
concurrent mutation during the synchronous handoff is unsupported. Encryption produces ciphertext
inside the existing Base64 document field; decorated reads decrypt it without rewriting the stored
ciphertext. The borrowed Elasticsearch client and its source serializer remain unchanged.

Indexing still uses create-only document writes and the configured refresh policy. `WaitFor` remains
the default; this change does not add bulk acceptance, alter lease/CAS behavior, or change bounded
sent-document cleanup. The generated source metadata configuration described above remains the
route for persisted document serialization. Legacy POCO enqueue retains its trimming/dynamic-code
warnings and is not certified for NativeAOT by source metadata or managed enqueue tests.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
