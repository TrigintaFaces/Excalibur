// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Unit tests for <see cref="VerificationException"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class VerificationExceptionShould
{
	[Fact]
	public void CreateWithDefaultConstructor()
	{
		// Arrange & Act
		var exception = new VerificationException();

		// Assert
		exception.ShouldNotBeNull();
		exception.Message.ShouldNotBeEmpty();
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessage()
	{
		// Arrange
		var message = "Signature verification failed";

		// Act
		var exception = new VerificationException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		// Arrange
		var message = "Signature verification failed";
		var innerException = new FormatException("Invalid signature format");

		// Act
		var exception = new VerificationException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
		exception.InnerException.Message.ShouldBe("Invalid signature format");
	}

	[Fact]
	public void InheritFromException()
	{
		// Assert
		typeof(VerificationException).BaseType.ShouldBe(typeof(Exception));
	}

	[Fact]
	public void BeThrowable()
	{
		// Arrange
		var message = "Signature mismatch";

		// Act & Assert
		Should.Throw<VerificationException>(() => throw new VerificationException(message))
			.Message.ShouldBe(message);
	}

	[Fact]
	public void BeCatchableAsException()
	{
		// Arrange
		var message = "Invalid signature";

		// Act & Assert
		Should.Throw<Exception>(() => throw new VerificationException(message))
			.ShouldBeOfType<VerificationException>();
	}

	[Fact]
	public void PreserveStackTrace_WhenThrown()
	{
		// Arrange & Act
		VerificationException? caughtException = null;
		try
		{
			ThrowVerificationException();
		}
		catch (VerificationException ex)
		{
			caughtException = ex;
		}

		// Assert
		caughtException.ShouldNotBeNull();
		caughtException.StackTrace.ShouldContain(nameof(ThrowVerificationException));
	}

	private static void ThrowVerificationException()
	{
		throw new VerificationException("Test");
	}
}
