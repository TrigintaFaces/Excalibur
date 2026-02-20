// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

// ============================================================================
// FluentValidation Sample - Validation Integration with Dispatch
// ============================================================================
// This sample demonstrates how to integrate FluentValidation with the Dispatch
// framework for comprehensive message validation before processing.
//
// Key concepts demonstrated:
// - FluentValidation integration via WithFluentValidation()
// - Basic validation rules (required, length, regex)
// - Password complexity validation
// - Conditional validation with When()
// - Cross-field validation with Must()
// - Custom validation rules (business logic checks)
// - Validation error handling
// ============================================================================

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Validation;

using FluentValidation;

using FluentValidationSample.Commands;
using FluentValidationSample.Validators;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("=================================================");
Console.WriteLine("  FluentValidation Sample - Dispatch Integration");
Console.WriteLine("=================================================");
Console.WriteLine();

// Step 1: Configure services
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

// Add Dispatch with handlers and validation middleware
services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	_ = dispatch.AddDispatchValidation() // Register ValidationMiddleware
		.WithFluentValidation(); // Use FluentValidation as the validator resolver
});

// Register FluentValidation validators from this assembly
services.AddValidatorsFromAssemblyContaining<CreateUserCommandValidator>();

// Build the service provider
var provider = services.BuildServiceProvider();

// Step 2: Get the dispatcher
var dispatcher = provider.GetRequiredService<IDispatcher>();

// Create a message context
var context = DispatchContextInitializer.CreateDefaultContext(provider);

// ============================================================================
// Demo 1: Basic Validation (CreateUserCommand)
// ============================================================================
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 1: Basic Validation Rules                ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();

// Valid user command
Console.WriteLine("1a. Valid CreateUserCommand:");
var validUser = new CreateUserCommand(
	Username: "john_doe",
	Email: "john@example.com",
	Password: "SecureP@ss1",
	Age: 25);

var result = await dispatcher.DispatchAsync(validUser, context, CancellationToken.None);
PrintResult(result);

// Invalid user command - multiple validation failures
Console.WriteLine("1b. Invalid CreateUserCommand (multiple errors):");
var invalidUser = new CreateUserCommand(
	Username: "ab", // Too short (min 3)
	Email: "not-an-email", // Invalid email
	Password: "weak", // Missing uppercase, digit, special char
	Age: 10); // Under 13

result = await dispatcher.DispatchAsync(invalidUser, context, CancellationToken.None);
PrintResult(result);

// ============================================================================
// Demo 2: Conditional and Cross-Field Validation (CreateOrderCommand)
// ============================================================================
Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 2: Conditional & Cross-Field Validation  ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();

// Valid order with promo code
Console.WriteLine("2a. Valid CreateOrderCommand with promo code:");
var validOrder = new CreateOrderCommand(
	CustomerId: Guid.NewGuid().ToString(),
	ProductId: Guid.NewGuid().ToString(),
	Quantity: 5,
	UnitPrice: 99.99m,
	ShippingAddress: "123 Main Street, Anytown, ST 12345",
	PromoCode: "SUMMER2026");

result = await dispatcher.DispatchAsync(validOrder, context, CancellationToken.None);
PrintResult(result);

// Invalid promo code
Console.WriteLine("2b. Invalid promo code:");
var invalidPromo = new CreateOrderCommand(
	CustomerId: Guid.NewGuid().ToString(),
	ProductId: Guid.NewGuid().ToString(),
	Quantity: 3,
	UnitPrice: 50.00m,
	ShippingAddress: "456 Oak Avenue, Somewhere, ST 67890",
	PromoCode: "INVALID_CODE");

result = await dispatcher.DispatchAsync(invalidPromo, context, CancellationToken.None);
PrintResult(result);

// Order total exceeds limit (cross-field validation)
Console.WriteLine("2c. Order total exceeds limit (cross-field):");
var excessiveOrder = new CreateOrderCommand(
	CustomerId: Guid.NewGuid().ToString(),
	ProductId: Guid.NewGuid().ToString(),
	Quantity: 100,
	UnitPrice: 600.00m, // 100 * 600 = 60,000 > 50,000 limit
	ShippingAddress: "789 Pine Road, Elsewhere, ST 11111",
	PromoCode: null);

result = await dispatcher.DispatchAsync(excessiveOrder, context, CancellationToken.None);
PrintResult(result);

// ============================================================================
// Demo 3: Optional Field Validation (UpdateProfileCommand)
// ============================================================================
Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 3: Optional Field Validation             ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();

// Valid partial update
Console.WriteLine("3a. Valid partial profile update:");
var validUpdate = new UpdateProfileCommand(
	UserId: Guid.NewGuid().ToString(),
	DisplayName: "John Doe",
	Bio: null,
	WebsiteUrl: "https://example.com",
	PhoneNumber: null);

