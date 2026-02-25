---
sidebar_position: 2
title: Testing Dispatch Handlers
description: How to unit test handlers, verify middleware behavior, and integration test the full pipeline
---

# Testing Dispatch Handlers

You have handlers that process actions and events. You have middleware that validates, authorizes, and retries. You need to know they work before deploying to production.

This guide covers the three levels of testing you will use most often: unit testing individual handlers, testing middleware behavior, and integration testing the full pipeline. It uses the same stack the Dispatch test suite itself runs on: **xUnit**, **Shouldly**, and **FakeItEasy**.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the testing packages:
  ```bash
  dotnet add package xunit
  dotnet add package Shouldly
  dotnet add package FakeItEasy
  dotnet add package Excalibur.Dispatch.Testing       # test harness and utilities
  dotnet add package Excalibur.Dispatch.Testing.Shouldly  # fluent assertions (optional)
  ```
- Familiarity with [actions and handlers](../core-concepts/actions-and-handlers.md)
- For pipeline testing, see also the [Test Harness](./test-harness.md) guide

## Test Stack Setup

Add the testing packages to your test project:

```bash
dotnet add package xunit
dotnet add package Shouldly
dotnet add package FakeItEasy
dotnet add package Microsoft.NET.Test.Sdk
```

A `GlobalUsings.cs` file keeps your test files clean:

```csharp
global using Xunit;
global using Shouldly;
global using FakeItEasy;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
```

## Unit Testing a Handler

A handler is a class with dependencies and a `HandleAsync` method. Test it like any other class: mock the dependencies, call the method, assert the result.

### Action Handler (Command)

```csharp
public record CreateOrderAction(string CustomerId, List<string> Items) : IDispatchAction;

public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(IOrderRepository repository, ILogger<CreateOrderHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task HandleAsync(CreateOrderAction action, CancellationToken cancellationToken)
    {
        var order = new Order(action.CustomerId, action.Items);
        await _repository.SaveAsync(order, cancellationToken);
    }
}
```

Test it:

```csharp
public class CreateOrderHandlerShould
{
    private readonly IOrderRepository _repository;
    private readonly CreateOrderHandler _handler;

    public CreateOrderHandlerShould()
    {
        _repository = A.Fake<IOrderRepository>();
        var logger = A.Fake<ILogger<CreateOrderHandler>>();
        _handler = new CreateOrderHandler(_repository, logger);
    }

    [Fact]
    public async Task Save_new_order_to_repository()
    {
        // Arrange
        var action = new CreateOrderAction("customer-123", ["item-a", "item-b"]);

        // Act
        await _handler.HandleAsync(action, CancellationToken.None);

        // Assert
        A.CallTo(() => _repository.SaveAsync(
            A<Order>.That.Matches(o => o.CustomerId == "customer-123"),
            A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Pass_cancellation_token_to_repository()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var action = new CreateOrderAction("customer-123", ["item-a"]);

        // Act
        await _handler.HandleAsync(action, token);

        // Assert
        A.CallTo(() => _repository.SaveAsync(
            A<Order>._,
            token))
            .MustHaveHappened();
    }
}
```

### Action Handler (Query)

```csharp
public record GetOrderAction(Guid OrderId) : IDispatchAction<Order>;

public class GetOrderHandler : IActionHandler<GetOrderAction, Order>
{
    private readonly IOrderRepository _repository;

    public GetOrderHandler(IOrderRepository repository) => _repository = repository;

    public async Task<Order> HandleAsync(
        GetOrderAction action, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(action.OrderId, cancellationToken)
            ?? throw new OrderNotFoundException(action.OrderId);
    }
}
```

Test both the happy path and the error case:

```csharp
public class GetOrderHandlerShould
{
    private readonly IOrderRepository _repository;
    private readonly GetOrderHandler _handler;

    public GetOrderHandlerShould()
    {
        _repository = A.Fake<IOrderRepository>();
        _handler = new GetOrderHandler(_repository);
    }

    [Fact]
    public async Task Return_order_when_found()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var expected = new Order { Id = orderId, CustomerId = "C1" };
        A.CallTo(() => _repository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns(expected);

        // Act
        var result = await _handler.HandleAsync(
            new GetOrderAction(orderId), CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task Throw_when_order_not_found()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        A.CallTo(() => _repository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns((Order?)null);

        // Act & Assert
        await Should.ThrowAsync<OrderNotFoundException>(
            _handler.HandleAsync(new GetOrderAction(orderId), CancellationToken.None));
    }
}
```

### Event Handler

Event handlers follow the same pattern. The difference is that events are broadcast to multiple handlers, so you test each handler in isolation:

