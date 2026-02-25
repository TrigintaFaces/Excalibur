// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Tests for <see cref="OperationTimeoutException"/> to verify operation timeout
/// error handling with HTTP 408 status code and duration information.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OperationTimeoutExceptionShould
{
	[Fact]
	public void InheritFromDispatchException()
	{
		// Arrange & Act
		var exception = new OperationTimeoutException();

		// Assert
		_ = exception.ShouldBeAssignableTo<DispatchException>();
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void Use408StatusCode()
	{
		// Arrange & Act
		var exception = new OperationTimeoutException();

		// Assert
		exception.DispatchStatusCode.ShouldBe((int)HttpStatusCode.RequestTimeout);
	}

	[Fact]
	public void UseTimeoutOperationExceededErrorCode()
	{
		// Arrange & Act
		var exception = new OperationTimeoutException();

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.TimeoutOperationExceeded);
	}

	[Fact]
	public void SetOperationAndDurationWhenCreated()
	{
		// Arrange
		var operation = "DatabaseQuery";
		var duration = TimeSpan.FromSeconds(30);

		// Act
		var exception = new OperationTimeoutException(operation, duration);

		// Assert
		exception.Operation.ShouldBe(operation);
		exception.Duration.ShouldBe(duration);
	}

	[Fact]
	public void SetTimeoutLimitWhenProvided()
	{
		// Arrange
		var operation = "ExternalApiCall";
		var duration = TimeSpan.FromSeconds(15);
		var timeout = TimeSpan.FromSeconds(10);

		// Act
		var exception = new OperationTimeoutException(operation, duration, timeout);

		// Assert
		exception.Operation.ShouldBe(operation);
		exception.Duration.ShouldBe(duration);
		exception.Timeout.ShouldBe(timeout);
	}

	[Fact]
	public void FormatMessageWithDuration()
	{
		// Arrange & Act
		var exception = new OperationTimeoutException("ProcessData", TimeSpan.FromSeconds(5));

		// Assert
		exception.Message.ShouldContain("ProcessData");
		exception.Message.ShouldContain("timed out");
	}

	[Fact]
	public void FormatMessageWithTimeoutLimit()
	{
		// Arrange & Act
		var exception = new OperationTimeoutException("SyncData", TimeSpan.FromSeconds(35), TimeSpan.FromSeconds(30));

		// Assert
		exception.Message.ShouldContain("SyncData");
		exception.Message.ShouldContain("exceeded");
	}

	[Fact]
	public void CreateFromCancellationException()
	{
		// Arrange
		var operationCanceledException = new OperationCanceledException("Task was cancelled");
		var elapsed = TimeSpan.FromSeconds(10);

		// Act
		var exception = OperationTimeoutException.FromCancellation("LongRunningTask", elapsed, operationCanceledException);

		// Assert
		exception.Operation.ShouldBe("LongRunningTask");
		exception.Duration.ShouldBe(elapsed);
		exception.InnerException.ShouldBe(operationCanceledException);
	}

	[Fact]
	public void CreateDatabaseQueryTimeout()
	{
		// Arrange & Act
		var exception = OperationTimeoutException.DatabaseQuery("GetUserOrders", TimeSpan.FromSeconds(60));

		// Assert
		exception.Operation.ShouldBe("Database:GetUserOrders");
		exception.Duration.ShouldBe(TimeSpan.FromSeconds(60));
		exception.Context.ShouldContainKeyAndValue("queryName", "GetUserOrders");
	}

	[Fact]
	public void CreateExternalServiceTimeout()
	{
		// Arrange & Act
		var exception = OperationTimeoutException.ExternalService("PaymentGateway", TimeSpan.FromSeconds(30));

		// Assert
		exception.Operation.ShouldBe("ExternalService:PaymentGateway");
		exception.Duration.ShouldBe(TimeSpan.FromSeconds(30));
		exception.Context.ShouldContainKeyAndValue("serviceName", "PaymentGateway");
	}

	[Fact]
	public void IncludeDurationInDispatchProblemDetailsExtensions()
	{
		// Arrange
		var exception = new OperationTimeoutException("TestOperation", TimeSpan.FromSeconds(5));

		// Act
		var dispatchProblemDetails = exception.ToDispatchProblemDetails();

		// Assert - ToDispatchProblemDetails() includes context in Extensions
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("operation", "TestOperation");
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("durationMs", 5000L);
	}

	[Fact]
	public void IncludeTimeoutLimitInDispatchProblemDetailsExtensions()
	{
		// Arrange
		var exception = new OperationTimeoutException("SlowOperation", TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(10));

		// Act
		var dispatchProblemDetails = exception.ToDispatchProblemDetails();

		// Assert - ToDispatchProblemDetails() includes context in Extensions
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("durationMs", 15000L);
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("timeoutMs", 10000L);
	}

	[Fact]
	public void IncludeDurationInContext()
	{
		// Arrange & Act
		var exception = new OperationTimeoutException("BatchProcess", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(3));

		// Assert
		exception.Context.ShouldContainKeyAndValue("operation", "BatchProcess");
		exception.Context.ShouldContainKeyAndValue("durationMs", 300000L);
		exception.Context.ShouldContainKeyAndValue("timeoutMs", 180000L);
	}

	[Fact]
	public void CreateFromMessageOnly()
	{
		// Arrange & Act
		var exception = new OperationTimeoutException("Custom timeout message");

		// Assert
		exception.Message.ShouldBe("Custom timeout message");
		exception.DispatchStatusCode.ShouldBe(408);
	}

	[Fact]
	public void IncludeInnerExceptionWhenProvided()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new OperationTimeoutException("Timeout occurred", innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(OperationTimeoutException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void HaveDefaultMessage()
	{
		// Arrange & Act
		var exception = new OperationTimeoutException();

		// Assert
		exception.Message.ShouldContain("timed out");
	}

	[Fact]
	public void FormatDurationInMilliseconds()
	{
		// Arrange & Act
		var exception = new OperationTimeoutException("QuickOp", TimeSpan.FromMilliseconds(500));

		// Assert
		exception.Message.ShouldContain("500ms");
	}

	[Fact]
	public void FormatDurationInSeconds()
	{
		// Arrange & Act
		var exception = new OperationTimeoutException("MediumOp", TimeSpan.FromSeconds(5.5));

		// Assert
		exception.Message.ShouldContain("5.5s");
	}

	[Fact]
	public void FormatDurationInMinutes()
	{
		// Arrange & Act
		var exception = new OperationTimeoutException("LongOp", TimeSpan.FromMinutes(2.5));

		// Assert
		exception.Message.ShouldContain("2.5m");
	}
}
