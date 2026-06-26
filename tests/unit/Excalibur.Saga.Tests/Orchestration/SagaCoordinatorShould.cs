// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Handlers;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Unit tests for <see cref="SagaCoordinator"/>.
/// Verifies event processing, saga discovery, state management, and completion tracking.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Priority", "1")]
public sealed class SagaCoordinatorShould : UnitTestBase
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ISagaStore _sagaStore;
	private readonly ILogger<SagaCoordinator> _logger;
	private readonly SagaCoordinator _sut;

	public SagaCoordinatorShould()
	{
		_sagaStore = A.Fake<ISagaStore>();
		_logger = A.Fake<ILogger<SagaCoordinator>>();

		var services = new ServiceCollection();
		services.AddSingleton(_sagaStore);
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(typeof(ILogger<>), typeof(FakeLogger<>));
		_serviceProvider = services.BuildServiceProvider();

		_sut = new SagaCoordinator(_serviceProvider, _sagaStore, _logger);
	}

	#region ProcessEventAsync Argument Validation Tests

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenMessageContextIsNull()
	{
		// Arrange
		var evt = CreateTestSagaEvent();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.ProcessEventAsync(null!, evt, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenEventIsNull()
	{
		// Arrange
		var messageContext = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.ProcessEventAsync(messageContext, null!, CancellationToken.None));
	}

	#endregion

	#region ProcessEventAsync Saga Discovery Tests

	[Fact]
	public async Task ReturnEarlyWhenNoSagaRegisteredForEventType()
	{
		// Arrange
		var messageContext = A.Fake<IMessageContext>();
		var evt = CreateUnregisteredSagaEvent();

		// Act
		await _sut.ProcessEventAsync(messageContext, evt, CancellationToken.None);

		// Assert - saga store should not be called since no saga was registered
		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(A<Guid>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _sagaStore.SaveAsync(A<TestSagaState>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region HandleEventInternalAsync Argument Validation Tests

	[Fact]
	public async Task ThrowArgumentNullExceptionInHandleEventInternalAsyncWhenMessageContextIsNull()
	{
		// Arrange
		var evt = CreateTestSagaEvent();
		var sagaInfo = new SagaInfo(typeof(TestSaga), typeof(TestSagaState));
		sagaInfo.StartsWith<TestStartEvent>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.HandleEventInternalAsync<TestSaga, TestSagaState>(
				null!, evt, sagaInfo, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionInHandleEventInternalAsyncWhenEventIsNull()
	{
		// Arrange
		var messageContext = A.Fake<IMessageContext>();
		var sagaInfo = new SagaInfo(typeof(TestSaga), typeof(TestSagaState));
		sagaInfo.StartsWith<TestStartEvent>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.HandleEventInternalAsync<TestSaga, TestSagaState>(
				messageContext, null!, sagaInfo, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionInHandleEventInternalAsyncWhenSagaInfoIsNull()
	{
		// Arrange
		var messageContext = A.Fake<IMessageContext>();
		var evt = CreateTestSagaEvent();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.HandleEventInternalAsync<TestSaga, TestSagaState>(
				messageContext, evt, null!, CancellationToken.None));
	}

	#endregion

	#region HandleEventInternalAsync Start Event Tests

	[Fact]
	public async Task CreateNewStateForStartEvent()
	{
		// Arrange
		var messageContext = A.Fake<IMessageContext>();
		var sagaId = Guid.NewGuid();
		var evt = new TestStartEvent { SagaId = sagaId.ToString(), StepId = null };
		var sagaInfo = new SagaInfo(typeof(TestSaga), typeof(TestSagaState));
		sagaInfo.StartsWith<TestStartEvent>();

		// Act
		await _sut.HandleEventInternalAsync<TestSaga, TestSagaState>(
			messageContext, evt, sagaInfo, CancellationToken.None);

		// Assert - state should be saved (not loaded since it's a start event)
		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(A<Guid>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _sagaStore.SaveAsync(A<TestSagaState>.That.Matches(s => s.SagaId == sagaId), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region HandleEventInternalAsync Continuation Event Tests

	[Fact]
	public async Task LoadExistingStateForContinuationEvent()
	{
		// Arrange
		var messageContext = A.Fake<IMessageContext>();
		var sagaId = Guid.NewGuid();
		var evt = new TestContinuationEvent { SagaId = sagaId.ToString(), StepId = "step-1" };
		var sagaInfo = new SagaInfo(typeof(TestSaga), typeof(TestSagaState));
		sagaInfo.StartsWith<TestStartEvent>();
		sagaInfo.Handles<TestContinuationEvent>();

		var existingState = new TestSagaState { SagaId = sagaId };
		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._))
			.Returns(existingState);

		// Act
		await _sut.HandleEventInternalAsync<TestSaga, TestSagaState>(
			messageContext, evt, sagaInfo, CancellationToken.None);

		// Assert - state should be loaded and then saved
		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _sagaStore.SaveAsync(existingState, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnEarlyWhenSagaNotFoundForContinuationEvent()
	{
		// Arrange
		var messageContext = A.Fake<IMessageContext>();
		var sagaId = Guid.NewGuid();
		var evt = new TestContinuationEvent { SagaId = sagaId.ToString(), StepId = "step-1" };
		var sagaInfo = new SagaInfo(typeof(TestSaga), typeof(TestSagaState));
		sagaInfo.StartsWith<TestStartEvent>();
		sagaInfo.Handles<TestContinuationEvent>();

		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._))
			.Returns((TestSagaState?)null);

		// Act
		await _sut.HandleEventInternalAsync<TestSaga, TestSagaState>(
			messageContext, evt, sagaInfo, CancellationToken.None);

		// Assert - state should not be saved since saga was not found
		A.CallTo(() => _sagaStore.SaveAsync(A<TestSagaState>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region HandleEventInternalAsync Event Handling Tests

	[Fact]
	public async Task ReturnEarlyWhenSagaDoesNotHandleEvent()
	{
		// Arrange
		var messageContext = A.Fake<IMessageContext>();
		var sagaId = Guid.NewGuid();
		var evt = new TestUnhandledEvent { SagaId = sagaId.ToString(), StepId = null };
		var sagaInfo = new SagaInfo(typeof(TestSaga), typeof(TestSagaState));
		sagaInfo.StartsWith<TestUnhandledEvent>(); // Registered as start but saga doesn't handle it

		// Act
		await _sut.HandleEventInternalAsync<TestSaga, TestSagaState>(
			messageContext, evt, sagaInfo, CancellationToken.None);

		// Assert - state should not be saved since saga doesn't handle the event
		A.CallTo(() => _sagaStore.SaveAsync(A<TestSagaState>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region ISagaTimeout<T> Dispatch Tests (bd-oe0a2k)

	[Fact]
	public async Task InvokeTimeoutHandlerWhenSagaImplementsISagaTimeout()
	{
		// Arrange
		var messageContext = A.Fake<IMessageContext>();
		var sagaId = Guid.NewGuid();
		var evt = new PaymentTimeoutEvent { SagaId = sagaId.ToString(), StepId = null };
		var sagaInfo = new SagaInfo(typeof(TimeoutAwareSaga), typeof(TimeoutSagaState));
		sagaInfo.StartsWith<PaymentTimeoutEvent>();

		var services = new ServiceCollection();
		services.AddSingleton(_sagaStore);
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(typeof(ILogger<>), typeof(FakeLogger<>));
		var sp = services.BuildServiceProvider();
		var coordinator = new SagaCoordinator(sp, _sagaStore, _logger);

		// Act
		await coordinator.HandleEventInternalAsync<TimeoutAwareSaga, TimeoutSagaState>(
			messageContext, evt, sagaInfo, CancellationToken.None);

		// Assert — state should be saved (timeout handler was invoked, not HandleAsync)
		A.CallTo(() => _sagaStore.SaveAsync(A<TimeoutSagaState>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FallThroughToHandleAsyncWhenSagaDoesNotImplementISagaTimeoutForEventType()
	{
		// Arrange — TestSaga does NOT implement ISagaTimeout<TestStartEvent>
		var messageContext = A.Fake<IMessageContext>();
		var sagaId = Guid.NewGuid();
		var evt = new TestStartEvent { SagaId = sagaId.ToString(), StepId = null };
		var sagaInfo = new SagaInfo(typeof(TestSaga), typeof(TestSagaState));
		sagaInfo.StartsWith<TestStartEvent>();

		// Act
		await _sut.HandleEventInternalAsync<TestSaga, TestSagaState>(
			messageContext, evt, sagaInfo, CancellationToken.None);

		// Assert — state is saved (HandleAsync was called since no ISagaTimeout<T>)
		A.CallTo(() => _sagaStore.SaveAsync(A<TestSagaState>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RouteMultipleTimeoutTypesToCorrectHandlers()
	{
		// Arrange — saga implements ISagaTimeout<PaymentTimeoutEvent> and ISagaTimeout<ShippingTimeoutEvent>
		var messageContext = A.Fake<IMessageContext>();
		var sagaId = Guid.NewGuid();
		var sagaInfo = new SagaInfo(typeof(MultiTimeoutSaga), typeof(MultiTimeoutSagaState));
		sagaInfo.StartsWith<PaymentTimeoutEvent>();
		sagaInfo.Handles<ShippingTimeoutEvent>();

		var services = new ServiceCollection();
		services.AddSingleton(_sagaStore);
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(typeof(ILogger<>), typeof(FakeLogger<>));
		var sp = services.BuildServiceProvider();
		var coordinator = new SagaCoordinator(sp, _sagaStore, _logger);

		// Act — send payment timeout as start event
		var paymentEvt = new PaymentTimeoutEvent { SagaId = sagaId.ToString(), StepId = null };
		await coordinator.HandleEventInternalAsync<MultiTimeoutSaga, MultiTimeoutSagaState>(
			messageContext, paymentEvt, sagaInfo, CancellationToken.None);

		// Act — send shipping timeout as continuation
		var existingState = new MultiTimeoutSagaState { SagaId = sagaId };
		A.CallTo(() => _sagaStore.LoadAsync<MultiTimeoutSagaState>(sagaId, A<CancellationToken>._))
			.Returns(existingState);

		var shippingEvt = new ShippingTimeoutEvent { SagaId = sagaId.ToString(), StepId = "step-ship" };
		await coordinator.HandleEventInternalAsync<MultiTimeoutSaga, MultiTimeoutSagaState>(
			messageContext, shippingEvt, sagaInfo, CancellationToken.None);

		// Assert — both events should result in state saves (2 total)
		A.CallTo(() => _sagaStore.SaveAsync(A<MultiTimeoutSagaState>._, A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);
	}

	#endregion

	#region ISagaTimeout Interface Shape Tests (bd-oe0a2k)

	[Fact]
	public void ISagaTimeoutShouldHaveExactlyOneMethod()
	{
		// Arrange
		var interfaceType = typeof(Excalibur.Saga.Abstractions.ISagaTimeout<>);

		// Act
		var methods = interfaceType.GetMethods();

		// Assert — ISP compliant: single method
		methods.Length.ShouldBe(1);
		methods[0].Name.ShouldBe("HandleTimeoutAsync");
	}

	[Fact]
	public void ISagaTimeoutShouldBeContravariantOnTMessage()
	{
		// Arrange
		var interfaceType = typeof(Excalibur.Saga.Abstractions.ISagaTimeout<>);
		var typeParam = interfaceType.GetGenericArguments()[0];

		// Act
		var attributes = typeParam.GenericParameterAttributes;

		// Assert — contravariant (in TMessage)
		(attributes & System.Reflection.GenericParameterAttributes.Contravariant)
			.ShouldNotBe((System.Reflection.GenericParameterAttributes)0,
			"ISagaTimeout<TMessage> should be contravariant (in TMessage)");
	}

	[Fact]
	public void HandleTimeoutAsyncShouldReturnTask()
	{
		// Arrange
		var method = typeof(Excalibur.Saga.Abstractions.ISagaTimeout<>).GetMethods()[0];

		// Assert
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void HandleTimeoutAsyncShouldAcceptMessageAndCancellationToken()
	{
		// Arrange
		var method = typeof(Excalibur.Saga.Abstractions.ISagaTimeout<>).GetMethods()[0];
		var parameters = method.GetParameters();

		// Assert — 2 parameters: TMessage + CancellationToken
		parameters.Length.ShouldBe(2);
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	#endregion

	#region S-E2 Saga-Not-Found Handler Lock (ckavfs — author≠impl)

	[Fact]
	public async Task InvokeRegisteredNotFoundHandler_WhenSagaNotFoundForContinuationEvent()
	{
		// ckavfs (S-E2 / ADR-336): when a correlated event arrives for a saga that does not exist, the
		// registered ISagaNotFoundHandler<TSaga> extension point MUST be resolved and invoked with
		// (message, sagaId, ct) — not silently log-and-dropped. RED on the pre-fix coordinator, which
		// only called LogSagaNotFound and never resolved/invoked the handler. Mirrors the existing
		// ReturnEarlyWhenSagaNotFoundForContinuationEvent setup (continuation event + LoadAsync→null),
		// adding a registered handler to a local service provider and asserting it was called.
		var messageContext = A.Fake<IMessageContext>();
		var sagaId = Guid.NewGuid();
		var evt = new TestContinuationEvent { SagaId = sagaId.ToString(), StepId = "step-1" };
		var sagaInfo = new SagaInfo(typeof(TestSaga), typeof(TestSagaState));
		sagaInfo.StartsWith<TestStartEvent>();
		sagaInfo.Handles<TestContinuationEvent>();

		// Concrete spy (not A.Fake): FakeItEasy cannot proxy a generic interface parameterized with the
		// private nested TestSaga type (DynamicProxy can't access it).
		var notFoundHandler = new SpyNotFoundHandler();
		var sagaStore = A.Fake<ISagaStore>();
		// LoadAsync → null = saga-not-found branch. Explicit (a bare fake would return a non-null dummy
		// TestSagaState, making the saga look "found" and skipping the handler path). Mirrors the sibling
		// ReturnEarlyWhenSagaNotFoundForContinuationEvent setup.
		A.CallTo(() => sagaStore.LoadAsync<TestSagaState>(A<Guid>._, A<CancellationToken>._))
			.Returns((TestSagaState?)null);

		var services = new ServiceCollection();
		services.AddSingleton(sagaStore);
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(typeof(ILogger<>), typeof(FakeLogger<>));
		services.AddSingleton<ISagaNotFoundHandler<TestSaga>>(notFoundHandler);
		var serviceProvider = services.BuildServiceProvider();
		var coordinator = new SagaCoordinator(serviceProvider, sagaStore, _logger);

		// Act
		await coordinator.HandleEventInternalAsync<TestSaga, TestSagaState>(
			messageContext, evt, sagaInfo, CancellationToken.None);

		// Assert — the registered handler is invoked exactly once with the orphaned event + its saga id.
		notFoundHandler.CallCount.ShouldBe(1);
		notFoundHandler.LastMessage.ShouldBeSameAs(evt);
		notFoundHandler.LastSagaId.ShouldBe(sagaId.ToString());
	}

	#endregion S-E2 Saga-Not-Found Handler Lock

	#region S-E1 Save-Then-Dispatch Lock (lc178k — author≠impl)

	[Fact]
	public async Task NotDispatchBufferedMessages_WhenSaveFails()
	{
		// lc178k (S-E1) KEYSTONE: the coordinator flushes the saga's buffered emissions ONLY after SaveAsync
		// succeeds. If SaveAsync throws, NOTHING is dispatched (the emissions re-buffer on the next delivery —
		// no double-dispatch). RED on pre-fix immediate-dispatch: there, SendCommandAsync dispatched DURING
		// HandleAsync, before SaveAsync, so a save failure still leaked a dispatch. This is the structural
		// "dispatch-before-save is inexpressible" invariant (SA 15528 clause c / PM 15559).
		var messageContext = A.Fake<IMessageContext>();
		var sagaId = Guid.NewGuid();
		var evt = new TestContinuationEvent { SagaId = sagaId.ToString(), StepId = "step-1" };
		var sagaInfo = new SagaInfo(typeof(EmittingSaga), typeof(EmittingSagaState));
		sagaInfo.StartsWith<TestStartEvent>();
		sagaInfo.Handles<TestContinuationEvent>();

		var dispatcher = A.Fake<IDispatcher>();
		var sagaStore = A.Fake<ISagaStore>();
		var existingState = new EmittingSagaState { SagaId = sagaId };
		A.CallTo(() => sagaStore.LoadAsync<EmittingSagaState>(A<Guid>._, A<CancellationToken>._))
			.Returns(existingState);
		A.CallTo(() => sagaStore.SaveAsync(A<EmittingSagaState>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("save failed"));

		var services = new ServiceCollection();
		services.AddSingleton(sagaStore);
		services.AddSingleton(dispatcher); // the saga (via ActivatorUtilities) emits onto THIS dispatcher
		services.AddSingleton(typeof(ILogger<>), typeof(FakeLogger<>));
		var serviceProvider = services.BuildServiceProvider();
		var coordinator = new SagaCoordinator(serviceProvider, sagaStore, _logger);

		// Act — SaveAsync throws; the coordinator surfaces it and must dispatch NOTHING.
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await coordinator.HandleEventInternalAsync<EmittingSaga, EmittingSagaState>(
				messageContext, evt, sagaInfo, CancellationToken.None));

		// Assert — the buffered command was never flushed (save failed before the flush point). Match ANY
		// DispatchAsync instantiation by method name: the pre-fix immediate path dispatched the concrete
		// DispatchAsync<EmittedCommand>, while the post-fix flush would use DispatchAsync<IDispatchMessage> —
		// a generic-specific matcher (<IDispatchMessage>) is VACUOUS against the pre-fix surface (verified:
		// it passed pre-fix in the RED-proof). Method-name matching is non-vacuous on both surfaces.
		A.CallTo(dispatcher)
			.Where(call => call.Method.Name == nameof(IDispatcher.DispatchAsync))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchBufferedMessages_AfterSuccessfulSave()
	{
		// lc178k (S-E1) POSITIVE keystone companion (nk0yek, SENTINEL coverage catch): the coordinator MUST
		// flush — i.e. dispatch — the saga's buffered emissions AFTER a SUCCESSFUL save. Without this positive
		// lock, the failure-path keystone (NotDispatchBufferedMessages_WhenSaveFails) passes VACUOUSLY if
		// SagaCoordinator's `await saga.FlushPendingDispatchesAsync(...)` (SagaCoordinator.cs:243) were deleted:
		// nothing would ever dispatch and the negative lock would still be green. RED on a flush-deletion
		// mutant (no dispatch); GREEN on the real coordinator. Together with the negative lock this makes the
		// save-then-dispatch keystone non-vacuous in both directions.
		var messageContext = A.Fake<IMessageContext>();
		var sagaId = Guid.NewGuid();
		var evt = new TestContinuationEvent { SagaId = sagaId.ToString(), StepId = "step-1" };
		var sagaInfo = new SagaInfo(typeof(EmittingSaga), typeof(EmittingSagaState));
		sagaInfo.StartsWith<TestStartEvent>();
		sagaInfo.Handles<TestContinuationEvent>();

		var dispatcher = A.Fake<IDispatcher>();
		var sagaStore = A.Fake<ISagaStore>();
		var existingState = new EmittingSagaState { SagaId = sagaId };
		A.CallTo(() => sagaStore.LoadAsync<EmittingSagaState>(A<Guid>._, A<CancellationToken>._))
			.Returns(existingState);
		// SaveAsync left unconfigured → succeeds (completed task) = the happy save-then-dispatch path.

		var services = new ServiceCollection();
		services.AddSingleton(sagaStore);
		services.AddSingleton(dispatcher); // the saga (via ActivatorUtilities) emits onto THIS dispatcher
		services.AddSingleton(typeof(ILogger<>), typeof(FakeLogger<>));
		var serviceProvider = services.BuildServiceProvider();
		var coordinator = new SagaCoordinator(serviceProvider, sagaStore, _logger);

		// Act — save succeeds, so the coordinator flushes the buffer post-save.
		await coordinator.HandleEventInternalAsync<EmittingSaga, EmittingSagaState>(
			messageContext, evt, sagaInfo, CancellationToken.None);

		// Assert — the buffered command WAS dispatched (post-save flush). Match ANY DispatchAsync
		// instantiation by method name (the flush dispatches via DispatchAsync<IDispatchMessage>).
		A.CallTo(dispatcher)
			.Where(call => call.Method.Name == nameof(IDispatcher.DispatchAsync))
			.MustHaveHappenedOnceExactly();
	}

	#endregion S-E1 Save-Then-Dispatch Lock

	#region Helper Methods

	private static TestSagaEvent CreateTestSagaEvent() =>
		new() { SagaId = Guid.NewGuid().ToString(), StepId = null };

	private static UnregisteredSagaEvent CreateUnregisteredSagaEvent() =>
		new() { SagaId = Guid.NewGuid().ToString(), StepId = null };

	#endregion

	#region Test Doubles

	private sealed class SpyNotFoundHandler : ISagaNotFoundHandler<TestSaga>
	{
		public int CallCount { get; private set; }
		public object? LastMessage { get; private set; }
		public string? LastSagaId { get; private set; }

		public Task HandleAsync(object message, string sagaId, CancellationToken cancellationToken)
		{
			CallCount++;
			LastMessage = message;
			LastSagaId = sagaId;
			return Task.CompletedTask;
		}
	}

	private sealed class TestSagaState : SagaState
	{
	}

	private sealed class EmittingSagaState : SagaState
	{
	}

	// A saga whose HandleAsync emits a command — used to prove save-then-dispatch (lc178k): the emission
	// buffers and is only dispatched on the coordinator's post-save flush.
	private sealed class EmittingSaga(
		EmittingSagaState initialState,
		IDispatcher dispatcher,
		ILogger<EmittingSaga> logger)
		: SagaBase<EmittingSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => true;

		public override async Task HandleAsync(object eventMessage, CancellationToken cancellationToken) =>
			await SendCommandAsync(new EmittedCommand(), cancellationToken);
	}

	private sealed class EmittedCommand : IDispatchMessage
	{
	}

	private sealed class TestSaga(
		TestSagaState initialState,
		IDispatcher dispatcher,
		ILogger<TestSaga> logger)
		: SagaBase<TestSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) =>
			eventMessage is TestStartEvent or TestContinuationEvent;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	// --- ISagaTimeout test doubles (bd-oe0a2k) ---

	private sealed class TimeoutSagaState : SagaState
	{
	}

	private sealed class TimeoutAwareSaga(
		TimeoutSagaState initialState,
		IDispatcher dispatcher,
		ILogger<TimeoutAwareSaga> logger)
		: SagaBase<TimeoutSagaState>(initialState, dispatcher, logger),
		  Excalibur.Saga.Abstractions.ISagaTimeout<PaymentTimeoutEvent>
	{
		public bool TimeoutHandlerInvoked { get; private set; }

		public override bool HandlesEvent(object eventMessage) => false; // Timeout bypasses this

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("Should not be called — timeout handler should be used instead");

		public Task HandleTimeoutAsync(PaymentTimeoutEvent message, CancellationToken cancellationToken)
		{
			TimeoutHandlerInvoked = true;
			return Task.CompletedTask;
		}
	}

	private sealed class MultiTimeoutSagaState : SagaState
	{
	}

	private sealed class MultiTimeoutSaga(
		MultiTimeoutSagaState initialState,
		IDispatcher dispatcher,
		ILogger<MultiTimeoutSaga> logger)
		: SagaBase<MultiTimeoutSagaState>(initialState, dispatcher, logger),
		  Excalibur.Saga.Abstractions.ISagaTimeout<PaymentTimeoutEvent>,
		  Excalibur.Saga.Abstractions.ISagaTimeout<ShippingTimeoutEvent>
	{
		public override bool HandlesEvent(object eventMessage) => false;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) =>
			throw new InvalidOperationException("Should not be called — timeout handler should be used");

		public Task HandleTimeoutAsync(PaymentTimeoutEvent message, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task HandleTimeoutAsync(ShippingTimeoutEvent message, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}

	private sealed class PaymentTimeoutEvent : ISagaEvent
	{
		public required string SagaId { get; init; }
		public string? StepId { get; init; }
	}

	private sealed class ShippingTimeoutEvent : ISagaEvent
	{
		public required string SagaId { get; init; }
		public string? StepId { get; init; }
	}

	private sealed class TestSagaEvent : ISagaEvent
	{
		public required string SagaId { get; init; }
		public string? StepId { get; init; }
	}

	private sealed class TestStartEvent : ISagaEvent
	{
		public required string SagaId { get; init; }
		public string? StepId { get; init; }
	}

	private sealed class TestContinuationEvent : ISagaEvent
	{
		public required string SagaId { get; init; }
		public string? StepId { get; init; }
	}

	private sealed class TestUnhandledEvent : ISagaEvent
	{
		public required string SagaId { get; init; }
		public string? StepId { get; init; }
	}

	private sealed class UnregisteredSagaEvent : ISagaEvent
	{
		public required string SagaId { get; init; }
		public string? StepId { get; init; }
	}

	private sealed class FakeLogger<T> : ILogger<T>
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel logLevel) => true;
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
		}
	}

	#endregion
}
