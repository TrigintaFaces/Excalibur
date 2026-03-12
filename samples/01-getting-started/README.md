# Getting Started Samples

Foundational samples for learning Dispatch and Excalibur basics.

## Samples

| Sample | Description | Difficulty |
|--------|-------------|------------|
| [WebApiQuickStart](WebApiQuickStart/) | ASP.NET Core API with commands, queries, events, and `[AutoRegister]` | Beginner |
| [DispatchOnly](DispatchOnly/) | Lightweight Dispatch-only console app with custom middleware | Beginner |
| [HelloDispatch](HelloDispatch/) | Simplest possible Dispatch setup | Beginner |
| [InteractiveDemo](InteractiveDemo/) | Interactive walkthrough of core concepts | Beginner |
| [EventSourcingIntro](EventSourcingIntro/) | CQRS pattern with Excalibur domain modeling | Intermediate |

## Recommended Learning Path

1. **[HelloDispatch](HelloDispatch/)** - Simplest possible Dispatch setup
2. **[WebApiQuickStart](WebApiQuickStart/)** - ASP.NET Core API with full messaging pattern
3. **[DispatchOnly](DispatchOnly/)** - See Dispatch without Excalibur dependencies
4. **[EventSourcingIntro](EventSourcingIntro/)** - Learn CQRS with aggregate roots

## Key Concepts Covered

### Message Types

- **Commands** (`IDispatchAction<TResult>`) - Intent to change state, returns a result
- **Queries** (`IDispatchAction<TResult>`) - Read data without side effects
- **Events** (`IDispatchEvent`) - Notify multiple handlers of something that happened

### Handler Registration

```csharp
// Auto-discover handlers from assembly
builder.Services.AddDispatch(typeof(Program).Assembly);

// Optional: Use source-generated registrations
builder.Services.AddGeneratedServices();
```

### [AutoRegister] Attribute

```csharp
[AutoRegister(Lifetime = ServiceLifetime.Singleton)]
public class OrderStore : IOrderStore { }
```

## Running the Samples

```bash
# WebApiQuickStart (ASP.NET Core API)
dotnet run --project samples/01-getting-started/WebApiQuickStart
# Test with: curl http://localhost:5000/

# DispatchOnly (Console)
dotnet run --project samples/01-getting-started/DispatchOnly

# EventSourcingIntro (Console)
dotnet run --project samples/01-getting-started/EventSourcingIntro
```

## What's Next?

After completing these samples, explore:

- [02-messaging-transports/](../02-messaging-transports/) - Transport configuration
- [04-reliability/](../04-reliability/) - Reliability patterns (Sagas)
- [09-advanced/](../09-advanced/) - Advanced patterns

---

*Category: Getting Started | Sprint 607*
