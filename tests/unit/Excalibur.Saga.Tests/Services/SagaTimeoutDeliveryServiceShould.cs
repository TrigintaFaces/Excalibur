// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Services;

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
	private readonly IDispatcher _dispatcher;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<SagaTimeoutDeliveryService> _logger;
	private readonly IOptions<SagaTimeoutOptions> _options;

	public SagaTimeoutDeliveryServiceShould()
	{
		_timeoutStore = A.Fake<ISagaTimeoutStore>();
		_dispatcher = A.Fake<IDispatcher>();
		_logger = NullLogger<SagaTimeoutDeliveryService>.Instance;

		var services = new ServiceCollection();
		services.AddSingleton(_dispatcher);
		_serviceProvider = services.BuildServiceProvider();

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
			new SagaTimeoutDeliveryService(null!, _serviceProvider, _logger, _options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenServiceProviderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SagaTimeoutDeliveryService(_timeoutStore, null!, _logger, _options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, null!, _options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, null!));
	}

	[Fact]
	public void CreateInstance_WithValidParameters()
	{
		// Act
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options);

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
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				_ = firstPollObserved.TrySetResult();
				return new List<SagaTimeout>();
			});

		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options);

		// Act - let it run one cycle then cancel
		var executeTask = service.StartAsync(cts.Token);
		await firstPollObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await cts.CancelAsync();

		try
		{
			await service.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
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

		var hasReturned = false;
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (!hasReturned)
				{
					hasReturned = true;
					return new List<SagaTimeout> { timeout };
				}
				return new List<SagaTimeout>();
			});

		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(MessageResult.Success()));
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(timeoutId, A<CancellationToken>._))
			.Invokes(() => _ = deliveredObserved.TrySetResult())
			.Returns(Task.CompletedTask);

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options);

		// Act
		var executeTask = service.StartAsync(cts.Token);
		await deliveredObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await cts.CancelAsync();

		try
		{
			await service.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert - timeout should be dispatched and marked delivered
		A.CallTo(() => _dispatcher.DispatchAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.MustHaveHappened();

		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(timeoutId, A<CancellationToken>._))
			.MustHaveHappened();
	}
