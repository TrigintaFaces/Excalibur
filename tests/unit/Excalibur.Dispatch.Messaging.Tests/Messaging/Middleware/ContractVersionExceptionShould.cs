// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="ContractVersionException"/>.
/// </summary>
/// <remarks>
/// Tests the exception thrown for contract version issues.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class ContractVersionExceptionShould
{
	#region Constructor Tests - Default

	[Fact]
	public void Constructor_Default_CreatesInstance()
	{
		// Act
		var exception = new ContractVersionException();

		// Assert
		_ = exception.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_Default_HasDefaultMessage()
	{
		// Act
		var exception = new ContractVersionException();

		// Assert
		_ = exception.Message.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_Default_HasNullInnerException()
	{
		// Act
		var exception = new ContractVersionException();

		// Assert
		exception.InnerException.ShouldBeNull();
	}

	#endregion

	#region Constructor Tests - With Message

	[Fact]
	public void Constructor_WithMessage_SetsMessage()
	{
		// Arrange
		const string message = "Contract version 2.0 is not compatible with handler expecting 1.0";

		// Act
		var exception = new ContractVersionException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Constructor_WithEmptyMessage_AcceptsEmptyString()
	{
		// Act
		var exception = new ContractVersionException(string.Empty);

		// Assert
		exception.Message.ShouldBe(string.Empty);
	}

	[Theory]
	[InlineData("Schema version mismatch")]
	[InlineData("Contract version not supported")]
	[InlineData("Breaking change detected in v3.0")]
	[InlineData("Cannot upgrade from version 1 to version 3")]
	public void Constructor_WithVariousMessages_PreservesMessage(string message)
	{
		// Act
		var exception = new ContractVersionException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	#endregion

	#region Constructor Tests - With Message and InnerException

	[Fact]
	public void Constructor_WithMessageAndInnerException_SetsBoth()
	{
		// Arrange
		const string message = "Version check failed";
		var innerException = new InvalidOperationException("Schema comparison error");

		// Act
		var exception = new ContractVersionException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void Constructor_WithNullInnerException_AcceptsNull()
	{
		// Act
		var exception = new ContractVersionException("Message", null!);

		// Assert
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithNestedInnerException_PreservesChain()
	{
		// Arrange
		var rootCause = new FormatException("Invalid version format");
		var innerException = new InvalidOperationException("Parse failed", rootCause);

		// Act
		var exception = new ContractVersionException("Contract version error", innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
		exception.InnerException.InnerException.ShouldBe(rootCause);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromInvalidOperationException()
	{
		// Act
		var exception = new ContractVersionException("test");

		// Assert
		_ = exception.ShouldBeAssignableTo<InvalidOperationException>();
	}

	[Fact]
	public void InheritsFromException()
	{
		// Act
		var exception = new ContractVersionException("test");

		// Assert
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void CanBeCaughtAsInvalidOperationException()
	{
		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => throw new ContractVersionException("test"));
	}

	[Fact]
	public void CanBeCaughtAsException()
	{
		// Act & Assert
		_ = Should.Throw<Exception>(() => throw new ContractVersionException("test"));
	}

	[Fact]
	public void CanBeCaughtAsContractVersionException()
	{
		// Act & Assert
		_ = Should.Throw<ContractVersionException>(() => throw new ContractVersionException("test"));
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void PreservesStackTraceWhenThrown()
	{
		// Arrange
		ContractVersionException? caught = null;

		// Act
		try
		{
			ThrowContractVersionException();
		}
		catch (ContractVersionException ex)
		{
			caught = ex;
		}

		// Assert
		_ = caught.ShouldNotBeNull();
		_ = caught.StackTrace.ShouldNotBeNull();
		caught.StackTrace.ShouldContain(nameof(ThrowContractVersionException));
	}

	[Fact]
	public void CanBeUsedWithTryCatchFinally()
	{
		// Arrange
		var finallyExecuted = false;

		// Act
		try
		{
			throw new ContractVersionException("Version mismatch");
		}
		catch (ContractVersionException ex)
		{
			ex.Message.ShouldBe("Version mismatch");
		}
		finally
		{
			finallyExecuted = true;
		}

		// Assert
		finallyExecuted.ShouldBeTrue();
	}

	[Fact]
	public void CanBeCaughtSpecificallyNotAsInvalidOperationException()
	{
		// Arrange
		var caughtContractVersion = false;

		// Act
		try
		{
			throw new ContractVersionException("test");
		}
		catch (ContractVersionException)
		{
			caughtContractVersion = true;
		}
		catch (InvalidOperationException)
		{
			Assert.Fail("Should have caught ContractVersionException first");
		}

		// Assert
		caughtContractVersion.ShouldBeTrue();
	}

	private static void ThrowContractVersionException()
	{
		throw new ContractVersionException("Thrown from helper method");
	}

	#endregion
}
