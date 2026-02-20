// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RouteFailureShould
{
	[Fact]
	public void Constructor_SetMessageAndExceptionType()
	{
		// Act
		var failure = new RouteFailure("connection refused", "System.TimeoutException");

		// Assert
		failure.Message.ShouldBe("connection refused");
		failure.ExceptionType.ShouldBe("System.TimeoutException");
	}

	[Fact]
	public void Constructor_WithMessageOnly_SetNullExceptionType()
	{
		// Act
		var failure = new RouteFailure("queue full");

		// Assert
		failure.Message.ShouldBe("queue full");
		failure.ExceptionType.ShouldBeNull();
	}

	[Fact]
	public void Constructor_ThrowOnNullMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new RouteFailure(null!));
	}

	[Fact]
	public void Constructor_ThrowOnEmptyMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new RouteFailure(""));
	}

	[Fact]
	public void Constructor_ThrowOnWhitespaceMessage()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new RouteFailure("   "));
	}

	[Fact]
	public void FromException_CreateFailureFromException()
	{
		// Arrange
		var exception = new InvalidOperationException("operation not permitted");

		// Act
		var failure = RouteFailure.FromException(exception);

		// Assert
		failure.Message.ShouldBe("operation not permitted");
		failure.ExceptionType.ShouldBe("System.InvalidOperationException");
	}

	[Fact]
	public void FromException_ThrowOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => RouteFailure.FromException(null!));
	}

	[Fact]
	public void FromException_HandleCustomExceptionType()
	{
		// Arrange
		var exception = new TimeoutException("request timed out");

		// Act
		var failure = RouteFailure.FromException(exception);

		// Assert
		failure.ExceptionType.ShouldBe("System.TimeoutException");
	}
}
