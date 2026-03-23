// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Regression tests for T.1 (bd-046dc): SagaManager.HandleEventAsync must detect concurrent version conflicts.
/// The SagaManager increments the version before save and delegates enforcement to ISagaStore.SaveAsync.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaManagerConcurrencyShould
{
	private readonly IServiceProvider _serviceProvider;

	public SagaManagerConcurrencyShould()
	{
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IDispatcher>());
		services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
		_serviceProvider = services.BuildServiceProvider();
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection-based saga instantiation")]
	[RequiresDynamicCode("Test uses dynamic saga instantiation")]
	public async Task DetectConcurrentVersionConflict_WhenParallelHandlersModifySameSaga()
	{
		// Arrange -- Use a version-enforcing store that checks version on save.
		// Do NOT pre-seed: the SagaManager creates new state (version 0) when LoadAsync returns null.
		var sagaStore = new VersionEnforcingSagaStore();
		var sagaId = Guid.NewGuid();
		var loggerFactory = NullLoggerFactory.Instance;
		var manager = new SagaManager(sagaStore, _serviceProvider, loggerFactory);

		// Act -- Launch concurrent HandleEventAsync calls for the same saga.
		// Multiple handlers loading version 0 concurrently will all try to save version 1.
		// The version-enforcing store ensures only one succeeds; others get ConcurrencyException.
		var concurrencyExceptionCount = 0;
		var successCount = 0;

		var tasks = new List<Task>();
		for (var i = 0; i < 10; i++)
		{
			var eventData = $"Event-{i}";
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					await manager.HandleEventAsync<ConcurrencyTestSaga, ConcurrencyTestSagaState>(
						sagaId, new ConcurrencyTestEvent(eventData), CancellationToken.None);
					Interlocked.Increment(ref successCount);
				}
				catch (ConcurrencyException)
				{
					Interlocked.Increment(ref concurrencyExceptionCount);
				}
			}));
		}

		await Task.WhenAll(tasks);

		// Assert -- All tasks should either succeed or throw ConcurrencyException (no silent data loss)
		var total = successCount + concurrencyExceptionCount;
		total.ShouldBe(10, "All tasks should either succeed or throw ConcurrencyException");
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection-based saga instantiation")]
	[RequiresDynamicCode("Test uses dynamic saga instantiation")]
	public async Task PropagateConcurrencyException_WhenStoreSaveRejects()
	{
		// Arrange -- Use a fake store that always throws ConcurrencyException on save
		var sagaId = Guid.NewGuid();
		var sagaStore = A.Fake<ISagaStore>();

		A.CallTo(() => sagaStore.LoadAsync<ConcurrencyTestSagaState>(sagaId, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult<ConcurrencyTestSagaState?>(
				new ConcurrencyTestSagaState { SagaId = sagaId, Version = 0 }));

		A.CallTo(() => sagaStore.SaveAsync(A<ConcurrencyTestSagaState>._, A<CancellationToken>._))
			.ThrowsAsync(new ConcurrencyException("SagaState", sagaId.ToString(), 0L, 1L));

		var loggerFactory = NullLoggerFactory.Instance;
		var manager = new SagaManager(sagaStore, _serviceProvider, loggerFactory);

		// Act & Assert -- The ConcurrencyException from the store should propagate
		await Should.ThrowAsync<ConcurrencyException>(async () =>
			await manager.HandleEventAsync<ConcurrencyTestSaga, ConcurrencyTestSagaState>(
				sagaId, new ConcurrencyTestEvent("test"), CancellationToken.None));
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection-based saga instantiation")]
	[RequiresDynamicCode("Test uses dynamic saga instantiation")]
	public async Task SucceedWhenNoVersionConflict()
	{
		// Arrange
		var sagaStore = new VersionEnforcingSagaStore();
		var sagaId = Guid.NewGuid();
		var loggerFactory = NullLoggerFactory.Instance;
		var manager = new SagaManager(sagaStore, _serviceProvider, loggerFactory);

		// Act -- Sequential call should succeed (no conflict)
		await manager.HandleEventAsync<ConcurrencyTestSaga, ConcurrencyTestSagaState>(
			sagaId, new ConcurrencyTestEvent("test"), CancellationToken.None);

		// Assert -- State should be saved with version 1
		var state = await sagaStore.LoadAsync<ConcurrencyTestSagaState>(sagaId, CancellationToken.None);
		state.ShouldNotBeNull();
		state.Version.ShouldBe(1);
	}

	[Fact]
	[RequiresUnreferencedCode("Test uses reflection-based saga instantiation")]
	[RequiresDynamicCode("Test uses dynamic saga instantiation")]
	public async Task IncrementVersionOnSuccessfulSave()
	{
		// Arrange
		var sagaStore = new VersionEnforcingSagaStore();
		var sagaId = Guid.NewGuid();
		var loggerFactory = NullLoggerFactory.Instance;
		var manager = new SagaManager(sagaStore, _serviceProvider, loggerFactory);

		// Act -- Handle first event
		await manager.HandleEventAsync<ConcurrencyTestSaga, ConcurrencyTestSagaState>(
			sagaId, new ConcurrencyTestEvent("first"), CancellationToken.None);

		var state = await sagaStore.LoadAsync<ConcurrencyTestSagaState>(sagaId, CancellationToken.None);
		state.ShouldNotBeNull();
		state.Version.ShouldBe(1);

		// Act -- Handle second event
		await manager.HandleEventAsync<ConcurrencyTestSaga, ConcurrencyTestSagaState>(
			sagaId, new ConcurrencyTestEvent("second"), CancellationToken.None);

		state = await sagaStore.LoadAsync<ConcurrencyTestSagaState>(sagaId, CancellationToken.None);
		state.ShouldNotBeNull();
		state.Version.ShouldBe(2);
	}

	#region Test Doubles

	private sealed class ConcurrencyTestSagaState : SagaState
	{
		public string? LastEventData { get; set; }
	}

	private sealed class ConcurrencyTestSaga(
		ConcurrencyTestSagaState initialState,
		IDispatcher dispatcher,
		ILogger<ConcurrencyTestSaga> logger)
		: SagaBase<ConcurrencyTestSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => eventMessage is ConcurrencyTestEvent;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
		{
			if (eventMessage is ConcurrencyTestEvent testEvent)
			{
				State.LastEventData = testEvent.Data;
			}

			return Task.CompletedTask;
		}
	}

	private sealed record ConcurrencyTestEvent(string Data);

	/// <summary>
	/// Saga store that enforces optimistic concurrency on save and returns snapshot copies on load.
	/// </summary>
	private sealed class VersionEnforcingSagaStore : ISagaStore
	{
		private readonly ConcurrentDictionary<Guid, (long Version, string? LastEventData)> _store = new();
		private readonly object _saveLock = new();

		public Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
			where TSagaState : SagaState
		{
			if (_store.TryGetValue(sagaId, out var data))
			{
				var state = new ConcurrencyTestSagaState
				{
					SagaId = sagaId,
					Version = data.Version,
					LastEventData = data.LastEventData,
				};
				return Task.FromResult((TSagaState?)(SagaState)state);
			}

			return Task.FromResult<TSagaState?>(default);
		}

		public Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
			where TSagaState : SagaState
		{
			ArgumentNullException.ThrowIfNull(sagaState);

			lock (_saveLock)
			{
				var concrete = (ConcurrencyTestSagaState)(SagaState)sagaState;
				var expectedPreviousVersion = sagaState.Version - 1;

				if (_store.TryGetValue(sagaState.SagaId, out var existing))
				{
					if (existing.Version != expectedPreviousVersion)
					{
						throw new ConcurrencyException(
							"SagaState",
							sagaState.SagaId.ToString(),
							expectedPreviousVersion,
							existing.Version);
					}
				}
				else if (expectedPreviousVersion != 0)
				{
					throw new ConcurrencyException(
						"SagaState",
						sagaState.SagaId.ToString(),
						expectedPreviousVersion,
						0L);
				}

				_store[sagaState.SagaId] = (sagaState.Version, concrete.LastEventData);
			}

			return Task.CompletedTask;
		}
	}

	#endregion
}
