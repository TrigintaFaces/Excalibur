// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextIntegrityException"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextIntegrityExceptionShould
{
	#region Constructor Tests

	[Fact]
	public void CreateWithDefaultConstructor()
	{
		// Arrange & Act
		var exception = new ContextIntegrityException();

		// Assert
		exception.ShouldNotBeNull();
		exception.Message.ShouldNotBeEmpty();
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessage()
	{
		// Arrange
		var message = "Context integrity validation failed";

		// Act
		var exception = new ContextIntegrityException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		// Arrange
		var message = "Context integrity validation failed";
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new ContextIntegrityException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritFromException()
	{
		// Arrange & Act
		var exception = new ContextIntegrityException();

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void BeCatchableAsException()
	{
		// Arrange
		var caughtAsException = false;

		// Act
		try
		{
			throw new ContextIntegrityException("Test");
		}
		catch (Exception)
		{
			caughtAsException = true;
		}

		// Assert
		caughtAsException.ShouldBeTrue();
	}

	[Fact]
	public void BeCatchableAsContextIntegrityException()
	{
		// Arrange
		var caughtAsSpecificType = false;

		// Act
		try
		{
			throw new ContextIntegrityException("Test");
		}
		catch (ContextIntegrityException)
		{
			caughtAsSpecificType = true;
		}

		// Assert
		caughtAsSpecificType.ShouldBeTrue();
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void SupportThrowingAndCatching()
	{
		// Arrange
		ContextIntegrityException? caughtException = null;

		// Act
		try
		{
			throw new ContextIntegrityException("Required field 'CorrelationId' was removed during processing");
		}
		catch (ContextIntegrityException ex)
		{
			caughtException = ex;
		}

		// Assert
		caughtException.ShouldNotBeNull();
		caughtException.Message.ShouldContain("CorrelationId");
	}

	[Fact]
	public void SupportWrappingOtherExceptions()
	{
		// Arrange
		var originalException = new KeyNotFoundException("Field not found");
		ContextIntegrityException? caughtException = null;

		// Act
		try
		{
			throw new ContextIntegrityException("Context validation error", originalException);
		}
		catch (ContextIntegrityException ex)
		{
			caughtException = ex;
		}

		// Assert
		caughtException.ShouldNotBeNull();
		caughtException.InnerException.ShouldBeOfType<KeyNotFoundException>();
	}

	[Fact]
	public void PreserveStackTrace()
	{
		// Arrange
		ContextIntegrityException? caughtException = null;

		// Act
		try
		{
			ThrowContextIntegrityException();
		}
		catch (ContextIntegrityException ex)
		{
			caughtException = ex;
		}

		// Assert
		caughtException.ShouldNotBeNull();
		caughtException.StackTrace.ShouldNotBeNull();
		caughtException.StackTrace.ShouldContain("ThrowContextIntegrityException");
	}

	private static void ThrowContextIntegrityException()
	{
		throw new ContextIntegrityException("Test exception");
	}

	#endregion
}
