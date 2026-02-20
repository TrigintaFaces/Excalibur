// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="ValidationError"/>.
/// </summary>
/// <remarks>
/// Tests the validation error data class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class ValidationErrorShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidParameters_CreatesInstance()
	{
		// Arrange & Act
		var error = new ValidationError("Email", "Email is required");

		// Assert
		_ = error.ShouldNotBeNull();
		error.PropertyName.ShouldBe("Email");
		error.ErrorMessage.ShouldBe("Email is required");
	}

	[Fact]
	public void Constructor_WithEmptyPropertyName_CreatesInstance()
	{
		// Arrange & Act
		var error = new ValidationError(string.Empty, "Error occurred");

		// Assert
		error.PropertyName.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_WithEmptyErrorMessage_CreatesInstance()
	{
		// Arrange & Act
		var error = new ValidationError("PropertyName", string.Empty);

		// Assert
		error.ErrorMessage.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_WithNullPropertyName_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ValidationError(null!, "Error message"));
	}

	[Fact]
	public void Constructor_WithNullErrorMessage_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ValidationError("PropertyName", null!));
	}

	#endregion

	#region PropertyName Tests

	[Fact]
	public void PropertyName_ReturnsCorrectValue()
	{
		// Arrange
		var error = new ValidationError("FirstName", "Name is required");

		// Act
		var result = error.PropertyName;

		// Assert
		result.ShouldBe("FirstName");
	}

	[Theory]
	[InlineData("Email")]
	[InlineData("PhoneNumber")]
	[InlineData("Address.Street")]
	[InlineData("Items[0].Quantity")]
	public void PropertyName_WithVariousNames_ReturnsCorrectValue(string propertyName)
	{
		// Arrange
		var error = new ValidationError(propertyName, "Validation failed");

		// Act & Assert
		error.PropertyName.ShouldBe(propertyName);
	}

	#endregion

	#region ErrorMessage Tests

	[Fact]
	public void ErrorMessage_ReturnsCorrectValue()
	{
		// Arrange
		var error = new ValidationError("Email", "Invalid email format");

		// Act
		var result = error.ErrorMessage;

		// Assert
		result.ShouldBe("Invalid email format");
	}

	[Theory]
	[InlineData("Field is required")]
	[InlineData("Value must be greater than 0")]
	[InlineData("Invalid format")]
	[InlineData("Maximum length exceeded")]
	public void ErrorMessage_WithVariousMessages_ReturnsCorrectValue(string message)
	{
		// Arrange
		var error = new ValidationError("Field", message);

		// Act & Assert
		error.ErrorMessage.ShouldBe(message);
	}

	[Fact]
	public void ErrorMessage_WithUnicodeCharacters_Works()
	{
		// Arrange
		var error = new ValidationError("Name", "名前は必須です (Name is required)");

		// Act & Assert
		error.ErrorMessage.ShouldBe("名前は必須です (Name is required)");
	}

	[Fact]
	public void ErrorMessage_WithLongMessage_Works()
	{
		// Arrange
		var longMessage = new string('x', 10000);
		var error = new ValidationError("Field", longMessage);

		// Act & Assert
		error.ErrorMessage.ShouldBe(longMessage);
		error.ErrorMessage.Length.ShouldBe(10000);
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public void PropertyName_IsReadOnly()
	{
		// Arrange
		var error = new ValidationError("Email", "Error");

		// Assert - Properties should be get-only (verified by type system)
		error.PropertyName.ShouldBe("Email");
	}

	[Fact]
	public void ErrorMessage_IsReadOnly()
	{
		// Arrange
		var error = new ValidationError("Email", "Error");

		// Assert - Properties should be get-only (verified by type system)
		error.ErrorMessage.ShouldBe("Error");
	}

	#endregion
}
