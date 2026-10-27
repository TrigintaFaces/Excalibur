# NativeAOT scheduled delivery

This sample uses the public scheduler producer and time-aware hosted worker to deliver an action and a document through source-generated handlers. It verifies message payloads, context and ambient tenant identity, correlation IDs, and completed schedule retirement. A non-null message omitted from the generated JSON context must fail before storage.

```powershell
dotnet publish -c Release -r win-x64 -p:BuildExamplesAndTests=true
./bin/Release/net10.0/win-x64/publish/Excalibur.Dispatch.Scheduling.Aot.Sample.exe --require-native
```

The acceptance command requires dynamic code and reflection-based JSON to be disabled. Register application JSON metadata with `AddAotJsonSerialization`, event serialization with `AddAotEventSerializer`, and generated handlers with `AddDispatch(dispatch => dispatch.AddDiscoveredHandlers())` before adding `AddTimeAwareScheduling`.

The public scheduling interfaces and their message, lease and completion data types live in `Excalibur.Dispatch.Abstractions`, with the `Excalibur.Dispatch.Delivery` namespace. The hosted scheduler and its registration live in `Excalibur.Dispatch`. Custom persistence providers implement the abstraction contracts on the same store instance used for administration and execution.

The sample explicitly permits the volatile development schedule store and uses a 30-second deadline. Pending schedules are lost on exit. This control demonstrates NativeAOT composition and delivery; it does not establish database durability, process recovery, distributed ownership, or exactly-once effects. Production hosts must register a durable schedule store.
