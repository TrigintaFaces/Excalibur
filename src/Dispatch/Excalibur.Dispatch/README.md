# Dispatch

Core messaging framework providing message dispatching, middleware pipeline, and DI integration for .NET applications.

## Installation

```bash
dotnet add package Dispatch
```

## Purpose

Dispatch is a lightweight, extensible messaging framework for building distributed systems. Use it as a MediatR alternative with built-in support for middleware pipelines, pluggable serialization, validation, and cloud-native messaging patterns.

## Key Types

- `IDispatchBuilder` - Fluent configuration builder
- `DispatchBuilder` - DI service registration
- `AddDispatch()` - Service collection extension
- `IMessageMiddleware` - Pipeline middleware base
- `MessageResult` - Standard result type

## Quick Start

```csharp
// Register Dispatch services
services.AddDispatch(options =>
{
    options.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Define a command
public record CreateOrder(string CustomerId, decimal Amount) : IDispatchAction;

// Create a handler
public class CreateOrderHandler : IDispatchHandler<CreateOrder>
{
    public Task<IDispatchResult> HandleAsync(
        CreateOrder command,
        IMessageContext context,
        CancellationToken cancellationToken)
    {
        // Implementation
        return Task.FromResult<IDispatchResult>(MessageResult.Success());
    }
}

// Dispatch a command
await dispatcher.DispatchAsync(new CreateOrder("cust-1", 99.99m), cancellationToken);
```

## Features

- **Message Handling**: Strongly-typed action, document, and event handlers
- **Middleware Pipeline**: Extensible pipeline for cross-cutting concerns
- **Pluggable Serialization**: MemoryPack (default), System.Text.Json, MessagePack
- **Validation**: DataAnnotations (built-in) or FluentValidation (optional)
- **Rate Limiting**: Built-in rate limiting capabilities
- **Health Checks**: Health check endpoints for monitoring

## Related Packages

- `Excalibur.Dispatch.Abstractions` - Core interfaces and types
- `Excalibur.Dispatch.Validation.FluentValidation` - FluentValidation integration
- `Excalibur.Dispatch.Transport.*` - Message transport implementations
- `Excalibur.Dispatch.Hosting.*` - Hosting integrations

## Documentation

Full documentation: https://github.com/TrigintaFaces/Excalibur

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
