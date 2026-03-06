// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Tests for the unified exception hierarchy to verify correct inheritance chain
/// and ensure all specialized exceptions are catchable as ApiException.
/// Sprint 438: Exception Hierarchy Consolidation (bd-0fkmg)
/// Sprint 585: Persistence exceptions moved to Excalibur.Data.Abstractions
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ExceptionHierarchyShould
{
	/// <summary>
	/// Verifies all exception types are assignable to ApiException.
	/// </summary>
	[Theory]
	[InlineData(typeof(DispatchException))]
	[InlineData(typeof(ResourceException))]
	[InlineData(typeof(ResourceNotFoundException))]
	[InlineData(typeof(ConflictException))]
	[InlineData(typeof(ConcurrencyException))]
	[InlineData(typeof(ForbiddenException))]
	[InlineData(typeof(OperationTimeoutException))]
	public void BeAssignableToApiException(Type exceptionType)
	{
		// Assert - All specialized exceptions should be assignable to ApiException
		typeof(ApiException).IsAssignableFrom(exceptionType).ShouldBeTrue(
			$"{exceptionType.Name} should be assignable to ApiException");
	}

	/// <summary>
	/// Verifies Dispatch-only exceptions are assignable to DispatchException.
	/// ForbiddenException and OperationTimeoutException still extend DispatchException.
	/// ResourceException hierarchy moved to Data.Abstractions and extends ApiException directly.
	/// </summary>
	[Theory]
	[InlineData(typeof(ForbiddenException))]
	[InlineData(typeof(OperationTimeoutException))]
	public void BeAssignableToDispatchException(Type exceptionType)
	{
		typeof(DispatchException).IsAssignableFrom(exceptionType).ShouldBeTrue(
			$"{exceptionType.Name} should be assignable to DispatchException");
	}

	[Theory]
	[InlineData(typeof(ResourceException))]
	[InlineData(typeof(ResourceNotFoundException))]
	[InlineData(typeof(ConflictException))]
	[InlineData(typeof(ConcurrencyException))]
	public void NotBeAssignableToDispatchException_ForDataAbstractionsTypes(Type exceptionType)
	{
		// Resource exceptions moved to Data.Abstractions and no longer extend DispatchException
		typeof(DispatchException).IsAssignableFrom(exceptionType).ShouldBeFalse(
			$"{exceptionType.Name} should NOT be assignable to DispatchException (moved to Data.Abstractions)");
	}

	[Fact]
	public void CatchAllSpecializedExceptionsAsApiException()
	{
		// Arrange
		var exceptions = new Exception[]
		{
			new ResourceNotFoundException("Resource", "id-1"),
			new ConflictException("Resource", "field", "reason"),
			new ConcurrencyException("Resource", "id-2", 1, 2),
			new ForbiddenException("Resource", "Operation"),
			new OperationTimeoutException("Operation", TimeSpan.FromSeconds(5)),
		};

		// Act & Assert - All should be catchable as ApiException
		foreach (var exception in exceptions)
		{
			try
			{
				throw exception;
			}
			catch (ApiException caught)
			{
				_ = caught.ShouldNotBeNull();
				_ = caught.ToProblemDetails().ShouldNotBeNull();
			}
		}
	}

	[Fact]
	public void MaintainResourceExceptionHierarchy()
	{
		// ResourceException hierarchy (now in Excalibur.Data.Abstractions):
		// ApiException
		// └── ResourceException
		//     ├── ResourceNotFoundException
		//     └── ConflictException
		//         └── ConcurrencyException
		//
		// ForbiddenException now extends DispatchException (stays in Dispatch)

		// Assert
		typeof(ResourceException).IsAssignableFrom(typeof(ResourceNotFoundException)).ShouldBeTrue();
		typeof(ResourceException).IsAssignableFrom(typeof(ConflictException)).ShouldBeTrue();
		typeof(ConflictException).IsAssignableFrom(typeof(ConcurrencyException)).ShouldBeTrue();

		// ForbiddenException no longer inherits from ResourceException
		typeof(ResourceException).IsAssignableFrom(typeof(ForbiddenException)).ShouldBeFalse();
	}

	[Fact]
	public void NotInheritFromResourceException_ForOperationTimeoutException()
	{
		// OperationTimeoutException inherits from DispatchException, not ResourceException
		typeof(ResourceException).IsAssignableFrom(typeof(OperationTimeoutException)).ShouldBeFalse();
		typeof(DispatchException).IsAssignableFrom(typeof(OperationTimeoutException)).ShouldBeTrue();
	}

	[Theory]
	[InlineData(typeof(ApiException))]
	[InlineData(typeof(DispatchException))]
	[InlineData(typeof(ResourceException))]
	[InlineData(typeof(ResourceNotFoundException))]
	[InlineData(typeof(ConflictException))]
	[InlineData(typeof(ConcurrencyException))]
	[InlineData(typeof(ForbiddenException))]
	[InlineData(typeof(OperationTimeoutException))]
	public void BeSerializable(Type exceptionType)
	{
		// Assert - All exceptions should have the Serializable attribute
		exceptionType.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty($"{exceptionType.Name} should have [Serializable] attribute");
	}

	[Fact]
	public void ProvideToProblemDetailsForAllTypes()
	{
		// Arrange
		var exceptions = new ApiException[]
		{
			new ApiException("API error"),
			new DispatchException("Dispatch error"),
			new ResourceException("Resource error"),
			new ResourceNotFoundException("Resource", "id"),
			new ConflictException("Resource", "field", "reason"),
			new ConcurrencyException("Resource", "id", 1, 2),
			new ForbiddenException("Resource", "Operation"),
			new OperationTimeoutException("Operation", TimeSpan.FromSeconds(5)),
		};

		// Act & Assert
		foreach (var exception in exceptions)
		{
			var problemDetails = exception.ToProblemDetails();

			_ = problemDetails.ShouldNotBeNull($"{exception.GetType().Name} should provide problem details");
			problemDetails.Type.ShouldNotBeNullOrEmpty();
			problemDetails.Title.ShouldNotBeNullOrEmpty();
			_ = problemDetails.Status.ShouldNotBeNull();
			problemDetails.Status.Value.ShouldBeGreaterThan(0);
		}
	}

	[Fact]
	public void HaveUniqueErrorCodesPerDispatchType()
	{
		// Arrange - Only Dispatch-based exceptions have ErrorCode
		var forbidden = new ForbiddenException();
		var timeout = new OperationTimeoutException();

		// Assert - Each exception type should have a distinct error code
		forbidden.ErrorCode.ShouldBe(ErrorCodes.SecurityForbidden);
		timeout.ErrorCode.ShouldBe(ErrorCodes.TimeoutOperationExceeded);

		// Verify they are different
		forbidden.ErrorCode.ShouldNotBe(timeout.ErrorCode);
	}

	[Fact]
	public void MapCorrectStatusCodeFromCategory()
	{
		// Arrange - Create exceptions with different error code prefixes
		var resourceException = new DispatchException(ErrorCodes.ResourceNotFound, "Resource error");
		var validationException = new DispatchException(ErrorCodes.ValidationFailed, "Validation error");
		var securityException = new DispatchException(ErrorCodes.SecurityAccessDenied, "Security error");
		var timeoutException = new DispatchException(ErrorCodes.TimeoutOperation, "Timeout error");

		// Act
		var resourcePd = resourceException.ToProblemDetails();
		var validationPd = validationException.ToProblemDetails();
		var securityPd = securityException.ToProblemDetails();
		var timeoutPd = timeoutException.ToProblemDetails();

		// Assert - Status codes should be determined by error code prefix
		resourcePd.Status.ShouldBe(404); // RES_ → 404
		validationPd.Status.ShouldBe(400); // VAL_ → 400
		securityPd.Status.ShouldBe(401); // SEC_ → 401
		timeoutPd.Status.ShouldBe(408); // TIM_ → 408
	}
}
