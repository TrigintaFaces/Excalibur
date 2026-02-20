// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
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
	public async Task DispatchCommandWithSagaCorrelation()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };
		var saga = new TestSaga(state, _dispatcher, _logger);
		var command = new TestCommand { Data = "test" };

		A.CallTo(() => _dispatcher.DispatchAsync(
			A<TestCommand>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(MessageResult.Success()));

		// Act
		var result = await saga.CallSendCommandAsync(command);

		// Assert
		result.ShouldNotBeNull();
		A.CallTo(() => _dispatcher.DispatchAsync(
			command,
			A<IMessageContext>.That.Matches(ctx =>
				ctx.GetItem<string>("saga.id") == sagaId.ToString() &&
				ctx.GetItem<string>("saga.type") == nameof(TestSaga)),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region PublishEventAsync Tests

	[Fact]
	public async Task DispatchEventWithSagaCorrelation()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId };
		var saga = new TestSaga(state, _dispatcher, _logger);
		var @event = new TestEvent { Data = "test" };

		A.CallTo(() => _dispatcher.DispatchAsync(
			A<TestEvent>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.Returns(Task.FromResult(MessageResult.Success()));

		// Act
		var result = await saga.CallPublishEventAsync(@event);

		// Assert
		result.ShouldNotBeNull();
		A.CallTo(() => _dispatcher.DispatchAsync(
			@event,
			A<IMessageContext>.That.Matches(ctx =>
				ctx.GetItem<string>("saga.id") == sagaId.ToString() &&
				ctx.GetItem<string>("saga.type") == nameof(TestSaga)),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Test Doubles

	private sealed class TestSagaState : Dispatch.Abstractions.Messaging.SagaState
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

		public Task<IMessageResult> CallSendCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
			where TCommand : IDispatchMessage
			=> SendCommandAsync(command, cancellationToken);

		public Task<IMessageResult> CallPublishEventAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
			where TEvent : IDispatchMessage
			=> PublishEventAsync(@event, cancellationToken);
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
