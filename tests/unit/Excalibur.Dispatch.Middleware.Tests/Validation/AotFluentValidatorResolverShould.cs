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
public sealed class AotFluentValidatorResolverShould
{
	#region Test Messages

	private sealed record TestAotMessage(string Name, int Age) : IDispatchMessage;

	private sealed record AotMessageWithoutValidator(string Data) : IDispatchMessage;

	private sealed record AotMessageWithMultipleFields(string Name, string Email, int Age) : IDispatchMessage;

	#endregion Test Messages

	#region Test Validators

	private sealed class TestAotMessageValidator : AbstractValidator<TestAotMessage>
	{
		public TestAotMessageValidator()
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

	private sealed class AotMultiFieldValidator : AbstractValidator<AotMessageWithMultipleFields>
	{
		public AotMultiFieldValidator()
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

	private sealed class TestAotMessageValidatorWithErrorCode : AbstractValidator<TestAotMessage>
	{
		public TestAotMessageValidatorWithErrorCode()
		{
			_ = RuleFor(x => x.Name)
				.NotEmpty()
				.WithMessage("Name is required")
				.WithErrorCode("ERR_NAME_REQUIRED");
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

	#endregion Helper Methods

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new AotFluentValidatorResolver(null!));
	}

	[Fact]
	public void CreateInstanceWithValidProvider()
	{
		// Arrange
		var provider = CreateServiceProvider();

		// Act
		var sut = new AotFluentValidatorResolver(provider);

		// Assert
		Assert.NotNull(sut);
	}

	#endregion Constructor Tests

	#region TryValidate Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenMessageIsNull()
	{
		// Arrange
		var provider = CreateServiceProvider();
		var sut = new AotFluentValidatorResolver(provider);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => sut.TryValidate(null!));
	}

	[Fact]
	public void ThrowNotSupportedFromTryValidateBecauseSourceGeneratorNotPresent()
	{
		// Arrange
		// ValidateMessage always throws NotSupportedException (stub for source generator)
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidator>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("John", 25);

		// Act & Assert
		_ = Should.Throw<NotSupportedException>(() => sut.TryValidate(message));
	}

	[Fact]
	public void ThrowNotSupportedFromTryValidateWhenNoValidatorsRegistered()
	{
		// Arrange
		var provider = CreateServiceProvider();
		var sut = new AotFluentValidatorResolver(provider);
		var message = new AotMessageWithoutValidator("test");

		// Act & Assert
		_ = Should.Throw<NotSupportedException>(() => sut.TryValidate(message));
	}

	#endregion TryValidate Tests

	#region ValidateTyped Tests - No Validators

	[Fact]
	public void ReturnNullFromValidateTypedWhenNoValidatorsRegistered()
	{
		// Arrange
		var provider = CreateServiceProvider();
		var sut = new AotFluentValidatorResolver(provider);
		var message = new AotMessageWithoutValidator("test data");

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.Null(result);
	}

	#endregion ValidateTyped Tests - No Validators

	#region ValidateTyped Tests - Validation Passes

	[Fact]
	public void ReturnSuccessFromValidateTypedWhenValidationPasses()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidator>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("John Doe", 25);

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void ReturnSuccessFromValidateTypedWhenAllFieldsAreValid()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<AotMessageWithMultipleFields>, AotMultiFieldValidator>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new AotMessageWithMultipleFields("John", "john@example.com", 25);

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	#endregion ValidateTyped Tests - Validation Passes

	#region ValidateTyped Tests - Validation Failures

	[Fact]
	public void ReturnFailedFromValidateTypedWhenNameIsEmpty()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidator>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("", 25);

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = (SerializableValidationResult)result;
		var error = (DispatchValidationError)serializableResult.Errors.First();
		error.Message.ShouldBe("Name is required");
		error.PropertyName.ShouldBe("Name");
	}

	[Fact]
	public void ReturnFailedFromValidateTypedWhenAgeIsOutOfRange()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidator>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("John", 10);

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = (SerializableValidationResult)result;
		var error = (DispatchValidationError)serializableResult.Errors.First();
		error.Message.ShouldBe("Age must be between 18 and 120");
		error.PropertyName.ShouldBe("Age");
	}

	[Fact]
	public void ReturnMultipleErrorsFromValidateTypedWhenMultipleFieldsFail()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<AotMessageWithMultipleFields>, AotMultiFieldValidator>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new AotMessageWithMultipleFields("", "invalid-email", 0);

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThanOrEqualTo(3);

		var errors = result.Errors.Cast<DispatchValidationError>().ToList();
		var propertyNames = errors.Select(e => e.PropertyName).ToHashSet();
		propertyNames.ShouldContain("Name");
		propertyNames.ShouldContain("Email");
		propertyNames.ShouldContain("Age");
	}

	[Fact]
	public void IncludeErrorCodeFromValidateTypedWhenValidatorSetsErrorCode()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidatorWithErrorCode>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("", 25);

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = (SerializableValidationResult)result;
		var error = (DispatchValidationError)serializableResult.Errors.First();
		error.ErrorCode.ShouldBe("ERR_NAME_REQUIRED");
	}

	#endregion ValidateTyped Tests - Validation Failures

	#region ValidateTyped Tests - Null Message

	[Fact]
	public void ThrowArgumentNullExceptionFromValidateTypedWhenMessageIsNull()
	{
		// Arrange
		var provider = CreateServiceProvider();
		var sut = new AotFluentValidatorResolver(provider);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => sut.ValidateTyped<TestAotMessage>(null!));
	}

	#endregion ValidateTyped Tests - Null Message

	#region ValidateTyped Tests - Boundary

	[Theory]
	[InlineData(18)]
	[InlineData(120)]
	public void ReturnSuccessFromValidateTypedWhenAgeIsAtBoundary(int age)
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidator>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("John", age);

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSuccessFromValidateTypedWhenNameIsExactlyMaxLength()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidator>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage(new string('a', 50), 25);

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFailedFromValidateTypedWhenNameExceedsMaxLength()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidator>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage(new string('a', 51), 25);

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
	}

	#endregion ValidateTyped Tests - Boundary

	#region ValidateTyped Tests - Multiple Validators

	[Fact]
	public void AggregateErrorsFromMultipleValidatorsInValidateTyped()
	{
		// Arrange - Register two validators for the same message type
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidator>();
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidatorWithErrorCode>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("", 10); // Fails both validators

		// Act
		var result = sut.ValidateTyped(message);

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
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidator>();
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidatorWithErrorCode>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("John", 25); // Passes both

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	#endregion ValidateTyped Tests - Multiple Validators

	#region ValidateTyped Tests - Result Type

	[Fact]
	public void ReturnSerializableValidationResultTypeOnSuccess()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidator>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("John", 25);

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SerializableValidationResult>(result);
	}

	[Fact]
	public void ReturnSerializableValidationResultTypeOnFailure()
	{
		// Arrange
		var provider = CreateServiceProvider(services =>
		{
			_ = services.AddScoped<IValidator<TestAotMessage>, TestAotMessageValidator>();
		});
		var sut = new AotFluentValidatorResolver(provider);
		var message = new TestAotMessage("", 10);

		// Act
		var result = sut.ValidateTyped(message);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SerializableValidationResult>(result);
	}

	#endregion ValidateTyped Tests - Result Type
}
