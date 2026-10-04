# DispatchOnly

This sample demonstrates Dispatch commands, events with two handlers, document processing, and custom middleware through the real `IDispatcher` pipeline. It uses `Excalibur.Dispatch` and `Excalibur.Dispatch.Abstractions`, plus Microsoft logging; it does not require the Excalibur CQRS wrapper.

Run from the repository root:

```powershell
dotnet run --project samples/01-getting-started/DispatchOnly -p:BuildExamplesAndTests=true
```

The command creates an order for five widgets and returns its identifier. Two awaited event handlers update an in-memory read model and record a local notification. The document handler reads that order and records the observed result. `Program.cs` checks the identifier, product, quantity, both event effects, and document contents. Any failed dispatch or incorrect effect throws and exits unsuccessfully.

`OrderStore` is demonstration state. Its concurrent collections allow the event handlers to execute concurrently; the sample does not implement durable storage, an external notification service, atomic command/event delivery, or exactly-once processing.

## Code to explore

- [Program.cs](Program.cs): service registration, middleware, dispatch and result assertions.
- [Messages](Messages): `IDispatchAction<Guid>`, `IDispatchEvent` and `IDispatchDocument` contracts.
- [Handlers](Handlers): command, two event handlers and document processing.
- [OrderStore.cs](OrderStore.cs): observable in-memory state shared by the handlers.
- [LoggingMiddleware.cs](Middleware/LoggingMiddleware.cs): pipeline logging.

## Validate the NuGet packages

Local development uses project references. With `UsePackageReferences=true`, the project consumes the two Dispatch packages at `DispatchPackageVersion` instead. The repository composition check builds an exact candidate feed, uses isolated caches, verifies consumed package hashes, and runs this scenario with a timeout:

```powershell
pwsh eng/validate-package-composition.ps1 -Version 0.0.0-local
```

Evidence remains under `artifacts/package-composition/`. A successful build alone does not certify the scenario. See [the contributor gate documentation](../../../docs/ci-gates.md#17-package-composition) for the complete contract.

Use the [ExcaliburCqrs sample](../ExcaliburCqrs) when you need aggregate roots, event sourcing or domain invariants.

## License

This project is available under the [Excalibur License 1.1](../../../licenses/LICENSE-EXCALIBUR.txt), [AGPL-3.0-or-later](../../../licenses/LICENSE-AGPL-3.0.txt), or [SSPL-1.0](../../../licenses/LICENSE-SSPL-1.0.txt). See [LICENSE](../../../LICENSE).
