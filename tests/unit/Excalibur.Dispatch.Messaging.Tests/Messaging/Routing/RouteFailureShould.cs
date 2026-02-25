// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

/// <summary>
///     Tests for the <see cref="RouteFailure" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RouteFailureShould
{
	[Fact]
	public void CreateWithMessageOnly()
	{
		var sut = new RouteFailure("Something went wrong");
		sut.Message.ShouldBe("Something went wrong");
		sut.ExceptionType.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessageAndExceptionType()
	{
		var sut = new RouteFailure("Error occurred", "System.InvalidOperationException");
		sut.Message.ShouldBe("Error occurred");
		sut.ExceptionType.ShouldBe("System.InvalidOperationException");
	}

	[Fact]
	public void ThrowForNullMessage() =>
		Should.Throw<ArgumentException>(() => new RouteFailure(null!));

	[Fact]
	public void ThrowForEmptyMessage() =>
		Should.Throw<ArgumentException>(() => new RouteFailure(string.Empty));

	[Fact]
	public void ThrowForWhiteSpaceMessage() =>
		Should.Throw<ArgumentException>(() => new RouteFailure("   "));

	[Fact]
	public void CreateFromException()
	{
		var exception = new InvalidOperationException("Test exception");
		var sut = RouteFailure.FromException(exception);

		sut.Message.ShouldBe("Test exception");
		sut.ExceptionType.ShouldBe("System.InvalidOperationException");
	}

	[Fact]
	public void ThrowFromExceptionForNull() =>
		Should.Throw<ArgumentNullException>(() => RouteFailure.FromException(null!));

	[Fact]
	public void CreateFromCustomException()
	{
		var exception = new ArgumentOutOfRangeException("param", "Out of range");
		var sut = RouteFailure.FromException(exception);

		sut.ExceptionType.ShouldBe("System.ArgumentOutOfRangeException");
	}
}
