# HelloDispatch

A minimal "hello world" sample demonstrating basic Dispatch command and event dispatching.

## Purpose

This sample shows the simplest possible Dispatch setup:
- Command dispatching with a return value
- Event dispatching with a handler
- Validation middleware

## What This Sample Demonstrates

- **Command Handling** - `IDispatchAction<TResult>` with `IActionHandler<TAction, TResult>`
- **Event Publishing** - `IDispatchEvent` with `IEventHandler<TEvent>`
- **Validation Middleware** - `AddDispatchValidation()` for pipeline validation
- **Handler Discovery** - `AddHandlersFromAssembly()` for automatic registration

## Running the Sample

```bash
dotnet run --project samples/01-getting-started/HelloDispatch
```

## Project Structure

```
HelloDispatch/
├── HelloDispatch.csproj     # Project file
├── Program.cs               # Console app with Dispatch configuration
├── PingCommand.cs           # Command: IDispatchAction<string>
├── PingCommandHandler.cs    # Handler: IActionHandler<PingCommand, string>
├── PingEvent.cs             # Event: IDispatchEvent
└── PingEventConsumer.cs     # Handler: IEventHandler<PingEvent>
```

## Key Configuration

```csharp
var services = new ServiceCollection();

services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

services.AddDispatch(dispatch =>
{
    _ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    _ = dispatch.AddDispatchValidation();
});
```

## Local Message Bus Initialization

`AddDispatch()` registers a keyed `IMessageBus` named `"Local"` for in-process messaging. In a console app without the Generic Host, you must resolve it before dispatching:

```csharp
var provider = services.BuildServiceProvider();
_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
```

**In ASP.NET Core / Generic Host apps**, this initialization happens automatically via hosted services — no manual resolution needed.

## Message Flow

1. **PingCommand** is dispatched and returns a `string` response ("Pong: Hello")
2. **PingEvent** is dispatched and handled by `PingEventHandler`

## Dependencies

- `Dispatch` - Core messaging
- `Excalibur.Dispatch.Abstractions` - Message interfaces

## Next Steps

- [WebApiQuickStart](../WebApiQuickStart/) - Full ASP.NET Core API example
- [DispatchOnly](../DispatchOnly/) - Dispatch-only console example with commands, events, and queries

---

*Category: Getting Started | Sprint 607*
