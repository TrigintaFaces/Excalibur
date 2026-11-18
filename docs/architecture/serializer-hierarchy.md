# Serializer Contracts and Implementations

## Selected Serialization APIs

This is a selection of contracts and implementations, not a complete hierarchy or a performance ranking.

| API | Kind | Format / purpose |
|-----|------|------------------|
| `IEventSerializer` | Interface | Event persistence and replay; format depends on the implementation |
| `ISerializer` | Interface | Format-neutral serialization contract; implementations include System.Text.Json, MemoryPack, MessagePack, Avro and Protobuf |
| `DispatchJsonSerializer` | Concrete class | System.Text.Json serialization; NativeAOT requires appropriate generated metadata |
| `SystemTextJsonSerializer` | Concrete class | JSON under an explicit metadata policy via `CreateWithMetadata(options)`, or an advanced context-owned policy via `Create(context)`; unsupported types fail |

## Which to Use

- **Storing events?** Use an `IEventSerializer` implementation compatible with the event store and persisted format.
- **Selecting a serialization format?** Use an `ISerializer` implementation and explicitly configure the required provider. The interface does not imply MemoryPack or a particular performance profile.
- **Standard JSON?** Consider `DispatchJsonSerializer` with the required options and type metadata.
- **NativeAOT deployment?** Select a compatible implementation and supply generated metadata or provider-specific registrations for every serialized type. `SystemTextJsonSerializer.CreateWithMetadata(options)` composes supplied metadata and framework fallbacks under one frozen policy; `Create(context)` retains the advanced context-owned policy. Neither route is an application-wide AOT guarantee.

`AddPluggableSerialization()` executes configured registrations; do not rely on this method alone to select an implicit default serializer. Verify the registration path used by the application.
