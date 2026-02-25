// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="PollyCircuitBreakerWrapper"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class PollyCircuitBreakerWrapperShould : UnitTestBase, IAsyncDisposable
{
	private PollyCircuitBreakerAdapter? _adapter;
	private PollyCircuitBreakerWrapper? _wrapper;

	public async ValueTask DisposeAsync()
	{
		if (_wrapper != null)
		{
			await _wrapper.DisposeAsync();
			_wrapper = null;
		}

		if (_adapter != null)
		{
			await _adapter.DisposeAsync();
			_adapter = null;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (_wrapper != null)
			{
				_ = _wrapper.DisposeAsync().AsTask();
				_wrapper = null;
			}

			if (_adapter != null)
			{
				_ = _adapter.DisposeAsync().AsTask();
				_adapter = null;
			}
		}

		base.Dispose(disposing);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullAdapter_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new PollyCircuitBreakerWrapper(null!));
	}

	[Fact]
	public void Constructor_WithValidAdapter_CreatesInstance()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);

		// Act
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Assert
		_wrapper.ShouldNotBeNull();
	}

	#endregion

	#region Properties Tests

	[Fact]
	public void State_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Act & Assert
		_wrapper.State.ShouldBe(ResilienceState.Closed);
		_wrapper.State.ShouldBe(_adapter.State);
	}

	[Fact]
	public void HealthStatus_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Act & Assert
		_wrapper.HealthStatus.ShouldBe(PatternHealthStatus.Healthy);
		_wrapper.HealthStatus.ShouldBe(_adapter.HealthStatus);
	}

	[Fact]
	public void Configuration_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions
		{
			FailureThreshold = 10,
			SuccessThreshold = 5
		};
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Act
		var config = _wrapper.Configuration;

		// Assert
		config.ShouldNotBeNull();
		config["FailureThreshold"].ShouldBe(10);
		config["SuccessThreshold"].ShouldBe(5);
	}

	#endregion

	#region ExecuteAsync Tests

	[Fact]
	public async Task ExecuteAsync_WithSuccessfulOperation_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Act
		var result = await _wrapper.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_WithCancellationToken_PassesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);
		using var cts = new CancellationTokenSource();

		// Act
		var result = await _wrapper.ExecuteAsync(() => Task.FromResult(99), cts.Token);

		// Assert
		result.ShouldBe(99);
	}

	[Fact]
	public async Task ExecuteAsync_WithFallback_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Act
		var result = await _wrapper.ExecuteAsync(
			() => Task.FromResult(42),
			() => Task.FromResult(99),
			CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_WithFallbackAndCancellationToken_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);
		using var cts = new CancellationTokenSource();

		// Act
		var result = await _wrapper.ExecuteAsync(
			() => Task.FromResult(42),
			() => Task.FromResult(99),
			cts.Token);

		// Assert
		result.ShouldBe(42);
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void Reset_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Act & Assert - should not throw
		_wrapper.Reset();
		_wrapper.State.ShouldBe(ResilienceState.Closed);
	}

	#endregion

	#region GetMetrics Tests

	[Fact]
	public void GetMetrics_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Act
		var metrics = _wrapper.GetMetrics();

		// Assert
		metrics.ShouldNotBeNull();
		metrics.TotalOperations.ShouldBe(0);
	}

	[Fact]
	public async Task GetMetrics_AfterOperations_ReturnsUpdatedMetrics()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);
		_ = await _wrapper.ExecuteAsync(() => Task.FromResult(1), CancellationToken.None);

		// Act
		var metrics = _wrapper.GetMetrics();

		// Assert
		metrics.TotalOperations.ShouldBe(1);
		metrics.SuccessfulOperations.ShouldBe(1);
	}

	#endregion

	#region Lifecycle Tests

	[Fact]
	public async Task InitializeAsync_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);
		var config = new Dictionary<string, object>();

		// Act & Assert - should not throw
		await _wrapper.InitializeAsync(config, CancellationToken.None);
	}

	[Fact]
	public async Task StartAsync_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Act & Assert - should not throw
		await _wrapper.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StopAsync_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Act & Assert - should not throw
		await _wrapper.StopAsync(CancellationToken.None);
	}

	#endregion

	#region DisposeAsync Tests

	[Fact]
	public async Task DisposeAsync_DelegatesToAdapter()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Act & Assert - should not throw
		await _wrapper.DisposeAsync();
		_wrapper = null;
		_adapter = null; // Prevent double dispose
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var options = new CircuitBreakerOptions();
		_adapter = new PollyCircuitBreakerAdapter("test-breaker", options);
		_wrapper = new PollyCircuitBreakerWrapper(_adapter);

		// Act & Assert - should not throw
		await _wrapper.DisposeAsync();
		await _wrapper.DisposeAsync();
		await _wrapper.DisposeAsync();

		_wrapper = null;
		_adapter = null; // Prevent double dispose
	}

	#endregion
}
