// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

// Use alias to disambiguate from Excalibur.Dispatch.Abstractions.ResourceException
using ResourceException = Excalibur.Dispatch.Exceptions.ResourceException;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Tests for <see cref="ConcurrencyException"/> to verify optimistic locking
/// conflict handling with HTTP 409 status code and version information.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConcurrencyExceptionShould
{
	[Fact]
	public void InheritFromConflictException()
	{
		// Arrange & Act
		var exception = new ConcurrencyException();

		// Assert
		_ = exception.ShouldBeAssignableTo<ConflictException>();
		_ = exception.ShouldBeAssignableTo<ResourceException>();
		_ = exception.ShouldBeAssignableTo<DispatchException>();
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void Use409StatusCode()
	{
		// Arrange & Act
		var exception = new ConcurrencyException();

		// Assert
		exception.DispatchStatusCode.ShouldBe((int)HttpStatusCode.Conflict);
	}

	[Fact]
	public void UseResourceConcurrencyErrorCode()
	{
		// Arrange & Act
		var exception = new ConcurrencyException();

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.ResourceConcurrency);
	}

	[Fact]
	public void SetNumericVersionsWhenCreated()
	{
		// Arrange
		var resource = "Order";
		var resourceId = "order-123";
		var expectedVersion = 5L;
		var actualVersion = 10L;

		// Act
		var exception = new ConcurrencyException(resource, resourceId, expectedVersion, actualVersion);

		// Assert
		exception.Resource.ShouldBe(resource);
		exception.ResourceId.ShouldBe(resourceId);
		exception.ExpectedVersion.ShouldBe(expectedVersion);
		exception.ActualVersion.ShouldBe(actualVersion);
	}

	[Fact]
	public void SetStringVersionsWhenCreated()
	{
		// Arrange
		var resource = "Document";
		var resourceId = "doc-456";
		var expectedVersion = "etag-abc";
		var actualVersion = "etag-xyz";

		// Act
		var exception = new ConcurrencyException(resource, resourceId, expectedVersion, actualVersion);

		// Assert
		exception.Resource.ShouldBe(resource);
		exception.ResourceId.ShouldBe(resourceId);
		exception.ExpectedVersionString.ShouldBe(expectedVersion);
		exception.ActualVersionString.ShouldBe(actualVersion);
	}

	[Fact]
	public void FormatMessageWithNumericVersions()
	{
		// Arrange & Act
		var exception = new ConcurrencyException("User", "user-123", 1, 2);

		// Assert
		exception.Message.ShouldContain("1");
		exception.Message.ShouldContain("2");
		exception.Message.ShouldContain("User");
	}

	[Fact]
	public void FormatMessageWithStringVersions()
	{
		// Arrange & Act
		var exception = new ConcurrencyException("Config", "config-789", "v1", "v2");

		// Assert
		exception.Message.ShouldContain("v1");
		exception.Message.ShouldContain("v2");
		exception.Message.ShouldContain("Config");
	}

	[Fact]
	public void CreateForAggregateWithFactory()
	{
		// Arrange & Act
		var exception = ConcurrencyException.ForAggregate<TestAggregate>("agg-123", 5, 10);

		// Assert
		exception.Resource.ShouldBe("TestAggregate");
		exception.ResourceId.ShouldBe("agg-123");
		exception.ExpectedVersion.ShouldBe(5);
		exception.ActualVersion.ShouldBe(10);
	}

	[Fact]
	public void CreateForETagMismatch()
	{
		// Arrange & Act
		var exception = ConcurrencyException.ETagMismatch("Document", "doc-456", "etag-old", "etag-new");

		// Assert
		exception.Resource.ShouldBe("Document");
		exception.ResourceId.ShouldBe("doc-456");
		exception.ExpectedVersionString.ShouldBe("etag-old");
		exception.ActualVersionString.ShouldBe("etag-new");
	}

	[Fact]
	public void IncludeNumericVersionsInDispatchProblemDetailsExtensions()
	{
		// Arrange
		var exception = new ConcurrencyException("Order", "order-123", 5L, 10L);

		// Act
		var dispatchProblemDetails = exception.ToDispatchProblemDetails();

		// Assert - ToDispatchProblemDetails() includes context in Extensions
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("resource", "Order");
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("resourceId", "order-123");
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("expectedVersion", 5L);
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("actualVersion", 10L);
	}

	[Fact]
	public void IncludeStringVersionsInDispatchProblemDetailsExtensions()
	{
		// Arrange
		var exception = new ConcurrencyException("Document", "doc-456", "etag-v1", "etag-v2");

		// Act
		var dispatchProblemDetails = exception.ToDispatchProblemDetails();

		// Assert - ToDispatchProblemDetails() includes context in Extensions
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("expectedVersion", "etag-v1");
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("actualVersion", "etag-v2");
	}

	[Fact]
	public void IncludeVersionsInContext()
	{
		// Arrange & Act
		var exception = new ConcurrencyException("Entity", "ent-789", 3L, 7L);

		// Assert
		exception.Context.ShouldContainKeyAndValue("expectedVersion", 3L);
		exception.Context.ShouldContainKeyAndValue("actualVersion", 7L);
	}

	[Fact]
	public void CreateFromMessageOnly()
	{
		// Arrange & Act
		var exception = new ConcurrencyException("Custom concurrency message");

		// Assert
		exception.Message.ShouldBe("Custom concurrency message");
		exception.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void IncludeInnerExceptionWhenProvided()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new ConcurrencyException("Concurrency error", innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(ConcurrencyException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void HaveDefaultMessage()
	{
		// Arrange & Act
		var exception = new ConcurrencyException();

		// Assert
		exception.Message.ShouldContain("concurrency");
	}

	// Helper class for testing generic factory method
	private sealed class TestAggregate { }
}
