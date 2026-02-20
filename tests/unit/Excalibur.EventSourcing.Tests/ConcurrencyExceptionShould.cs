// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Dispatch.Exceptions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests;

/// <summary>
/// Tests for <see cref="ConcurrencyException"/> to verify it properly inherits from
/// <see cref="ConflictException"/> with HTTP 409 status code support.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConcurrencyExceptionShould
{
	[Fact]
	public void InheritFromConflictException()
	{
		// Arrange & Act
		var exception = new ConcurrencyException("Order", "key-123", 1, 2);

		// Assert
		_ = exception.ShouldBeAssignableTo<ConflictException>();
	}

	[Fact]
	public void Use409ConflictStatusCode()
	{
		// Arrange & Act
		var exception = new ConcurrencyException("Order", "key-123", 1, 2);

		// Assert
		exception.DispatchStatusCode.ShouldBe((int)HttpStatusCode.Conflict);
	}

	[Fact]
	public void SetResourceAndResourceIdWhenCreatedWithVersions()
	{
		// Arrange
		var resource = "Order";
		var resourceId = "order-123";

		// Act
		var exception = new ConcurrencyException(resource, resourceId, 1, 2);

		// Assert
		exception.Resource.ShouldBe(resource);
		exception.ResourceId.ShouldBe(resourceId);
	}

	[Fact]
	public void IncludeNumericVersionInformationWhenCreated()
	{
		// Arrange
		var resource = "UserAggregate";
		var resourceId = "aggregate-456";
		var expectedVersion = 1L;
		var actualVersion = 2L;

		// Act
		var exception = new ConcurrencyException(resource, resourceId, expectedVersion, actualVersion);

		// Assert
		exception.ExpectedVersion.ShouldBe(expectedVersion);
		exception.ActualVersion.ShouldBe(actualVersion);
		exception.Message.ShouldContain(expectedVersion.ToString());
		exception.Message.ShouldContain(actualVersion.ToString());
		exception.Message.ShouldContain(resource);
	}

	[Fact]
	public void IncludeStringVersionInformationWhenCreated()
	{
		// Arrange
		var resource = "UserAggregate";
		var resourceId = "aggregate-456";
		var expectedVersion = "v1";
		var actualVersion = "v2";

		// Act
		var exception = new ConcurrencyException(resource, resourceId, expectedVersion, actualVersion);

		// Assert
		exception.ExpectedVersionString.ShouldBe(expectedVersion);
		exception.ActualVersionString.ShouldBe(actualVersion);
		exception.Message.ShouldContain(expectedVersion);
		exception.Message.ShouldContain(actualVersion);
		exception.Message.ShouldContain(resource);
	}

	[Fact]
	public void CreateFromMessageOnly()
	{
		// Arrange
		var message = "Custom concurrency error";

		// Act
		var exception = new ConcurrencyException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void IncludeInnerExceptionWhenProvided()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");
		var message = "Outer concurrency error";

		// Act
		var exception = new ConcurrencyException(message, innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert - Check that the exception has the Serializable attribute
		typeof(ConcurrencyException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
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
		var exception = ConcurrencyException.ETagMismatch("Order", "order-123", "etag-v1", "etag-v2");

		// Assert
		exception.Resource.ShouldBe("Order");
		exception.ResourceId.ShouldBe("order-123");
		exception.ExpectedVersionString.ShouldBe("etag-v1");
		exception.ActualVersionString.ShouldBe("etag-v2");
	}

	// Helper class for testing generic factory method
	private sealed class TestAggregate { }
}
