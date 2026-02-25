// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Exceptions;

namespace Excalibur.Tests.Domain.Exceptions;

/// <summary>
/// Unit tests for <see cref="DomainException"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DomainExceptionShould
{
	#region T419.11: DomainException Tests

	[Fact]
	public void ThrowIf_Throws_WhenConditionIsTrue()
	{
		// Arrange
		const string message = "Validation failed";

		// Act & Assert
		var exception = Should.Throw<DomainException>(() =>
			DomainException.ThrowIf(true, message));
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void ThrowIf_DoesNotThrow_WhenConditionIsFalse()
	{
		// Arrange
		const string message = "Validation failed";

		// Act & Assert - should not throw
		DomainException.ThrowIf(false, message);
	}

	[Fact]
	public void ThrowIf_WithInnerException_Throws_WhenConditionIsTrue()
	{
		// Arrange
		const string message = "Internal error";
		var innerException = new InvalidOperationException("Inner error");

		// Act & Assert
		var exception = Should.Throw<DomainException>(() =>
			DomainException.ThrowIf(true, message, innerException));
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void ThrowIf_WithInnerException_DoesNotThrow_WhenConditionIsFalse()
	{
		// Arrange
		const string message = "Internal error";
		var innerException = new InvalidOperationException("Inner error");

		// Act & Assert - should not throw
		DomainException.ThrowIf(false, message, innerException);
	}

	[Fact]
	public void Constructor_Default_CreatesExceptionWithDefaultMessage()
	{
		// Arrange & Act
		var exception = new DomainException();

		// Assert
		exception.Message.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Constructor_WithMessage_SetsMessage()
	{
		// Arrange
		const string message = "Custom domain error";

		// Act
		var exception = new DomainException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Constructor_WithMessageAndInnerException_SetsProperties()
	{
		// Arrange
		const string message = "Domain error with inner";
		var innerException = new ArgumentException("Inner argument error");

		// Act
		var exception = new DomainException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void InheritsFromException_NotApiException()
	{
		// Arrange & Act
		var exception = new DomainException("test");

		// Assert - inherits from Exception, decoupled from ApiException (S551.19)
		_ = exception.ShouldBeAssignableTo<Exception>();
		exception.ShouldNotBeAssignableTo<ApiException>();
	}

	#endregion T419.11: DomainException Tests
}
