// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Extensions;

namespace Excalibur.Dispatch.Tests.Messaging.Extensions;

/// <summary>
/// Unit tests for <see cref="ExceptionExtensions"/>.
/// </summary>
/// <remarks>
/// Tests the exception extension methods for error and status code retrieval.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Extensions")]
[Trait("Priority", "0")]
public sealed class ExceptionExtensionsShould
{
	#region GetErrorCode Tests

	[Fact]
	public void GetErrorCode_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.GetErrorCode());
	}

	[Fact]
	public void GetErrorCode_WithExceptionWithErrorCodeProperty_ReturnsErrorCode()
	{
		// Arrange
		var exception = new ExceptionWithErrorCode(123);

		// Act
		var result = exception.GetErrorCode();

		// Assert
		result.ShouldBe(123);
	}

	[Fact]
	public void GetErrorCode_WithExceptionWithoutErrorCode_ReturnsNull()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetErrorCode();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetErrorCode_WithErrorCodeInData_ReturnsErrorCode()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");
		exception.Data["ErrorCode"] = 456;

		// Act
		var result = exception.GetErrorCode();

		// Assert
		result.ShouldBe(456);
	}

	[Fact]
	public void GetErrorCode_WithInnerExceptionHavingErrorCode_ReturnsInnerErrorCode()
	{
		// Arrange
		var innerException = new ExceptionWithErrorCode(789);
		var exception = new InvalidOperationException("Outer", innerException);

		// Act
		var result = exception.GetErrorCode();

		// Assert
		result.ShouldBe(789);
	}

	[Fact]
	public void GetErrorCode_WithAggregateException_ReturnsFirstInnerErrorCode()
	{
		// Arrange
		var inner1 = new InvalidOperationException("No code");
		var inner2 = new ExceptionWithErrorCode(111);
		var aggEx = new AggregateException(inner1, inner2);

		// Act
		var result = aggEx.GetErrorCode();

		// Assert
		result.ShouldBe(111);
	}

	[Fact]
	public void GetErrorCode_WithAggregateExceptionNoErrorCodes_ReturnsNull()
	{
		// Arrange
		var inner1 = new InvalidOperationException("No code");
		var inner2 = new ArgumentException("No code");
		var aggEx = new AggregateException(inner1, inner2);

		// Act
		var result = aggEx.GetErrorCode();

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region GetStatusCode Tests

	[Fact]
	public void GetStatusCode_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.GetStatusCode());
	}

	[Fact]
	public void GetStatusCode_WithExceptionWithStatusCodeProperty_ReturnsStatusCode()
	{
		// Arrange
		var exception = new ExceptionWithStatusCode(404);

		// Act
		var result = exception.GetStatusCode();

		// Assert
		result.ShouldBe(404);
	}

	[Fact]
	public void GetStatusCode_WithExceptionWithoutStatusCode_ReturnsNull()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetStatusCode();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetStatusCode_WithStatusCodeInData_ReturnsStatusCode()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");
		exception.Data["StatusCode"] = 500;

		// Act
		var result = exception.GetStatusCode();

		// Assert
		result.ShouldBe(500);
	}

	[Fact]
	public void GetStatusCode_WithInnerExceptionHavingStatusCode_ReturnsInnerStatusCode()
	{
		// Arrange
		var innerException = new ExceptionWithStatusCode(401);
		var exception = new InvalidOperationException("Outer", innerException);

		// Act
		var result = exception.GetStatusCode();

		// Assert
		result.ShouldBe(401);
	}

	#endregion

	#region GetStatusCodeOrDefault Tests

	[Fact]
	public void GetStatusCodeOrDefault_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.GetStatusCodeOrDefault());
	}

	[Fact]
	public void GetStatusCodeOrDefault_WithStatusCode_ReturnsStatusCode()
	{
		// Arrange
		var exception = new ExceptionWithStatusCode(403);

		// Act
		var result = exception.GetStatusCodeOrDefault();

		// Assert
		result.ShouldBe(403);
	}

	[Fact]
	public void GetStatusCodeOrDefault_WithoutStatusCode_ReturnsDefaultValue()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetStatusCodeOrDefault();

		// Assert
		result.ShouldBe(500);
	}

	[Fact]
	public void GetStatusCodeOrDefault_WithCustomDefault_ReturnsCustomDefault()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetStatusCodeOrDefault(418);

		// Assert
		result.ShouldBe(418);
	}

	#endregion

	#region GetErrorCodeOrDefault Tests

	[Fact]
	public void GetErrorCodeOrDefault_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.GetErrorCodeOrDefault());
	}

	[Fact]
	public void GetErrorCodeOrDefault_WithErrorCode_ReturnsErrorCode()
	{
		// Arrange
		var exception = new ExceptionWithErrorCode(999);

		// Act
		var result = exception.GetErrorCodeOrDefault();

		// Assert
		result.ShouldBe(999);
	}

	[Fact]
	public void GetErrorCodeOrDefault_WithoutErrorCode_ReturnsDefaultValue()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetErrorCodeOrDefault();

		// Assert
		result.ShouldBe(-1);
	}

	[Fact]
	public void GetErrorCodeOrDefault_WithCustomDefault_ReturnsCustomDefault()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetErrorCodeOrDefault(0);

		// Assert
		result.ShouldBe(0);
	}

	#endregion

	#region HasErrorCode Tests

	[Fact]
	public void HasErrorCode_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.HasErrorCode());
	}

	[Fact]
	public void HasErrorCode_WithErrorCode_ReturnsTrue()
	{
		// Arrange
		var exception = new ExceptionWithErrorCode(123);

		// Act
		var result = exception.HasErrorCode();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasErrorCode_WithoutErrorCode_ReturnsFalse()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.HasErrorCode();

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region HasStatusCode Tests

	[Fact]
	public void HasStatusCode_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.HasStatusCode());
	}

	[Fact]
	public void HasStatusCode_WithStatusCode_ReturnsTrue()
	{
		// Arrange
		var exception = new ExceptionWithStatusCode(500);

		// Act
		var result = exception.HasStatusCode();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasStatusCode_WithoutStatusCode_ReturnsFalse()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.HasStatusCode();

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region Caching Tests

	[Fact]
	public void GetErrorCode_CalledMultipleTimes_UsesCache()
	{
		// Arrange
		var exception = new ExceptionWithErrorCode(123);

		// Act - Call multiple times
		var result1 = exception.GetErrorCode();
		var result2 = exception.GetErrorCode();
		var result3 = exception.GetErrorCode();

		// Assert - All should return same value (cache is used)
		result1.ShouldBe(123);
		result2.ShouldBe(123);
		result3.ShouldBe(123);
	}

	[Fact]
	public void GetStatusCode_CalledMultipleTimes_UsesCache()
	{
		// Arrange
		var exception = new ExceptionWithStatusCode(404);

		// Act - Call multiple times
		var result1 = exception.GetStatusCode();
		var result2 = exception.GetStatusCode();
		var result3 = exception.GetStatusCode();

		// Assert - All should return same value (cache is used)
		result1.ShouldBe(404);
		result2.ShouldBe(404);
		result3.ShouldBe(404);
	}

	#endregion

	#region Data Dictionary Priority Tests

	[Fact]
	public void GetErrorCode_WithBothPropertyAndData_PreferencesProperty()
	{
		// Arrange
		var exception = new ExceptionWithErrorCode(100);
		exception.Data["ErrorCode"] = 200;

		// Act
		var result = exception.GetErrorCode();

		// Assert - Property takes precedence
		result.ShouldBe(100);
	}

	[Fact]
	public void GetStatusCode_WithBothPropertyAndData_PreferencesProperty()
	{
		// Arrange
		var exception = new ExceptionWithStatusCode(400);
		exception.Data["StatusCode"] = 500;

		// Act
		var result = exception.GetStatusCode();

		// Assert - Property takes precedence
		result.ShouldBe(400);
	}

	#endregion

	#region Test Exception Types

	private sealed class ExceptionWithErrorCode : Exception
	{
		public ExceptionWithErrorCode(int errorCode)
			: base("Exception with error code")
		{
			ErrorCode = errorCode;
		}

		public int ErrorCode { get; }
	}

	private sealed class ExceptionWithStatusCode : Exception
	{
		public ExceptionWithStatusCode(int statusCode)
			: base("Exception with status code")
		{
			StatusCode = statusCode;
		}

		public int StatusCode { get; }
	}

	#endregion
}
