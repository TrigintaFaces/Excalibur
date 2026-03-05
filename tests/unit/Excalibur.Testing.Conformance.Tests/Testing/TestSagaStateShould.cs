// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Testing.Conformance;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing;

/// <summary>
/// Unit tests for <see cref="TestSagaState"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class TestSagaStateShould
{
	[Fact]
	public void Have_Default_Status_As_Pending()
	{
		// Arrange & Act
		var state = new TestSagaState();

		// Assert
		state.Status.ShouldBe("Pending");
	}

	[Fact]
	public void Have_Default_Counter_As_Zero()
	{
		// Arrange & Act
		var state = new TestSagaState();

		// Assert
		state.Counter.ShouldBe(0);
	}

	[Fact]
	public void Have_Default_CreatedUtc_Near_UtcNow()
	{
		// Arrange
		var before = DateTime.UtcNow;

		// Act
		var state = new TestSagaState();

		// Assert
		var after = DateTime.UtcNow;
		state.CreatedUtc.ShouldBeGreaterThanOrEqualTo(before);
		state.CreatedUtc.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Have_Default_CompletedUtc_As_Null()
	{
		// Arrange & Act
		var state = new TestSagaState();

		// Assert
		state.CompletedUtc.ShouldBeNull();
	}

	[Fact]
	public void Have_Default_Data_As_Empty_Dictionary()
	{
		// Arrange & Act
		var state = new TestSagaState();

		// Assert
		state.Data.ShouldNotBeNull();
		state.Data.ShouldBeEmpty();
	}

	[Fact]
	public void Allow_Setting_Status()
	{
		// Arrange
		var state = new TestSagaState();

		// Act
		state.Status = "Completed";

		// Assert
		state.Status.ShouldBe("Completed");
	}

	[Fact]
	public void Allow_Setting_Counter()
	{
		// Arrange
		var state = new TestSagaState();

		// Act
		state.Counter = 42;

		// Assert
		state.Counter.ShouldBe(42);
	}

	[Fact]
	public void Allow_Setting_CreatedUtc()
	{
		// Arrange
		var state = new TestSagaState();
		var customTime = new DateTime(2025, 3, 15, 8, 30, 0, DateTimeKind.Utc);

		// Act
		state.CreatedUtc = customTime;

		// Assert
		state.CreatedUtc.ShouldBe(customTime);
	}

	[Fact]
	public void Allow_Setting_CompletedUtc()
	{
		// Arrange
		var state = new TestSagaState();
		var completionTime = new DateTime(2025, 3, 15, 10, 45, 0, DateTimeKind.Utc);

		// Act
		state.CompletedUtc = completionTime;

		// Assert
		state.CompletedUtc.ShouldBe(completionTime);
	}

	[Fact]
	public void Allow_Adding_Data_Entries()
	{
		// Arrange
		var state = new TestSagaState();

		// Act
		state.Data["key1"] = "value1";
		state.Data["key2"] = "value2";

		// Assert
		state.Data.Count.ShouldBe(2);
		state.Data["key1"].ShouldBe("value1");
		state.Data["key2"].ShouldBe("value2");
	}

	[Fact]
	public void Allow_Replacing_Data_Dictionary()
	{
		// Arrange
		var state = new TestSagaState();
		var newData = new Dictionary<string, string>
		{
			["replaced1"] = "newValue1",
			["replaced2"] = "newValue2",
			["replaced3"] = "newValue3"
		};

		// Act
		state.Data = newData;

		// Assert
		state.Data.ShouldBeSameAs(newData);
		state.Data.Count.ShouldBe(3);
	}

	[Fact]
	public void Create_With_SagaId()
	{
		// Arrange
		var sagaId = Guid.NewGuid();

		// Act
		var state = TestSagaState.Create(sagaId);

		// Assert
		state.ShouldNotBeNull();
		state.SagaId.ShouldBe(sagaId);
	}

	[Fact]
	public void Create_With_Status_As_Created()
	{
		// Arrange
		var sagaId = Guid.NewGuid();

		// Act
		var state = TestSagaState.Create(sagaId);

		// Assert
		state.Status.ShouldBe("Created");
	}

	[Fact]
	public void Create_With_Counter_As_Zero()
	{
		// Arrange
		var sagaId = Guid.NewGuid();

		// Act
		var state = TestSagaState.Create(sagaId);

		// Assert
		state.Counter.ShouldBe(0);
	}

	[Fact]
	public void Create_With_CreatedUtc_Near_UtcNow()
	{
		// Arrange
		var before = DateTime.UtcNow;
		var sagaId = Guid.NewGuid();

		// Act
		var state = TestSagaState.Create(sagaId);

		// Assert
		var after = DateTime.UtcNow;
		state.CreatedUtc.ShouldBeGreaterThanOrEqualTo(before);
		state.CreatedUtc.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Inherit_From_SagaState()
	{
		// Arrange & Act
		var state = new TestSagaState();

		// Assert
		state.ShouldBeAssignableTo<SagaState>();
	}

	[Fact]
	public void Have_SagaId_Property_From_Base_Class()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };

		// Assert
		state.SagaId.ShouldBe(sagaId);
	}

	[Fact]
	public void Support_Typical_Saga_Workflow()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);

		// Act - Simulate saga workflow
		state.Status = "Processing";
		state.Counter++;
		state.Data["orderId"] = "ORD-123";

		state.Status = "PaymentPending";
		state.Counter++;
		state.Data["paymentId"] = "PAY-456";

		state.Status = "Completed";
		state.Counter++;
		state.CompletedUtc = DateTime.UtcNow;

		// Assert
		state.SagaId.ShouldBe(sagaId);
		state.Status.ShouldBe("Completed");
		state.Counter.ShouldBe(3);
		state.Data.Count.ShouldBe(2);
		state.CompletedUtc.ShouldNotBeNull();
		state.CompletedUtc.Value.ShouldBeGreaterThanOrEqualTo(state.CreatedUtc);
	}

	[Fact]
	public void Support_State_Transition_Tracking()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);
		var transitions = new List<string> { state.Status };

		// Act
		state.Status = "Step1";
		transitions.Add(state.Status);

		state.Status = "Step2";
		transitions.Add(state.Status);

		state.Status = "Completed";
		transitions.Add(state.Status);

		// Assert
		transitions.ShouldBe(["Created", "Step1", "Step2", "Completed"]);
	}

	[Fact]
	public void Support_Counter_Increment_For_Retry_Tracking()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);

		// Act - Simulate retry attempts
		for (var i = 0; i < 5; i++)
		{
			state.Counter++;
		}

		// Assert
		state.Counter.ShouldBe(5);
	}

	[Fact]
	public void Support_Storing_Correlation_Data()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = TestSagaState.Create(sagaId);

		// Act
		state.Data["correlationId"] = Guid.NewGuid().ToString();
		state.Data["sourceSystem"] = "OrderService";
		state.Data["targetSystem"] = "PaymentService";
		state.Data["messageId"] = "MSG-789";

		// Assert
		state.Data.Count.ShouldBe(4);
		state.Data.ContainsKey("correlationId").ShouldBeTrue();
		state.Data.ContainsKey("sourceSystem").ShouldBeTrue();
		state.Data.ContainsKey("targetSystem").ShouldBeTrue();
		state.Data.ContainsKey("messageId").ShouldBeTrue();
	}
}
