# Getting Started with Dispatch

This sample demonstrates the fundamentals of the Dispatch messaging framework with a minimal ASP.NET Core API.

## What This Sample Demonstrates

- **Commands** - Intent to change state (CreateOrderCommand)
- **Queries** - Read data without side effects (GetOrderQuery)
- **Events** - Notify multiple handlers of something that happened (OrderShippedEvent)
- **Handlers** - Process messages using IActionHandler and IEventHandler
- **[AutoRegister]** - Compile-time service registration with source generators

## Running the Sample

```bash
dotnet run
```

The API starts at `http://localhost:5000` (or the port shown in the console output).

## Testing the Endpoints

### Create an Order (Command)

Commands represent intent to change state and return a result.

```bash
curl -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"productId": "WIDGET-001", "quantity": 5}'
```

Response:
```json
{
  "orderId": "a1b2c3d4-...",
  "message": "Order created successfully"
}
```

### Get Order Details (Query)

Queries read data without modifying state.

```bash
curl http://localhost:5000/orders/{orderId}
```

Response:
```json
{
  "id": "a1b2c3d4-...",
  "productId": "WIDGET-001",
  "quantity": 5,
  "status": "Pending",
  "createdAt": "2026-01-21T12:00:00Z"
}
```

### Ship an Order (Event)

Events notify multiple handlers that something happened.

```bash
curl -X POST http://localhost:5000/orders/{orderId}/ship
```

Response:
```json
{
  "message": "Order shipped event published"
}
```

Check the order status again - it will now show "Shipped":

```bash
curl http://localhost:5000/orders/{orderId}
```

## Project Structure

```
GettingStarted/
├── GettingStarted.csproj    # Project file with Dispatch references
├── README.md                 # This file
├── Program.cs                # ASP.NET Core API setup with Dispatch
├── Messages/
│   ├── CreateOrderCommand.cs # Command - creates an order
│   ├── GetOrderQuery.cs      # Query - retrieves order details
│   └── OrderShippedEvent.cs  # Event - notifies of shipment
└── Handlers/
    ├── OrderStore.cs              # In-memory store with [AutoRegister]
    ├── CreateOrderHandler.cs      # Handles CreateOrderCommand
    ├── GetOrderHandler.cs         # Handles GetOrderQuery
    ├── OrderShippedHandler.cs     # Handles OrderShippedEvent (updates status)
    └── OrderShippedNotificationHandler.cs # Handles OrderShippedEvent (sends notification)
```

## Key Concepts

### Message Types

| Type | Interface | Purpose | Handlers |
|------|-----------|---------|----------|
| Command | `IDispatchAction<TResult>` | Change state, return result | Exactly one |
| Query | `IDispatchAction<TResult>` | Read data, return result | Exactly one |
| Event | `IDispatchEvent` | Notify something happened | Zero or more |

### Handler Registration

Handlers are discovered automatically by calling `AddDispatch()`:

```csharp
builder.Services.AddDispatch(typeof(Program).Assembly);
```

### [AutoRegister] Attribute

Services can opt-in to compile-time registration using `[AutoRegister]`:

```csharp
[AutoRegister(Lifetime = ServiceLifetime.Singleton)]
public class OrderStore : IOrderStore
{
    // ...
}
```

Then call the generated extension method:

```csharp
builder.Services.AddGeneratedServices();
```

This provides:
- No runtime reflection
- Faster startup
- Native AOT compatibility
- Trimming safety

## Next Steps

- See `samples/DispatchMinimal` for a console-based example
- See `samples/ExcaliburCqrs` for full CQRS with event sourcing
- See `docs-site/docs/source-generators/getting-started.md` for more on [AutoRegister]
- See `samples/CONVERSION-GUIDE.md` for converting to PackageReference

## Converting to PackageReference

For your own projects, replace ProjectReference with PackageReference:

```xml
<!-- Instead of: -->
<ProjectReference Include="..\..\src\Dispatch\Dispatch\Excalibur.Dispatch.csproj" />

<!-- Use: -->
<PackageReference Include="Dispatch" Version="1.0.0" />
<PackageReference Include="Excalibur.Dispatch.Abstractions" Version="1.0.0" />
```

See `samples/CONVERSION-GUIDE.md` for complete instructions.
