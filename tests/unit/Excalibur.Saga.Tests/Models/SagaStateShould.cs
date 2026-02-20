// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Models;

/// <summary>
/// Unit tests for <see cref="SagaState"/>.
/// Verifies saga state model behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaStateShould
{
	#region Default Value Tests

	[Fact]
	public void HaveEmptyStringDefaults()
	{
		// Act
		var state = new SagaState();

		// Assert
		state.SagaId.ShouldBe(string.Empty);
		state.SagaName.ShouldBe(string.Empty);
		state.CorrelationId.ShouldBe(string.Empty);
		state.DataJson.ShouldBe(string.Empty);
		state.DataType.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultVersion()
	{
		// Act
		var state = new SagaState();

		// Assert
		state.Version.ShouldBe("1.0");
	}

	[Fact]
	public void HaveDefaultStatus()
	{
		// Act
		var state = new SagaState();

		// Assert
		state.Status.ShouldBe(SagaStatus.Created);
	}

	[Fact]
	public void HaveEmptyStepHistory()
	{
		// Act
		var state = new SagaState();

		// Assert
		state.StepHistory.ShouldNotBeNull();
		state.StepHistory.ShouldBeEmpty();
	}

	[Fact]
	public void HaveEmptyMetadata()
	{
		// Act
		var state = new SagaState();

		// Assert
		state.Metadata.ShouldNotBeNull();
		state.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void HaveNullCompletedAt()
	{
		// Act
		var state = new SagaState();

		// Assert
		state.CompletedAt.ShouldBeNull();
	}

	[Fact]
	public void HaveNullErrorMessage()
	{
		// Act
		var state = new SagaState();

		// Assert
		state.ErrorMessage.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSagaIdToBeSet()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.SagaId = "saga-123";

		// Assert
		state.SagaId.ShouldBe("saga-123");
	}

	[Fact]
	public void AllowSagaNameToBeSet()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.SagaName = "OrderSaga";

		// Assert
		state.SagaName.ShouldBe("OrderSaga");
	}

	[Fact]
	public void AllowVersionToBeSet()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.Version = "2.0";

		// Assert
		state.Version.ShouldBe("2.0");
	}

	[Fact]
	public void AllowCorrelationIdToBeSet()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.CorrelationId = "corr-456";

		// Assert
		state.CorrelationId.ShouldBe("corr-456");
	}

	[Fact]
	public void AllowStatusToBeSet()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.Status = SagaStatus.Running;

		// Assert
		state.Status.ShouldBe(SagaStatus.Running);
	}

	[Fact]
	public void AllowCurrentStepIndexToBeSet()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.CurrentStepIndex = 5;

		// Assert
		state.CurrentStepIndex.ShouldBe(5);
	}

	[Fact]
	public void AllowDataJsonToBeSet()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.DataJson = "{\"orderId\": 123}";

		// Assert
		state.DataJson.ShouldBe("{\"orderId\": 123}");
	}

	[Fact]
	public void AllowDataTypeToBeSet()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.DataType = "OrderData";

		// Assert
		state.DataType.ShouldBe("OrderData");
	}

	[Fact]
	public void AllowStartedAtToBeSet()
	{
		// Arrange
		var state = new SagaState();
		var startTime = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);

		// Act
		state.StartedAt = startTime;

		// Assert
		state.StartedAt.ShouldBe(startTime);
	}

	[Fact]
	public void AllowCompletedAtToBeSet()
	{
		// Arrange
		var state = new SagaState();
		var completedTime = new DateTime(2026, 1, 15, 11, 0, 0, DateTimeKind.Utc);

		// Act
		state.CompletedAt = completedTime;

		// Assert
		state.CompletedAt.ShouldBe(completedTime);
	}

	[Fact]
	public void AllowLastUpdatedAtToBeSet()
	{
		// Arrange
		var state = new SagaState();
		var updateTime = new DateTime(2026, 1, 15, 10, 45, 0, DateTimeKind.Utc);

		// Act
		state.LastUpdatedAt = updateTime;

		// Assert
		state.LastUpdatedAt.ShouldBe(updateTime);
	}

	[Fact]
	public void AllowErrorMessageToBeSet()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.ErrorMessage = "Something went wrong";

		// Assert
		state.ErrorMessage.ShouldBe("Something went wrong");
	}

	#endregion

	#region StepHistory Tests

	[Fact]
	public void AllowAddingToStepHistory()
	{
		// Arrange
		var state = new SagaState();
		var record = new StepExecutionRecord
		{
			StepName = "Step1",
			IsSuccess = true
		};

		// Act
		state.StepHistory.Add(record);

		// Assert
		state.StepHistory.ShouldContain(record);
		state.StepHistory.Count.ShouldBe(1);
	}

	#endregion

	#region Metadata Tests

	[Fact]
	public void AllowAddingToMetadata()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.Metadata["key1"] = "value1";
		state.Metadata["key2"] = 42;

		// Assert
		state.Metadata["key1"].ShouldBe("value1");
		state.Metadata["key2"].ShouldBe(42);
		state.Metadata.Count.ShouldBe(2);
	}

	[Fact]
	public void UseCaseOrdinalComparisonForMetadataKeys()
	{
		// Arrange
		var state = new SagaState();

		// Act
		state.Metadata["Key"] = "value1";
		state.Metadata["KEY"] = "value2";

		// Assert - Keys should be treated as different (case-sensitive)
		state.Metadata.Count.ShouldBe(2);
	}

	#endregion

	#region GetData Tests

	[Fact]
	public void ReturnNull_WhenDataJsonIsEmpty()
	{
		// Arrange
		var state = new SagaState { DataJson = string.Empty };

		// Act
		var data = state.GetData<TestSagaData>();

		// Assert
		data.ShouldBeNull();
	}

	[Fact]
	public void ReturnNull_WhenDataJsonIsNull()
	{
		// Arrange
		var state = new SagaState { DataJson = null! };

		// Act
		var data = state.GetData<TestSagaData>();

		// Assert
		data.ShouldBeNull();
	}

	[Fact]
	public void DeserializeDataCorrectly()
	{
		// Arrange
		var state = new SagaState
		{
			DataJson = "{\"OrderId\":123,\"CustomerName\":\"John Doe\"}"
		};

		// Act
		var data = state.GetData<TestSagaData>();

		// Assert
		data.ShouldNotBeNull();
		data.OrderId.ShouldBe(123);
		data.CustomerName.ShouldBe("John Doe");
	}

	#endregion

	#region SetData Tests

	[Fact]
	public void SerializeDataCorrectly()
	{
		// Arrange
		var state = new SagaState();
		var data = new TestSagaData { OrderId = 456, CustomerName = "Jane Doe" };

		// Act
		state.SetData(data);

		// Assert
		state.DataJson.ShouldNotBeNullOrEmpty();
		state.DataJson.ShouldContain("456");
		state.DataJson.ShouldContain("Jane Doe");
	}

	[Fact]
	public void SetDataTypeCorrectly()
	{
		// Arrange
		var state = new SagaState();
		var data = new TestSagaData { OrderId = 789 };

		// Act
		state.SetData(data);

		// Assert
		state.DataType.ShouldNotBeNullOrEmpty();
		state.DataType.ShouldContain(nameof(TestSagaData));
	}

	[Fact]
	public void RoundTripDataCorrectly()
	{
		// Arrange
		var state = new SagaState();
		var originalData = new TestSagaData { OrderId = 999, CustomerName = "Round Trip" };

		// Act
		state.SetData(originalData);
		var retrievedData = state.GetData<TestSagaData>();

		// Assert
		retrievedData.ShouldNotBeNull();
		retrievedData.OrderId.ShouldBe(originalData.OrderId);
		retrievedData.CustomerName.ShouldBe(originalData.CustomerName);
	}

	#endregion

	#region Test Types

	private sealed class TestSagaData
	{
		public int OrderId { get; set; }
		public string CustomerName { get; set; } = string.Empty;
	}

	#endregion
}