```csharp
public record OrderCreatedEvent(Guid OrderId, string CustomerId) : DomainEventBase;

public class SendOrderConfirmationHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IEmailService _emailService;

    public SendOrderConfirmationHandler(IEmailService emailService)
        => _emailService = emailService;

    public async Task HandleAsync(
        OrderCreatedEvent @event, CancellationToken cancellationToken)
    {
        await _emailService.SendOrderConfirmationAsync(
            @event.OrderId, @event.CustomerId, cancellationToken);
    }
}

public class SendOrderConfirmationHandlerShould
{
    [Fact]
    public async Task Send_confirmation_email_with_order_details()
    {
        // Arrange
        var emailService = A.Fake<IEmailService>();
        var handler = new SendOrderConfirmationHandler(emailService);
        var orderId = Guid.NewGuid();

        // Act
        await handler.HandleAsync(
            new OrderCreatedEvent(orderId, "customer-123"),
            CancellationToken.None);

        // Assert
        A.CallTo(() => emailService.SendOrderConfirmationAsync(
            orderId, "customer-123", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
```

## Testing Middleware Behavior

Middleware wraps the pipeline and can short-circuit execution, modify context, or add cross-cutting behavior. Testing middleware requires simulating the pipeline by providing a `next` delegate.

### Validation Short-Circuit

When validation fails, the middleware should prevent the handler from running:

```csharp
public class ValidationMiddlewareShould
{
    private readonly IValidationService _validationService;
    private bool _handlerCalled;

    public ValidationMiddlewareShould()
    {
        _validationService = A.Fake<IValidationService>();
        _handlerCalled = false;
    }

    // The "next" delegate simulates the rest of the pipeline
    private ValueTask<IMessageResult> NextDelegate(
        IDispatchMessage message, IMessageContext context, CancellationToken ct)
    {
        _handlerCalled = true;
        return new ValueTask<IMessageResult>(MessageResult.Success());
    }

    [Fact]
    public async Task Skip_handler_when_validation_fails()
    {
        // Arrange
        A.CallTo(() => _validationService.ValidateAsync(
            A<object>._, A<MessageValidationContext>._, A<CancellationToken>._))
            .Returns(ValidationResult.Failure(
                new ValidationError("Amount", "Amount must be positive")));

        var options = Microsoft.Extensions.Options.Options.Create(
            new ValidationOptions { Enabled = true, UseCustomValidation = true });
        var middleware = new ValidationMiddleware(
            options, _validationService, A.Fake<ILogger<ValidationMiddleware>>());
        var message = A.Fake<IDispatchMessage>();
        var context = A.Fake<IMessageContext>();

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(
            middleware.InvokeAsync(
                message, context, NextDelegate, CancellationToken.None).AsTask());

        _handlerCalled.ShouldBeFalse("Handler should not run when validation fails");
    }

    [Fact]
    public async Task Call_handler_when_validation_passes()
    {
        // Arrange
        A.CallTo(() => _validationService.ValidateAsync(
            A<object>._, A<MessageValidationContext>._, A<CancellationToken>._))
            .Returns(ValidationResult.Success());

        var options = Microsoft.Extensions.Options.Options.Create(
            new ValidationOptions { Enabled = true, UseCustomValidation = true });
        var middleware = new ValidationMiddleware(
            options, _validationService, A.Fake<ILogger<ValidationMiddleware>>());
        var message = A.Fake<IDispatchMessage>();
        var context = A.Fake<IMessageContext>();

        // Act
        var result = await middleware.InvokeAsync(
            message, context, NextDelegate, CancellationToken.None);

        // Assert
        _handlerCalled.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
    }
}
```

### Testing Middleware Execution Order

When you compose multiple middleware, you need to verify they run in the correct order:

```csharp
public class MiddlewareOrderShould
{
    [Fact]
    public async Task Execute_middleware_in_registration_order()
    {
        // Arrange
        var executionOrder = new List<string>();

        var middleware1 = new TrackingMiddleware("Auth", executionOrder);
        var middleware2 = new TrackingMiddleware("Validation", executionOrder);
        var middleware3 = new TrackingMiddleware("Logging", executionOrder);

        ValueTask<IMessageResult> handler(
            IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
        {
            executionOrder.Add("Handler");
            return new ValueTask<IMessageResult>(MessageResult.Success());
        }

        // Act -- chain middleware manually to simulate the pipeline
        await middleware1.InvokeAsync(
            A.Fake<IDispatchMessage>(),
            A.Fake<IMessageContext>(),
            (m1, c1, t1) => middleware2.InvokeAsync(
                m1, c1,
                (m2, c2, t2) => middleware3.InvokeAsync(m2, c2, handler, t2),
                t1),
            CancellationToken.None);

        // Assert
        executionOrder.ShouldBe(["Auth", "Validation", "Logging", "Handler"]);
    }

    // Simple middleware that records when it runs
    private sealed class TrackingMiddleware(string name, List<string> order)
    {
        public ValueTask<IMessageResult> InvokeAsync(
            IDispatchMessage message,
            IMessageContext context,
            DispatchRequestDelegate next,
            CancellationToken cancellationToken)
        {
            order.Add(name);
            return next(message, context, cancellationToken);
        }
    }
}
```

