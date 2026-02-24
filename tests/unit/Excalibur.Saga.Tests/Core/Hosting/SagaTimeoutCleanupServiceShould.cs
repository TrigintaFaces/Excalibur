// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Hosting;
using Excalibur.Saga.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SagaStateModel = Excalibur.Saga.Models.SagaState;

namespace Excalibur.Saga.Tests.Core.Hosting;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaTimeoutCleanupServiceShould
{
	private readonly ISagaStateStore _stateStore;
	private readonly ISagaStateStoreQuery _stateStoreQuery;

	public SagaTimeoutCleanupServiceShould()
	{
		_stateStore = A.Fake<ISagaStateStore>();
		_stateStoreQuery = A.Fake<ISagaStateStoreQuery>();
	}

	[Fact]
	public async Task CleanupTimedOutSagas_WhenRunning()
	{
		// Arrange
		var timedOutSaga = new SagaStateModel
		{
			SagaId = "saga-1",
			SagaName = "OrderSaga",
			Status = SagaStatus.Running,
			LastUpdatedAt = DateTime.UtcNow.AddHours(-48) // Well past the 24h default
		};

		A.CallTo(() => _stateStoreQuery.GetByStatusAsync(
				SagaStatus.Running, A<int>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<SagaStateModel>>(new[] { timedOutSaga }));

		var updateObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _stateStore.UpdateStateAsync(A<SagaStateModel>._, A<CancellationToken>._))
			.Invokes(() => updateObserved.TrySetResult(true))
			.Returns(true);

		var options = new SagaTimeoutCleanupOptions
		{
			CleanupInterval = TimeSpan.FromMilliseconds(50),
			TimeoutThreshold = TimeSpan.FromHours(24),
			BatchSize = 100,
			EnableVerboseLogging = true
		};

		using var cts = new CancellationTokenSource();
		using var sut = CreateService(options);

		// Act
		await sut.StartAsync(cts.Token);
		await updateObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await sut.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _stateStore.UpdateStateAsync(
				A<SagaStateModel>.That.Matches(s => s.Status == SagaStatus.Expired),
				A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task NotCleanup_WhenSagasAreWithinTimeout()
	{
		// Arrange
		var queryCallCount = 0;
		var firstQueryObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var recentSaga = new SagaStateModel
		{
			SagaId = "saga-1",
			SagaName = "OrderSaga",
			Status = SagaStatus.Running,
			LastUpdatedAt = DateTime.UtcNow // Just now, not timed out
		};

		A.CallTo(() => _stateStoreQuery.GetByStatusAsync(
				SagaStatus.Running, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				queryCallCount++;
				firstQueryObserved.TrySetResult(true);
				return Task.FromResult<IEnumerable<SagaStateModel>>(new[] { recentSaga });
			});

		var options = new SagaTimeoutCleanupOptions
		{
			CleanupInterval = TimeSpan.FromMilliseconds(50),
			TimeoutThreshold = TimeSpan.FromHours(24)
		};

		using var cts = new CancellationTokenSource();
		using var sut = CreateService(options);

		// Act
		await sut.StartAsync(cts.Token);
		await firstQueryObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await sut.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _stateStore.UpdateStateAsync(A<SagaStateModel>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ContinueAfterTransientError()
	{
		// Arrange
		var callCount = 0;
		var transientFailureObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondQueryObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _stateStoreQuery.GetByStatusAsync(
				SagaStatus.Running, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var observedCount = Interlocked.Increment(ref callCount);
				if (observedCount == 1)
				{
					transientFailureObserved.TrySetResult(true);
					throw new InvalidOperationException("transient");
				}

				secondQueryObserved.TrySetResult(true);
				return Task.FromResult<IEnumerable<SagaStateModel>>(Array.Empty<SagaStateModel>());
			});

		var options = new SagaTimeoutCleanupOptions
		{
			CleanupInterval = TimeSpan.FromMilliseconds(50),
			TimeoutThreshold = TimeSpan.FromHours(24)
		};

		using var cts = new CancellationTokenSource();
		using var sut = CreateService(options);

		// Act
		await sut.StartAsync(cts.Token);
		await transientFailureObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await secondQueryObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
		await sut.StopAsync(CancellationToken.None);

		// Assert - should have retried after error
		Volatile.Read(ref callCount).ShouldBeGreaterThan(1);
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		var store = A.Fake<ISagaStateStore>();
		var query = A.Fake<ISagaStateStoreQuery>();
		var logger = NullLogger<SagaTimeoutCleanupService>.Instance;
		var opts = Microsoft.Extensions.Options.Options.Create(new SagaTimeoutCleanupOptions());

		Should.Throw<ArgumentNullException>(() => new SagaTimeoutCleanupService(null!, query, logger, opts));
		Should.Throw<ArgumentNullException>(() => new SagaTimeoutCleanupService(store, null!, logger, opts));
		Should.Throw<ArgumentNullException>(() => new SagaTimeoutCleanupService(store, query, null!, opts));
		Should.Throw<ArgumentNullException>(() => new SagaTimeoutCleanupService(store, query, logger, null!));
	}

	private SagaTimeoutCleanupService CreateService(SagaTimeoutCleanupOptions options) =>
		new(
			_stateStore,
			_stateStoreQuery,
			NullLogger<SagaTimeoutCleanupService>.Instance,
			Microsoft.Extensions.Options.Options.Create(options));
}
