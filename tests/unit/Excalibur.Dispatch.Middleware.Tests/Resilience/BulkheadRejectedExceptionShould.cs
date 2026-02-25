// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="BulkheadRejectedException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class BulkheadRejectedExceptionShould : UnitTestBase
{
	[Fact]
	public void DefaultConstructor_CreatesException()
	{
		// Act
		var exception = new BulkheadRejectedException();

		// Assert
		_ = exception.ShouldNotBeNull();
		exception.Message.ShouldNotBeNull();
	}

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		// Arrange
		const string message = "Bulkhead is full";

		// Act
		var exception = new BulkheadRejectedException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void MessageAndInnerExceptionConstructor_SetsBoth()
	{
		// Arrange
		const string message = "Bulkhead is full";
		var innerException = new InvalidOperationException("Inner");

		// Act
		var exception = new BulkheadRejectedException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void CanBeThrown()
	{
		// Act & Assert
		_ = Should.Throw<BulkheadRejectedException>(() =>
			throw new BulkheadRejectedException("Test"));
	}

	[Fact]
	public void IsException()
	{
		// Arrange
		var exception = new BulkheadRejectedException();

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}
}
