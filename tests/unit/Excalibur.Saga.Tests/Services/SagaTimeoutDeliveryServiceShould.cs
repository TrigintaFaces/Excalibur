// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Services;
using Excalibur.Saga.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SagaTimeoutDeliveryService"/>.
/// Verifies timeout polling, message deserialization, dispatch, and delivery tracking.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.Services")]
[Trait("Priority", "1")]
public sealed class SagaTimeoutDeliveryServiceShould : UnitTestBase
{
	private readonly ISagaTimeoutStore _timeoutStore;
	private readonly InMemorySagaTimeoutStore _actualTimeoutStore;
	private readonly RecordedFailureFacet _failureFacet;
	private readonly Dictionary<string, TaskCompletionSource> _heldSignals = new(StringComparer.Ordinal);
	private readonly IDispatcher _dispatcher;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<SagaTimeoutDeliveryService> _logger;
	private readonly IOptions<SagaTimeoutOptions> _options;

	// The registry is the service's only type-resolution path, so a host that never registered a type
	// cannot deliver it. These tests stand in for that composition step: every timeout type they schedule
	// is registered here, exactly as AddSaga<T>() would register it during DI composition.
	private readonly SagaTypeRegistry _typeRegistry;

	public SagaTimeoutDeliveryServiceShould()
	{
		_timeoutStore = A.Fake<ISagaTimeoutStore>();
		_dispatcher = A.Fake<IDispatcher>();
		_logger = NullLogger<SagaTimeoutDeliveryService>.Instance;

		var services = new ServiceCollection();
		services.AddDefaultTenantContext();
		services.AddSingleton(_dispatcher);
		_serviceProvider = services.BuildServiceProvider();
		_actualTimeoutStore = new InMemorySagaTimeoutStore(_serviceProvider.GetRequiredService<ITenantContext>());
		_failureFacet = new RecordedFailureFacet(_actualTimeoutStore, receipt =>
		{
			if (_heldSignals.TryGetValue(receipt.Timeout.TimeoutId, out var signal)) { _ = signal.TrySetResult(); }
		});
		// The fake records existing call-count oracles; ALL storage authority/effects come from this real store.
		A.CallTo(() => _timeoutStore.GetService(A<Type>._)).ReturnsLazily((Type type) =>
			type == typeof(ISagaTimeoutFailureStore) ? _failureFacet : ((IServiceProvider)_actualTimeoutStore).GetService(type));
		A.CallTo(() => _timeoutStore.ScheduleTimeoutAsync(A<SagaTimeout>._, A<CancellationToken>._))
			.ReturnsLazily((SagaTimeout timeout, CancellationToken token) => _actualTimeoutStore.ScheduleTimeoutAsync(timeout, token));
		A.CallTo(() => _timeoutStore.CancelTimeoutAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string saga, string id, CancellationToken token) => _actualTimeoutStore.CancelTimeoutAsync(saga, id, token));
		A.CallTo(() => _timeoutStore.CancelAllTimeoutsAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string saga, CancellationToken token) => _actualTimeoutStore.CancelAllTimeoutsAsync(saga, token));
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily((DateTimeOffset asOf, CancellationToken token) => _actualTimeoutStore.GetDueTimeoutsAsync(asOf, token));
		A.CallTo(() => _timeoutStore.ClaimDueTimeoutsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((DateTimeOffset asOf, int batch, CancellationToken token) => _actualTimeoutStore.ClaimDueTimeoutsAsync(asOf, batch, token));
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(A<ClaimedSagaTimeout>._, A<CancellationToken>._))
			.ReturnsLazily((ClaimedSagaTimeout claim, CancellationToken token) => _actualTimeoutStore.MarkDeliveredAsync(claim, token));


		_typeRegistry = new SagaTypeRegistry();
		_typeRegistry.RegisterType(typeof(TestTimeoutMessage));
		_typeRegistry.RegisterType(typeof(NonDispatchMessage));
		_typeRegistry.RegisterType(typeof(NoDefaultCtorDispatchMessage));

		_options = Options.Create(new SagaTimeoutOptions
		{
			PollInterval = TimeSpan.FromMilliseconds(50),
			BatchSize = 10,
			EnableVerboseLogging = false
		});
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenTimeoutStoreIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SagaTimeoutDeliveryService(null!, _serviceProvider, _logger, _options, _typeRegistry));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenServiceProviderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SagaTimeoutDeliveryService(_timeoutStore, null!, _logger, _options, _typeRegistry));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, null!, _options, _typeRegistry));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, null!, _typeRegistry));
	}

	[Fact]
	public void CreateInstance_WithValidParameters()
	{
		// Act
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Assert
		service.ShouldNotBeNull();
	}

	#endregion

	#region ExecuteAsync Tests

	[Fact]
	public async Task PollForDueTimeouts_WhenExecuting()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var firstPollObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _timeoutStore.ClaimDueTimeoutsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((DateTimeOffset asOf, int batch, CancellationToken token) =>
			{
				_ = firstPollObserved.TrySetResult();
				return _actualTimeoutStore.ClaimDueTimeoutsAsync(asOf, batch, token);
			});

		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Act - let it run one cycle then cancel
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstPollObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();

		await StopAndJoinAsync(service, cts.Token);

		// Assert
		A.CallTo(() => _timeoutStore.ClaimDueTimeoutsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
#pragma warning disable CA1506 // Test method orchestrates multiple fakes/probes by design.
	public async Task ProcessDueTimeouts_WhenTimeoutsExist()
	{
		// Arrange
		var sagaId = Guid.NewGuid().ToString();
		var timeoutId = Guid.NewGuid().ToString();
		var deliveredObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var timeout = CreateTimeout(
			timeoutId,
			sagaId,
			typeof(TestTimeoutMessage).AssemblyQualifiedName,
			JsonSerializer.SerializeToUtf8Bytes(new TestTimeoutMessage { Value = "test" }));

		await _timeoutStore.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(MessageResult.Success()));
		ObserveActualRetirement(timeoutId, deliveredObserved);

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			deliveredObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();

		await StopAndJoinAsync(service, cts.Token);

		// Assert - timeout should be dispatched and marked delivered
		A.CallTo(() => _dispatcher.DispatchAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.MustHaveHappened();

		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(A<ClaimedSagaTimeout>.That.Matches(c => c.Timeout.TimeoutId == timeoutId), A<CancellationToken>._))
			.MustHaveHappened();
	}
