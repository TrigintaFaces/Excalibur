// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Faults;

namespace Excalibur.Saga.Tests.Faults;

/// <summary>
/// Functional tests for <see cref="SagaFaultGenerator"/> covering
/// fault event creation from strings and exceptions, metadata capture, and validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SagaFaultGeneratorFunctionalShould
{
	[Fact]
	public void CreateFaultEvent_FromStringReason()
	{
		// Act
		var fault = SagaFaultGenerator.CreateFaultEvent("saga-1", "ProcessPayment", "Timeout exceeded");

		// Assert
		fault.ShouldNotBeNull();
		fault.SagaId.ShouldBe("saga-1");
		fault.FailedStepName.ShouldBe("ProcessPayment");
		fault.FaultReason.ShouldBe("Timeout exceeded");
		fault.AggregateId.ShouldBe("saga-1");
		fault.OccurredAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void CreateFaultEvent_FromException()
	{
		// Arrange
		var exception = new InvalidOperationException("Database connection failed");

		// Act
		var fault = SagaFaultGenerator.CreateFaultEvent("saga-2", "SaveOrder", exception);

		// Assert
		fault.ShouldNotBeNull();
		fault.SagaId.ShouldBe("saga-2");
		fault.FailedStepName.ShouldBe("SaveOrder");
		fault.FaultReason.ShouldBe("Database connection failed");
		fault.AggregateId.ShouldBe("saga-2");
	}

	[Fact]
	public void CaptureExceptionMetadata()
	{
		// Arrange
		InvalidOperationException exception;
		try
		{
			throw new InvalidOperationException("Test error");
		}
		catch (InvalidOperationException ex)
		{
			exception = ex;
		}

		// Act
		var fault = SagaFaultGenerator.CreateFaultEvent("saga-3", "FailingStep", exception);

		// Assert
		fault.Metadata.ShouldNotBeNull();
		fault.Metadata["ExceptionType"].ShouldBe(typeof(InvalidOperationException).FullName);
		fault.Metadata["StackTrace"].ShouldNotBe(string.Empty);
	}

	[Fact]
	public void CaptureNestedExceptionType()
	{
		// Arrange
		var inner = new TimeoutException("Connection timed out");
		var outer = new InvalidOperationException("Operation failed", inner);

		// Act
		var fault = SagaFaultGenerator.CreateFaultEvent("saga-4", "Step1", outer);

		// Assert
		fault.Metadata.ShouldNotBeNull();
		fault.Metadata["ExceptionType"].ShouldBe(typeof(InvalidOperationException).FullName);
		fault.FaultReason.ShouldBe("Operation failed");
	}

	[Fact]
	public void SetAggregateIdToSagaId()
	{
		// Act
		var fault = SagaFaultGenerator.CreateFaultEvent("my-saga", "step", "reason");

		// Assert - AggregateId should match SagaId for event routing
		fault.AggregateId.ShouldBe("my-saga");
		fault.SagaId.ShouldBe("my-saga");
	}

	[Fact]
	public void SetOccurredAtToNearCurrentTime()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var fault = SagaFaultGenerator.CreateFaultEvent("saga-1", "step", "reason");

		// Assert
		var after = DateTimeOffset.UtcNow;
		fault.OccurredAt.ShouldBeGreaterThanOrEqualTo(before);
		fault.OccurredAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void ThrowOnNullOrEmptyParameters_StringOverload()
	{
		Should.Throw<ArgumentException>(() =>
			SagaFaultGenerator.CreateFaultEvent(null!, "step", "reason"));
		Should.Throw<ArgumentException>(() =>
			SagaFaultGenerator.CreateFaultEvent("", "step", "reason"));
		Should.Throw<ArgumentException>(() =>
			SagaFaultGenerator.CreateFaultEvent("saga", null!, "reason"));
		Should.Throw<ArgumentException>(() =>
			SagaFaultGenerator.CreateFaultEvent("saga", "", "reason"));
		Should.Throw<ArgumentException>(() =>
			SagaFaultGenerator.CreateFaultEvent("saga", "step", (string)null!));
		Should.Throw<ArgumentException>(() =>
			SagaFaultGenerator.CreateFaultEvent("saga", "step", ""));
	}

	[Fact]
	public void ThrowOnNullOrEmptyParameters_ExceptionOverload()
	{
		var ex = new InvalidOperationException("test");

		Should.Throw<ArgumentException>(() =>
			SagaFaultGenerator.CreateFaultEvent(null!, "step", ex));
		Should.Throw<ArgumentException>(() =>
			SagaFaultGenerator.CreateFaultEvent("", "step", ex));
		Should.Throw<ArgumentException>(() =>
			SagaFaultGenerator.CreateFaultEvent("saga", null!, ex));
		Should.Throw<ArgumentException>(() =>
			SagaFaultGenerator.CreateFaultEvent("saga", "", ex));
		Should.Throw<ArgumentNullException>(() =>
			SagaFaultGenerator.CreateFaultEvent("saga", "step", (Exception)null!));
	}

	[Fact]
	public void ReturnISagaFaultEvent()
	{
		// Act
		var fault = SagaFaultGenerator.CreateFaultEvent("saga-1", "step", "reason");

		// Assert
		fault.ShouldBeAssignableTo<ISagaFaultEvent>();
	}

	[Fact]
	public void GenerateUniqueEventIds()
	{
		// Act
		var fault1 = SagaFaultGenerator.CreateFaultEvent("saga-1", "step", "reason1");
		var fault2 = SagaFaultGenerator.CreateFaultEvent("saga-1", "step", "reason2");

		// Assert
		fault1.EventId.ShouldNotBe(fault2.EventId);
	}
}
