// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Unit tests for <see cref="SagaManager"/>.
/// Verifies saga lifecycle operations including event handling and state management.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaManagerShould
{
	private readonly ISagaStore _sagaStore;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IServiceProvider _serviceProvider;
	private readonly SagaManager _sut;

	public SagaManagerShould()
	{
		_sagaStore = A.Fake<ISagaStore>();
		_loggerFactory = A.Fake<ILoggerFactory>();

		A.CallTo(() => _loggerFactory.CreateLogger(A<string>._))
			.Returns(NullLogger.Instance);

		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton(_loggerFactory);
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
		_serviceProvider = services.BuildServiceProvider();

		_sut = new SagaManager(_sagaStore, _serviceProvider, _loggerFactory);
	}

	#region HandleEventAsync Tests

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection-based saga instantiation")]
	[RequiresDynamicCode("Test uses dynamic saga instantiation")]
	public async Task LoadExistingState_WhenSagaStateExists()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var existingState = new TestSagaState { SagaId = sagaId };
		var testEvent = new TestEvent("TestData");

		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._))
			.Returns(existingState);

		// Act
		await _sut.HandleEventAsync<TestSaga, TestSagaState>(sagaId, testEvent, CancellationToken.None);

		// Assert
		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _sagaStore.SaveAsync(A<TestSagaState>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection-based saga instantiation")]
	[RequiresDynamicCode("Test uses dynamic saga instantiation")]
	public async Task CreateNewState_WhenNoStateExists()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var testEvent = new TestEvent("TestData");

		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._))
			.Returns((TestSagaState?)null);

		// Act
		await _sut.HandleEventAsync<TestSaga, TestSagaState>(sagaId, testEvent, CancellationToken.None);

		// Assert
		A.CallTo(() => _sagaStore.SaveAsync(
			A<TestSagaState>.That.Matches(s => s.SagaId == sagaId),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection-based saga instantiation")]
	[RequiresDynamicCode("Test uses dynamic saga instantiation")]
	public async Task ResolveDependenciesViaActivatorUtilities()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var testEvent = new TestEvent("TestData");

		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._))
			.Returns((TestSagaState?)null);

		// Act — ActivatorUtilities resolves IDispatcher and ILogger<TestSaga> from DI
		await _sut.HandleEventAsync<TestSaga, TestSagaState>(sagaId, testEvent, CancellationToken.None);

		// Assert — saga was created and state was saved (proves DI resolution succeeded)
		A.CallTo(() => _sagaStore.SaveAsync(
			A<TestSagaState>.That.Matches(s => s.SagaId == sagaId),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection-based saga instantiation")]
	[RequiresDynamicCode("Test uses dynamic saga instantiation")]
	public async Task SaveStateAfterEventHandling()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var existingState = new TestSagaState { SagaId = sagaId };
		var testEvent = new TestEvent("TestData");

		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._))
			.Returns(existingState);

		// Act
		await _sut.HandleEventAsync<TestSaga, TestSagaState>(sagaId, testEvent, CancellationToken.None);

		// Assert - verify ordering: load before save
		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly()
			.Then(A.CallTo(() => _sagaStore.SaveAsync(existingState, A<CancellationToken>._))
				.MustHaveHappenedOnceExactly());
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection-based saga instantiation")]
	[RequiresDynamicCode("Test uses dynamic saga instantiation")]
	public async Task RespectCancellationToken()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var testEvent = new TestEvent("TestData");
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		A.CallTo(() => _sagaStore.LoadAsync<TestSagaState>(sagaId, A<CancellationToken>._))
			.ThrowsAsync(new OperationCanceledException());

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await _sut.HandleEventAsync<TestSaga, TestSagaState>(sagaId, testEvent, cts.Token));
	}

	#endregion

	#region Test Doubles

	private sealed class TestSagaState : SagaState
	{
		public string? LastEventData { get; set; }
	}

	private sealed class TestSaga(TestSagaState initialState, IDispatcher dispatcher, ILogger<TestSaga> logger)
		: SagaBase<TestSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => eventMessage is TestEvent;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
		{
			if (eventMessage is TestEvent testEvent)
			{
				State.LastEventData = testEvent.Data;
			}
			return Task.CompletedTask;
		}
	}

	private sealed record TestEvent(string Data);

	#endregion
}