## Testing with Message Context

Many handlers and middleware need values from `IMessageContext` -- the user ID, tenant ID, or custom items set by earlier middleware. You have two options for providing context in tests.

### Option 1: Fake with FakeItEasy

For simple cases, fake only the properties you need:

```csharp
var context = A.Fake<IMessageContext>();
A.CallTo(() => context.MessageId).Returns("msg-001");
A.CallTo(() => context.UserId).Returns("user-123");
A.CallTo(() => context.TenantId).Returns("tenant-456");
```

### Option 2: Concrete Test Double

For tests that read and write context items, a concrete implementation is simpler:

```csharp
public sealed class TestMessageContext : IMessageContext
{
    private readonly Dictionary<string, object> _items = [];

    public string? MessageId { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public IDictionary<string, object> Items => _items;

    public T? GetItem<T>(string key) =>
        _items.TryGetValue(key, out var value) && value is T typed ? typed : default;

    public void SetItem<T>(string key, T value) => _items[key] = value!;
}
```

Use it when your handler reads context items set by middleware:

```csharp
[Fact]
public async Task Use_tenant_from_context()
{
    var context = new TestMessageContext { TenantId = "acme-corp" };
    context.SetItem("CorrelationId", "corr-123");

    // Pass context to your handler or middleware under test
    await handler.HandleAsync(action, context, CancellationToken.None);

    // Assert the handler used the correct tenant
    A.CallTo(() => _repository.SaveAsync(
        A<Order>.That.Matches(o => o.TenantId == "acme-corp"),
        A<CancellationToken>._))
        .MustHaveHappened();
}
```

## Integration Testing the Pipeline

Unit tests verify individual handlers and middleware. Integration tests verify they work together through the real Dispatch pipeline.

### Setting Up a Test Host

Register Dispatch with the in-memory transport, add your handlers and middleware, then dispatch a message and verify the result:

```csharp
public class OrderPipelineShould : IAsyncLifetime
{
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(typeof(CreateOrderHandler).Assembly);
            dispatch.UseValidation();
            dispatch.UseOpenTelemetry();
        });

        // Register your dependencies
        services.AddSingleton(A.Fake<IOrderRepository>());
        services.AddSingleton(A.Fake<IEmailService>());

        _provider = services.BuildServiceProvider();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Dispatch_action_through_full_pipeline()
    {
        // Arrange
        var dispatcher = _provider.GetRequiredService<IDispatcher>();
        var repository = _provider.GetRequiredService<IOrderRepository>();

        // Act
        await dispatcher.DispatchAsync(
            new CreateOrderAction("customer-123", ["item-a"]),
            CancellationToken.None);

        // Assert -- handler was invoked through the pipeline
        A.CallTo(() => repository.SaveAsync(
            A<Order>.That.Matches(o => o.CustomerId == "customer-123"),
            A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Return_result_from_query_handler()
    {
        // Arrange
        var dispatcher = _provider.GetRequiredService<IDispatcher>();
        var repository = _provider.GetRequiredService<IOrderRepository>();
        var orderId = Guid.NewGuid();
        var expected = new Order { Id = orderId, CustomerId = "C1" };

        A.CallTo(() => repository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns(expected);

        // Act
        var result = await dispatcher.DispatchAsync(
            new GetOrderAction(orderId),
            CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
    }
}
```

### Testing Middleware Rejects Invalid Messages

```csharp
[Fact]
public async Task Reject_invalid_action_before_handler_runs()
{
    // Arrange
    var dispatcher = _provider.GetRequiredService<IDispatcher>();
    var repository = _provider.GetRequiredService<IOrderRepository>();

    // Action with invalid data (empty customer ID)
    var invalidAction = new CreateOrderAction("", []);

    // Act & Assert
    await Should.ThrowAsync<ValidationException>(
        dispatcher.DispatchAsync(invalidAction, CancellationToken.None));

    // Handler should never have been called
    A.CallTo(() => repository.SaveAsync(A<Order>._, A<CancellationToken>._))
        .MustNotHaveHappened();
}
```

## Testing Custom Middleware

