// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.StateMachine;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.StateMachine;

/// <summary>
/// Unit tests for <see cref="ProcessManager{TData}"/>.
/// Verifies state machine transitions, event handling, and state definitions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class ProcessManagerShould
{
	private readonly IDispatcher _dispatcher;
	private readonly ILogger _logger;

	public ProcessManagerShould()
	{
		_dispatcher = A.Fake<IDispatcher>();
		_logger = NullLogger.Instance;
	}

	#region Initial State Tests

	[Fact]
	public void StartInInitialState()
	{
		// Arrange & Act
		var sut = CreateProcessManager();

		// Assert
		sut.CurrentState.ShouldBe("Initial");
	}

	[Fact]
	public void ExposeIsCompletedAsFalse_Initially()
	{
		// Arrange & Act
		var sut = CreateProcessManager();

		// Assert
		sut.IsCompleted.ShouldBeFalse();
	}

	#endregion

	#region HandlesEvent Tests

	[Fact]
	public void ThrowArgumentNullException_WhenEventIsNull_InHandlesEvent()
	{
		// Arrange
		var sut = CreateProcessManager();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.HandlesEvent(null!));
	}

	[Fact]
	public void ReturnTrue_WhenCurrentStateHandlesEventType()
	{
		// Arrange
		var sut = CreateProcessManager();

		// Act
		var result = sut.HandlesEvent(new OrderPlaced("order-1"));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalse_WhenCurrentStateDoesNotHandleEventType()
	{
		// Arrange
		var sut = CreateProcessManager();

		// Act - PaymentReceived is not handled in Initial state
		var result = sut.HandlesEvent(new PaymentReceived("payment-1"));

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalse_WhenNoStateIsDefined()
	{
		// Arrange
		var sut = CreateEmptyProcessManager();

		// Act
		var result = sut.HandlesEvent(new OrderPlaced("order-1"));

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region HandleAsync Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenEventIsNull_InHandleAsync()
	{
		// Arrange
		var sut = CreateProcessManager();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.HandleAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowInvalidStateTransitionException_WhenNoStateIsDefined()
	{
		// Arrange
		var sut = CreateEmptyProcessManager();

		// Act & Assert
		await Should.ThrowAsync<InvalidStateTransitionException>(async () =>
			await sut.HandleAsync(new OrderPlaced("order-1"), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowInvalidStateTransitionException_WhenNoHandlerForEventType()
	{
		// Arrange
		var sut = CreateProcessManager();

		// Act & Assert - PaymentReceived not handled in Initial state
		await Should.ThrowAsync<InvalidStateTransitionException>(async () =>
			await sut.HandleAsync(new PaymentReceived("payment-1"), CancellationToken.None));
	}

	#endregion

	#region State Definition Tests

	[Fact]
	public void AllowMultipleStatesWithDuring()
	{
		// Arrange
		var sut = CreateProcessManagerWithMultipleStates();

		// Assert - starts in Initial
		sut.CurrentState.ShouldBe("Initial");
		sut.HandlesEvent(new OrderPlaced("order-1")).ShouldBeTrue();
	}

	#endregion

	#region InvalidStateTransitionException Tests

	[Fact]
	public void CreateExceptionWithCorrectProperties()
	{
		// Arrange & Act
		var exception = new InvalidStateTransitionException(
			"CurrentState",
			"TargetState",
			typeof(OrderPlaced));

		// Assert
		exception.CurrentState.ShouldBe("CurrentState");
		exception.AttemptedTransition.ShouldBe("TargetState");
		exception.MessageType.ShouldBe(typeof(OrderPlaced));
		exception.Message.ShouldContain("CurrentState");
		exception.Message.ShouldContain("TargetState");
		exception.Message.ShouldContain("OrderPlaced");
	}

	[Fact]
	public void CreateExceptionWithNullMessageType()
	{
		// Arrange & Act
		var exception = new InvalidStateTransitionException(
			"CurrentState",
			"TargetState",
			null);

		// Assert
		exception.MessageType.ShouldBeNull();
		exception.Message.ShouldContain("CurrentState");
		exception.Message.ShouldContain("TargetState");
	}

	[Fact]
	public void CreateExceptionWithInnerException()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new InvalidStateTransitionException(
			"CurrentState",
			"TargetState",
			typeof(OrderPlaced),
			innerException);

		// Assert
		exception.InnerException.ShouldBeSameAs(innerException);
	}

	#endregion

	#region State Transition Tests

	[Fact]
	public async Task TransitionToNextState_WhenHandlerConfiguresTransition()
	{
		// Arrange
		var sut = CreateProcessManagerWithTransitions();

		// Act
		await sut.HandleAsync(new OrderPlaced("order-1"), CancellationToken.None);

		// Assert
		sut.CurrentState.ShouldBe("PaymentPending");
	}

	[Fact]
	public async Task UpdateDataDuringTransition()
	{
		// Arrange
		var sut = CreateProcessManagerWithDataUpdates();

		// Act
		await sut.HandleAsync(new OrderPlaced("order-123"), CancellationToken.None);

		// Assert
		sut.State.OrderId.ShouldBe("order-123");
	}

	[Fact]
	public async Task HandleMultipleTransitions_InSequence()
	{
		// Arrange
		var sut = CreateProcessManagerWithTransitions();

		// Act
		await sut.HandleAsync(new OrderPlaced("order-1"), CancellationToken.None);
		await sut.HandleAsync(new PaymentReceived("payment-1"), CancellationToken.None);

		// Assert
		sut.CurrentState.ShouldBe("Shipping");
	}

	[Fact]
	public void ThrowInvalidStateTransitionException_WhenTransitionToUndefinedState()
	{
		// Arrange
		var sut = CreateProcessManagerWithInvalidTransition();

		// Act & Assert
		Should.ThrowAsync<InvalidStateTransitionException>(async () =>
			await sut.HandleAsync(new OrderPlaced("order-1"), CancellationToken.None));
	}

	#endregion

	#region OnEnter/OnExit Callback Tests

	[Fact]
	public async Task InvokeOnEnter_WhenTransitioningToState()
	{
		// Arrange
		var sut = CreateProcessManagerWithCallbacks();

		// Act
		await sut.HandleAsync(new OrderPlaced("order-1"), CancellationToken.None);

		// Assert
		sut.EnteredStates.ShouldContain("PaymentPending");
	}

	[Fact]
	public async Task InvokeOnExit_WhenLeavingState()
	{
		// Arrange
		var sut = CreateProcessManagerWithCallbacks();

		// Act
		await sut.HandleAsync(new OrderPlaced("order-1"), CancellationToken.None);

		// Assert
		sut.ExitedStates.ShouldContain("Initial");
	}

	[Fact]
	public async Task ExecuteCallbacksInCorrectOrder()
	{
		// Arrange
		var sut = CreateProcessManagerWithCallbacks();

		// Act
		await sut.HandleAsync(new OrderPlaced("order-1"), CancellationToken.None);

		// Assert - OnExit should be called before OnEnter
		var exitIndex = sut.CallbackOrder.IndexOf("Exit:Initial");
		var enterIndex = sut.CallbackOrder.IndexOf("Enter:PaymentPending");
		exitIndex.ShouldBeLessThan(enterIndex);
	}

	#endregion

	#region Conditional Handler Tests

	[Fact]
	public async Task ExecuteHandler_WhenConditionIsTrue()
	{
		// Arrange
		var sut = CreateProcessManagerWithCondition();

		// Act - amount > 100 triggers special handling
		await sut.HandleAsync(new OrderPlaced("order-1") { Amount = 150 }, CancellationToken.None);

		// Assert
		sut.CurrentState.ShouldBe("HighValueReview");
	}

	[Fact]
	public async Task SkipHandler_WhenConditionIsFalse()
	{
		// Arrange
		var sut = CreateProcessManagerWithCondition();

		// Act - amount <= 100 does not meet condition
		await sut.HandleAsync(new OrderPlaced("order-1") { Amount = 50 }, CancellationToken.None);

		// Assert - remains in Initial because handler didn't execute
		sut.CurrentState.ShouldBe("Initial");
	}

	#endregion

	#region Saga Completion Tests

	[Fact]
	public async Task MarkAsCompleted_WhenCompleteIsCalled()
	{
		// Arrange
		var sut = CreateProcessManagerWithCompletion();

		// Act
		await sut.HandleAsync(new OrderPlaced("order-1"), CancellationToken.None);

		// Assert
		sut.IsCompleted.ShouldBeTrue();
	}

	#endregion

	#region Finally State Tests

	[Fact]
	public void AllowFinallyStateDefinition()
	{
		// Arrange
		var sut = CreateProcessManagerWithFinalState();

		// Act - transition to Final state
		sut.ForceTransitionTo("Final");

		// Assert
		sut.CurrentState.ShouldBe("Final");
		sut.HandlesEvent(new OrderShipped("track-1")).ShouldBeTrue();
	}

	#endregion

	#region Helper Methods

	private TestProcessManager CreateProcessManager()
	{
		var state = new TestOrderData { SagaId = Guid.NewGuid() };
		return new TestProcessManager(state, _dispatcher, _logger);
	}

	private TestProcessManagerWithStates CreateProcessManagerWithMultipleStates()
	{
		var state = new TestOrderData { SagaId = Guid.NewGuid() };
		return new TestProcessManagerWithStates(state, _dispatcher, _logger);
	}

	private EmptyProcessManager CreateEmptyProcessManager()
	{
		var state = new TestOrderData { SagaId = Guid.NewGuid() };
		return new EmptyProcessManager(state, _dispatcher, _logger);
	}

	private ProcessManagerWithTransitions CreateProcessManagerWithTransitions()
	{
		var state = new TestOrderData { SagaId = Guid.NewGuid() };
		return new ProcessManagerWithTransitions(state, _dispatcher, _logger);
	}

	private ProcessManagerWithDataUpdates CreateProcessManagerWithDataUpdates()
	{
		var state = new TestOrderData { SagaId = Guid.NewGuid() };
		return new ProcessManagerWithDataUpdates(state, _dispatcher, _logger);
	}

	private ProcessManagerWithInvalidTransition CreateProcessManagerWithInvalidTransition()
	{
		var state = new TestOrderData { SagaId = Guid.NewGuid() };
		return new ProcessManagerWithInvalidTransition(state, _dispatcher, _logger);
	}

	private ProcessManagerWithCallbacks CreateProcessManagerWithCallbacks()
	{
		var state = new TestOrderData { SagaId = Guid.NewGuid() };
		return new ProcessManagerWithCallbacks(state, _dispatcher, _logger);
	}

	private ProcessManagerWithCondition CreateProcessManagerWithCondition()
	{
		var state = new TestOrderData { SagaId = Guid.NewGuid() };
		return new ProcessManagerWithCondition(state, _dispatcher, _logger);
	}

	private ProcessManagerWithCompletion CreateProcessManagerWithCompletion()
	{
		var state = new TestOrderData { SagaId = Guid.NewGuid() };
		return new ProcessManagerWithCompletion(state, _dispatcher, _logger);
	}

	private ProcessManagerWithFinalState CreateProcessManagerWithFinalState()
	{
		var state = new TestOrderData { SagaId = Guid.NewGuid() };
		return new ProcessManagerWithFinalState(state, _dispatcher, _logger);
	}

	#endregion

	#region Test Types

	internal sealed class TestOrderData : SagaState
	{
		public string? OrderId { get; set; }
		public string? PaymentId { get; set; }
		public string CurrentStateName { get; set; } = "Initial";
	}

	internal sealed record OrderPlaced(string OrderId)
	{
		public decimal Amount { get; init; }
	}

	internal sealed record PaymentReceived(string PaymentId);
	internal sealed record OrderShipped(string TrackingNumber);

	/// <summary>
	/// Basic process manager with Initial state only.
	/// </summary>
	internal class TestProcessManager : ProcessManager<TestOrderData>
	{
		public TestProcessManager(TestOrderData state, IDispatcher dispatcher, ILogger logger)
			: base(state, dispatcher, logger)
		{
			Initially(s => s
				.When<OrderPlaced>(h => { }));
		}
	}

	/// <summary>
	/// Process manager with multiple states.
	/// </summary>
	internal class TestProcessManagerWithStates : ProcessManager<TestOrderData>
	{
		public TestProcessManagerWithStates(TestOrderData state, IDispatcher dispatcher, ILogger logger)
			: base(state, dispatcher, logger)
		{
			Initially(s => s
				.When<OrderPlaced>(h => { }));

			During("PaymentPending", s => s
				.When<PaymentReceived>(h => { }));

			Finally(s => s
				.When<OrderShipped>(h => { }));
		}
	}

	/// <summary>
	/// Process manager with no state definitions.
	/// </summary>
	internal class EmptyProcessManager : ProcessManager<TestOrderData>
	{
		public EmptyProcessManager(TestOrderData state, IDispatcher dispatcher, ILogger logger)
			: base(state, dispatcher, logger)
		{
			// No states defined
		}
	}

	/// <summary>
	/// Process manager with state transitions.
	/// </summary>
	internal class ProcessManagerWithTransitions : ProcessManager<TestOrderData>
	{
		public ProcessManagerWithTransitions(TestOrderData state, IDispatcher dispatcher, ILogger logger)
			: base(state, dispatcher, logger)
		{
			Initially(s => s
				.When<OrderPlaced>(h => h.TransitionTo("PaymentPending")));

			During("PaymentPending", s => s
				.When<PaymentReceived>(h => h.TransitionTo("Shipping")));

			During("Shipping", s => s
				.When<OrderShipped>(h => h.Complete()));
		}
	}

	/// <summary>
	/// Process manager that updates data during transitions.
	/// </summary>
	internal class ProcessManagerWithDataUpdates : ProcessManager<TestOrderData>
	{
		public ProcessManagerWithDataUpdates(TestOrderData state, IDispatcher dispatcher, ILogger logger)
			: base(state, dispatcher, logger)
		{
			Initially(s => s
				.When<OrderPlaced>(h => h
					.Then(ctx => ctx.Data.OrderId = ctx.Message.OrderId)
					.TransitionTo("Processing")));

			During("Processing", s => s
				.When<PaymentReceived>(h => h
					.Then(ctx => ctx.Data.PaymentId = ctx.Message.PaymentId)));
		}
	}

	/// <summary>
	/// Process manager with an invalid state transition.
	/// </summary>
	internal class ProcessManagerWithInvalidTransition : ProcessManager<TestOrderData>
	{
		public ProcessManagerWithInvalidTransition(TestOrderData state, IDispatcher dispatcher, ILogger logger)
			: base(state, dispatcher, logger)
		{
			Initially(s => s
				.When<OrderPlaced>(h => h.TransitionTo("NonExistentState")));
		}
	}

	/// <summary>
	/// Process manager with OnEnter/OnExit callbacks.
	/// </summary>
	internal class ProcessManagerWithCallbacks : ProcessManager<TestOrderData>
	{
		public List<string> EnteredStates { get; } = [];
		public List<string> ExitedStates { get; } = [];
		public List<string> CallbackOrder { get; } = [];

		public ProcessManagerWithCallbacks(TestOrderData state, IDispatcher dispatcher, ILogger logger)
			: base(state, dispatcher, logger)
		{
			Initially(s => s
				.OnExit(_ =>
				{
					ExitedStates.Add("Initial");
					CallbackOrder.Add("Exit:Initial");
				})
				.When<OrderPlaced>(h => h.TransitionTo("PaymentPending")));

			During("PaymentPending", s => s
				.OnEnter(_ =>
				{
					EnteredStates.Add("PaymentPending");
					CallbackOrder.Add("Enter:PaymentPending");
				})
				.OnExit(_ =>
				{
					ExitedStates.Add("PaymentPending");
					CallbackOrder.Add("Exit:PaymentPending");
				})
				.When<PaymentReceived>(h => h.TransitionTo("Shipping")));
		}
	}

	/// <summary>
	/// Process manager with conditional handlers.
	/// </summary>
	internal class ProcessManagerWithCondition : ProcessManager<TestOrderData>
	{
		public ProcessManagerWithCondition(TestOrderData state, IDispatcher dispatcher, ILogger logger)
			: base(state, dispatcher, logger)
		{
			Initially(s => s
				.When<OrderPlaced>(h => h
					.If((_, m) => m.Amount > 100)
					.TransitionTo("HighValueReview")));

			During("HighValueReview", s => s
				.When<PaymentReceived>(h => h.Complete()));
		}
	}

	/// <summary>
	/// Process manager that completes immediately.
	/// </summary>
	internal class ProcessManagerWithCompletion : ProcessManager<TestOrderData>
	{
		public ProcessManagerWithCompletion(TestOrderData state, IDispatcher dispatcher, ILogger logger)
			: base(state, dispatcher, logger)
		{
			Initially(s => s
				.When<OrderPlaced>(h => h.Complete()));
		}
	}

	/// <summary>
	/// Process manager with Finally state.
	/// </summary>
	internal class ProcessManagerWithFinalState : ProcessManager<TestOrderData>
	{
		public ProcessManagerWithFinalState(TestOrderData state, IDispatcher dispatcher, ILogger logger)
			: base(state, dispatcher, logger)
		{
			Initially(s => s
				.When<OrderPlaced>(h => h.TransitionTo("Processing")));

			During("Processing", s => s
				.When<PaymentReceived>(h => h.TransitionTo("Final")));

			Finally(s => s
				.When<OrderShipped>(h => h.Complete()));
		}

		public void ForceTransitionTo(string stateName) => TransitionTo(stateName);
	}

	#endregion
}
