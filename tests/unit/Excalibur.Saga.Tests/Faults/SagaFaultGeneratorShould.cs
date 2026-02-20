// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Saga.Faults;

namespace Excalibur.Saga.Tests.Faults;

/// <summary>
/// Unit tests for <see cref="SagaFaultGenerator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaFaultGeneratorShould
{
	#region CreateFaultEvent (string reason) Tests

	[Fact]
	public void CreateFaultEvent_WithStringReason_ShouldSetSagaId()
	{
		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-123", "ProcessPayment", "Insufficient funds");

		// Assert
		result.SagaId.ShouldBe("saga-123");
	}

	[Fact]
	public void CreateFaultEvent_WithStringReason_ShouldSetAggregateIdToSagaId()
	{
		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-456", "ProcessPayment", "Insufficient funds");

		// Assert
		result.AggregateId.ShouldBe("saga-456");
	}

	[Fact]
	public void CreateFaultEvent_WithStringReason_ShouldSetFailedStepName()
	{
		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-123", "ProcessPayment", "Insufficient funds");

		// Assert
		result.FailedStepName.ShouldBe("ProcessPayment");
	}

	[Fact]
	public void CreateFaultEvent_WithStringReason_ShouldSetFaultReason()
	{
		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-123", "ProcessPayment", "Insufficient funds");

		// Assert
		result.FaultReason.ShouldBe("Insufficient funds");
	}

	[Fact]
	public void CreateFaultEvent_WithStringReason_ShouldSetOccurredAt()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-123", "Step1", "Error");

		// Assert
		result.OccurredAt.ShouldBeGreaterThanOrEqualTo(before);
		result.OccurredAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}

	[Fact]
	public void CreateFaultEvent_WithStringReason_ShouldReturnISagaFaultEvent()
	{
		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-123", "Step1", "Error");

		// Assert
		result.ShouldBeAssignableTo<ISagaFaultEvent>();
	}

	[Fact]
	public void CreateFaultEvent_WithStringReason_ShouldReturnIDomainEvent()
	{
		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-123", "Step1", "Error");

		// Assert
		result.ShouldBeAssignableTo<IDomainEvent>();
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	public void CreateFaultEvent_WithStringReason_ShouldThrow_WhenSagaIdIsNullOrEmpty(string? sagaId)
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => SagaFaultGenerator.CreateFaultEvent(sagaId!, "Step1", "Error"));
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	public void CreateFaultEvent_WithStringReason_ShouldThrow_WhenStepNameIsNullOrEmpty(string? stepName)
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => SagaFaultGenerator.CreateFaultEvent("saga-123", stepName!, "Error"));
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	public void CreateFaultEvent_WithStringReason_ShouldThrow_WhenReasonIsNullOrEmpty(string? reason)
	{
		// Act & Assert
		Should.Throw<ArgumentException>(
			() => SagaFaultGenerator.CreateFaultEvent("saga-123", "Step1", reason!));
	}

	#endregion

	#region CreateFaultEvent (Exception) Tests

	[Fact]
	public void CreateFaultEvent_WithException_ShouldSetFaultReasonToExceptionMessage()
	{
		// Arrange
		var exception = new InvalidOperationException("Something went wrong");

		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-123", "ProcessPayment", exception);

		// Assert
		result.FaultReason.ShouldBe("Something went wrong");
	}

	[Fact]
	public void CreateFaultEvent_WithException_ShouldSetSagaId()
	{
		// Arrange
		var exception = new InvalidOperationException("Error");

		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-789", "Step1", exception);

		// Assert
		result.SagaId.ShouldBe("saga-789");
	}

	[Fact]
	public void CreateFaultEvent_WithException_ShouldSetFailedStepName()
	{
		// Arrange
		var exception = new InvalidOperationException("Error");

		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-123", "ReserveInventory", exception);

		// Assert
		result.FailedStepName.ShouldBe("ReserveInventory");
	}

	[Fact]
	public void CreateFaultEvent_WithException_ShouldIncludeExceptionTypeInMetadata()
	{
		// Arrange
		var exception = new InvalidOperationException("Error");

		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-123", "Step1", exception);

		// Assert
		result.Metadata.ShouldNotBeNull();
		result.Metadata["ExceptionType"].ShouldBe(typeof(InvalidOperationException).FullName);
	}

	[Fact]
	public void CreateFaultEvent_WithException_ShouldIncludeStackTraceInMetadata()
	{
		// Arrange
		Exception exception;
		try
		{
			throw new InvalidOperationException("Error");
		}
		catch (Exception ex)
		{
			exception = ex;
		}

		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-123", "Step1", exception);

		// Assert
		result.Metadata.ShouldNotBeNull();
		result.Metadata["StackTrace"].ShouldNotBe(string.Empty);
	}

	[Fact]
	public void CreateFaultEvent_WithException_ShouldHandleNullStackTrace()
	{
		// Arrange — exception not thrown, so StackTrace is null
		var exception = new InvalidOperationException("Error");

		// Act
		var result = SagaFaultGenerator.CreateFaultEvent("saga-123", "Step1", exception);

		// Assert
		result.Metadata.ShouldNotBeNull();
		result.Metadata["StackTrace"].ShouldBe(string.Empty);
	}

	[Fact]
	public void CreateFaultEvent_WithException_ShouldThrow_WhenExceptionIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => SagaFaultGenerator.CreateFaultEvent("saga-123", "Step1", (Exception)null!));
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	public void CreateFaultEvent_WithException_ShouldThrow_WhenSagaIdIsNullOrEmpty(string? sagaId)
	{
		// Arrange
		var exception = new InvalidOperationException("Error");

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => SagaFaultGenerator.CreateFaultEvent(sagaId!, "Step1", exception));
	}

	#endregion
}