#pragma warning restore CA1506

	[Fact]
	public async Task SkipDeliveredTimeouts_WhenNoDueTimeouts()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var firstPollObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _timeoutStore.ClaimDueTimeoutsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((DateTimeOffset asOf, int batch, CancellationToken token) =>
			{
				_ = firstPollObserved.TrySetResult();
				return _actualTimeoutStore.ClaimDueTimeoutsAsync(asOf, batch, token);
			});

		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstPollObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();

		await StopAndJoinAsync(service, cts.Token);

		// Assert - no dispatch should occur
		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RetainUnknown_WhenTimeoutTypeCannotBeResolved()
	{
		// Arrange
		var timeoutId = Guid.NewGuid().ToString();
		var heldObserved = ObserveActualHold(timeoutId);
		var timeout = CreateTimeout(
			timeoutId,
			Guid.NewGuid().ToString(),
			"NonExistent.Type, NonExistent.Assembly",
			null);

		await _timeoutStore.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			heldObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();

		await StopAndJoinAsync(service, cts.Token);

		// Assert - configuration failures retain actual Unknown hold; never acknowledge undelivered work.
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(A<ClaimedSagaTimeout>.That.Matches(c => c.Timeout.TimeoutId == timeoutId), A<CancellationToken>._))
			.MustNotHaveHappened();
		await AssertActualHoldAsync(timeout, MessageFailureKind.Unknown);
	}

	[Fact]
