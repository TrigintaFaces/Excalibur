// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
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

	#region Helper Methods

	private static TestSagaEvent CreateTestSagaEvent() =>
		new() { SagaId = Guid.NewGuid().ToString(), StepId = null };

	private static UnregisteredSagaEvent CreateUnregisteredSagaEvent() =>
		new() { SagaId = Guid.NewGuid().ToString(), StepId = null };

	#endregion

	#region Test Doubles

	private sealed class TestSagaState : SagaState
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
