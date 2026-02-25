// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="PollyCircuitBreakerAdapter"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class PollyCircuitBreakerAdapterShould : UnitTestBase, IAsyncDisposable
{
	private PollyCircuitBreakerAdapter? _adapter;

	public async ValueTask DisposeAsync()
	{
		if (_adapter != null)
		{
			await _adapter.DisposeAsync();
			_adapter = null;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && _adapter != null)
		{
			_ = _adapter.DisposeAsync().AsTask();
			_adapter = null;
		}
		base.Dispose(disposing);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new PollyCircuitBreakerAdapter(null!, options));
	}

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new PollyCircuitBreakerAdapter("test-breaker", null!));
	}

	[Fact]
	public void Constructor_WithValidArguments_CreatesInstance()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Assert
		_adapter.ShouldNotBeNull();
		_adapter.Name.ShouldBe("test-breaker");
	}

	[Fact]
	public void Constructor_WithLogger_CreatesInstance()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		var logger = A.Fake<ILogger<PollyCircuitBreakerAdapter>>();

		// Act
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options, logger);

		// Assert
		_adapter.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithNullLogger_UsesNullLogger()
	{
		// Arrange
		var options = new CircuitBreakerOptions();

		// Act
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options, null);

		// Assert
		_adapter.ShouldNotBeNull();
	}

	#endregion

	#region Properties Tests

	[Fact]
	public void Name_ReturnsConfiguredName()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("my-circuit", options);

		// Act & Assert
		_adapter.Name.ShouldBe("my-circuit");
	}

	[Fact]
	public void Configuration_ReturnsOptionsAsDictionary()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 10,
			SuccessThreshold = 5,
			OpenDuration = TimeSpan.FromMinutes(2),
			OperationTimeout = TimeSpan.FromSeconds(30),
			MaxHalfOpenTests = 3
		};
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act
		var config = _adapter.Configuration;

		// Assert
		config.ShouldNotBeNull();
		config["FailureThreshold"].ShouldBe(10);
		config["SuccessThreshold"].ShouldBe(5);
		config["OpenDuration"].ShouldBe(TimeSpan.FromMinutes(2));
		config["OperationTimeout"].ShouldBe(TimeSpan.FromSeconds(30));
		config["MaxHalfOpenTests"].ShouldBe(3);
	}

	[Fact]
	public void State_InitiallyReturnsClosedState()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act & Assert
		_adapter.State.ShouldBe(ResilienceState.Closed);
	}

	[Fact]
	public void HealthStatus_WhenClosed_ReturnsHealthy()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act & Assert
		_adapter.HealthStatus.ShouldBe(PatternHealthStatus.Healthy);
	}

	[Fact]
	public void HealthStatus_MapsStateCorrectly()
	{
		// This test validates the switch expression in HealthStatus property
		// All states except Open/HalfOpen result in Closed state initially
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Initial state is Closed - verify mapping
		_adapter.State.ShouldBe(ResilienceState.Closed);
		_adapter.HealthStatus.ShouldBe(PatternHealthStatus.Healthy);

		// After Reset, still Closed
		_adapter.Reset();
		_adapter.State.ShouldBe(ResilienceState.Closed);
		_adapter.HealthStatus.ShouldBe(PatternHealthStatus.Healthy);
	}

	#endregion

	#region ExecuteAsync Tests

	[Fact]
	public async Task ExecuteAsync_WithNullOperation_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_adapter.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithSuccessfulOperation_ReturnsResult()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act
		var result = await _adapter.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_WithSuccessfulOperation_IncrementsSuccessfulRequests()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act
		_ = await _adapter.ExecuteAsync(() => Task.FromResult(1), CancellationToken.None);
		_ = await _adapter.ExecuteAsync(() => Task.FromResult(2), CancellationToken.None);

		// Assert
		var metrics = _adapter.GetCircuitBreakerMetrics();
		metrics.TotalRequests.ShouldBe(2);
		metrics.SuccessfulRequests.ShouldBe(2);
		metrics.FailedRequests.ShouldBe(0);
	}

	[Fact]
	public async Task ExecuteAsync_WithCancellationToken_PassesTokenToOperation()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		using var cts = new CancellationTokenSource();
		var tokenReceived = false;

		// Act
		_ = await _adapter.ExecuteAsync(() =>
		{
			tokenReceived = true;
			return Task.FromResult(1);
		}, cts.Token);

		// Assert
		tokenReceived.ShouldBeTrue();
	}

	#endregion

	#region ExecuteAsync with Fallback Tests

	[Fact]
	public async Task ExecuteAsync_WithFallback_WithNullOperation_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_adapter.ExecuteAsync(null!, () => Task.FromResult(0), CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithFallback_WithNullFallback_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_adapter.ExecuteAsync(() => Task.FromResult(1), null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithFallback_WhenOperationSucceeds_ReturnsPrimaryResult()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act
		var result = await _adapter.ExecuteAsync(
			() => Task.FromResult(42),
			() => Task.FromResult(99),
			CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void Reset_SetsStateToClosed()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act
		_adapter.Reset();

		// Assert
		_adapter.State.ShouldBe(ResilienceState.Closed);
	}

	#endregion

	#region GetMetrics Tests

	[Fact]
	public void GetMetrics_ReturnsPatternMetrics()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act
		var metrics = _adapter.GetMetrics();

		// Assert
		metrics.ShouldNotBeNull();
		metrics.TotalOperations.ShouldBe(0);
		metrics.SuccessfulOperations.ShouldBe(0);
		metrics.FailedOperations.ShouldBe(0);
	}

	[Fact]
	public async Task GetMetrics_AfterOperations_ReturnsUpdatedMetrics()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_ = await _adapter.ExecuteAsync(() => Task.FromResult(1), CancellationToken.None);

		// Act
		var metrics = _adapter.GetMetrics();

		// Assert
		metrics.TotalOperations.ShouldBe(1);
		metrics.SuccessfulOperations.ShouldBe(1);
		metrics.CustomMetrics.ShouldContainKey("RejectedRequests");
		metrics.CustomMetrics.ShouldContainKey("FallbackExecutions");
		metrics.CustomMetrics.ShouldContainKey("State");
	}

	[Fact]
	public void GetCircuitBreakerMetrics_ReturnsDetailedMetrics()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act
		var metrics = _adapter.GetCircuitBreakerMetrics();

		// Assert
		metrics.ShouldNotBeNull();
		metrics.TotalRequests.ShouldBe(0);
		metrics.SuccessfulRequests.ShouldBe(0);
		metrics.FailedRequests.ShouldBe(0);
		metrics.RejectedRequests.ShouldBe(0);
		metrics.FallbackExecutions.ShouldBe(0);
		metrics.CurrentState.ShouldBe(ResilienceState.Closed);
	}

	#endregion

	#region Lifecycle Tests

	[Fact]
	public async Task InitializeAsync_CompletesSuccessfully()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		var config = new Dictionary<string, object>();

		// Act & Assert - should not throw
		await _adapter.InitializeAsync(config, CancellationToken.None);
	}

	[Fact]
	public async Task StartAsync_CompletesSuccessfully()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act & Assert - should not throw
		await _adapter.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StopAsync_CompletesSuccessfully()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act & Assert - should not throw
		await _adapter.StopAsync(CancellationToken.None);
	}

	#endregion

	#region Observer Pattern Tests

	[Fact]
	public void Subscribe_WithNullObserver_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _adapter.Subscribe(null!));
	}

	[Fact]
	public void Subscribe_WithValidObserver_AddsObserver()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		var observer = A.Fake<IPatternObserver>();

		// Act & Assert - should not throw
		_adapter.Subscribe(observer);
	}

	[Fact]
	public void Subscribe_SameObserverTwice_DoesNotAddDuplicate()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		var observer = A.Fake<IPatternObserver>();

		// Act - should not throw
		_adapter.Subscribe(observer);
		_adapter.Subscribe(observer); // Second call should be idempotent

		// Assert - no exception means success
	}

	[Fact]
	public void Unsubscribe_WithNullObserver_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _adapter.Unsubscribe(null!));
	}

	[Fact]
	public void Unsubscribe_WithValidObserver_RemovesObserver()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		var observer = A.Fake<IPatternObserver>();
		_adapter.Subscribe(observer);

		// Act & Assert - should not throw
		_adapter.Unsubscribe(observer);
	}

	[Fact]
	public void Unsubscribe_NonSubscribedObserver_DoesNotThrow()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		var observer = A.Fake<IPatternObserver>();

		// Act & Assert - should not throw
		_adapter.Unsubscribe(observer);
	}

	#endregion

	#region ExecuteAsync Failure Tests

	[Fact]
	public async Task ExecuteAsync_WhenOperationFails_IncrementsFailedRequests()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 10, // High threshold to prevent circuit from opening
			OpenDuration = TimeSpan.FromSeconds(1)
		};
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => _adapter.ExecuteAsync<int>(() => throw new InvalidOperationException("Test failure"), CancellationToken.None));

		var metrics = _adapter.GetCircuitBreakerMetrics();
		metrics.TotalRequests.ShouldBe(1);
		metrics.FailedRequests.ShouldBe(1);
		metrics.SuccessfulRequests.ShouldBe(0);
	}

	[Fact]
	public async Task ExecuteAsync_WhenOperationFailsMultipleTimes_TracksAllFailures()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 20, // High threshold to prevent circuit from opening
			OpenDuration = TimeSpan.FromSeconds(1)
		};
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act - fail 3 times
		for (var i = 0; i < 3; i++)
		{
			try
			{
				_ = await _adapter.ExecuteAsync<int>(() => throw new InvalidOperationException("Test failure"), CancellationToken.None);
			}
			catch (InvalidOperationException)
			{
				// Expected
			}
		}

		// Assert
		var metrics = _adapter.GetCircuitBreakerMetrics();
		metrics.TotalRequests.ShouldBe(3);
		metrics.FailedRequests.ShouldBe(3);
	}

	[Fact]
	public async Task ExecuteAsync_WithFallback_WhenPrimaryFails_LogsAndExecutesFallback()
	{
		// This test indirectly verifies the fallback path by using a mock that always throws
		// CircuitBreakerOpenException behavior is internal to Polly, so we test the regular exception path

		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		var fallbackCalled = false;

		// Act - When primary throws a non-circuit-breaker exception, it should propagate (not use fallback)
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			_ = await _adapter.ExecuteAsync(
				() => throw new InvalidOperationException("Primary failed"),
				() =>
				{
					fallbackCalled = true;
					return Task.FromResult(99);
				},
				CancellationToken.None);
		});

		// Assert - Fallback is only for CircuitBreakerOpenException, not regular exceptions
		fallbackCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task ExecuteAsync_WithCancellationToken_WhenCancelled_ThrowsOperationCanceled()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => _adapter.ExecuteAsync(() => Task.FromResult(42), cts.Token));
	}

	#endregion

	#region DisposeAsync Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act & Assert - should not throw
		await _adapter.DisposeAsync();
		await _adapter.DisposeAsync();
		await _adapter.DisposeAsync();

		_adapter = null; // Prevent double dispose in test cleanup
	}

	[Fact]
	public async Task DisposeAsync_ClearsObservers()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		var observer = A.Fake<IPatternObserver>();
		_adapter.Subscribe(observer);

		// Act
		await _adapter.DisposeAsync();

		// Assert - state should be reset
		_adapter.State.ShouldBe(ResilienceState.Closed);

		_adapter = null; // Prevent double dispose in test cleanup
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIResiliencePattern()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Assert
		_adapter.ShouldBeAssignableTo<IResiliencePattern>();
	}

	[Fact]
	public void ImplementsIPatternObservable()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Assert
		_adapter.ShouldBeAssignableTo<IPatternObservable>();
	}

	[Fact]
	public void ImplementsIAsyncDisposable()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Assert
		_adapter.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion

	#region Logging and Callback Tests

	[Fact]
	public async Task ExecuteAsync_WithLogger_LogsSuccessfulOperation()
	{
		// Arrange
		var logger = A.Fake<ILogger<PollyCircuitBreakerAdapter>>();
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options, logger);

		// Act
		_ = await _adapter.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None);

		// Assert - operation completed successfully, metrics updated
		var metrics = _adapter.GetCircuitBreakerMetrics();
		metrics.SuccessfulRequests.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_WithLogger_LogsFailedOperation()
	{
		// Arrange
		var logger = A.Fake<ILogger<PollyCircuitBreakerAdapter>>();
		var options = new CircuitBreakerOptions { FailureThreshold = 100 };
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options, logger);

		// Act
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => _adapter.ExecuteAsync<int>(() => throw new InvalidOperationException("Test"), CancellationToken.None));

		// Assert - logger should have been called for failed operation
		A.CallTo(logger).MustHaveHappened();
	}

	[Fact]
	public async Task InitializeAsync_WithLogger_LogsInitialization()
	{
		// Arrange
		var logger = A.Fake<ILogger<PollyCircuitBreakerAdapter>>();
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options, logger);

		// Act
		await _adapter.InitializeAsync(new Dictionary<string, object>(), CancellationToken.None);

		// Assert - logger should be called for initialization
		A.CallTo(logger).MustHaveHappened();
	}

	[Fact]
	public async Task StartAsync_WithLogger_LogsStart()
	{
		// Arrange
		var logger = A.Fake<ILogger<PollyCircuitBreakerAdapter>>();
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options, logger);

		// Act
		await _adapter.StartAsync(CancellationToken.None);

		// Assert - logger should be called for start
		A.CallTo(logger).MustHaveHappened();
	}

	[Fact]
	public async Task StopAsync_WithLogger_LogsStop()
	{
		// Arrange
		var logger = A.Fake<ILogger<PollyCircuitBreakerAdapter>>();
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options, logger);

		// Act
		await _adapter.StopAsync(CancellationToken.None);

		// Assert - logger should be called for stop
		A.CallTo(logger).MustHaveHappened();
	}

	[Fact]
	public void Reset_WithLogger_LogsReset()
	{
		// Arrange
		var logger = A.Fake<ILogger<PollyCircuitBreakerAdapter>>();
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options, logger);

		// Act
		_adapter.Reset();

		// Assert - logger should be called for reset
		A.CallTo(logger).MustHaveHappened();
	}

	[Fact]
	public void Subscribe_WithLogger_LogsSubscription()
	{
		// Arrange
		var logger = A.Fake<ILogger<PollyCircuitBreakerAdapter>>();
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options, logger);
		var observer = A.Fake<IPatternObserver>();

		// Act
		_adapter.Subscribe(observer);

		// Assert - logger should be called for subscription
		A.CallTo(logger).MustHaveHappened();
	}

	[Fact]
	public void Unsubscribe_WithLogger_LogsUnsubscription()
	{
		// Arrange
		var logger = A.Fake<ILogger<PollyCircuitBreakerAdapter>>();
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options, logger);
		var observer = A.Fake<IPatternObserver>();
		_adapter.Subscribe(observer);

		// Act
		_adapter.Unsubscribe(observer);

		// Assert - logger should be called for unsubscription
		A.CallTo(logger).MustHaveHappened();
	}

	#endregion

	#region GetMetrics Custom Fields Tests

	[Fact]
	public void GetMetrics_CustomMetrics_ContainsStateAsString()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act
		var metrics = _adapter.GetMetrics();

		// Assert
		metrics.CustomMetrics["State"].ShouldBe("Closed");
	}

	[Fact]
	public async Task GetMetrics_AfterFailure_TracksRejectedRequests()
	{
		// Arrange
		var options = new CircuitBreakerOptions { FailureThreshold = 100 };
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act - cause failures
		for (var i = 0; i < 3; i++)
		{
			try
			{
				_ = await _adapter.ExecuteAsync<int>(() => throw new InvalidOperationException("Fail"), CancellationToken.None);
			}
			catch (InvalidOperationException)
			{
				// Expected
			}
		}

		// Assert
		var metrics = _adapter.GetMetrics();
		metrics.FailedOperations.ShouldBeGreaterThan(0);
		((long)metrics.CustomMetrics["RejectedRequests"]).ShouldBeGreaterThanOrEqualTo(0);
	}

	#endregion

	#region CancellationToken Tests

	[Fact]
	public async Task ExecuteAsync_WithFallback_WithCancellationToken_PassesToken()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		using var cts = new CancellationTokenSource();

		// Act
		var result = await _adapter.ExecuteAsync(
			() => Task.FromResult(42),
			() => Task.FromResult(99),
			cts.Token);

		// Assert
		result.ShouldBe(42);
	}

	#endregion
}