#pragma warning disable CA1506 // Test method orchestrates multiple fakes/probes by design.
	public async Task NotMarkDelivered_WhenDispatchFails()
	{
		// Arrange
		var timeoutId = Guid.NewGuid().ToString();
		var dispatchAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var heldObserved = ObserveActualHold(timeoutId);
		var timeout = CreateTimeout(
			timeoutId,
			Guid.NewGuid().ToString(),
			typeof(TestTimeoutMessage).AssemblyQualifiedName,
			JsonSerializer.SerializeToUtf8Bytes(new TestTimeoutMessage { Value = "test" }));

		await _timeoutStore.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes(() => _ = dispatchAttempted.TrySetResult())
			.Throws(new InvalidOperationException("Dispatch failed"));

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			dispatchAttempted.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(heldObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();

		await StopAndJoinAsync(service, cts.Token);

		// Assert - should NOT mark as delivered when dispatch fails
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(A<ClaimedSagaTimeout>.That.Matches(c => c.Timeout.TimeoutId == timeoutId), A<CancellationToken>._))
			.MustNotHaveHappened();
		await AssertActualHoldAsync(timeout, MessageFailureKind.Unknown);
	}
#pragma warning restore CA1506

	[Fact]
#pragma warning disable CA1506 // Test method orchestrates multiple fakes/probes by design.
	public async Task NotMarkDelivered_WhenDispatchReturnsFailedResultWithoutThrowing()
	{
		// A dispatch can FAIL WITHOUT THROWING. TimeoutMiddleware returns a Succeeded:false result when
		// TimeoutOptions.ThrowOnTimeout is disabled, and RateLimitingMiddleware returns one when the limit
		// is exceeded. The sibling arm above covers only the THROWING case, so this path was unguarded:
		// the service discarded the result and deleted the row, producing ZERO deliveries plus deletion --
		// the saga then waits forever for a timeout that no longer exists. That is strictly outside the
		// at-least-once guarantee ISagaTimeoutStore documents.
		//
		// This arm is RED against the pre-fix service (which called MarkDeliveredAsync unconditionally)
		// and GREEN once the result is inspected. Its liveness partner is
		// ProcessDueTimeouts_WhenTimeoutsExist, which proves a SUCCEEDED dispatch still retires the row --
		// without that partner, a service that never marked anything delivered would pass this arm.

		// Arrange
		var timeoutId = Guid.NewGuid().ToString();
		var dispatchAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var heldObserved = ObserveActualHold(timeoutId);
		var timeout = CreateTimeout(
			timeoutId,
			Guid.NewGuid().ToString(),
			typeof(TestTimeoutMessage).AssemblyQualifiedName,
			JsonSerializer.SerializeToUtf8Bytes(new TestTimeoutMessage { Value = "test" }));

		await _timeoutStore.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		// Returns normally. Does NOT throw. Succeeded is false.
		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes(() => _ = dispatchAttempted.TrySetResult())
			.Returns(Task.FromResult(MessageResult.Failed("Timed out; ThrowOnTimeout disabled.")));

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			dispatchAttempted.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(heldObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();

		await StopAndJoinAsync(service, cts.Token);

		// Assert - original untyped failure is retained for explicit recovery, never acknowledged or blindly retried.
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(A<ClaimedSagaTimeout>.That.Matches(c => c.Timeout.TimeoutId == timeoutId), A<CancellationToken>._))
			.MustNotHaveHappened();
		await AssertActualHoldAsync(timeout, MessageFailureKind.Unknown);
	}
#pragma warning restore CA1506

	[Fact]
