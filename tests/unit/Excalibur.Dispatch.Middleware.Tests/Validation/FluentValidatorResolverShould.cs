// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Validation.FluentValidation;

using FluentValidation;
using DispatchValidationError = Excalibur.Dispatch.Abstractions.Validation.ValidationError;

namespace Excalibur.Dispatch.Middleware.Tests.Validation;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FluentValidatorResolverShould
{
	#region Test Messages

	private sealed record TestMessage(string Name, int Age) : IDispatchMessage;

	private sealed record MessageWithoutValidator(string Data) : IDispatchMessage;

	private sealed record MessageWithMultipleFields(string Name, string Email, int Age) : IDispatchMessage;

	#endregion Test Messages

	#region Test Validators

	private sealed class TestMessageValidator : AbstractValidator<TestMessage>
	{
		public TestMessageValidator()
		{
			_ = RuleFor(x => x.Name)
				.NotEmpty()
				.WithMessage("Name is required")
				.MaximumLength(50)
				.WithMessage("Name must not exceed 50 characters");

			_ = RuleFor(x => x.Age)
				.InclusiveBetween(18, 120)
				.WithMessage("Age must be between 18 and 120");
		}
	}

	private sealed class MultiFieldValidator : AbstractValidator<MessageWithMultipleFields>
	{
		public MultiFieldValidator()
		{
			_ = RuleFor(x => x.Name)
				.NotEmpty()
				.WithMessage("Name is required");

			_ = RuleFor(x => x.Email)
				.NotEmpty()
				.WithMessage("Email is required")
				.EmailAddress()
				.WithMessage("Invalid email format");

			_ = RuleFor(x => x.Age)
				.GreaterThan(0)
				.WithMessage("Age must be positive");
		}
	}

	private sealed class TestMessageValidatorWithErrorCode : AbstractValidator<TestMessage>
	{
		public TestMessageValidatorWithErrorCode()
		{
			_ = RuleFor(x => x.Name)
				.NotEmpty()
				.WithMessage("Name is required")
				.WithErrorCode("ERR_NAME_REQUIRED");
		}
	}

	private sealed class SecondTestMessageValidator : AbstractValidator<TestMessage>
	{
		public SecondTestMessageValidator()
		{
			_ = RuleFor(x => x.Age)
				.GreaterThan(0)
				.WithMessage("Age must be positive")
				.WithErrorCode("ERR_AGE_POSITIVE");
		}
	}

	#endregion Test Validators

	#region Helper Methods

	private static IServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
	{
		var services = new ServiceCollection();
		configure?.Invoke(services);
		return services.BuildServiceProvider();
	}

	private static DispatchValidationError GetFirstError(SerializableValidationResult result)
	{
		return (DispatchValidationError)result.Errors.First();
	}

	private static List<DispatchValidationError> GetAllErrors(SerializableValidationResult result)
	{
		return [.. result.Errors.Cast<DispatchValidationError>()];
	}

	#endregion Helper Methods

	#region Happy Path Tests

	[Fact]
	public void ReturnNullWhenNoValidatorIsRegistered()
	{
		// Arrange
		var provider = CreateServiceProvider();
		var sut = new FluentValidatorResolver(provider);
		var message = new MessageWithoutValidator("test data");

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnSuccessResultWhenValidationPasses()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage("John Doe", 25);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void ReturnSuccessResultWhenAllFieldsAreValid()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<MessageWithMultipleFields>, MultiFieldValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new MessageWithMultipleFields("John", "john@example.com", 25);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	#endregion Happy Path Tests

	#region Validation Failure Tests

	[Fact]
	public void ReturnFailedResultWhenNameIsEmpty()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage("", 25);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldBe("Name is required");
		error.PropertyName.ShouldBe("Name");
	}

	[Fact]
	public void ReturnFailedResultWhenNameIsTooLong()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage(new string('a', 51), 25);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldBe("Name must not exceed 50 characters");
	}

	[Fact]
	public void ReturnFailedResultWhenAgeIsTooLow()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage("John", 10);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldBe("Age must be between 18 and 120");
		error.PropertyName.ShouldBe("Age");
	}

	[Fact]
	public void ReturnFailedResultWhenAgeIsTooHigh()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage("John", 150);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldBe("Age must be between 18 and 120");
	}

	[Fact]
	public void ReturnMultipleErrorsWhenMultipleFieldsFail()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<MessageWithMultipleFields>, MultiFieldValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new MessageWithMultipleFields("", "invalid-email", 0);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public void ReturnFailedResultWhenEmailIsInvalid()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<MessageWithMultipleFields>, MultiFieldValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new MessageWithMultipleFields("John", "not-an-email", 25);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldBe("Invalid email format");
		error.PropertyName.ShouldBe("Email");
	}

	#endregion Validation Failure Tests

	#region Error Code Tests

	[Fact]
	public void IncludeErrorCodeInValidationErrors()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidatorWithErrorCode>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage("", 25);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.ErrorCode.ShouldBe("ERR_NAME_REQUIRED");
	}

	#endregion Error Code Tests

	#region Multiple Validators Tests

	[Fact]
	public void AggregateErrorsFromMultipleValidatorsForSameMessageType()
	{
		// Arrange - Register two different validators for the same message type
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
			_ = services.AddScoped<IValidator<TestMessage>, SecondTestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage("", -5); // Fails both validators

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		// Should have errors from both validators
		result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void ReturnSuccessWhenMultipleValidatorsAllPass()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
			_ = services.AddScoped<IValidator<TestMessage>, SecondTestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage("John", 25); // Passes both validators

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	#endregion Multiple Validators Tests

	#region Boundary Tests

	[Theory]
	[InlineData(18)]
	[InlineData(120)]
	public void ReturnSuccessWhenAgeIsAtBoundary(int age)
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage("John", age);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSuccessWhenNameIsAtMaxLength()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage(new string('a', 50), 25);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
	}

	#endregion Boundary Tests

	#region Property Name Tests

	[Fact]
	public void IncludeCorrectPropertyNameInError()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage("", 25);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.PropertyName.ShouldBe("Name");
	}

	[Fact]
	public void IncludeCorrectPropertyNamesInMultipleErrors()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<MessageWithMultipleFields>, MultiFieldValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new MessageWithMultipleFields("", "", 0);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var errors = GetAllErrors(serializableResult);
		var propertyNames = errors.Select(e => e.PropertyName).ToHashSet();

		propertyNames.ShouldContain("Name");
		propertyNames.ShouldContain("Email");
		propertyNames.ShouldContain("Age");
	}

	#endregion Property Name Tests

	#region Exception Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenMessageIsNull()
	{
		// Arrange
		var provider = CreateServiceProvider();
		var sut = new FluentValidatorResolver(provider);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => sut.TryValidate(null!));
	}

	#endregion Exception Tests

	#region Result Type Tests

	[Fact]
	public void ReturnSerializableValidationResultTypeOnSuccess()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage("John", 25);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		_ = result.ShouldBeAssignableTo<SerializableValidationResult>();
	}

	[Fact]
	public void ReturnSerializableValidationResultTypeOnFailure()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestMessage>, TestMessageValidator>();
		});
		var sut = new FluentValidatorResolver(provider);
		var message = new TestMessage("", 10);

		// Act
		var result = sut.TryValidate(message);

		// Assert
		_ = result.ShouldBeAssignableTo<SerializableValidationResult>();
	}

	#endregion Result Type Tests
}
