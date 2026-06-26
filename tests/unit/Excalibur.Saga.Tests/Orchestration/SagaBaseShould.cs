// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Unit tests for <see cref="SagaBase{TSagaState}"/>.
/// Verifies saga lifecycle, timeout management, and correlation tracking.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Orchestration")]
[Trait("Priority", "1")]
public sealed class SagaBaseShould : UnitTestBase
{
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<TestSaga> _logger;
	private readonly ISagaTimeoutStore _timeoutStore;

	public SagaBaseShould()
	{
		_dispatcher = A.Fake<IDispatcher>();
		_logger = NullLogger<TestSaga>.Instance;
		_timeoutStore = A.Fake<ISagaTimeoutStore>();
	}

	#region Property Tests

	[Fact]
	public void ExposeIdFromState()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };
		var saga = new TestSaga(state, _dispatcher, _logger);

		// Act
		var id = saga.Id;

		// Assert
		id.ShouldBe(sagaId);
	}

	[Fact]
	public void ExposeIsCompletedFromState()
	{
		// Arrange
		var state = new TestSagaState { Completed = true };
		var saga = new TestSaga(state, _dispatcher, _logger);

		// Act
		var isCompleted = saga.IsCompleted;

		// Assert
		isCompleted.ShouldBeTrue();
	}

	[Fact]
	public void ExposeStateProperty()
	{
		// Arrange
		var state = new TestSagaState();
		var saga = new TestSaga(state, _dispatcher, _logger);

		// Act
		var sagaState = saga.State;

		// Assert
		sagaState.ShouldBeSameAs(state);
	}

	#endregion

	#region MarkCompleted Tests

	[Fact]
	public void SetCompletedToTrueOnMarkCompleted()
	{
		// Arrange
		var state = new TestSagaState();
		var saga = new TestSaga(state, _dispatcher, _logger);

		// Act
		saga.CallMarkCompleted();

		// Assert
		state.Completed.ShouldBeTrue();
		saga.IsCompleted.ShouldBeTrue();
	}

	#endregion

	#region MarkCompletedAsync Tests

	[Fact]
	public async Task SetCompletedToTrueOnMarkCompletedAsync()
	{
		// Arrange
		var state = new TestSagaState();
		var saga = new TestSaga(state, _dispatcher, _logger);

		// Act
		await saga.CallMarkCompletedAsync();

		// Assert
		state.Completed.ShouldBeTrue();
	}

	[Fact]
	public async Task CancelAllTimeoutsWhenMarkCompletedAsyncCalledWithTimeoutStore()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };
		var saga = new TestSaga(state, _dispatcher, _logger)
		{
			TimeoutStore = _timeoutStore
		};

		// Act
		await saga.CallMarkCompletedAsync();

		// Assert
		A.CallTo(() => _timeoutStore.CancelAllTimeoutsAsync(sagaId.ToString(), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotFailWhenMarkCompletedAsyncCalledWithoutTimeoutStore()
	{
		// Arrange
		var state = new TestSagaState();
		var saga = new TestSaga(state, _dispatcher, _logger);
		// TimeoutStore is null by default

		// Act & Assert - should not throw
		await saga.CallMarkCompletedAsync();

		state.Completed.ShouldBeTrue();
	}

	#endregion

	#region RequestTimeoutAsync Tests

	[Fact]
	public async Task ThrowInvalidOperationExceptionWhenRequestTimeoutAsyncCalledWithoutTimeoutStore()
	{
		// Arrange
		var state = new TestSagaState();
		var saga = new TestSaga(state, _dispatcher, _logger);
		// TimeoutStore is null

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await saga.CallRequestTimeoutAsync<TestTimeoutData>(TimeSpan.FromMinutes(5)));
	}

	[Fact]
	public async Task ScheduleTimeoutWithTimeoutStore()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };
		var saga = new TestSaga(state, _dispatcher, _logger)
		{
			TimeoutStore = _timeoutStore
		};
		var delay = TimeSpan.FromMinutes(5);

		// Act
		var timeoutId = await saga.CallRequestTimeoutAsync<TestTimeoutData>(delay);

		// Assert
		timeoutId.ShouldNotBeNullOrWhiteSpace();
		A.CallTo(() => _timeoutStore.ScheduleTimeoutAsync(
			A<SagaTimeout>.That.Matches(t =>
				t.SagaId == sagaId.ToString() &&
				t.TimeoutType.Contains(nameof(TestTimeoutData))),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ScheduleTimeoutWithCustomData()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };
		var saga = new TestSaga(state, _dispatcher, _logger)
		{
			TimeoutStore = _timeoutStore
		};
		var delay = TimeSpan.FromMinutes(5);
		var timeoutData = new TestTimeoutData { Value = "test-value" };

		// Act
		var timeoutId = await saga.CallRequestTimeoutAsync(delay, timeoutData);

		// Assert
		timeoutId.ShouldNotBeNullOrWhiteSpace();
		A.CallTo(() => _timeoutStore.ScheduleTimeoutAsync(
			A<SagaTimeout>.That.Matches(t =>
				t.TimeoutData != null &&
				t.TimeoutData.Length > 0),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region CancelTimeoutAsync Tests

	[Fact]
	public async Task ThrowInvalidOperationExceptionWhenCancelTimeoutAsyncCalledWithoutTimeoutStore()
	{
		// Arrange
		var state = new TestSagaState();
		var saga = new TestSaga(state, _dispatcher, _logger);
		// TimeoutStore is null

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await saga.CallCancelTimeoutAsync("timeout-id"));
	}

	[Fact]
	public async Task ThrowArgumentExceptionWhenTimeoutIdIsNullOrWhitespace()
	{
		// Arrange
		var state = new TestSagaState();
		var saga = new TestSaga(state, _dispatcher, _logger)
		{
			TimeoutStore = _timeoutStore
		};

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await saga.CallCancelTimeoutAsync(null!));

		await Should.ThrowAsync<ArgumentException>(async () =>
			await saga.CallCancelTimeoutAsync(""));

		await Should.ThrowAsync<ArgumentException>(async () =>
			await saga.CallCancelTimeoutAsync("   "));
	}

	[Fact]
	public async Task CancelTimeoutWithTimeoutStore()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };
		var saga = new TestSaga(state, _dispatcher, _logger)
		{
			TimeoutStore = _timeoutStore
		};
		var timeoutId = "timeout-123";

		// Act
		await saga.CallCancelTimeoutAsync(timeoutId);

		// Assert
		A.CallTo(() => _timeoutStore.CancelTimeoutAsync(sagaId.ToString(), timeoutId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region SendCommandAsync Tests

	[Fact]
	public async Task BufferCommandUntilFlush_ThenDispatchWithSagaCorrelation()
	{
		// lc178k (S-E1 save-then-dispatch): SendCommandAsync must NOT dispatch — it buffers the command with
		// its saga-correlated context captured at emit time. Dispatch happens only when the coordinator flushes
		// the buffer (after SaveAsync succeeds). RED on pre-fix immediate-dispatch (the command would already be
		// dispatched before any flush). Strengthens the old "dispatched with correlation" assertion: it now also
		// pins (a) NO dispatch before flush and (b) correlation survives the deferral.
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };
		var saga = new TestSaga(state, _dispatcher, _logger);
		var command = new TestCommand { Data = "test" };

		// The flush dispatches through the buffer's static IDispatchMessage type, so the recorded call is
		// DispatchAsync<IDispatchMessage> (not <TestCommand>) — assert that instantiation.
		A.CallTo(() => _dispatcher.DispatchAsync<IDispatchMessage>(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(MessageResult.Success()));

		// Act 1 — emit: buffered, NOT dispatched.
		await saga.CallSendCommandAsync(command);

		A.CallTo(() => _dispatcher.DispatchAsync<IDispatchMessage>(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();

		// Act 2 — coordinator flush (post-save): dispatched exactly once, saga correlation preserved.
		await saga.CallFlushPendingDispatchesAsync();

		A.CallTo(() => _dispatcher.DispatchAsync<IDispatchMessage>(
			A<IDispatchMessage>.That.IsSameAs(command),
			A<IMessageContext>.That.Matches(ctx =>
				ctx.GetItem<string>("saga.id") == sagaId.ToString() &&
				ctx.GetItem<string>("saga.type") == nameof(TestSaga)),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region PublishEventAsync Tests

	[Fact]
	public async Task BufferEventUntilFlush_ThenDispatchWithSagaCorrelation()
	{
		// lc178k (S-E1 save-then-dispatch): PublishEventAsync must NOT dispatch — it buffers the event with its
		// saga-correlated context, dispatched only on the coordinator's post-save flush. RED on pre-fix
		// immediate-dispatch. Pins NO dispatch before flush + correlation preserved across the deferral.
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };
		var saga = new TestSaga(state, _dispatcher, _logger);
		var @event = new TestEvent { Data = "test" };

		// Flush dispatches through the buffer's static IDispatchMessage type → DispatchAsync<IDispatchMessage>.
		A.CallTo(() => _dispatcher.DispatchAsync<IDispatchMessage>(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(MessageResult.Success()));

		// Act 1 — emit: buffered, NOT dispatched.
		await saga.CallPublishEventAsync(@event);

		A.CallTo(() => _dispatcher.DispatchAsync<IDispatchMessage>(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();

		// Act 2 — coordinator flush (post-save): dispatched exactly once, saga correlation preserved.
		await saga.CallFlushPendingDispatchesAsync();

		A.CallTo(() => _dispatcher.DispatchAsync<IDispatchMessage>(
			A<IDispatchMessage>.That.IsSameAs(@event),
			A<IMessageContext>.That.Matches(ctx =>
				ctx.GetItem<string>("saga.id") == sagaId.ToString() &&
				ctx.GetItem<string>("saga.type") == nameof(TestSaga)),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Test Doubles

	private sealed class TestSagaState : Dispatch.Messaging.SagaState
	{
	}

	private sealed class TestSaga(
		TestSagaState initialState,
		IDispatcher dispatcher,
		ILogger<TestSaga> logger)
		: SagaBase<TestSagaState>(initialState, dispatcher, logger)
	{
		public new ISagaTimeoutStore? TimeoutStore
		{
			get => base.TimeoutStore;
			set => base.TimeoutStore = value;
		}

		public override bool HandlesEvent(object eventMessage) => true;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public void CallMarkCompleted() => MarkCompleted();

		public Task CallMarkCompletedAsync(CancellationToken cancellationToken = default) =>
			MarkCompletedAsync(cancellationToken);

		public Task<string> CallRequestTimeoutAsync<TTimeout>(TimeSpan delay, CancellationToken cancellationToken = default)
			where TTimeout : class, new()
			=> RequestTimeoutAsync<TTimeout>(delay, cancellationToken);

		public Task<string> CallRequestTimeoutAsync<TTimeout>(TimeSpan delay, TTimeout? data, CancellationToken cancellationToken = default)
			where TTimeout : class
			=> RequestTimeoutAsync(delay, data, cancellationToken);

		public Task CallCancelTimeoutAsync(string timeoutId, CancellationToken cancellationToken = default)
			=> CancelTimeoutAsync(timeoutId, cancellationToken);

		public Task CallSendCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
			where TCommand : IDispatchMessage
			=> SendCommandAsync(command, cancellationToken);

		public Task CallPublishEventAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
			where TEvent : IDispatchMessage
			=> PublishEventAsync(@event, cancellationToken);

		public Task CallFlushPendingDispatchesAsync(CancellationToken cancellationToken = default)
			=> FlushPendingDispatchesAsync(cancellationToken);
	}

	private sealed class TestTimeoutData
	{
		public string Value { get; init; } = string.Empty;
	}

	private sealed class TestCommand : IDispatchMessage
	{
		public string Data { get; init; } = string.Empty;
	}

	private sealed class TestEvent : IDispatchMessage
	{
		public string Data { get; init; } = string.Empty;
	}

	#endregion
}
