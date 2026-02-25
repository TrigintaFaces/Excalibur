---
sidebar_position: 4
title: Validation
description: Pipeline validation middleware with pluggable providers — built-in DataAnnotations or FluentValidation via a separate package.
---

# Validation

Dispatch includes a `ValidationMiddleware` that validates messages before they reach handlers. It supports three validation approaches that can be used together:

| Approach | Package | Dependencies |
|----------|---------|--------------|
| **DataAnnotations** | `Excalibur.Dispatch` (built-in) | None (BCL only) |
| **FluentValidation** | `Excalibur.Dispatch.Validation.FluentValidation` | [FluentValidation](https://docs.fluentvalidation.net/) |
| **Self-validation** | `Excalibur.Dispatch` (built-in) | None — implement `IValidate` on your message |

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- For FluentValidation support:
  ```bash
  dotnet add package Excalibur.Dispatch.Validation.FluentValidation
  ```
- Familiarity with [middleware concepts](index.md) and [pipeline stages](../pipeline/index.md)

## How It Works

The `ValidationMiddleware` runs at the `DispatchMiddlewareStage.Validation` stage. It uses an `IValidatorResolver` to find and execute validators for the incoming message:

1. The resolver (`IValidatorResolver.TryValidate`) is called first. If it returns a result, that result is used.
2. If no resolver handles the message, the middleware checks whether the message implements `IValidate` and calls `Validate()`.
3. Finally, `System.ComponentModel.DataAnnotations` attributes are evaluated.

If validation fails, the middleware returns a `MessageResult.Failed` with `MessageProblemDetails` (status 400) containing the errors. The handler is never invoked.

## Setup

### DataAnnotations (Zero Dependencies)

DataAnnotations validation uses only `System.ComponentModel.DataAnnotations` from the BCL — no external packages required.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDispatch(builder =>
{
    builder.AddDispatchValidation();
    builder.WithDataAnnotationsValidation();
});
```

`WithDataAnnotationsValidation()` replaces the default `NoOpValidatorResolver` with `DataAnnotationsValidatorResolver`, which calls `Validator.TryValidateObject` on every message.

### FluentValidation (Separate Package)

For richer validation rules, install the FluentValidation integration package:

```bash
dotnet add package Excalibur.Dispatch.Validation.FluentValidation
```

Then register it:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDispatch(builder =>
{
    builder.AddDispatchValidation();
    builder.WithFluentValidation();
});

// Register your FluentValidation validators
services.AddValidatorsFromAssembly(typeof(Program).Assembly);
```

`WithFluentValidation()` registers `FluentValidatorResolver` as the `IValidatorResolver`. It resolves `IValidator<T>` instances from the DI container for the incoming message type and executes them.

For AOT scenarios, use `WithAotFluentValidation()` instead, which registers `AotFluentValidatorResolver` designed for Native AOT compilation.

## DataAnnotations Examples

### Attribute-Based Validation

```csharp
using System.ComponentModel.DataAnnotations;
using Excalibur.Dispatch.Abstractions;

public record CreateOrderAction(
    [Required]
    [StringLength(50, MinimumLength = 1)]
    string CustomerId,

    [Required]
    [MinLength(1, ErrorMessage = "At least one item required")]
    List<OrderItem> Items,

    [Range(0, 1_000_000)]
    decimal MaxAmount,

    [EmailAddress]
    string? NotificationEmail
) : IDispatchAction;

public record OrderItem(
    [Required] string ProductId,
    [Range(1, 1000)] int Quantity,
    [Range(0.01, 999999.99)] decimal UnitPrice
);
```

### Custom Validation Attributes

```csharp
public class FutureDateAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(
        object? value,
        ValidationContext context)
    {
        if (value is DateTime date && date <= DateTime.UtcNow)
        {
            return new ValidationResult("Date must be in the future");
        }

        return ValidationResult.Success;
    }
}

public record ScheduleOrderAction(
    [Required] string OrderId,
    [FutureDate] DateTime ScheduledDate
) : IDispatchAction;
```

### IValidatableObject

For cross-property validation with DataAnnotations, implement `IValidatableObject` from `System.ComponentModel.DataAnnotations`:

```csharp
using System.ComponentModel.DataAnnotations;

public record CreateOrderAction(
    string CustomerId,
    List<OrderItem> Items,
    decimal Discount
) : IDispatchAction, IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        var total = Items.Sum(i => i.Quantity * i.UnitPrice);

        if (Discount > total)
        {
            yield return new ValidationResult(
                "Discount cannot exceed total",
                [nameof(Discount)]);
        }
    }
}
```

## FluentValidation Examples

### Basic Validator

```csharp
using FluentValidation;

public class CreateOrderValidator : AbstractValidator<CreateOrderAction>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must have at least one item");
    }
}
```

### Async and Conditional Rules

```csharp
public class CreateOrderValidator : AbstractValidator<CreateOrderAction>
{
    public CreateOrderValidator(ICustomerService customerService)
    {
        RuleFor(x => x.CustomerId).NotEmpty();

        // Async validation
        RuleFor(x => x.CustomerId)
            .MustAsync(async (id, ct) =>
                await customerService.ExistsAsync(id, ct))
            .WithMessage("Customer not found");

        // Conditional rules
        RuleFor(x => x.ShippingAddress)
            .NotNull()
            .When(x => x.DeliveryType == DeliveryType.Shipping);

        // Child validators
        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemValidator());
    }
}

public class OrderItemValidator : AbstractValidator<OrderItem>
{
    public OrderItemValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
    }
}
```

## Self-Validation with IValidate

Messages can validate themselves by implementing `IValidate`. The middleware calls `Validate()` when no `IValidatorResolver` handles the message:

```csharp
using Excalibur.Dispatch.Validation;
using Excalibur.Dispatch.Abstractions.Serialization;

public record CreateOrderCommand(decimal Amount, string Currency)
    : IDispatchAction, IValidate
{
    public ValidationResult Validate()
    {
        if (Amount <= 0)
            return SerializableValidationResult.Failed("Amount must be positive");

        if (string.IsNullOrWhiteSpace(Currency))
            return SerializableValidationResult.Failed("Currency is required");

        return ValidationResult.Success();
    }
}
```

## Validation Results

When validation fails, the middleware returns a `MessageProblemDetails` with status 400:

```csharp
var result = await dispatcher.DispatchAsync(action, ct);

if (!result.IsSuccess && result.ProblemDetails is { } problem)
{
    // problem.Title == "Validation Failed"
    // problem.Status == 400
    // problem.Extensions["errors"] contains the error list
}
```

### ValidationError

Errors are represented as `ValidationError` instances (namespace `Excalibur.Dispatch.Abstractions.Validation`):

```csharp
public sealed class ValidationError
{
    public string? PropertyName { get; }
    public string Message { get; }
    public string? ErrorCode { get; set; }
    public IDictionary<string, object>? Metadata { get; init; }
}
```

## Configuration

### ValidationOptions

Configure validation behavior via the options pattern:

```csharp
services.Configure<ValidationOptions>(options =>
{
    options.Enabled = true;                  // Enable/disable validation (default: true)
    options.FailFast = true;                 // Stop on first error (default: true)
    options.MaxErrors = 10;                  // Max errors to collect (default: 10)
    options.IncludeDetailedErrors = true;    // Include detailed error info (default: true)
    options.ValidationTimeout = TimeSpan.FromSeconds(5); // Timeout budget
});
```

There is also a middleware-specific `ValidationOptions` in `Excalibur.Dispatch.Options.Middleware`:

```csharp
services.Configure<Excalibur.Dispatch.Options.Middleware.ValidationOptions>(options =>
{
    options.Enabled = true;               // Enable/disable (default: true)
    options.UseDataAnnotations = true;    // Use DataAnnotations (default: true)
    options.UseCustomValidation = true;   // Use IValidationService (default: true)
    options.StopOnFirstError = false;     // Fail-fast mode (default: false)
    options.BypassValidationForTypes = new[] { "HealthCheckAction" }; // Skip types
});
```

## Testing Validators

### FluentValidation Tests

```csharp
public class CreateOrderValidatorTests
{
    private readonly CreateOrderValidator _validator = new();

    [Fact]
    public void Should_have_error_when_CustomerId_empty()
    {
        var action = new CreateOrderAction(
            CustomerId: "",
            Items: new List<OrderItem>());

        var result = _validator.TestValidate(action);

        result.ShouldHaveValidationErrorFor(x => x.CustomerId);
    }

    [Fact]
    public void Should_pass_when_valid()
    {
        var action = new CreateOrderAction(
            CustomerId: "customer-1",
            Items: new List<OrderItem>
            {
                new("product-1", 1, 10.00m)
            });

        var result = _validator.TestValidate(action);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
```

## Next Steps

- [Authorization](authorization.md) — Permission checks in the pipeline
- [Custom Middleware](custom.md) — Build your own middleware

## See Also

- [Custom Middleware](custom.md) - Build your own middleware for application-specific cross-cutting concerns
- [Middleware Overview](index.md) - Introduction to middleware concepts, stages, and registration
- [Actions and Handlers](../core-concepts/actions-and-handlers.md) - Understanding the message types that validation applies to
