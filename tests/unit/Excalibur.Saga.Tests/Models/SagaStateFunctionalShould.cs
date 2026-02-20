// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Models;

/// <summary>
/// Functional tests for <see cref="SagaState"/> covering
/// data serialization round-trips, step history tracking, and metadata management.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SagaStateFunctionalShould
{
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality

	[Fact]
	public void RoundTripSagaData_ViaGetDataSetData()
	{
		// Arrange
		var state = new SagaState();
		var data = new TestSagaData { Value = "order-123", Counter = 5 };

		// Act
		state.SetData(data);
		var loaded = state.GetData<TestSagaData>();

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Value.ShouldBe("order-123");
		loaded.Counter.ShouldBe(5);
	}

	[Fact]
	public void ReturnNull_WhenDataJsonIsEmpty()
	{
		// Arrange
		var state = new SagaState();

		// Act
		var result = state.GetData<TestSagaData>();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void SetDataType_ToAssemblyQualifiedName()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.SetData(new TestSagaData { Value = "test" });

		// Assert
		state.DataType.ShouldNotBeEmpty();
		state.DataType.ShouldContain("TestSagaData");
	}

	[Fact]
	public void OverwriteDataOnMultipleSetDataCalls()
	{
		// Arrange
		var state = new SagaState();
		state.SetData(new TestSagaData { Value = "first", Counter = 1 });

		// Act
		state.SetData(new TestSagaData { Value = "second", Counter = 2 });
		var loaded = state.GetData<TestSagaData>();

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Value.ShouldBe("second");
		loaded.Counter.ShouldBe(2);
	}

#pragma warning restore IL3050
#pragma warning restore IL2026

	[Fact]
	public void TrackStepHistory()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.StepHistory.Add(new StepExecutionRecord
		{
			StepName = "Step1",
			StepIndex = 0,
			StartedAt = DateTime.UtcNow.AddSeconds(-1),
			CompletedAt = DateTime.UtcNow,
			IsSuccess = true,
		});

		state.StepHistory.Add(new StepExecutionRecord
		{
			StepName = "Step2",
			StepIndex = 1,
			StartedAt = DateTime.UtcNow,
			CompletedAt = DateTime.UtcNow,
			IsSuccess = false,
			ErrorMessage = "Failed",
			RetryCount = 2,
		});

		// Assert
		state.StepHistory.Count.ShouldBe(2);
		state.StepHistory[0].IsSuccess.ShouldBeTrue();
		state.StepHistory[1].IsSuccess.ShouldBeFalse();
		state.StepHistory[1].RetryCount.ShouldBe(2);
	}

	[Fact]
	public void StoreAndRetrieveMetadata()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.Metadata["OrderId"] = "ORD-123";
		state.Metadata["CustomerId"] = "CUST-456";
		state.Metadata["Amount"] = 99.99;

		// Assert
		state.Metadata.Count.ShouldBe(3);
		state.Metadata["OrderId"].ShouldBe("ORD-123");
		state.Metadata["CustomerId"].ShouldBe("CUST-456");
		state.Metadata["Amount"].ShouldBe(99.99);
	}

	[Fact]
	public void HaveCorrectDefaults()
	{
		// Act
		var state = new SagaState();

		// Assert
		state.SagaId.ShouldBe(string.Empty);
		state.SagaName.ShouldBe(string.Empty);
		state.Version.ShouldBe("1.0");
		state.CorrelationId.ShouldBe(string.Empty);
		state.Status.ShouldBe(SagaStatus.Created);
		state.CurrentStepIndex.ShouldBe(0);
		state.DataJson.ShouldBe(string.Empty);
		state.DataType.ShouldBe(string.Empty);
		state.ErrorMessage.ShouldBeNull();
		state.CompletedAt.ShouldBeNull();
		state.StepHistory.ShouldNotBeNull();
		state.StepHistory.Count.ShouldBe(0);
		state.Metadata.ShouldNotBeNull();
		state.Metadata.Count.ShouldBe(0);
	}

	[Fact]
	public void TrackSagaLifecycleTimestamps()
	{
		// Arrange
		var startTime = DateTime.UtcNow;

		var state = new SagaState
		{
			SagaId = "saga-1",
			StartedAt = startTime,
			Status = SagaStatus.Running,
		};

		// Act - simulate completion
		state.Status = SagaStatus.Completed;
		state.CompletedAt = startTime.AddMinutes(5);
		state.LastUpdatedAt = startTime.AddMinutes(5);

		// Assert
		state.StartedAt.ShouldBe(startTime);
		state.CompletedAt.ShouldBe(startTime.AddMinutes(5));
		state.LastUpdatedAt.ShouldBe(startTime.AddMinutes(5));
	}

	[Fact]
	public void TrackErrorState()
	{
		// Arrange
		var state = new SagaState
		{
			SagaId = "saga-1",
			Status = SagaStatus.Running,
		};

		// Act - simulate failure
		state.Status = SagaStatus.Failed;
		state.ErrorMessage = "Payment declined: insufficient funds";

		// Assert
		state.Status.ShouldBe(SagaStatus.Failed);
		state.ErrorMessage.ShouldBe("Payment declined: insufficient funds");
	}

	[Fact]
	public void SupportCorrelationTracking()
	{
		// Arrange & Act
		var state = new SagaState
		{
			SagaId = "saga-42",
			SagaName = "OrderFulfillment",
			CorrelationId = "order-ORD-789",
			Version = "2.0",
		};

		// Assert
		state.SagaId.ShouldBe("saga-42");
		state.SagaName.ShouldBe("OrderFulfillment");
		state.CorrelationId.ShouldBe("order-ORD-789");
		state.Version.ShouldBe("2.0");
	}
}
