// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextSizeExceededException"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextSizeExceededExceptionShould
{
	#region Constructor Tests

	[Fact]
	public void CreateWithDefaultConstructor()
	{
		// Arrange & Act
		var exception = new ContextSizeExceededException();

		// Assert
		exception.ShouldNotBeNull();
		exception.Message.ShouldNotBeEmpty();
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessage()
	{
		// Arrange
		var message = "Context size exceeded maximum threshold of 100KB";

		// Act
		var exception = new ContextSizeExceededException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		// Arrange
		var message = "Context size exceeded maximum threshold";
		var innerException = new InvalidOperationException("Size limit reached");

		// Act
		var exception = new ContextSizeExceededException(message, innerException);

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
		var exception = new ContextSizeExceededException();

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
			throw new ContextSizeExceededException("Test");
		}
		catch (Exception)
		{
			caughtAsException = true;
		}

		// Assert
		caughtAsException.ShouldBeTrue();
	}

	[Fact]
	public void BeCatchableAsContextSizeExceededException()
	{
		// Arrange
		var caughtAsSpecificType = false;

		// Act
		try
		{
			throw new ContextSizeExceededException("Test");
		}
		catch (ContextSizeExceededException)
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
		ContextSizeExceededException? caughtException = null;

		// Act
		try
		{
			throw new ContextSizeExceededException("Context size is 150KB, which exceeds the 100KB limit");
		}
		catch (ContextSizeExceededException ex)
		{
			caughtException = ex;
		}

		// Assert
		caughtException.ShouldNotBeNull();
		caughtException.Message.ShouldContain("150KB");
	}

	[Fact]
	public void SupportWrappingOtherExceptions()
	{
		// Arrange
		var originalException = new ArgumentException("Value too large");
		ContextSizeExceededException? caughtException = null;

		// Act
		try
		{
			throw new ContextSizeExceededException("Context size validation error", originalException);
		}
		catch (ContextSizeExceededException ex)
		{
			caughtException = ex;
		}

		// Assert
		caughtException.ShouldNotBeNull();
		caughtException.InnerException.ShouldBeOfType<ArgumentException>();
	}

	[Fact]
	public void PreserveStackTrace()
	{
		// Arrange
		ContextSizeExceededException? caughtException = null;

		// Act
		try
		{
			ThrowContextSizeExceededException();
		}
		catch (ContextSizeExceededException ex)
		{
			caughtException = ex;
		}

		// Assert
		caughtException.ShouldNotBeNull();
		caughtException.StackTrace.ShouldNotBeNull();
		caughtException.StackTrace.ShouldContain("ThrowContextSizeExceededException");
	}

	private static void ThrowContextSizeExceededException()
	{
		throw new ContextSizeExceededException("Test exception");
	}

	[Fact]
	public void SupportSizeThresholdScenario()
	{
		// Arrange
		const int maxSize = 100_000;
		const int actualSize = 150_000;
		ContextSizeExceededException? caughtException = null;

		// Act
		try
		{
			throw new ContextSizeExceededException(
				$"Context size ({actualSize:N0} bytes) exceeds maximum allowed size ({maxSize:N0} bytes)");
		}
		catch (ContextSizeExceededException ex)
		{
			caughtException = ex;
		}

		// Assert
		caughtException.ShouldNotBeNull();
		caughtException.Message.ShouldContain("150,000");
		caughtException.Message.ShouldContain("100,000");
	}

	#endregion
}
