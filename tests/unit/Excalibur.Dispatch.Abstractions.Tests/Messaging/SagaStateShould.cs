// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Abstractions.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="SagaState"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class SagaStateShould : UnitTestBase
{
	#region Test Implementation

	/// <summary>
	/// Concrete implementation of SagaState for testing.
	/// </summary>
	private sealed class TestSagaState : SagaState
	{
		public string? CustomProperty { get; set; }
	}

	/// <summary>
	/// Another implementation to test inheritance.
	/// </summary>
	private sealed class OrderSagaState : SagaState
	{
		public string? OrderId { get; set; }

		public decimal TotalAmount { get; set; }
	}

	#endregion Test Implementation

	#region Default Value Tests

	[Fact]
	public void HaveDefaultSagaIdGeneratedOnCreation()
	{
		// Arrange & Act
		var state = new TestSagaState();

		// Assert
		state.SagaId.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void GenerateUniqueSagaIdForEachInstance()
	{
		// Arrange & Act
		var state1 = new TestSagaState();
		var state2 = new TestSagaState();

		// Assert
		state1.SagaId.ShouldNotBe(state2.SagaId);
	}

	[Fact]
	public void HaveCompletedDefaultToFalse()
	{
		// Arrange & Act
		var state = new TestSagaState();

		// Assert
		state.Completed.ShouldBeFalse();
	}

	#endregion Default Value Tests

	#region Property Setting Tests

	[Fact]
	public void AllowSagaIdToBeSet()
	{
		// Arrange
		var customId = Guid.NewGuid();
		var state = new TestSagaState();

		// Act
		state.SagaId = customId;

		// Assert
		state.SagaId.ShouldBe(customId);
	}

	[Fact]
	public void AllowCompletedToBeSet()
	{
		// Arrange
		var state = new TestSagaState();

		// Act
		state.Completed = true;

		// Assert
		state.Completed.ShouldBeTrue();
	}

	[Fact]
	public void AllowCompletedToBeToggled()
	{
		// Arrange
		var state = new TestSagaState();

		// Act
		state.Completed = true;
		state.Completed = false;

		// Assert
		state.Completed.ShouldBeFalse();
	}

	#endregion Property Setting Tests

	#region Inheritance Tests

	[Fact]
	public void AllowDerivedClassToAddProperties()
	{
		// Arrange
		var state = new OrderSagaState
		{
			OrderId = "ORD-12345",
			TotalAmount = 99.99m,
		};

		// Assert
		state.OrderId.ShouldBe("ORD-12345");
		state.TotalAmount.ShouldBe(99.99m);
	}

	[Fact]
	public void InheritSagaIdInDerivedClass()
	{
		// Arrange
		var state = new OrderSagaState();

		// Assert
		state.SagaId.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void InheritCompletedInDerivedClass()
	{
		// Arrange
		var state = new OrderSagaState { Completed = true };

		// Assert
		state.Completed.ShouldBeTrue();
	}

	[Fact]
	public void BeAssignableToSagaState()
	{
		// Arrange
		SagaState state = new TestSagaState();

		// Assert
		state.ShouldBeAssignableTo<SagaState>();
	}

	#endregion Inheritance Tests

	#region Saga Workflow Tests

	[Fact]
	public void SupportTypicalSagaWorkflow()
	{
		// Arrange - Create a new saga
		var state = new OrderSagaState
		{
			OrderId = "ORD-001",
		};

		// Act - Process through workflow
		state.TotalAmount = 150.00m;
		state.Completed = true;

		// Assert
		state.SagaId.ShouldNotBe(Guid.Empty);
		state.OrderId.ShouldBe("ORD-001");
		state.TotalAmount.ShouldBe(150.00m);
		state.Completed.ShouldBeTrue();
	}

	[Fact]
	public void TrackSagaIdentityThroughWorkflow()
	{
		// Arrange
		var state = new OrderSagaState();
		var originalSagaId = state.SagaId;

		// Act - Modify state during workflow
		state.OrderId = "ORD-002";
		state.TotalAmount = 200.00m;

		// Assert - SagaId remains stable
		state.SagaId.ShouldBe(originalSagaId);
	}

	[Fact]
	public void SupportRehydrationWithExistingSagaId()
	{
		// Arrange - Simulate rehydrating state from storage
		var persistedSagaId = Guid.NewGuid();

		// Act
		var state = new OrderSagaState
		{
			SagaId = persistedSagaId,
			OrderId = "ORD-003",
			TotalAmount = 300.00m,
			Completed = false,
		};

		// Assert
		state.SagaId.ShouldBe(persistedSagaId);
	}

	#endregion Saga Workflow Tests

	#region Correlation Tests

	[Fact]
	public void ProvideSagaIdForCorrelation()
	{
		// Arrange
		var state1 = new OrderSagaState { OrderId = "ORD-001" };
		var state2 = new OrderSagaState { OrderId = "ORD-002" };

		// Act - Use SagaId as correlation identifier
		var correlationId1 = state1.SagaId;
		var correlationId2 = state2.SagaId;

		// Assert - Different sagas have different correlation IDs
		correlationId1.ShouldNotBe(correlationId2);
	}

	[Fact]
	public void AllowSagaIdToBeUsedAsEventRoutingKey()
	{
		// Arrange
		var state = new OrderSagaState();

		// Act - Extract routing key for event delivery
		var routingKey = state.SagaId.ToString();

		// Assert
		routingKey.ShouldNotBeNullOrEmpty();
		Guid.TryParse(routingKey, out var parsedGuid).ShouldBeTrue();
		parsedGuid.ShouldBe(state.SagaId);
	}

	#endregion Correlation Tests

	#region State Completion Tests

	[Fact]
	public void IndicateIncompleteByDefault()
	{
		// Arrange & Act
		var state = new TestSagaState();

		// Assert
		state.Completed.ShouldBeFalse();
	}

	[Fact]
	public void IndicateCompletionWhenMarked()
	{
		// Arrange
		var state = new TestSagaState();

		// Act
		state.Completed = true;

		// Assert
		state.Completed.ShouldBeTrue();
	}

	[Fact]
	public void SupportCompletionCheck()
	{
		// Arrange
		var incompleteSaga = new TestSagaState { Completed = false };
		var completedSaga = new TestSagaState { Completed = true };

		// Assert
		incompleteSaga.Completed.ShouldBeFalse();
		completedSaga.Completed.ShouldBeTrue();
	}

	#endregion State Completion Tests

	#region Persistence Simulation Tests

	[Fact]
	public void SupportRoundTripSerialization()
	{
		// Arrange - Original state
		var originalState = new OrderSagaState
		{
			SagaId = Guid.NewGuid(),
			OrderId = "ORD-PERSIST-001",
			TotalAmount = 500.00m,
			Completed = false,
		};

		// Act - Simulate serialization/deserialization
		var restoredState = new OrderSagaState
		{
			SagaId = originalState.SagaId,
			OrderId = originalState.OrderId,
			TotalAmount = originalState.TotalAmount,
			Completed = originalState.Completed,
		};

		// Assert
		restoredState.SagaId.ShouldBe(originalState.SagaId);
		restoredState.OrderId.ShouldBe(originalState.OrderId);
		restoredState.TotalAmount.ShouldBe(originalState.TotalAmount);
		restoredState.Completed.ShouldBe(originalState.Completed);
	}

	#endregion Persistence Simulation Tests
}
