// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.Serialization;

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="NoFallbackAvailableException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class NoFallbackAvailableExceptionShould : UnitTestBase
{
	[Fact]
	public void DefaultConstructor_CreatesException()
	{
		// Act
		var exception = new NoFallbackAvailableException();

		// Assert
		_ = exception.ShouldNotBeNull();
		exception.Message.ShouldNotBeNull();
	}

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		// Arrange
		const string message = "No fallback available";

		// Act
		var exception = new NoFallbackAvailableException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void MessageAndInnerExceptionConstructor_SetsBoth()
	{
		// Arrange
		const string message = "No fallback available";
		var innerException = new InvalidOperationException("Inner");

		// Act
		var exception = new NoFallbackAvailableException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void CanBeThrown()
	{
		// Act & Assert
		_ = Should.Throw<NoFallbackAvailableException>(() =>
			throw new NoFallbackAvailableException("Test"));
	}

	[Fact]
	public void IsException()
	{
		// Arrange
		var exception = new NoFallbackAvailableException();

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}

	// [Serializable] attribute-absence tests removed -- enforced by RS0030 banned API analyzer (Sprint 690)

	[Fact]
	public void BeSealed()
	{
		// Assert - T.31: Leaf exceptions are sealed to prevent inheritance
		typeof(NoFallbackAvailableException).IsSealed.ShouldBeTrue();
	}
}
