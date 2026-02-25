// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Tests for <see cref="ApiException"/> to verify RFC 7807 problem details support
/// and the enhanced virtual methods added in Sprint 438.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ApiExceptionShould
{
	[Fact]
	public void HaveUniqueIdWhenCreated()
	{
		// Arrange & Act
		var exception1 = new ApiException();
		var exception2 = new ApiException();

		// Assert
		exception1.Id.ShouldNotBe(Guid.Empty);
		exception2.Id.ShouldNotBe(Guid.Empty);
		exception1.Id.ShouldNotBe(exception2.Id);
	}

	[Fact]
	public void DefaultStatusCodeTo500()
	{
		// Arrange & Act
		var exception = new ApiException();

		// Assert
		exception.StatusCode.ShouldBe(500);
	}

	[Fact]
	public void UseProvidedStatusCode()
	{
		// Arrange & Act
		var exception = new ApiException(404, "Resource not found", null);

		// Assert
		exception.StatusCode.ShouldBe(404);
	}

	[Fact]
	public void ThrowArgumentOutOfRangeForInvalidStatusCode()
	{
		// Act & Assert - below range
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new ApiException(99, "Too low", null));

		// Act & Assert - above range
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new ApiException(600, "Too high", null));
	}

	[Fact]
	public void AcceptBoundaryStatusCodes()
	{
		// Act & Assert - lower boundary
		var lower = new ApiException(100, "Continue", null);
		lower.StatusCode.ShouldBe(100);

		// Act & Assert - upper boundary
		var upper = new ApiException(599, "Network timeout", null);
		upper.StatusCode.ShouldBe(599);
	}

	[Fact]
	public void ToProblemDetails_ReturnsValidRfc7807Structure()
	{
		// Arrange
		var exception = new ApiException("Something went wrong");

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		_ = problemDetails.ShouldNotBeNull();
		problemDetails.Type.ShouldStartWith("urn:dispatch:error:");
		problemDetails.Title.ShouldBe("ApiException");
		problemDetails.Status.ShouldBe(500);
		problemDetails.Detail.ShouldBe("Something went wrong");
		problemDetails.Instance.ShouldStartWith("urn:dispatch:exception:");
		problemDetails.Instance.ShouldContain(exception.Id.ToString());
	}

	[Fact]
	public void ToProblemDetails_UsesStatusCodeForErrorCode()
	{
		// Arrange
		var exception = new ApiException(404, "Not found", null);

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		problemDetails.ErrorCode.ShouldBe(404);
	}

	[Fact]
	public void ToProblemDetails_GeneratesLowercaseTypeUri()
	{
		// Arrange
		var exception = new ApiException("Test");

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		problemDetails.Type.ShouldBe("urn:dispatch:error:apiexception");
	}

	[Fact]
	public void ToProblemDetails_IncludesUniqueInstanceUri()
	{
		// Arrange
		var exception = new ApiException();

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		problemDetails.Instance.ShouldBe($"urn:dispatch:exception:{exception.Id}");
	}

	[Fact]
	public void UseDefaultMessageWhenNoneProvided()
	{
		// Arrange & Act
		var exception = new ApiException();

		// Assert
		exception.Message.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void PreserveInnerException()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new ApiException("Outer error", innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
		exception.Message.ShouldBe("Outer error");
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(ApiException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void ToProblemDetails_ReturnsNullExtensionsByDefault()
	{
		// Arrange
		var exception = new ApiException("Test");

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert - base ApiException has no extensions
		problemDetails.Extensions.ShouldBeEmpty();
	}
}
