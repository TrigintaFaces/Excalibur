// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Validation.DataAnnotations;

namespace Excalibur.Dispatch.Tests.Validation;

[Trait("Category", "Unit")]
public sealed class DataAnnotationsValidatorResolverShould
{
	private readonly DataAnnotationsValidatorResolver _sut = new();

	#region Test Messages

	private sealed record ValidMessage : IDispatchMessage;

	private sealed record MessageWithRequiredAttribute(
		[property: Required]
		string Name
	) : IDispatchMessage;

	private sealed record MessageWithRequiredAttributeAndCustomMessage(
		[property: Required(ErrorMessage = "Custom name is required")]
		string Name
	) : IDispatchMessage;

	private sealed record MessageWithStringLengthAttribute(
		[property: StringLength(10, MinimumLength = 2)]
		string Name
	) : IDispatchMessage;

	private sealed record MessageWithRangeAttribute(
		[property: Range(1, 100)]
		int Amount
	) : IDispatchMessage;

	private sealed record MessageWithRegularExpressionAttribute(
		[property: RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Invalid email format")]
		string Email
	) : IDispatchMessage;

	private sealed record MessageWithMultipleAttributes(
		[property: Required(ErrorMessage = "Username is required")]
		[property: StringLength(20, MinimumLength = 3, ErrorMessage = "Username must be 3-20 characters")]
		string Username,

		[property: Required(ErrorMessage = "Age is required")]
		[property: Range(18, 120, ErrorMessage = "Age must be between 18 and 120")]
		int Age
	) : IDispatchMessage;

	private sealed record MessageWithEmailAddressAttribute(
		[property: EmailAddress(ErrorMessage = "Invalid email address")]
		string Email
	) : IDispatchMessage;

	private sealed record MessageWithPhoneAttribute(
		[property: Phone(ErrorMessage = "Invalid phone number")]
		string Phone
	) : IDispatchMessage;

	private sealed record MessageWithUrlAttribute(
		[property: Url(ErrorMessage = "Invalid URL")]
		string WebsiteUrl
	) : IDispatchMessage;

	private sealed record MessageWithCreditCardAttribute(
		[property: CreditCard(ErrorMessage = "Invalid credit card number")]
		string CardNumber
	) : IDispatchMessage;

	private sealed record MessageWithMinLengthAttribute(
		[property: MinLength(5, ErrorMessage = "Name must have at least 5 characters")]
		string Name
	) : IDispatchMessage;

	private sealed record MessageWithMaxLengthAttribute(
		[property: MaxLength(10, ErrorMessage = "Name cannot exceed 10 characters")]
		string Name
	) : IDispatchMessage;

	#endregion Test Messages

	#region Helper Methods

	private static ValidationError GetFirstError(SerializableValidationResult result)
	{
		return (ValidationError)result.Errors.First();
	}

	private static List<ValidationError> GetAllErrors(SerializableValidationResult result)
	{
		return [.. result.Errors.Cast<ValidationError>()];
	}

	#endregion Helper Methods

	#region Happy Path Tests

	[Fact]
	public void ReturnNullForValidMessageWithNoAttributes()
	{
		// Arrange
		var message = new ValidMessage();

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenRequiredFieldIsProvided()
	{
		// Arrange
		var message = new MessageWithRequiredAttribute("John");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenStringLengthIsWithinRange()
	{
		// Arrange
		var message = new MessageWithStringLengthAttribute("John");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenRangeValueIsValid()
	{
		// Arrange
		var message = new MessageWithRangeAttribute(50);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenRegexPatternMatches()
	{
		// Arrange
		var message = new MessageWithRegularExpressionAttribute("test@example.com");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenAllAttributesPass()
	{
		// Arrange
		var message = new MessageWithMultipleAttributes("JohnDoe", 25);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenEmailAddressIsValid()
	{
		// Arrange
		var message = new MessageWithEmailAddressAttribute("user@domain.com");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenPhoneNumberIsValid()
	{
		// Arrange
		var message = new MessageWithPhoneAttribute("555-555-5555");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenUrlIsValid()
	{
		// Arrange
		var message = new MessageWithUrlAttribute("https://example.com");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenMinLengthIsSatisfied()
	{
		// Arrange
		var message = new MessageWithMinLengthAttribute("Johnny");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenMaxLengthIsSatisfied()
	{
		// Arrange
		var message = new MessageWithMaxLengthAttribute("John");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	#endregion Happy Path Tests

	#region Validation Failure Tests

	[Fact]
	public void ReturnFailedResultWhenRequiredFieldIsNull()
	{
		// Arrange
		var message = new MessageWithRequiredAttribute(null!);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldNotBeEmpty();
	}

	[Fact]
	public void ReturnCustomErrorMessageWhenRequiredFieldIsNull()
	{
		// Arrange
		var message = new MessageWithRequiredAttributeAndCustomMessage(null!);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldBe("Custom name is required");
	}

	[Fact]
	public void ReturnFailedResultWhenStringLengthIsTooShort()
	{
		// Arrange
		var message = new MessageWithStringLengthAttribute("J");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldNotBeEmpty();
	}

	[Fact]
	public void ReturnFailedResultWhenStringLengthIsTooLong()
	{
		// Arrange
		var message = new MessageWithStringLengthAttribute("VeryLongNameThatExceedsLimit");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldNotBeEmpty();
	}

	[Fact]
	public void ReturnFailedResultWhenRangeValueIsTooLow()
	{
		// Arrange
		var message = new MessageWithRangeAttribute(0);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldNotBeEmpty();
	}

	[Fact]
	public void ReturnFailedResultWhenRangeValueIsTooHigh()
	{
		// Arrange
		var message = new MessageWithRangeAttribute(101);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldNotBeEmpty();
	}

	[Fact]
	public void ReturnFailedResultWhenRegexPatternDoesNotMatch()
	{
		// Arrange
		var message = new MessageWithRegularExpressionAttribute("invalid-email");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldBe("Invalid email format");
	}

	[Fact]
	public void ReturnFailedResultWhenMultipleValidationsFailForUsername()
	{
		// Arrange - Username too short
		var message = new MessageWithMultipleAttributes("AB", 25);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldContain("3-20 characters");
	}

	[Fact]
	public void ReturnFailedResultWhenAgeIsOutOfRange()
	{
		// Arrange - Age under minimum
		var message = new MessageWithMultipleAttributes("ValidUser", 10);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldContain("18 and 120");
	}

	[Fact]
	public void ReturnMultipleErrorsWhenMultipleValidationsFail()
	{
		// Arrange - Both username (null) and age (under minimum) invalid
		var message = new MessageWithMultipleAttributes(null!, 10);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void ReturnFailedResultWhenEmailAddressIsInvalid()
	{
		// Arrange
		var message = new MessageWithEmailAddressAttribute("not-an-email");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldBe("Invalid email address");
	}

	[Fact]
	public void ReturnFailedResultWhenPhoneNumberIsInvalid()
	{
		// Arrange
		var message = new MessageWithPhoneAttribute("not-a-phone");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldBe("Invalid phone number");
	}

	[Fact]
	public void ReturnFailedResultWhenUrlIsInvalid()
	{
		// Arrange
		var message = new MessageWithUrlAttribute("not-a-url");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldBe("Invalid URL");
	}

	[Fact]
	public void ReturnFailedResultWhenMinLengthIsNotSatisfied()
	{
		// Arrange
		var message = new MessageWithMinLengthAttribute("Joe");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldContain("at least 5 characters");
	}

	[Fact]
	public void ReturnFailedResultWhenMaxLengthIsExceeded()
	{
		// Arrange
		var message = new MessageWithMaxLengthAttribute("VeryLongNameThatExceedsLimit");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.Message.ShouldContain("exceed 10 characters");
	}

	#endregion Validation Failure Tests

	#region Boundary Tests

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	public void ReturnNullWhenRangeValueIsAtBoundary(int amount)
	{
		// Arrange
		var message = new MessageWithRangeAttribute(amount);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenStringLengthIsAtMinimum()
	{
		// Arrange - Exactly 2 characters (minimum)
		var message = new MessageWithStringLengthAttribute("Jo");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public void ReturnNullWhenStringLengthIsAtMaximum()
	{
		// Arrange - Exactly 10 characters (maximum)
		var message = new MessageWithStringLengthAttribute("JohnMiller");

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.Null(result);
	}

	#endregion Boundary Tests

	#region Property Name Tests

	[Fact]
	public void IncludePropertyNameInValidationError()
	{
		// Arrange
		var message = new MessageWithRequiredAttribute(null!);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var error = GetFirstError(serializableResult);
		error.PropertyName.ShouldContain("Name");
	}

	[Fact]
	public void IncludeCorrectPropertyNamesInMultipleErrors()
	{
		// Arrange
		var message = new MessageWithMultipleAttributes(null!, 10);

		// Act
		var result = _sut.TryValidate(message);

		// Assert
		Assert.NotNull(result);
		var serializableResult = result.ShouldBeOfType<SerializableValidationResult>();
		var errors = GetAllErrors(serializableResult);

		// At least one error should mention Username
		errors.Any(e => e.PropertyName != null && e.PropertyName.Contains("Username")).ShouldBeTrue("Expected at least one error for Username");
		// At least one error should mention Age
		errors.Any(e => e.PropertyName != null && e.PropertyName.Contains("Age")).ShouldBeTrue("Expected at least one error for Age");
	}

	#endregion Property Name Tests

	#region Exception Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenMessageIsNull()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _sut.TryValidate(null!));
	}

	#endregion Exception Tests
}