result = await dispatcher.DispatchAsync(validUpdate, context, CancellationToken.None);
PrintResult(result);

// Invalid - no fields to update
Console.WriteLine("3b. Invalid - no fields to update:");
var emptyUpdate = new UpdateProfileCommand(
	UserId: Guid.NewGuid().ToString(),
	DisplayName: null,
	Bio: null,
	WebsiteUrl: null,
	PhoneNumber: null);

result = await dispatcher.DispatchAsync(emptyUpdate, context, CancellationToken.None);
PrintResult(result);

// Invalid URL format
Console.WriteLine("3c. Invalid URL format:");
var invalidUrl = new UpdateProfileCommand(
	UserId: Guid.NewGuid().ToString(),
	DisplayName: "Jane",
	Bio: null,
	WebsiteUrl: "not-a-valid-url",
	PhoneNumber: null);

result = await dispatcher.DispatchAsync(invalidUrl, context, CancellationToken.None);
PrintResult(result);

// Invalid phone number
Console.WriteLine("3d. Invalid phone number format:");
var invalidPhone = new UpdateProfileCommand(
	UserId: Guid.NewGuid().ToString(),
	DisplayName: null,
	Bio: "I love coding!",
	WebsiteUrl: null,
	PhoneNumber: "123"); // Too short

result = await dispatcher.DispatchAsync(invalidPhone, context, CancellationToken.None);
PrintResult(result);

// ============================================================================
// Demo 4: Custom Validation (RegisterEmailCommand)
// ============================================================================
Console.WriteLine();
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║  Demo 4: Custom Validation & Business Rules    ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.WriteLine();

// Valid registration
Console.WriteLine("4a. Valid email registration:");
var validRegistration = new RegisterEmailCommand(
	Email: "newuser@example.com",
	AcceptTerms: true,
	ReferralCode: "REF2026");

result = await dispatcher.DispatchAsync(validRegistration, context, CancellationToken.None);
PrintResult(result);

// Duplicate email (custom rule)
Console.WriteLine("4b. Duplicate email (business rule check):");
var duplicateEmail = new RegisterEmailCommand(
	Email: "admin@example.com", // Already exists in mock data
	AcceptTerms: true,
	ReferralCode: null);

result = await dispatcher.DispatchAsync(duplicateEmail, context, CancellationToken.None);
PrintResult(result);

// Terms not accepted
Console.WriteLine("4c. Terms not accepted:");
var noTerms = new RegisterEmailCommand(
	Email: "another@example.com",
	AcceptTerms: false,
	ReferralCode: null);

result = await dispatcher.DispatchAsync(noTerms, context, CancellationToken.None);
PrintResult(result);

// Invalid referral code (custom rule)
Console.WriteLine("4d. Invalid referral code (business rule check):");
var invalidReferral = new RegisterEmailCommand(
	Email: "referred@example.com",
	AcceptTerms: true,
	ReferralCode: "FAKE_CODE");

result = await dispatcher.DispatchAsync(invalidReferral, context, CancellationToken.None);
PrintResult(result);

// ============================================================================
// Summary
// ============================================================================
Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine("  Sample Complete!");
Console.WriteLine("=================================================");
Console.WriteLine();
Console.WriteLine("Key takeaways:");
Console.WriteLine("- AddDispatchValidation() registers the validation middleware");
Console.WriteLine("- WithFluentValidation() enables FluentValidation as resolver");
Console.WriteLine("- Validators run before handlers in the pipeline");
Console.WriteLine("- Use When() for conditional validation");
Console.WriteLine("- Use Must() for cross-field and custom validation");
Console.WriteLine("- Validation errors are returned in MessageResult.ProblemDetails");
Console.WriteLine();

// ============================================================================
// Helper Methods
// ============================================================================

static void PrintResult(IMessageResult result)
{
	if (result.Succeeded)
	{
		Console.WriteLine("  [PASS] Validation succeeded - Handler executed");
	}
	else
	{
		Console.WriteLine("  [FAIL] Validation failed:");
		if (result.ProblemDetails?.Extensions.TryGetValue("errors", out var errorsObj) == true
			&& errorsObj is IEnumerable<object> errors)
		{
			foreach (var error in errors)
			{
				// Cast to ValidationError to get property name and message
				if (error is Excalibur.Dispatch.Abstractions.Validation.ValidationError ve)
				{
					var prop = string.IsNullOrEmpty(ve.PropertyName) ? "(General)" : ve.PropertyName;
					Console.WriteLine($"    - [{prop}]: {ve.Message}");
				}
				else
				{
					Console.WriteLine($"    - {error}");
				}
			}
		}
		else if (!string.IsNullOrEmpty(result.ErrorMessage))
		{
			Console.WriteLine($"    - {result.ErrorMessage}");
		}
	}

	Console.WriteLine();
}
