// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="AuthenticationException"/>.
/// </summary>
/// <remarks>
/// Tests the exception thrown when authentication fails.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class AuthenticationExceptionShould
{
	#region Constructor Tests - Default

	[Fact]
	public void Constructor_Default_CreatesInstance()
	{
		// Act
		var exception = new AuthenticationException();

		// Assert
		_ = exception.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_Default_HasDefaultMessage()
	{
		// Act
		var exception = new AuthenticationException();

		// Assert
		_ = exception.Message.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_Default_HasNullInnerException()
	{
		// Act
		var exception = new AuthenticationException();

		// Assert
		exception.InnerException.ShouldBeNull();
	}

	#endregion

	#region Constructor Tests - With Message

	[Fact]
	public void Constructor_WithMessage_SetsMessage()
	{
		// Arrange
		const string message = "Authentication token is invalid";

		// Act
		var exception = new AuthenticationException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Constructor_WithEmptyMessage_AcceptsEmptyString()
	{
		// Act
		var exception = new AuthenticationException(string.Empty);

		// Assert
		exception.Message.ShouldBe(string.Empty);
	}

	[Theory]
	[InlineData("Token expired")]
	[InlineData("Invalid credentials")]
	[InlineData("Missing authorization header")]
	[InlineData("API key not recognized")]
	public void Constructor_WithVariousMessages_PreservesMessage(string message)
	{
		// Act
		var exception = new AuthenticationException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	#endregion

	#region Constructor Tests - With Message and InnerException

	[Fact]
	public void Constructor_WithMessageAndInnerException_SetsBoth()
	{
		// Arrange
		const string message = "Authentication failed";
		var innerException = new InvalidOperationException("Token parsing error");

		// Act
		var exception = new AuthenticationException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void Constructor_WithNullInnerException_AcceptsNull()
	{
		// Act
		var exception = new AuthenticationException("Message", null!);

		// Assert
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithNestedInnerException_PreservesChain()
	{
		// Arrange
		var rootCause = new FormatException("Invalid JWT format");
		var innerException = new InvalidOperationException("Token decode failed", rootCause);

		// Act
		var exception = new AuthenticationException("Authentication failed", innerException);

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
		var exception = new AuthenticationException("test");

		// Assert
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void CanBeCaughtAsException()
	{
		// Act & Assert
		_ = Should.Throw<Exception>(() => throw new AuthenticationException("test"));
	}

	[Fact]
	public void CanBeCaughtAsAuthenticationException()
	{
		// Act & Assert
		_ = Should.Throw<AuthenticationException>(() => throw new AuthenticationException("test"));
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void PreservesStackTraceWhenThrown()
	{
		// Arrange
		AuthenticationException? caught = null;

		// Act
		try
		{
			ThrowAuthenticationException();
		}
		catch (AuthenticationException ex)
		{
			caught = ex;
		}

		// Assert
		_ = caught.ShouldNotBeNull();
		_ = caught.StackTrace.ShouldNotBeNull();
		caught.StackTrace.ShouldContain(nameof(ThrowAuthenticationException));
	}

	[Fact]
	public void CanBeUsedWithTryCatchFinally()
	{
		// Arrange
		var finallyExecuted = false;

		// Act
		try
		{
			throw new AuthenticationException("Auth failed");
		}
		catch (AuthenticationException ex)
		{
			ex.Message.ShouldBe("Auth failed");
		}
		finally
		{
			finallyExecuted = true;
		}

		// Assert
		finallyExecuted.ShouldBeTrue();
	}

	private static void ThrowAuthenticationException()
	{
		throw new AuthenticationException("Thrown from helper method");
	}

	#endregion
}
