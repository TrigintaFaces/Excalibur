# FluentValidation Sample

This sample demonstrates how to integrate [FluentValidation](https://docs.fluentvalidation.net/) with the Dispatch framework for comprehensive message validation.

## Overview

FluentValidation is a popular .NET library for building strongly-typed validation rules. When integrated with Dispatch, validation runs automatically in the message pipeline **before** handlers execute.

## Quick Start

```csharp
// 1. Add Dispatch with FluentValidation
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
}).WithFluentValidation();

// 2. Register validators from your assembly
services.AddValidatorsFromAssemblyContaining<CreateUserCommandValidator>();

// 3. Validation runs automatically when dispatching
var result = await dispatcher.DispatchAsync(command, context, ct);
if (!result.Succeeded)
{
    // Handle validation errors
    foreach (var error in result.ProblemDetails.Errors)
    {
        Console.WriteLine($"{error.Key}: {string.Join(", ", error.Value)}");
    }
}
```

## Validation Patterns Demonstrated

### 1. Basic Validation Rules

```csharp
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters")
            .MaximumLength(50).WithMessage("Username cannot exceed 50 characters")
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("Username can only contain letters, numbers, and underscores");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("A valid email address is required");
    }
}
```

### 2. Password Complexity

```csharp
RuleFor(x => x.Password)
    .NotEmpty().WithMessage("Password is required")
    .MinimumLength(8).WithMessage("Password must be at least 8 characters")
    .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
    .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
    .Matches("[0-9]").WithMessage("Password must contain at least one digit")
    .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");
```

### 3. Conditional Validation

Use `.When()` to apply rules only when certain conditions are met:

```csharp
// Only validate promo code format if one is provided
RuleFor(x => x.PromoCode)
    .Must(BeValidPromoCode).WithMessage("Invalid promo code")
    .When(x => !string.IsNullOrWhiteSpace(x.PromoCode));

// Only validate URL if provided
RuleFor(x => x.WebsiteUrl)
    .Must(BeValidUrl).WithMessage("Please enter a valid URL")
    .When(x => !string.IsNullOrWhiteSpace(x.WebsiteUrl));
```

### 4. Cross-Field Validation

Use `RuleFor(x => x)` with `.Must()` to validate across multiple fields:

```csharp
// Ensure order total doesn't exceed limit
RuleFor(x => x)
    .Must(HaveReasonableOrderTotal).WithMessage("Order total cannot exceed $50,000")
    .WithName("OrderTotal");

private static bool HaveReasonableOrderTotal(CreateOrderCommand command)
{
    var total = command.Quantity * command.UnitPrice;
    return total <= 50000;
}

// Ensure at least one field is provided for update
RuleFor(x => x)
    .Must(HaveAtLeastOneFieldToUpdate).WithMessage("At least one field must be provided")
    .WithName("Profile");
```

### 5. Custom Validation Methods

```csharp
RuleFor(x => x.UserId)
    .Must(BeValidGuid).WithMessage("User ID must be a valid GUID");

private static bool BeValidGuid(string value)
{
    return Guid.TryParse(value, out _);
}

// Using source-generated regex (AOT-friendly)
RuleFor(x => x.PhoneNumber)
    .Matches(PhoneNumberPattern()).WithMessage("Please enter a valid phone number")
    .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

[GeneratedRegex(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$")]
private static partial Regex PhoneNumberPattern();
```

## Project Structure

```
FluentValidation/
├── Commands/
│   └── Commands.cs           # Command definitions
├── Validators/
│   ├── CreateUserCommandValidator.cs      # Basic validation rules
│   ├── CreateOrderCommandValidator.cs     # Conditional & cross-field
│   ├── UpdateProfileCommandValidator.cs   # Optional field validation
│   └── RegisterEmailCommandValidator.cs   # Async validation
├── Handlers/
│   └── Handlers.cs           # Command handlers
├── Program.cs                # Demo scenarios
└── README.md                 # This file
```

## How It Works

1. **Registration**: `WithFluentValidation()` registers `FluentValidatorResolver` as the `IValidatorResolver`
2. **Discovery**: `AddValidatorsFromAssemblyContaining<T>()` registers all validators with DI
3. **Pipeline**: `ValidationMiddleware` intercepts messages before handlers
4. **Resolution**: `FluentValidatorResolver` looks up `IValidator<T>` from the service provider
5. **Execution**: Validation runs, and failures short-circuit the pipeline
6. **Result**: Validation errors are returned in `MessageResult.ProblemDetails.Errors`

## Error Handling

Validation failures return a `MessageResult` with:
- `Succeeded = false`
- `ProblemDetails.Errors` - Dictionary of field names to error messages

```csharp
var result = await dispatcher.DispatchAsync(command, context, ct);

if (!result.Succeeded && result.ProblemDetails?.Errors != null)
{
    foreach (var (field, messages) in result.ProblemDetails.Errors)
    {
        Console.WriteLine($"{field}: {string.Join(", ", messages)}");
    }
}
```

## Best Practices

1. **One Validator Per Command**: Create a dedicated validator class for each command type
2. **Descriptive Error Messages**: Always use `.WithMessage()` to provide user-friendly errors
3. **Conditional Validation**: Use `.When()` for optional fields to avoid false positives
4. **Async for External Calls**: Use `.MustAsync()` for database/API validation checks
5. **Source-Generated Regex**: Use `[GeneratedRegex]` for AOT compatibility and performance
6. **Cross-Field Validation**: Use `RuleFor(x => x).Must()` for multi-field constraints

## Running the Sample

```bash
cd samples/09-advanced/FluentValidation
dotnet run
```

## Dependencies

- `Dispatch` - Core messaging framework
- `Excalibur.Dispatch.Abstractions` - Interfaces and contracts
- `Excalibur.Dispatch.Validation.FluentValidation` - FluentValidation integration
- `FluentValidation` - Validation library
- `FluentValidation.DependencyInjectionExtensions` - DI support
