// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Unit tests for resilience exception types: <see cref="BulkheadRejectedException"/>,
/// <see cref="DegradationRejectedException"/>, and <see cref="NoFallbackAvailableException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class ResilienceExceptionsShould : UnitTestBase
{
	#region BulkheadRejectedException

	[Fact]
	public void BulkheadRejectedException_DefaultConstructor_CreatesInstance()
	{
		// Act
		var ex = new BulkheadRejectedException();

		// Assert
		ex.ShouldNotBeNull();
		ex.Message.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void BulkheadRejectedException_WithMessage_SetsMessage()
	{
		// Act
		var ex = new BulkheadRejectedException("Bulkhead full");

		// Assert
		ex.Message.ShouldBe("Bulkhead full");
	}

	[Fact]
	public void BulkheadRejectedException_WithMessageAndInner_SetsProperties()
	{
		// Arrange
		var inner = new InvalidOperationException("inner");

		// Act
		var ex = new BulkheadRejectedException("Bulkhead full", inner);

		// Assert
		ex.Message.ShouldBe("Bulkhead full");
		ex.InnerException.ShouldBe(inner);
	}

	#endregion

	#region DegradationRejectedException

	[Fact]
	public void DegradationRejectedException_DefaultConstructor_CreatesInstance()
	{
		// Act
		var ex = new DegradationRejectedException();

		// Assert
		ex.ShouldNotBeNull();
	}

	[Fact]
	public void DegradationRejectedException_WithMessage_SetsMessage()
	{
		// Act
		var ex = new DegradationRejectedException("Degraded");

		// Assert
		ex.Message.ShouldBe("Degraded");
	}

	[Fact]
	public void DegradationRejectedException_WithMessageAndInner_SetsProperties()
	{
		// Arrange
		var inner = new TimeoutException("timeout");

		// Act
		var ex = new DegradationRejectedException("Degraded", inner);

		// Assert
		ex.Message.ShouldBe("Degraded");
		ex.InnerException.ShouldBe(inner);
	}

	#endregion

	#region NoFallbackAvailableException

	[Fact]
	public void NoFallbackAvailableException_DefaultConstructor_CreatesInstance()
	{
		// Act
		var ex = new NoFallbackAvailableException();

		// Assert
		ex.ShouldNotBeNull();
	}

	[Fact]
	public void NoFallbackAvailableException_WithMessage_SetsMessage()
	{
		// Act
		var ex = new NoFallbackAvailableException("No fallback");

		// Assert
		ex.Message.ShouldBe("No fallback");
	}

	[Fact]
	public void NoFallbackAvailableException_WithMessageAndInner_SetsProperties()
	{
		// Arrange
		var inner = new InvalidOperationException("primary failed");

		// Act
		var ex = new NoFallbackAvailableException("No fallback", inner);

		// Assert
		ex.Message.ShouldBe("No fallback");
		ex.InnerException.ShouldBe(inner);
	}

	#endregion
}
