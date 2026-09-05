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

- **.NET 10.0**
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

The `ValidationMiddleware` runs at the `DispatchMiddlewareStage.Validation` stage. Every applicable source
runs and **accumulates** its errors — none of them short-circuits another:

1. The registered validator, if the resolver (`IValidatorResolver.TryValidate`) claims the message type —
   this is what `WithFluentValidation()`/`WithAotFluentValidation()`/`WithDataAnnotationsValidation()`
   supplies. Runs when `UseCustomValidation` is enabled (default: `true`).
2. Self-validation: when the message implements `IValidate`, its `Validate()` method is called. Runs when
   `UseCustomValidation` is enabled.
3. `System.ComponentModel.DataAnnotations` attributes, when `UseDataAnnotations` is enabled (default: `true`).
4. The pluggable `IMessageValidationService`, when `UseCustomValidation` is enabled.

A message that both has a registered validator and carries `[Required]`-style attributes is subject to
**both** — registering a validator does not disable declarative attributes on the same message. This
matches how ASP.NET Core model validation and `Microsoft.Extensions.Validation` combine DataAnnotations
and `IValidatableObject` rather than letting one suppress the other. If `StopOnFirstError` is enabled, the
accumulated errors are trimmed to the first one after every source has run.

Dispatch validation is a separate moment from ASP.NET Core model validation: ASP.NET validates the bound
request model at model binding, while this middleware validates the message when it is dispatched. Do not
expect one to substitute for the other — both end up in the same problem-details wire shape (see
[Validation Results](#validation-results) below).

If validation fails, the middleware throws `Excalibur.Dispatch.Exceptions.ValidationException`. The
handler is never invoked, and the exception propagates out of `DispatchAsync` to the caller — it is not
surfaced as `IMessageResult.IsSuccess == false`.

:::note The local fast path defers to your validation
Dispatch takes an internal fast path for local messages, but only when no middleware applies to that
message type. Registering validation middleware means it applies to the types it covers, so those
dispatches take the pipeline and your validators run. There is nothing to opt into, no path to route
around, and no reason to duplicate validation inside handlers to defend against the fast path.
:::

## Setup

:::warning Call `UseValidation()` — registering a validator is not enough
`UseValidation()` is what puts `ValidationMiddleware` into the pipeline. Registering a validator, or
calling `WithFluentValidation()` on its own, gives the middleware something to call but never places the
middleware anywhere — so nothing calls it.

The failure is quiet. `ValidationMiddleware` appears in the default pipeline profile as an **optional**
entry, which means a container that has not registered it drops that entry while building the pipeline
and records the skip at `Debug` level. Under a normal production logging configuration nobody sees it.
The symptom is that validators are constructed, resolve correctly, and are never invoked — invalid
messages reach handlers and no error is reported anywhere.

If you are unsure whether validation is live, assert it rather than infer it: resolve
`ValidationMiddleware` from your built container in a test. If it does not resolve, it is not running.
:::

### DataAnnotations (Zero Dependencies)

DataAnnotations validation uses only `System.ComponentModel.DataAnnotations` from the BCL — no external packages required.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDispatch(builder =>
{
    builder.UseValidation();
    builder.WithDataAnnotationsValidation();
});
```

`WithDataAnnotationsValidation()` replaces the default `NoOpValidatorResolver` with `DataAnnotationsValidatorResolver`, which calls `Validator.TryValidateObject` on every message.

:::note
`DataAnnotationsValidatorResolver` and the middleware's built-in `UseDataAnnotations` pass read the same
`System.ComponentModel.DataAnnotations` attributes, so `WithDataAnnotationsValidation()` sets
`UseDataAnnotations` to `false` for you — attribute evaluation belongs to exactly one source and a violated
attribute is reported once. If you want both passes, set `options.UseDataAnnotations = true` after calling
it; the later configuration wins.
:::

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
    builder.UseValidation();
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
using Excalibur.Dispatch;

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

Messages can validate themselves by implementing `IValidate`. When `UseCustomValidation` is enabled, the
middleware always calls `Validate()` on a message that implements it — in addition to, not instead of, a
registered resolver or DataAnnotations attributes:

```csharp
using Excalibur.Dispatch.Validation;

public record CreateOrderCommand(decimal Amount, string Currency)
    : IDispatchAction, IValidate
{
    public ValidationResult Validate()
    {
        if (Amount <= 0)
            return ValidationResult.Failure("Amount must be positive");

        if (string.IsNullOrWhiteSpace(Currency))
            return ValidationResult.Failure("Currency is required");

        return ValidationResult.Success();
    }
}
```

## Validation Results

When validation fails, the middleware throws `Excalibur.Dispatch.Exceptions.ValidationException` rather
than returning a failed `IMessageResult` — catch it around the dispatch call:

```csharp
try
{
    var result = await dispatcher.DispatchAsync(action, ct);
}
catch (Excalibur.Dispatch.Exceptions.ValidationException ex)
{
    // ex.DispatchStatusCode == 400
    // ex.ValidationErrors is IDictionary<string, string[]>, keyed by property name.
    // An error not attributable to a single property uses the empty-string key,
    // matching ASP.NET Core's ModelState.
}
```

In an ASP.NET Core host built on `Excalibur.Hosting.Web`, you don't need to catch it yourself: the
`GlobalExceptionHandler` catches `ValidationException`, sets the response status from
`DispatchStatusCode` (400), and projects `ValidationErrors` into the RFC 9457 `errors` extension member —
the same shape ASP.NET Core's own `ValidationProblemDetails.Errors` uses, so a client that already reads
`response.errors` works against either source.

A `FluentValidation.ValidationException` thrown directly by your own code — rather than raised through the
dispatch pipeline — is projected onto that same `errors` member and the same per-property shape, so the
response contract does not depend on which path rejected the request.

### ValidationError

When you author a custom `IValidatorResolver` or an `IValidate.Validate()` implementation, individual
errors are represented as `ValidationError` instances (namespace `Excalibur.Dispatch.Validation`), passed
to `ValidationResult.Failure(...)`:

```csharp
public sealed class ValidationError
{
    public string? PropertyName { get; }
    public string Message { get; }
    public string? ErrorCode { get; set; }
    public IDictionary<string, object>? Metadata { get; init; }
}
```

These are mapped onto the `IDictionary<string, string[]>` shape shown above before they reach the caller
as `ValidationException.ValidationErrors`.

## Configuration

### ValidationOptions

`ValidationMiddleware` is configured via `Excalibur.Dispatch.Options.Middleware.ValidationOptions`:

```csharp
services.Configure<Excalibur.Dispatch.Options.Middleware.ValidationOptions>(options =>
{
    options.Enabled = true;               // Enable/disable validation entirely (default: true)
    options.UseDataAnnotations = true;    // Evaluate DataAnnotations attributes (default: true)
    options.UseCustomValidation = true;   // Run the resolver, IValidate, and IMessageValidationService (default: true)
    options.StopOnFirstError = false;     // Trim accumulated errors to the first one (default: false)
    options.BypassValidationForTypes = new[] { "HealthCheckAction" }; // Skip validation for these message type names
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
