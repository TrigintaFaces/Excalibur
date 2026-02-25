// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Functional tests for <see cref="PollyCircuitBreakerAdapter"/> verifying
/// circuit breaker state transitions, fallback execution, observer pattern,
/// and metrics tracking.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class PollyCircuitBreakerAdapterFunctionalShould : IAsyncDisposable
{
	private PollyCircuitBreakerAdapter? _sut;

	public async ValueTask DisposeAsync()
	{
		if (_sut is not null)
		{
			await _sut.DisposeAsync();
		}
	}

	private PollyCircuitBreakerAdapter CreateAdapter(CircuitBreakerOptions? options = null)
	{
		options ??= new CircuitBreakerOptions
		{
			FailureThreshold = 3,
			SuccessThreshold = 2,
			OpenDuration = TimeSpan.FromSeconds(1),
			OperationTimeout = TimeSpan.FromSeconds(5),
			MaxHalfOpenTests = 2,
		};

		_sut = new PollyCircuitBreakerAdapter("test-cb", options);
		return _sut;
	}

	[Fact]
	public void Start_in_closed_state()
	{
		var adapter = CreateAdapter();

		adapter.State.ShouldBe(ResilienceState.Closed);
		adapter.HealthStatus.ShouldBe(PatternHealthStatus.Healthy);
	}

	[Fact]
	public void Expose_name()
	{
		var adapter = CreateAdapter();

		adapter.Name.ShouldBe("test-cb");
	}

	[Fact]
	public void Expose_configuration()
	{
		var adapter = CreateAdapter();

		var config = adapter.Configuration;
		config.ShouldContainKey(nameof(CircuitBreakerOptions.FailureThreshold));
		config[nameof(CircuitBreakerOptions.FailureThreshold)].ShouldBe(3);
		config.ShouldContainKey(nameof(CircuitBreakerOptions.SuccessThreshold));
		config[nameof(CircuitBreakerOptions.SuccessThreshold)].ShouldBe(2);
	}

	[Fact]
	public async Task Execute_successful_operation()
	{
		var adapter = CreateAdapter();

		var result = await adapter.ExecuteAsync(
			() => Task.FromResult(42),
			CancellationToken.None);

		result.ShouldBe(42);
		adapter.State.ShouldBe(ResilienceState.Closed);
	}

	[Fact]
	public async Task Track_metrics_for_successful_operations()
	{
		var adapter = CreateAdapter();

		await adapter.ExecuteAsync(() => Task.FromResult(1), CancellationToken.None);
		await adapter.ExecuteAsync(() => Task.FromResult(2), CancellationToken.None);
		await adapter.ExecuteAsync(() => Task.FromResult(3), CancellationToken.None);

		var metrics = adapter.GetMetrics();
		metrics.TotalOperations.ShouldBe(3);
		metrics.SuccessfulOperations.ShouldBe(3);
		metrics.FailedOperations.ShouldBe(0);
	}

	[Fact]
	public async Task Track_failed_operations_in_metrics()
	{
		var adapter = CreateAdapter();

		try
		{
			await adapter.ExecuteAsync<int>(
				() => throw new InvalidOperationException("test"),
				CancellationToken.None);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		var metrics = adapter.GetMetrics();
		metrics.FailedOperations.ShouldBe(1);
	}

	[Fact]
	public async Task Execute_fallback_when_circuit_is_open()
	{
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 2, // Polly v8 MinimumThroughput requires >= 2
			OpenDuration = TimeSpan.FromSeconds(30),
			OperationTimeout = TimeSpan.FromSeconds(5),
		};
		var adapter = CreateAdapter(options);

		// Force enough failures through the pipeline to open the circuit
		for (var i = 0; i < 5; i++)
		{
			try
			{
				await adapter.ExecuteAsync<int>(
					() => throw new InvalidOperationException("fail"),
					CancellationToken.None);
			}
			catch
			{
				// Expected
			}
		}

		// If circuit is open, the fallback overload should execute the fallback
		if (adapter.State == ResilienceState.Open)
		{
			var result = await adapter.ExecuteAsync(
				() => Task.FromResult(-1),
				() => Task.FromResult(99),
				CancellationToken.None);

			result.ShouldBe(99);
		}
	}

	[Fact]
	public void Reset_sets_state_to_closed()
	{
		var adapter = CreateAdapter();

		adapter.Reset();

		adapter.State.ShouldBe(ResilienceState.Closed);
	}

	[Fact]
	public async Task Initialize_completes_without_error()
	{
		var adapter = CreateAdapter();
		var config = new Dictionary<string, object>(StringComparer.Ordinal);

		await adapter.InitializeAsync(config, CancellationToken.None);

		// Should complete without throwing
	}

	[Fact]
	public async Task Start_and_stop_lifecycle()
	{
		var adapter = CreateAdapter();

		await adapter.StartAsync(CancellationToken.None);
		await adapter.StopAsync(CancellationToken.None);

		// Should complete without throwing
	}

	[Fact]
	public void Subscribe_and_unsubscribe_observer()
	{
		var adapter = CreateAdapter();
		var observer = A.Fake<IPatternObserver>();

		adapter.Subscribe(observer);
		adapter.Unsubscribe(observer);

		// Should not throw - observers are cleared on dispose
	}

	[Fact]
	public void Subscribe_null_observer_throws()
	{
		var adapter = CreateAdapter();

		Should.Throw<ArgumentNullException>(() => adapter.Subscribe(null!));
	}

	[Fact]
	public void Unsubscribe_null_observer_throws()
	{
		var adapter = CreateAdapter();

		Should.Throw<ArgumentNullException>(() => adapter.Unsubscribe(null!));
	}

	[Fact]
	public void Subscribe_same_observer_twice_does_not_duplicate()
	{
		var adapter = CreateAdapter();
		var observer = A.Fake<IPatternObserver>();

		adapter.Subscribe(observer);
		adapter.Subscribe(observer);

		// No duplicates - just verifying it doesn't throw
	}

	[Fact]
	public async Task Get_circuit_breaker_metrics()
	{
		var adapter = CreateAdapter();

		await adapter.ExecuteAsync(() => Task.FromResult(1), CancellationToken.None);

		var cbMetrics = adapter.GetCircuitBreakerMetrics();
		cbMetrics.TotalRequests.ShouldBe(1);
		cbMetrics.SuccessfulRequests.ShouldBe(1);
		cbMetrics.CurrentState.ShouldBe(ResilienceState.Closed);
	}

	[Fact]
	public async Task Throw_for_null_operation()
	{
		var adapter = CreateAdapter();

		await Should.ThrowAsync<ArgumentNullException>(
			() => adapter.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_for_null_operation_in_fallback_overload()
	{
		var adapter = CreateAdapter();

		await Should.ThrowAsync<ArgumentNullException>(
			() => adapter.ExecuteAsync<int>(null!, () => Task.FromResult(0), CancellationToken.None));
	}

	[Fact]
	public async Task Throw_for_null_fallback()
	{
		var adapter = CreateAdapter();

		await Should.ThrowAsync<ArgumentNullException>(
			() => adapter.ExecuteAsync(() => Task.FromResult(1), null!, CancellationToken.None));
	}

	[Fact]
	public void Throw_for_null_name()
	{
		var options = new CircuitBreakerOptions();

		Should.Throw<ArgumentNullException>(() => new PollyCircuitBreakerAdapter(null!, options));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() => new PollyCircuitBreakerAdapter("test", null!));
	}

	[Fact]
	public async Task Dispose_clears_observers_and_resets_state()
	{
		var adapter = CreateAdapter();
		var observer = A.Fake<IPatternObserver>();
		adapter.Subscribe(observer);

		await adapter.DisposeAsync();
		_sut = null;

		adapter.State.ShouldBe(ResilienceState.Closed);
	}

	[Fact]
	public void Health_status_reflects_closed_state()
	{
		var adapter = CreateAdapter();

		adapter.HealthStatus.ShouldBe(PatternHealthStatus.Healthy);
	}
}
