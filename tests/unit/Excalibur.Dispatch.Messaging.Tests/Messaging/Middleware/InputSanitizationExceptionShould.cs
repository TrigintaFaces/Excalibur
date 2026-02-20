// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="InputSanitizationException"/>.
/// </summary>
/// <remarks>
/// Tests the exception thrown when input sanitization fails.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class InputSanitizationExceptionShould
{
	#region Constructor Tests - Default

	[Fact]
	public void Constructor_Default_CreatesInstance()
	{
		// Act
		var exception = new InputSanitizationException();

		// Assert
		_ = exception.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_Default_HasDefaultMessage()
	{
		// Act
		var exception = new InputSanitizationException();

		// Assert
		_ = exception.Message.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_Default_HasNullInnerException()
	{
		// Act
		var exception = new InputSanitizationException();

		// Assert
		exception.InnerException.ShouldBeNull();
	}

	#endregion

	#region Constructor Tests - With Message

	[Fact]
	public void Constructor_WithMessage_SetsMessage()
	{
		// Arrange
		const string message = "Potential XSS attack detected in input field";

		// Act
		var exception = new InputSanitizationException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Constructor_WithEmptyMessage_AcceptsEmptyString()
	{
		// Act
		var exception = new InputSanitizationException(string.Empty);

		// Assert
		exception.Message.ShouldBe(string.Empty);
	}

	[Theory]
	[InlineData("Malicious script detected")]
	[InlineData("SQL injection attempt blocked")]
	[InlineData("Invalid HTML tags in input")]
	[InlineData("Dangerous characters detected")]
	public void Constructor_WithVariousMessages_PreservesMessage(string message)
	{
		// Act
		var exception = new InputSanitizationException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	#endregion

	#region Constructor Tests - With Message and InnerException

	[Fact]
	public void Constructor_WithMessageAndInnerException_SetsBoth()
	{
		// Arrange
		const string message = "Input sanitization failed";
		var innerException = new InvalidOperationException("Parsing error");

		// Act
		var exception = new InputSanitizationException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void Constructor_WithNullInnerException_AcceptsNull()
	{
		// Act
		var exception = new InputSanitizationException("Message", null!);

		// Assert
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithNestedInnerException_PreservesChain()
	{
		// Arrange
		var rootCause = new FormatException("Invalid encoding");
		var innerException = new InvalidOperationException("Decode failed", rootCause);

		// Act
		var exception = new InputSanitizationException("Sanitization failed", innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
		exception.InnerException.InnerException.ShouldBe(rootCause);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromException()
	{
		// Act
		var exception = new InputSanitizationException("test");

		// Assert
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void CanBeCaughtAsException()
	{
		// Act & Assert
		_ = Should.Throw<Exception>(() => throw new InputSanitizationException("test"));
	}

	[Fact]
	public void CanBeCaughtAsInputSanitizationException()
	{
		// Act & Assert
		_ = Should.Throw<InputSanitizationException>(() => throw new InputSanitizationException("test"));
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void PreservesStackTraceWhenThrown()
	{
		// Arrange
		InputSanitizationException? caught = null;

		// Act
		try
		{
			ThrowSanitizationException();
		}
		catch (InputSanitizationException ex)
		{
			caught = ex;
		}

		// Assert
		_ = caught.ShouldNotBeNull();
		_ = caught.StackTrace.ShouldNotBeNull();
		caught.StackTrace.ShouldContain(nameof(ThrowSanitizationException));
	}

	[Fact]
	public void CanBeUsedWithTryCatchFinally()
	{
		// Arrange
		var finallyExecuted = false;

		// Act
		try
		{
			throw new InputSanitizationException("Sanitization failed");
		}
		catch (InputSanitizationException ex)
		{
			ex.Message.ShouldBe("Sanitization failed");
		}
		finally
		{
			finallyExecuted = true;
		}

		// Assert
		finallyExecuted.ShouldBeTrue();
	}

	private static void ThrowSanitizationException()
	{
		throw new InputSanitizationException("Thrown from helper method");
	}

	#endregion
}
