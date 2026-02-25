// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Unit tests for <see cref="SigningException"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SigningExceptionShould
{
	[Fact]
	public void CreateWithDefaultConstructor()
	{
		// Arrange & Act
		var exception = new SigningException();

		// Assert
		exception.ShouldNotBeNull();
		exception.Message.ShouldNotBeEmpty();
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessage()
	{
		// Arrange
		var message = "Signing operation failed";

		// Act
		var exception = new SigningException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		// Arrange
		var message = "Signing operation failed";
		var innerException = new InvalidOperationException("Key not found");

		// Act
		var exception = new SigningException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
		exception.InnerException.Message.ShouldBe("Key not found");
	}

	[Fact]
	public void InheritFromException()
	{
		// Assert
		typeof(SigningException).BaseType.ShouldBe(typeof(Exception));
	}

	[Fact]
	public void BeThrowable()
	{
		// Arrange
		var message = "Test signing error";

		// Act & Assert
		Should.Throw<SigningException>(() => throw new SigningException(message))
			.Message.ShouldBe(message);
	}

	[Fact]
	public void BeCatchableAsException()
	{
		// Arrange
		var message = "Signing key expired";

		// Act & Assert
		Should.Throw<Exception>(() => throw new SigningException(message))
			.ShouldBeOfType<SigningException>();
	}

	[Fact]
	public void PreserveStackTrace_WhenThrown()
	{
		// Arrange & Act
		SigningException? caughtException = null;
		try
		{
			ThrowSigningException();
		}
		catch (SigningException ex)
		{
			caughtException = ex;
		}

		// Assert
		caughtException.ShouldNotBeNull();
		caughtException.StackTrace.ShouldContain(nameof(ThrowSigningException));
	}

	private static void ThrowSigningException()
	{
		throw new SigningException("Test");
	}
}
