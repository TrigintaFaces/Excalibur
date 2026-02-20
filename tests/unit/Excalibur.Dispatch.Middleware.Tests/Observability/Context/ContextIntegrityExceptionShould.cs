// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextIntegrityException" />.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Excalibur.Dispatch.Observability")]
[Trait("Feature", "Context")]
public sealed class ContextIntegrityExceptionShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithDefaultConstructor_HasNullMessage()
	{
		// Act
		var exception = new ContextIntegrityException();

		// Assert
		exception.ShouldNotBeNull();
		exception.Message.ShouldNotBeNullOrEmpty(); // Default exception message
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void Create_WithMessage_StoresMessage()
	{
		// Arrange
		const string message = "Context integrity validation failed";

		// Act
		var exception = new ContextIntegrityException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void Create_WithMessageAndInnerException_StoresBoth()
	{
		// Arrange
		const string message = "Context integrity validation failed";
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
	public void Inherit_FromException()
	{
		// Act
		var exception = new ContextIntegrityException("test");

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void BeThrowable()
	{
		// Arrange
		var exception = new ContextIntegrityException("test");

		// Act & Assert
		Should.Throw<ContextIntegrityException>(() => throw exception);
	}

	#endregion
}
