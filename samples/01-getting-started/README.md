# Getting Started Samples

Foundational samples for learning Dispatch and Excalibur basics.

## Samples

| Sample | Description | Difficulty |
|--------|-------------|------------|
| [GettingStarted](GettingStarted/) | ASP.NET Core API with commands, queries, events, and `[AutoRegister]` | Beginner |
| [DispatchMinimal](DispatchMinimal/) | Lightweight Dispatch-only console app with custom middleware | Beginner |
| [MinimalSample](MinimalSample/) | Simplest possible Dispatch setup | Beginner |
| [QuickDemo](QuickDemo/) | Rapid demonstration of core concepts | Beginner |
| [ExcaliburCqrs](ExcaliburCqrs/) | CQRS pattern with Excalibur domain modeling | Intermediate |

## Recommended Learning Path

1. **[GettingStarted](GettingStarted/)** - Start here to understand the full messaging pattern
2. **[DispatchMinimal](DispatchMinimal/)** - See Dispatch without Excalibur dependencies
3. **[ExcaliburCqrs](ExcaliburCqrs/)** - Learn CQRS with aggregate roots

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
# GettingStarted (ASP.NET Core API)
dotnet run --project samples/01-getting-started/GettingStarted
# Test with: curl http://localhost:5000/

# DispatchMinimal (Console)
dotnet run --project samples/01-getting-started/DispatchMinimal

# ExcaliburCqrs (Console)
dotnet run --project samples/01-getting-started/ExcaliburCqrs
```

## What's Next?

After completing these samples, explore:

- [02-messaging-transports/](../02-messaging-transports/) - Transport configuration
- [04-reliability/](../04-reliability/) - Reliability patterns (Sagas)
- [09-advanced/](../09-advanced/) - Advanced patterns

---

*Category: Getting Started | Sprint 428*
