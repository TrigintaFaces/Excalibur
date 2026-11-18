# Versioning API Reference

This section documents the public APIs for Universal Message Upcasting in Excalibur.Dispatch.

## Overview

Universal Message Upcasting provides schema evolution support for all message types through a unified pipeline with automatic path finding.

## Interfaces (Excalibur.Dispatch.Abstractions)

| Interface | Description |
|-----------|-------------|
| [IVersionedMessage](./IVersionedMessage.md) | Marker interface for versioned messages |
| [IMessageUpcaster<TOld, TNew>](./IMessageUpcaster.md) | Type-safe transformation between versions |
| [IUpcastingPipeline](./IUpcastingPipeline.md) | Orchestrates multi-hop upcasting with BFS |

## Classes (Dispatch)

| Class | Description |
|-------|-------------|
| [UpcastingBuilder](./UpcastingBuilder.md) | Fluent builder for configuring upcasters |
| [UpcastingPipeline](./IUpcastingPipeline.md#implementation-upcastingpipeline) | Thread-safe implementation with path caching |

## Extension Methods

| Method | Description |
|--------|-------------|
| [AddMessageUpcasting](./AddMessageUpcasting.md) | DI registration extension |

## Quick Reference

### Register Upcasters

```csharp
services.AddMessageUpcasting(builder =>
{
    // Option 1: Manual registration
    builder.RegisterUpcaster(new UserEventV1ToV2());

    // Option 2: Assembly scanning
    builder.ScanAssembly(typeof(Program).Assembly);

    // Option 3: With DI
    builder.RegisterUpcaster<AddressV1, AddressV2>(sp =>
        new AddressV1ToV2(sp.GetRequiredService<IMapper>()));
});
```

### Implement Upcaster

```csharp
public class UserEventV1ToV2 : IMessageUpcaster<UserEventV1, UserEventV2>
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public UserEventV2 Upcast(UserEventV1 old) => new UserEventV2
    {
        EventId = old.EventId,
        AggregateId = old.AggregateId,
        // ... transform properties
    };
}
```

### Use Pipeline

```csharp
var pipeline = serviceProvider.GetRequiredService<IUpcastingPipeline>();
var latest = pipeline.Upcast(oldEvent);
```

## Performance

The legacy estimates of ~1 ns passthrough, ~13â€“18 ns path lookup and ~90â€“105 ns
per transformation have no linked source epoch, matched runtime or raw evidence
here. They are unverified historical claims, not current measurements, and they
are not restated as targets anywhere in the pipeline or its benchmarks.

What is established, and how:

- **Registry reads do not acquire the registration lock.** Registered upcasters live in an immutable
  registry read with a single volatile read; registration publishes a replacement.
  A message already at the latest version resolves no path and acquires nothing.
  Path-cache population may synchronize internally. The pipeline invokes consumer
  upcaster delegates after path resolution, without holding its registration or cache-population
  locks. Registration is not refused merely because it occurs inside an upcaster callback;
  invalid metadata, duplicates and disposal still reject registration.
- **Allocation and timing require workload-specific evidence.** Traversal with
  preconstructed outputs does different work from upcasters that construct new
  messages. Post-change local observations are not a matched before/after result.
  NativeAOT support requires a published native executable and observed execution;
  managed BenchmarkDotNet results do not establish it.

The performance campaign tracks current verification, with raw evidence under
`artifacts/benchmarks/current-refresh-20261101`. The November 1 external-session
results are preserved in `external-handoff-1841/preservation.json` as diagnostic
candidates; their preserved files do not establish original execution isolation or
complete source provenance. The benchmark job changed from HostProcess to an
in-process diagnostic configuration, so results from those configurations cannot
be used as a matched performance delta. A reported failed earlier attempt does
not establish that the subsystem was never measured.

Use the benchmark's exact job, runtime, fixture and source epoch when interpreting
results. Historical design targets and unsupported speedup estimates above are
not current performance claims.

## Registration and lifetime

Type and version metadata is captured once when an upgrader is registered. Later
getter changes do not remap that registration; transformations still call the
original upgrader instance. Resolution caches belong to immutable registration
snapshots, so an older in-flight search cannot publish a result for a newer graph.
Only registered endpoint pairs enter the memo. This bounds each memo by graph size, not by a fixed byte limit. Concurrent
invalidation can leave earlier memos live until their in-flight readers finish.

`UpcastingPipeline` implements `IDisposable`. Disposal rejects new operations that
need registry access, while already-admitted traversals may finish. It does not
dispose consumer-owned upcasters. Identity/downcast checks that need no registry
retain their normal behavior. Cached `CanUpcast` queries now reject access after
disposal just like uncached queries; the previous cache-dependent behavior is not
preserved. Repeated disposal is harmless and cannot allow registration to revive
a disposed pipeline.

## Verified compatibility scope

The November 1 repair passed 58 Dispatch and 125 event-sourcing Release tests,
including generation races, disposal admission, metadata capture and typed snapshot
upgrades (`upcasting-green-dispatch-r3` and `upcasting-green-event-r3` under the
campaign evidence directory). The actual win-x64 NativeAOT probe passed 18 checks
with dynamic code disabled, including typed multihop snapshot bytes and serializer
call order (`upcasting-native-execution-r2`). This covers those exercised paths,
not arbitrary consumer serializers or a NativeAOT performance comparison.

The native publish subprocess exited zero without warnings, but its outer process
containment check rejected a surviving `vctip` helper and terminated that owned job.
The separate native execution ended cleanly; binary hashes remained unchanged.
Both outcomes are retained in `upcasting-native-reconciliation-r1.json`. A subsequent
incremental publish (`upcasting-native-publish-r4`) and execution (`upcasting-native-execution-r2`)
passed the same strict checks with empty process trees. The executable SHA-256
remained `f27cdee0187d71b92664c75182d8a74fa16c2cc54bc2660ef7eab588b4dcd812`;
the repeat does not erase the earlier containment failure.

## See Also

- [Developer Guide](../../../../docs/versioning/universal-upcasting-guide.md) - Step-by-step tutorial
