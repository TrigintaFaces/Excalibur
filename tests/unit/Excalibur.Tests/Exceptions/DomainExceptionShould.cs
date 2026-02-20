// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.Serialization;

using Excalibur.Domain.Exceptions;

namespace Excalibur.Tests.Exceptions;

/// <summary>
///     Unit tests for the <see cref="DomainException" /> class.
/// </summary>
/// <remarks> Tests domain exception construction, inheritance behavior, static helper methods, and serialization functionality. </remarks>
[Trait("Category", "Unit")]
public class DomainExceptionShould
{
	[Fact]
	public void DefaultConstructor_SetsDefaultMessage()
	{
		// Arrange & Act
		var exception = new DomainException();

		// Assert
		exception.Message.ShouldBe("Exception within application logic.");
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithMessage_SetsMessage()
	{
		// Arrange
		const string message = "Custom domain exception message";

		// Act
		var exception = new DomainException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithMessageAndInnerException_SetsBoth()
	{
		// Arrange
		const string message = "Conflict in domain logic";
		var innerException = new InvalidOperationException("Inner exception");

		// Act
		var exception = new DomainException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void ShouldInheritFromException()
	{
		// Arrange & Act
		var exception = new DomainException("test");

		// Assert
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void ShouldNotInheritFromApiException()
	{
		// DomainException is decoupled from API concerns (S551.19)
		var exception = new DomainException("test");

		// Assert - verify it's NOT an ApiException
		exception.ShouldNotBeAssignableTo<ApiException>();
	}

	[Fact]
	public void ShouldBeSerializable()
	{
		// Arrange & Act
		var exception = new DomainException("test");

		// Assert
		_ = exception.ShouldBeAssignableTo<ISerializable>();
		typeof(DomainException).IsDefined(typeof(SerializableAttribute), false).ShouldBeTrue();
	}

	[Fact]
	public void ThrowIf_ShouldNotThrow_WhenConditionIsFalse()
	{
		// Arrange & Act & Assert
		Should.NotThrow(static () => DomainException.ThrowIf(false, "Should not throw"));
	}

	[Fact]
	public void ThrowIf_ShouldThrow_WhenConditionIsTrue()
	{
		// Arrange
		const string message = "Validation failed";

		// Act & Assert
		var exception = Should.Throw<DomainException>(static () => DomainException.ThrowIf(true, message));
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void ThrowIf_WithInnerException_ShouldNotThrow_WhenConditionIsFalse()
	{
		// Arrange
		var innerException = new ArgumentException("Inner");

		// Act & Assert
		Should.NotThrow(() => DomainException.ThrowIf(false, "Should not throw", innerException));
	}

	[Fact]
	public void ThrowIf_WithInnerException_ShouldThrow_WhenConditionIsTrue()
	{
		// Arrange
		const string message = "Conflict occurred";
		var innerException = new InvalidOperationException("Inner exception");

		// Act & Assert
		var exception = Should.Throw<DomainException>(() => DomainException.ThrowIf(true, message, innerException));
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void ThrowIf_WithEmptyMessage_ShouldUseProvidedEmptyMessage()
	{
		// Act & Assert
		var exception = Should.Throw<DomainException>(static () => DomainException.ThrowIf(true, ""));
		exception.Message.ShouldBe("");
	}

	[Fact]
	public void ThrowIf_ShouldWorkWithComplexConditions()
	{
		// Arrange
		var list = new List<string> { "item1", "item2" };
		const string message = "List validation failed";

		// Act & Assert
		Should.NotThrow(() =>
			DomainException.ThrowIf(list.Count != 2, message)); // Should not throw - list.Count == 2, condition is false
		_ = Should.Throw<DomainException>(() =>
			DomainException.ThrowIf(list.Count < 5, message)); // Should throw - condition true
	}

	[Fact]
	public void MultipleDomainExceptions_ShouldBeIndependent()
	{
		// Arrange
		const string message1 = "First exception";
		const string message2 = "Second exception";

		// Act
		var exception1 = new DomainException(message1);
		var exception2 = new DomainException(message2);

		// Assert
		exception1.Message.ShouldBe(message1);
		exception2.Message.ShouldBe(message2);
		exception1.ShouldNotBe(exception2);
	}
}