#pragma warning restore CA1506

	[Fact]
	public async Task SkipDeliveredTimeouts_WhenNoDueTimeouts()
	{
		// Arrange
		var firstPollObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				_ = firstPollObserved.TrySetResult();
				return new List<SagaTimeout>();
			});

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options);

		// Act
		var executeTask = service.StartAsync(cts.Token);
		await firstPollObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await cts.CancelAsync();

		try
		{
			await service.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert - no dispatch should occur
		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task MarkDelivered_WhenTimeoutTypeCannotBeResolved()
	{
		// Arrange
		var timeoutId = Guid.NewGuid().ToString();
		var deliveredObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var timeout = CreateTimeout(
			timeoutId,
			Guid.NewGuid().ToString(),
			"NonExistent.Type, NonExistent.Assembly",
			null);

		var hasReturned = false;
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (!hasReturned)
				{
					hasReturned = true;
					return new List<SagaTimeout> { timeout };
				}
				return new List<SagaTimeout>();
			});

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options);
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(timeoutId, A<CancellationToken>._))
			.Invokes(() => _ = deliveredObserved.TrySetResult())
			.Returns(Task.CompletedTask);

		// Act
		var executeTask = service.StartAsync(cts.Token);
		await deliveredObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await cts.CancelAsync();

		try
		{
			await service.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert - should mark as delivered to prevent retry loop
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(timeoutId, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
#pragma warning disable CA1506 // Test method orchestrates multiple fakes/probes by design.
	public async Task NotMarkDelivered_WhenDispatchFails()
	{
		// Arrange
		var timeoutId = Guid.NewGuid().ToString();
		var dispatchAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var timeout = CreateTimeout(
			timeoutId,
			Guid.NewGuid().ToString(),
			typeof(TestTimeoutMessage).AssemblyQualifiedName,
			JsonSerializer.SerializeToUtf8Bytes(new TestTimeoutMessage { Value = "test" }));

		var hasReturned = false;
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (!hasReturned)
				{
					hasReturned = true;
					return new List<SagaTimeout> { timeout };
				}
				return new List<SagaTimeout>();
			});

		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes(() => _ = dispatchAttempted.TrySetResult())
			.Throws(new InvalidOperationException("Dispatch failed"));

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options);

		// Act
		var executeTask = service.StartAsync(cts.Token);
		await dispatchAttempted.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await cts.CancelAsync();

		try
		{
			await service.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert - should NOT mark as delivered when dispatch fails
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(timeoutId, A<CancellationToken>._))
			.MustNotHaveHappened();
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

		var hasReturned = false;
		var dispatchCount = 0;
		var secondDispatchObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (!hasReturned)
				{
					hasReturned = true;
					return timeouts;
				}
				return new List<SagaTimeout>();
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
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, options);

		// Act
		await service.StartAsync(cts.Token);
		await secondDispatchObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
		await cts.CancelAsync();

		try
		{
			await service.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert - only batch size (2) should be processed in first cycle
		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);
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

		var hasReturned = false;
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (!hasReturned)
				{
					hasReturned = true;
					return new List<SagaTimeout> { timeout };
				}
				return new List<SagaTimeout>();
			});

		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes(() => _ = dispatchObserved.TrySetResult())
			.Returns(Task.FromResult(MessageResult.Success()));

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options);

		// Act
		var executeTask = service.StartAsync(cts.Token);
		await dispatchObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await cts.CancelAsync();

		try
		{
			await service.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert - should dispatch default instance
		A.CallTo(() => _dispatcher.DispatchAsync(
			A<IDispatchMessage>._,
			A<IMessageContext>._,
			A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task MarkDelivered_WhenMessageTypeDoesNotImplementIDispatchMessage()
	{
		// Arrange
		var timeoutId = Guid.NewGuid().ToString();
		var deliveredObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var timeout = CreateTimeout(
			timeoutId,
			Guid.NewGuid().ToString(),
			typeof(NonDispatchMessage).AssemblyQualifiedName,
			JsonSerializer.SerializeToUtf8Bytes(new NonDispatchMessage { Data = "test" }));

		var hasReturned = false;
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (!hasReturned)
				{
					hasReturned = true;
					return new List<SagaTimeout> { timeout };
				}
				return new List<SagaTimeout>();
			});

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options);
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(timeoutId, A<CancellationToken>._))
			.Invokes(() => _ = deliveredObserved.TrySetResult())
			.Returns(Task.CompletedTask);

		// Act
		var executeTask = service.StartAsync(cts.Token);
		await deliveredObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await cts.CancelAsync();

		try
		{
			await service.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert - should mark as delivered to prevent retry loop
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(timeoutId, A<CancellationToken>._))
			.MustHaveHappened();

		// And should not dispatch
		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ContinueProcessing_WhenPollCycleFails()
	{
		// Arrange
		var callCount = 0;
		var observedSecondPoll = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
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
				return new List<SagaTimeout>();
			});

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options);

		// Act
		var executeTask = service.StartAsync(cts.Token);
		await observedSecondPoll.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await cts.CancelAsync();

		try
		{
			await service.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

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

		var hasReturned = false;
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (!hasReturned)
				{
					hasReturned = true;
					return new List<SagaTimeout> { timeout };
				}
				return new List<SagaTimeout>();
			});

		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(Task.FromResult(MessageResult.Success()));
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(timeoutId, A<CancellationToken>._))
			.Invokes(() => _ = deliveredObserved.TrySetResult())
			.Returns(Task.CompletedTask);

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options);

		// Act
		await service.StartAsync(cts.Token);
		await deliveredObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await cts.CancelAsync();
		try
		{
			await service.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(timeoutId, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task MarkDelivered_WhenNullTimeoutData_AndTypeHasNoParameterlessConstructor()
	{
		// Arrange
		var timeoutId = Guid.NewGuid().ToString();
		var deliveredObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var timeout = CreateTimeout(
			timeoutId,
			Guid.NewGuid().ToString(),
			typeof(NoDefaultCtorDispatchMessage).AssemblyQualifiedName!,
			null);

		var hasReturned = false;
		A.CallTo(() => _timeoutStore.GetDueTimeoutsAsync(A<DateTimeOffset>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (!hasReturned)
				{
					hasReturned = true;
					return new List<SagaTimeout> { timeout };
				}
				return new List<SagaTimeout>();
			});

		using var cts = new CancellationTokenSource();
		var service = new SagaTimeoutDeliveryService(_timeoutStore, _serviceProvider, _logger, _options);
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(timeoutId, A<CancellationToken>._))
			.Invokes(() => _ = deliveredObserved.TrySetResult())
			.Returns(Task.CompletedTask);

		// Act
		await service.StartAsync(cts.Token);
		await deliveredObserved.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await cts.CancelAsync();
		try
		{
			await service.StopAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		A.CallTo(() => _timeoutStore.MarkDeliveredAsync(timeoutId, A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => _dispatcher.DispatchAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Helper Methods

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
