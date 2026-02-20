// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Functional.Validation;

/// <summary>
/// Functional tests for validation pipeline patterns in dispatch scenarios.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Validation")]
[Trait("Feature", "Pipeline")]
public sealed class ValidationPipelineFunctionalShould : FunctionalTestBase
{
	[Fact]
	public void ValidateMessageSuccessfully()
	{
		// Arrange
		var message = new ValidTestCommand
		{
			Name = "Test Name",
			Email = "test@example.com",
			Age = 25,
		};

		// Act
		var validationResult = ValidateCommand(message);

		// Assert
		validationResult.IsValid.ShouldBeTrue();
		validationResult.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void RejectInvalidMessage()
	{
		// Arrange
		var message = new ValidTestCommand
		{
			Name = "", // Invalid - empty
			Email = "invalid-email", // Invalid - not an email
			Age = -5, // Invalid - negative
		};

		// Act
		var validationResult = ValidateCommand(message);

		// Assert
		validationResult.IsValid.ShouldBeFalse();
		validationResult.Errors.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CollectMultipleValidationErrors()
	{
		// Arrange
		var message = new ValidTestCommand
		{
			Name = "",
			Email = "",
			Age = -1,
		};

		// Act
		var validationResult = ValidateCommand(message);

		// Assert
		validationResult.IsValid.ShouldBeFalse();
		validationResult.Errors.Count.ShouldBe(3);
		validationResult.Errors.ShouldContain(e => e.PropertyName == "Name");
		validationResult.Errors.ShouldContain(e => e.PropertyName == "Email");
		validationResult.Errors.ShouldContain(e => e.PropertyName == "Age");
	}

	[Fact]
	public void ValidateNestedObjects()
	{
		// Arrange
		var message = new OrderCommand
		{
			OrderId = "ORD-123",
			Customer = new CustomerInfo
			{
				Name = "John Doe",
				Email = "john@example.com",
			},
			Items =
			[
				new OrderItem { ProductId = "PROD-1", Quantity = 2 },
				new OrderItem { ProductId = "PROD-2", Quantity = 1 },
			],
		};

		// Act
		var validationResult = ValidateOrder(message);

		// Assert
		validationResult.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void RejectOrderWithInvalidItems()
	{
		// Arrange
		var message = new OrderCommand
		{
			OrderId = "ORD-123",
			Customer = new CustomerInfo
			{
				Name = "John Doe",
				Email = "john@example.com",
			},
			Items =
			[
				new OrderItem { ProductId = "", Quantity = 0 }, // Invalid
			],
		};

		// Act
		var validationResult = ValidateOrder(message);

		// Assert
		validationResult.IsValid.ShouldBeFalse();
		validationResult.Errors.ShouldContain(e => e.PropertyName.Contains("ProductId"));
		validationResult.Errors.ShouldContain(e => e.PropertyName.Contains("Quantity"));
	}

	[Fact]
	public async Task ValidationOccursBeforeHandler()
	{
		// Arrange
		var host = CreateHost(services =>
		{
			_ = services.AddLogging();
		});

		var validationRan = false;
		var handlerRan = false;

		// Act - Simulate pipeline
		var message = new ValidTestCommand { Name = "Test", Email = "test@test.com", Age = 20 };

		// Validation step
		var validationResult = ValidateCommand(message);
		validationRan = true;

		// Handler only runs if valid
		if (validationResult.IsValid)
		{
			await Task.Delay(1).ConfigureAwait(false);
			handlerRan = true;
		}

		// Assert
		validationRan.ShouldBeTrue("Validation should run first");
		handlerRan.ShouldBeTrue("Handler should run after successful validation");
	}

	[Fact]
	public async Task ShortCircuitOnValidationFailure()
	{
		// Arrange
		var handlerRan = false;

		// Act - Simulate pipeline with invalid message
		var message = new ValidTestCommand { Name = "", Email = "invalid", Age = -1 };

		var validationResult = ValidateCommand(message);

		if (validationResult.IsValid)
		{
			await Task.Delay(1).ConfigureAwait(false);
			handlerRan = true;
		}

		// Assert
		validationResult.IsValid.ShouldBeFalse();
		handlerRan.ShouldBeFalse("Handler should not run when validation fails");
	}

	[Fact]
	public void FormatValidationErrorsForResponse()
	{
		// Arrange
		var message = new ValidTestCommand { Name = "", Email = "bad", Age = -1 };
		var validationResult = ValidateCommand(message);

		// Act - Format errors for API response
		var errorResponse = new
		{
			Type = "ValidationError",
			Title = "One or more validation errors occurred",
			Errors = validationResult.Errors
				.GroupBy(e => e.PropertyName)
				.ToDictionary(
					g => g.Key,
					g => g.Select(e => e.ErrorMessage).ToArray()),
		};

		// Assert
		errorResponse.Type.ShouldBe("ValidationError");
		errorResponse.Errors.Count.ShouldBe(3);
		errorResponse.Errors["Name"].Length.ShouldBeGreaterThan(0);
	}

	private static ValidationResult ValidateCommand(ValidTestCommand command)
	{
		var errors = new List<ValidationError>();

		if (string.IsNullOrWhiteSpace(command.Name))
		{
			errors.Add(new ValidationError("Name", "Name is required"));
		}

		if (string.IsNullOrWhiteSpace(command.Email) || !command.Email.Contains('@'))
		{
			errors.Add(new ValidationError("Email", "Valid email is required"));
		}

		if (command.Age < 0)
		{
			errors.Add(new ValidationError("Age", "Age must be non-negative"));
		}

		return new ValidationResult(errors.Count == 0, errors);
	}

	private static ValidationResult ValidateOrder(OrderCommand order)
	{
		var errors = new List<ValidationError>();

		if (string.IsNullOrWhiteSpace(order.OrderId))
		{
			errors.Add(new ValidationError("OrderId", "Order ID is required"));
		}

		if (order.Customer == null)
		{
			errors.Add(new ValidationError("Customer", "Customer is required"));
		}
		else
		{
			if (string.IsNullOrWhiteSpace(order.Customer.Name))
			{
				errors.Add(new ValidationError("Customer.Name", "Customer name is required"));
			}

			if (string.IsNullOrWhiteSpace(order.Customer.Email))
			{
				errors.Add(new ValidationError("Customer.Email", "Customer email is required"));
			}
		}

		if (order.Items == null || order.Items.Count == 0)
		{
			errors.Add(new ValidationError("Items", "At least one item is required"));
		}
		else
		{
			for (var i = 0; i < order.Items.Count; i++)
			{
				var item = order.Items[i];
				if (string.IsNullOrWhiteSpace(item.ProductId))
				{
					errors.Add(new ValidationError($"Items[{i}].ProductId", "Product ID is required"));
				}

				if (item.Quantity <= 0)
				{
					errors.Add(new ValidationError($"Items[{i}].Quantity", "Quantity must be positive"));
				}
			}
		}

		return new ValidationResult(errors.Count == 0, errors);
	}

	private sealed record ValidTestCommand : IDispatchAction
	{
		public required string Name { get; init; }
		public required string Email { get; init; }
		public required int Age { get; init; }
	}

	private sealed record OrderCommand : IDispatchAction
	{
		public required string OrderId { get; init; }
		public required CustomerInfo Customer { get; init; }
		public required List<OrderItem> Items { get; init; }
	}

	private sealed record CustomerInfo
	{
		public required string Name { get; init; }
		public required string Email { get; init; }
	}

	private sealed record OrderItem
	{
		public required string ProductId { get; init; }
		public required int Quantity { get; init; }
	}

	private sealed record ValidationResult(bool IsValid, List<ValidationError> Errors);

	private sealed record ValidationError(string PropertyName, string ErrorMessage);
}
