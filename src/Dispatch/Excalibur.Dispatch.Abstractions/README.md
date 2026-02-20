# Excalibur.Dispatch.Abstractions

Core interfaces and types for the Dispatch messaging framework.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Abstractions
```

## Purpose

This package contains the foundational abstractions for Dispatch messaging. Use it when defining message contracts, handlers, or middleware without taking a dependency on the full Dispatch implementation. Ideal for shared libraries and domain projects.

## Key Types

- `IDispatchMessage` - Base interface for all messages
- `IDispatchAction` / `IDispatchAction<TResponse>` - Command-style messages (with optional response)
- `IDispatchDocument` - Query-style messages for read operations
- `IDispatchEvent` - Event notification messages
- `IDomainEvent` - Domain events for aggregate state changes
- `IIntegrationEvent` - Cross-boundary integration events
- `IDispatchHandler<T>` - Message handler interface
- `IDispatcher` - Message dispatch interface
- `IMessageMiddleware` - Middleware pipeline interface
- `IMessageContext` - Per-message context and metadata

## Quick Start

```csharp
// Define a command (action)
public record CreateOrder(string CustomerId, decimal Amount) : IDispatchAction;

// Define a query (document) with response
public record GetOrder(string OrderId) : IDispatchDocument;

// Define a handler
public class CreateOrderHandler : IDispatchHandler<CreateOrder>
{
    public Task<IDispatchResult> HandleAsync(
        CreateOrder message,
        IMessageContext context,
        CancellationToken cancellationToken)
    {
        // Handle command
        return Task.FromResult<IDispatchResult>(MessageResult.Success());
    }
}
```

## Documentation

Full documentation: https://github.com/TrigintaFaces/Excalibur

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
