# MinimalSample

A minimal sample demonstrating Dispatch with inbox/outbox patterns and scheduling.

## Purpose

This sample shows how to configure Dispatch with:
- In-memory inbox and outbox stores
- Dispatch scheduling for delayed/recurring messages
- JSON serialization
- Validation middleware

## What This Sample Demonstrates

- **Inbox Pattern** - Idempotent message processing
- **Outbox Pattern** - Reliable message publishing
- **Scheduling** - Delayed message delivery
- **Command Handling** - Processing commands with return values
- **Event Publishing** - Fire-and-forget event dispatching

## Running the Sample

```bash
dotnet run --project samples/01-getting-started/MinimalSample
```

## Project Structure

```
MinimalSample/
├── MinimalSample.csproj     # Project file
├── Program.cs               # Application entry with configuration
├── PingCommand.cs           # Command message definition
├── PingCommandHandler.cs    # Command handler
├── PingEvent.cs             # Event message definition
└── PingEventConsumer.cs     # Event handler
```

## Key Configuration

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    _ = dispatch.AddDispatchValidation();
    _ = dispatch.AddDispatchSerializer<AotJsonSerializer>(version: 0);
});

// Add inbox/outbox with in-memory stores
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInbox<InMemoryInboxStore>();

// Add scheduling
builder.Services.AddDispatchScheduling();

// Add hosted services for background processing
builder.Services.AddOutboxHostedService();
builder.Services.AddInboxHostedService();
builder.Services.AddDispatchSchedulingHostedService();
```

## Message Flow

1. **Command** is dispatched and returns a response
2. **Event** is dispatched and consumed by the event handler
3. **Outbox** ensures reliable event publication
4. **Inbox** prevents duplicate processing

## Dependencies

- `Dispatch` - Core messaging
- `Excalibur.Dispatch.Abstractions` - Message interfaces
- `Excalibur.Data.InMemory` - In-memory stores

## Next Steps

- [GettingStarted](../GettingStarted/) - Full ASP.NET Core API example
- [DispatchMinimal](../DispatchMinimal/) - Even simpler console example
- See `samples/CONVERSION-GUIDE.md` for using in your projects

---

*Category: Getting Started | Sprint 428*
