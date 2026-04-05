# Testing Sample

Demonstrates how to unit test handlers, pipelines, and message contexts using the **Excalibur.Dispatch.Testing** package.

## What This Sample Shows

### HandlerUnitTests

- **`HandlerTestHarness<T>`** creates an isolated DI container for a single handler.
- Use `ConfigureServices` to register fakes (FakeItEasy) for handler dependencies.
- Call `HandleAsync<TAction, TResult>` to execute the handler without the full pipeline.
- Access `Handler` directly when you need fine-grained control.

### PipelineIntegrationTests

- **`DispatchTestHarness`** wires up the real Dispatch pipeline with middleware, handler resolution, and tracking.
- Use `ConfigureDispatch` to register handlers via `AddHandlersFromAssembly`.
- Use `ConfigureServices` to register fakes or test doubles.
- Check `harness.Dispatched` to verify which messages flowed through the pipeline.

### MessageContextTests

- **`MessageContextBuilder`** creates `IMessageContext` instances with sensible defaults.
- Auto-generates `MessageId` and `CorrelationId` when not set explicitly.
- Use `WithItem` for custom metadata and `WithTenantId`/`WithUserId` for identity features.

## Running

```bash
dotnet test samples/09-advanced/Testing/
```
