# Excalibur.Dispatch.Hosting.GoogleCloudFunctions

Google Cloud Functions hosting integration for the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Hosting.GoogleCloudFunctions
```

## What `AddGoogleCloudFunctionsServerless` registers — and what it does not

`AddGoogleCloudFunctionsServerless` wires the serverless host integration **only**. It does not set up messaging.

| registered | purpose |
|---|---|
| `IServerlessHostProvider` → `GoogleCloudFunctionsHostProvider` | the host integration for this platform |
| `IColdStartOptimizer` → `GoogleCloudFunctionsColdStartOptimizer` | cold-start optimisation for this platform |
| `ServerlessHostOptions` | defaults registered; the action overload configures them and the configuration overload binds them |
| `IValidateOptions<ServerlessHostOptions>` | the shared validator enforces `MemoryLimitMB >= 1` when a value is supplied; options creation and Generic Host startup run this validation |

The provider and cold-start optimizer defaults use `TryAdd`, preserving consumer registrations. The shared validator uses `TryAddEnumerable`; options configuration delegates compose in registration order. This is not a promise that every repeated registration leaves all descriptor counts unchanged.

A Generic Host validates these options during `StartAsync`. A manually built service provider validates when the options value is resolved. These facades do not add `IServerlessHostProviderFactory`; factory composition remains the separate `AddServerlessHosting` profile.

**It does NOT register the dispatcher.** You must also call `AddDispatch(...)`. A function that injects
`IDispatcher` without it fails with a dependency-resolution error on its first invocation — not at
startup — so the omission is easy to ship.

```csharp
services.AddDispatch(dispatch => { /* your handlers */ });
services.AddGoogleCloudFunctionsServerless();
```

**It also does not register** a transport, an outbox, or an inbox. Add those explicitly if your function
needs them; nothing here implies them.

## Features

- Cloud Functions integration
- Pub/Sub trigger support
- HTTP trigger support
- Cloud Storage triggers

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