If you write your own middleware, test that it correctly calls `next` (or doesn't) and that it modifies context or results as expected.

```csharp
public class CorrelationIdMiddleware : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;

    public ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate next,
        CancellationToken cancellationToken)
    {
        if (context.GetItem<string>("CorrelationId") is null)
        {
            context.SetItem("CorrelationId", Guid.NewGuid().ToString());
        }

        return next(message, context, cancellationToken);
    }
}

public class CorrelationIdMiddlewareShould
{
    [Fact]
    public async Task Add_correlation_id_when_missing()
    {
        // Arrange
        var middleware = new CorrelationIdMiddleware();
        var context = new TestMessageContext();

        // Act
        await middleware.InvokeAsync(
            A.Fake<IDispatchMessage>(),
            context,
            (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        // Assert
        context.GetItem<string>("CorrelationId").ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Preserve_existing_correlation_id()
    {
        // Arrange
        var middleware = new CorrelationIdMiddleware();
        var context = new TestMessageContext();
        context.SetItem("CorrelationId", "existing-id");

        // Act
        await middleware.InvokeAsync(
            A.Fake<IDispatchMessage>(),
            context,
            (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        // Assert
        context.GetItem<string>("CorrelationId").ShouldBe("existing-id");
    }

    [Fact]
    public async Task Call_next_delegate()
    {
        // Arrange
        var middleware = new CorrelationIdMiddleware();
        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(
            A.Fake<IDispatchMessage>(),
            new TestMessageContext(),
            (msg, ctx, ct) =>
            {
                nextCalled = true;
                return new ValueTask<IMessageResult>(MessageResult.Success());
            },
            CancellationToken.None);

        // Assert
        nextCalled.ShouldBeTrue();
    }
}
```

## Common Patterns

### Capturing Arguments

Use FakeItEasy's `Invokes` to capture arguments for detailed assertions:

```csharp
Order? savedOrder = null;
A.CallTo(() => _repository.SaveAsync(A<Order>._, A<CancellationToken>._))
    .Invokes((Order order, CancellationToken _) => savedOrder = order);

await _handler.HandleAsync(action, CancellationToken.None);

savedOrder.ShouldNotBeNull();
savedOrder!.CustomerId.ShouldBe("customer-123");
savedOrder.Items.Count.ShouldBe(2);
```

### Testing Async Exceptions

`ValueTask` methods need `.AsTask()` for Shouldly's `ThrowAsync`:

```csharp
await Should.ThrowAsync<ValidationException>(
    middleware.InvokeAsync(message, context, next, ct).AsTask());
```

### Verifying No Side Effects

After a test that should short-circuit, verify downstream services were not called:

```csharp
A.CallTo(() => _emailService.SendOrderConfirmationAsync(
    A<Guid>._, A<string>._, A<CancellationToken>._))
    .MustNotHaveHappened();
```

### Handling CancellationToken

Always verify your handlers respect cancellation:

```csharp
[Fact]
public async Task Respect_cancellation()
{
    // Arrange
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    A.CallTo(() => _repository.SaveAsync(A<Order>._, A<CancellationToken>._))
        .ThrowsAsync(new OperationCanceledException());

    // Act & Assert
    await Should.ThrowAsync<OperationCanceledException>(
        _handler.HandleAsync(
            new CreateOrderAction("C1", ["item"]),
            cts.Token));
}
```

## Test Organization

Organize tests to mirror your source structure:

```
tests/
├── YourApp.Tests/
│   ├── GlobalUsings.cs
│   ├── Handlers/
│   │   ├── CreateOrderHandlerShould.cs
│   │   ├── GetOrderHandlerShould.cs
│   │   └── SendOrderConfirmationHandlerShould.cs
│   ├── Middleware/
│   │   ├── CorrelationIdMiddlewareShould.cs
│   │   └── TenantMiddlewareShould.cs
│   ├── Pipeline/
│   │   └── OrderPipelineShould.cs
│   └── TestDoubles/
│       └── TestMessageContext.cs
```

**Naming convention:** `{ClassUnderTest}Should` with test methods named as plain-English sentences: `Save_new_order_to_repository`, `Throw_when_order_not_found`.

## Next Steps

- [Actions and Handlers](../core-concepts/actions-and-handlers.md) -- Handler interface reference
- [Middleware](../middleware/index.md) -- Built-in and custom middleware
- [Idempotent Consumer Guide](../patterns/idempotent-consumer.md) -- Testing idempotency behavior
- [Aggregate Testing](./aggregate-testing.md) -- Testing event-sourced aggregates with Given-When-Then

## See Also

- [Test Harness](./test-harness.md) -- DispatchTestHarness for full-pipeline testing with DI and message tracking
- [Validation Middleware](../middleware/validation.md) -- Built-in validation middleware tested in this guide
- [Shouldly Assertions](./shouldly-assertions.md) -- Fluent assertion extensions for Dispatch testing types