#pragma warning disable CA1506 // Excessive class coupling (test method with complex FakeItEasy setup)
	public async Task RespectBatchSize_WhenProcessingTimeouts()
	{
		// Arrange
		var options = Options.Create(new SagaTimeoutOptions
		{
			PollInterval = TimeSpan.FromMilliseconds(50),
			BatchSize = 2,
			EnableVerboseLogging = false
		});

		var timeouts = Enumerable.Range(0, 5)
			.Select(i => CreateTimeout(
				Guid.NewGuid().ToString(),
				Guid.NewGuid().ToString(),
				typeof(TestTimeoutMessage).AssemblyQualifiedName,
				JsonSerializer.SerializeToUtf8Bytes(new TestTimeoutMessage { Value = $"test-{i}" })))
			.ToList();

		foreach (var timeout in timeouts) { await _timeoutStore.ScheduleTimeoutAsync(timeout, CancellationToken.None); }
		var nextCycleObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var cycles = 0;
		var dispatchCount = 0;
		var secondDispatchObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Gate only the second poll after the first real bounded claim/dispatch/retirement cycle.
		A.CallTo(() => _timeoutStore.ClaimDueTimeoutsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(async (DateTimeOffset asOf, int batchSize, CancellationToken token) =>
			{
				if (Interlocked.Increment(ref cycles) > 1)
				{
					_ = nextCycleObserved.TrySetResult();
					await Task.Delay(Timeout.InfiniteTimeSpan, token);
				}
				return await _actualTimeoutStore.ClaimDueTimeoutsAsync(asOf, batchSize, token);
			});

		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var currentCount = Interlocked.Increment(ref dispatchCount);
				if (currentCount >= 2)
				{
					_ = secondDispatchObserved.TrySetResult();
				}

				return Task.FromResult(MessageResult.Success());
			});

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, options, _typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			secondDispatchObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(nextCycleObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		await cts.CancelAsync();

		await StopAndJoinAsync(service, cts.Token);

		// Assert - only batch size (2) should be processed in first cycle
		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);

		_actualTimeoutStore.GetPendingCount().ShouldBe(3);

		// The configured batch size must reach the store's lease call, not merely bound the dispatch count.
		// Without this, the service could claim an unbounded batch and the dispatch assertion above would
		// still pass whenever the fake happened to return exactly two timeouts.
		A.CallTo(() => _timeoutStore.ClaimDueTimeoutsAsync(A<DateTimeOffset>._, 2, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task HandleNullTimeoutData_ByCreatingDefaultInstance()
	{
		// Arrange
		var timeoutId = Guid.NewGuid().ToString();
		var dispatchObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var timeout = CreateTimeout(
			timeoutId,
			Guid.NewGuid().ToString(),
			typeof(TestTimeoutMessage).AssemblyQualifiedName,
			null); // No data - should create default instance

		await _timeoutStore.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes(() => _ = dispatchObserved.TrySetResult())
			.Returns(Task.FromResult(MessageResult.Success()));

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			dispatchObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();

		await StopAndJoinAsync(service, cts.Token);

		// Assert - should dispatch default instance
		A.CallTo(() => _dispatcher.DispatchAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RetainUnknown_WhenMessageTypeDoesNotImplementIDispatchMessage()
	{
		// Arrange
		var timeoutId = Guid.NewGuid().ToString();
		var heldObserved = ObserveActualHold(timeoutId);
		var timeout = CreateTimeout(
			timeoutId,
			Guid.NewGuid().ToString(),
			typeof(NonDispatchMessage).AssemblyQualifiedName,
			JsonSerializer.SerializeToUtf8Bytes(new NonDispatchMessage { Data = "test" }));

		await _timeoutStore.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			heldObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();

		await StopAndJoinAsync(service, cts.Token);

		// Assert - configuration failures retain actual Unknown hold; never acknowledge undelivered work.
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(A<ClaimedSagaTimeout>.That.Matches(c => c.Timeout.TimeoutId == timeoutId), A<CancellationToken>._))
			.MustNotHaveHappened();

		// And should not dispatch
		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		await AssertActualHoldAsync(timeout, MessageFailureKind.Unknown);
	}

	[Fact]
	public async Task ContinueProcessing_WhenPollCycleFails()
	{
		// Arrange
		var callCount = 0;
		var observedSecondPoll = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _timeoutStore.ClaimDueTimeoutsAsync(A<DateTimeOffset>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((DateTimeOffset asOf, int batch, CancellationToken token) =>
			{
				var currentCallCount = Interlocked.Increment(ref callCount);
				if (currentCallCount == 1)
				{
					throw new InvalidOperationException("Database error");
				}
				if (currentCallCount >= 2)
				{
					observedSecondPoll.TrySetResult(true);
				}
				return _actualTimeoutStore.ClaimDueTimeoutsAsync(asOf, batch, token);
			});

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			observedSecondPoll.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();

		await StopAndJoinAsync(service, cts.Token);

		// Assert - should have tried multiple times (service continued after error)
		Interlocked.CompareExchange(ref callCount, 0, 0).ShouldBeGreaterThan(1);
	}

	[Fact]
	public async Task ProcessTimeout_WhenTypeResolvedBySimpleNameFallback()
	{
		// Arrange
		var timeoutId = Guid.NewGuid().ToString();
		var deliveredObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var timeout = CreateTimeout(
			timeoutId,
			Guid.NewGuid().ToString(),
			$"{typeof(TestTimeoutMessage).FullName}, Missing.Assembly.Name",
			JsonSerializer.SerializeToUtf8Bytes(new TestTimeoutMessage { Value = "fallback" }));

		await _timeoutStore.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(MessageResult.Success()));
		ObserveActualRetirement(timeoutId, deliveredObserved);

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			deliveredObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();
		await StopAndJoinAsync(service, cts.Token);

		// Assert
		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(A<ClaimedSagaTimeout>.That.Matches(c => c.Timeout.TimeoutId == timeoutId), A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RetainUnknown_WhenNullTimeoutData_AndTypeHasNoParameterlessConstructor()
	{
		// Arrange
		var timeoutId = Guid.NewGuid().ToString();
		var heldObserved = ObserveActualHold(timeoutId);
		var timeout = CreateTimeout(
			timeoutId,
			Guid.NewGuid().ToString(),
			typeof(NoDefaultCtorDispatchMessage).AssemblyQualifiedName!,
			null);

		await _timeoutStore.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, _typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			heldObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();
		await StopAndJoinAsync(service, cts.Token);

		// Assert
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(A<ClaimedSagaTimeout>.That.Matches(c => c.Timeout.TimeoutId == timeoutId), A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		await AssertActualHoldAsync(timeout, MessageFailureKind.Unknown);
	}

	[Fact]
#pragma warning disable CA1506 // Test method orchestrates multiple fakes/probes by design.
	public async Task ResolveTimeoutType_ViaSagaTypeRegistry_WhenProvided()
	{
		// Arrange
		var sagaId = Guid.NewGuid().ToString();
		var timeoutId = Guid.NewGuid().ToString();
		var deliveredObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Use a type name that won't resolve via assembly scanning -- registry must handle it
		var registryTypeName = typeof(TestTimeoutMessage).FullName!;

		var timeout = CreateTimeout(
			timeoutId,
			sagaId,
			registryTypeName,
			JsonSerializer.SerializeToUtf8Bytes(new TestTimeoutMessage { Value = "registry-resolved" }));

		var typeRegistry = new SagaTypeRegistry();
		typeRegistry.RegisterType(typeof(TestTimeoutMessage));

		await _timeoutStore.ScheduleTimeoutAsync(timeout, CancellationToken.None);

		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(MessageResult.Success()));
		ObserveActualRetirement(timeoutId, deliveredObserved);

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(
			_timeoutStore, _serviceProvider, _logger, _options, typeRegistry);

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			deliveredObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		await cts.CancelAsync();

		await StopAndJoinAsync(service, cts.Token);

		// Assert - should dispatch successfully using registry-resolved type
		A.CallTo(() => _dispatcher.DispatchAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.MustHaveHappened();

		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(A<ClaimedSagaTimeout>.That.Matches(c => c.Timeout.TimeoutId == timeoutId), A<CancellationToken>._))
			.MustHaveHappened();
	}
#pragma warning restore CA1506

	[Fact]
	public void ThrowArgumentNullException_WhenSagaTypeRegistryIsNull()
	{
		// The registry is the only resolution path. A service without it cannot resolve configured
		// work; refusing it at construction makes that state inexpressible
		// rather than a silent estate-wide loss of timeouts.
		Should.Throw<ArgumentNullException>(() =>
			new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options, null!));
	}

	#endregion

	#region Helper Methods


	private TaskCompletionSource ObserveActualHold(string timeoutId)
	{
		var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_heldSignals.Add(timeoutId, signal);
		return signal;
	}

	private void ObserveActualRetirement(string timeoutId, TaskCompletionSource signal)
	{
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(A<ClaimedSagaTimeout>.That.Matches(claim => claim.Timeout.TimeoutId == timeoutId), A<CancellationToken>._))
			.ReturnsLazily(async (ClaimedSagaTimeout claim, CancellationToken token) =>
			{
				var outcome = await _actualTimeoutStore.MarkDeliveredAsync(claim, token);
				if (outcome == SagaTimeoutRetirementOutcome.Retired) { _ = signal.TrySetResult(); }
				return outcome;
			});
	}

	private async Task AssertActualHoldAsync(SagaTimeout expected, MessageFailureKind kind)
	{
		var page = await _failureFacet.GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, expected.SagaId, 10, null, CancellationToken.None);
		var held = page.Items.ShouldHaveSingleItem();
		held.State.ShouldBe(SagaTimeoutHoldState.Held); held.Kind.ShouldBe(kind);
		held.Timeout.TimeoutId.ShouldBe(expected.TimeoutId);
		held.Timeout.TimeoutData.ShouldBe(expected.TimeoutData);
		held.Timeout.DueAt.ShouldBe(expected.DueAt);
		(await _actualTimeoutStore.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, CancellationToken.None)).ShouldHaveSingleItem().TimeoutId.ShouldBe(expected.TimeoutId);
		(await _actualTimeoutStore.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow.AddMinutes(3), 10, CancellationToken.None)).ShouldBeEmpty();
	}

	private static async Task StopAndJoinAsync(SagaTimeoutDeliveryService service, CancellationToken callerToken)
	{
		try
		{
			await service.StopAsync(CancellationToken.None);
			if (service.ExecuteTask is { } pump) { await pump; }
		}
		catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { }
	}

	// Transparent post-commit observation: never fabricates Held or changes the real claim/hold lifecycle.
	private sealed class RecordedFailureFacet(ISagaTimeoutFailureStore inner, Action<SagaTimeoutHoldReceipt> held) : ISagaTimeoutFailureStore
	{
		public bool HoldsSurviveProcessRestart => inner.HoldsSurviveProcessRestart;
		public ValueTask ValidateFailureStorageAsync(CancellationToken cancellationToken) => inner.ValidateFailureStorageAsync(cancellationToken);
		public async ValueTask<SagaTimeoutHoldAttempt> TryHoldFailureAsync(SagaTimeoutFailureObservation observation,
			ClaimedSagaTimeout claim, Guid operationId, CancellationToken cancellationToken)
		{
			var attempt = await inner.TryHoldFailureAsync(observation, claim, operationId, cancellationToken);
			if (attempt.Outcome == SagaTimeoutHoldOutcome.Held) { held(attempt.Receipt!); }
			return attempt;
		}
		public ValueTask<SagaTimeoutHoldPage> GetHeldFailuresAsync(string tenantId, string? sagaId, int pageSize, string? cursor, CancellationToken cancellationToken) =>
			inner.GetHeldFailuresAsync(tenantId, sagaId, pageSize, cursor, cancellationToken);
		public ValueTask<SagaTimeoutHoldReceipt?> TryResumeHeldFailureAsync(SagaTimeoutHoldToken token, Guid commandId, CancellationToken cancellationToken) =>
			inner.TryResumeHeldFailureAsync(token, commandId, cancellationToken);
	}

	private static SagaTimeout CreateTimeout(
		string timeoutId,
		string sagaId,
		string timeoutType,
		byte[]? timeoutData)
	{
		return new SagaTimeout(
			TimeoutId: timeoutId,
			SagaId: sagaId,
			SagaType: "TestSagaType",
			TimeoutType: timeoutType,
			TimeoutData: timeoutData,
			DueAt: DateTime.UtcNow.AddMinutes(-1),
			ScheduledAt: DateTime.UtcNow.AddMinutes(-5));
	}

	#endregion

	#region Test Types

	private sealed class TestTimeoutMessage : IDispatchMessage
	{
		public string Value { get; init; } = string.Empty;
	}

	private sealed class NonDispatchMessage
	{
		public string Data { get; init; } = string.Empty;
	}

	private sealed class NoDefaultCtorDispatchMessage : IDispatchMessage
	{
		public NoDefaultCtorDispatchMessage(string value) => Value = value;

		public string Value { get; }
	}

	#endregion
}