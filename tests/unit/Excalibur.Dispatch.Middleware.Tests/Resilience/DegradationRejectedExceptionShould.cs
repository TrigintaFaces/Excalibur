// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="DegradationRejectedException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class DegradationRejectedExceptionShould : UnitTestBase
{
	[Fact]
	public void DefaultConstructor_CreatesException()
	{
		// Act
		var exception = new DegradationRejectedException();

		// Assert
		_ = exception.ShouldNotBeNull();
		exception.Message.ShouldNotBeNull();
	}

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		// Arrange
		const string message = "Operation rejected due to degradation";

		// Act
		var exception = new DegradationRejectedException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void MessageAndInnerExceptionConstructor_SetsBoth()
	{
		// Arrange
		const string message = "Operation rejected";
		var innerException = new InvalidOperationException("Inner");

		// Act
		var exception = new DegradationRejectedException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void CanBeThrown()
	{
		// Act & Assert
		_ = Should.Throw<DegradationRejectedException>(() =>
			throw new DegradationRejectedException("Test"));
	}

	[Fact]
	public void IsException()
	{
		// Arrange
		var exception = new DegradationRejectedException();

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}
}
