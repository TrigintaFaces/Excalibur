// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="DistributedCircuitBreaker"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class DistributedCircuitBreakerShould : UnitTestBase, IAsyncDisposable
{
	private DistributedCircuitBreaker? _circuitBreaker;
	private IDistributedCache? _cache;
	private ILogger<DistributedCircuitBreaker>? _logger;

	public async ValueTask DisposeAsync()
	{
		if (_circuitBreaker != null)
		{
			await _circuitBreaker.DisposeAsync();
			_circuitBreaker = null;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && _circuitBreaker != null)
		{
			_ = _circuitBreaker.DisposeAsync().AsTask();
			_circuitBreaker = null;
		}
		base.Dispose(disposing);
	}

	private DistributedCircuitBreaker CreateCircuitBreaker(
		string name = "test-circuit",
		DistributedCircuitBreakerOptions? options = null)
	{
		_cache = A.Fake<IDistributedCache>();
		_logger = A.Fake<ILogger<DistributedCircuitBreaker>>();
		var optionsWrapper = MsOptions.Create(options ?? new DistributedCircuitBreakerOptions());

		_circuitBreaker = new DistributedCircuitBreaker(name, _cache, optionsWrapper, _logger);
		return _circuitBreaker;
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange
		var cache = A.Fake<IDistributedCache>();
		var options = MsOptions.Create(new DistributedCircuitBreakerOptions());
		var logger = A.Fake<ILogger<DistributedCircuitBreaker>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker(null!, cache, options, logger));
	}

	[Fact]
	public void Constructor_WithNullCache_ThrowsArgumentNullException()
	{
		// Arrange
		var options = MsOptions.Create(new DistributedCircuitBreakerOptions());
		var logger = A.Fake<ILogger<DistributedCircuitBreaker>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker("test", null!, options, logger));
	}

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		var cache = A.Fake<IDistributedCache>();
		var logger = A.Fake<ILogger<DistributedCircuitBreaker>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker("test", cache, null!, logger));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var cache = A.Fake<IDistributedCache>();
		var options = MsOptions.Create(new DistributedCircuitBreakerOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker("test", cache, options, null!));
	}

	[Fact]
	public void Constructor_WithValidArguments_CreatesInstance()
	{
		// Act
		var cb = CreateCircuitBreaker();

		// Assert
		cb.ShouldNotBeNull();
		cb.Name.ShouldBe("test-circuit");
	}

	#endregion

	#region Name Property Tests

	[Fact]
	public void Name_ReturnsConfiguredName()
	{
		// Arrange
		var cb = CreateCircuitBreaker("my-custom-circuit");

		// Assert
		cb.Name.ShouldBe("my-custom-circuit");
	}

	#endregion

	#region GetStateAsync Tests

	[Fact]
	public async Task GetStateAsync_WhenCacheIsEmpty_ReturnsClosedState()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		var state = await cb.GetStateAsync(CancellationToken.None);

		// Assert
		state.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public async Task GetStateAsync_WhenCacheHasState_ReturnsStoredState()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		var stateJson = JsonSerializer.Serialize(new { State = (int)CircuitState.Open, OpenedAt = DateTime.UtcNow, OpenUntil = DateTime.UtcNow.AddMinutes(1), InstanceId = "test" });
		var stateBytes = System.Text.Encoding.UTF8.GetBytes(stateJson);
		A.CallTo(() => _cache.GetAsync(A<string>.That.Contains("state"), A<CancellationToken>._))
			.Returns(stateBytes);

		// Act
		var state = await cb.GetStateAsync(CancellationToken.None);

		// Assert
		state.ShouldBe(CircuitState.Open);
	}

	[Fact]
	public async Task GetStateAsync_WhenCacheThrows_ReturnsLastKnownState()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Cache error"));

		// Act
		var state = await cb.GetStateAsync(CancellationToken.None);

		// Assert - Should return last known state (Closed initially) and not throw
		state.ShouldBe(CircuitState.Closed);
	}

	#endregion

	#region ExecuteAsync Tests

	[Fact]
	public async Task ExecuteAsync_WithNullOperation_ThrowsArgumentNullException()
	{
		// Arrange
		var cb = CreateCircuitBreaker();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			cb.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WhenCircuitClosed_ExecutesOperation()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		var result = await cb.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_WhenOperationSucceeds_RecordsSuccess()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		_ = await cb.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("metrics"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_WhenOperationFails_RecordsFailure()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await cb.ExecuteAsync<int>(() => throw new InvalidOperationException("Test error"), CancellationToken.None));

		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("metrics"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_WithCancellationToken_PassesToken()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		using var cts = new CancellationTokenSource();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		var result = await cb.ExecuteAsync(() => Task.FromResult(1), cts.Token);

		// Assert
		result.ShouldBe(1);
	}

	#endregion

	#region RecordSuccessAsync Tests

	[Fact]
	public async Task RecordSuccessAsync_UpdatesMetricsInCache()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		await cb.RecordSuccessAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("metrics"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RecordSuccessAsync_WhenCacheThrows_DoesNotPropagate()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Cache error"));

		// Act & Assert - Should not throw
		await cb.RecordSuccessAsync(CancellationToken.None);
	}

	#endregion

	#region RecordFailureAsync Tests

	[Fact]
	public async Task RecordFailureAsync_UpdatesMetricsInCache()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("Test"));

		// Assert
		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("metrics"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RecordFailureAsync_WithNullException_DoesNotThrow()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act & Assert - Should not throw
		await cb.RecordFailureAsync(CancellationToken.None);
	}

	[Fact]
	public async Task RecordFailureAsync_WhenCacheThrows_DoesNotPropagate()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Cache error"));

		// Act & Assert - Should not throw
		await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("Test"));
	}

	#endregion

	#region ResetAsync Tests

	[Fact]
	public async Task ResetAsync_RemovesCacheEntries()
	{
		// Arrange
		var cb = CreateCircuitBreaker();

		// Act
		await cb.ResetAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.RemoveAsync(A<string>.That.Contains("state"), A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => _cache.RemoveAsync(A<string>.That.Contains("metrics"), A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ResetAsync_WhenCacheThrows_DoesNotPropagate()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		A.CallTo(() => _cache.RemoveAsync(A<string>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Cache error"));

		// Act & Assert - Should not throw
		await cb.ResetAsync(CancellationToken.None);
	}

	#endregion

	#region Circuit State Transition Tests

	[Fact]
	public async Task ExecuteAsync_WhenCircuitOpenAndNotExpired_ThrowsBrokenCircuitException()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		var openState = new
		{
			State = (int)CircuitState.Open,
			OpenedAt = DateTime.UtcNow,
			OpenUntil = DateTime.UtcNow.AddMinutes(5), // Not expired
			InstanceId = "test"
		};
		var stateJson = JsonSerializer.Serialize(openState);
		var stateBytes = System.Text.Encoding.UTF8.GetBytes(stateJson);

		A.CallTo(() => _cache.GetAsync(A<string>.That.Contains("state"), A<CancellationToken>._))
			.Returns(stateBytes);

		// Act & Assert
		var ex = await Should.ThrowAsync<Polly.CircuitBreaker.BrokenCircuitException>(
			() => cb.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None));
		ex.Message.ShouldContain("test-circuit");
	}

	[Fact]
	public async Task ExecuteAsync_WhenCircuitOpenButExpired_TransitionsToHalfOpen()
	{
		// Arrange
		var cb = CreateCircuitBreaker();
		var openState = new
		{
			State = (int)CircuitState.Open,
			OpenedAt = DateTime.UtcNow.AddMinutes(-10),
			OpenUntil = DateTime.UtcNow.AddMinutes(-5), // Already expired
			InstanceId = "test"
		};
		var stateJson = JsonSerializer.Serialize(openState);
		var stateBytes = System.Text.Encoding.UTF8.GetBytes(stateJson);

		A.CallTo(() => _cache.GetAsync(A<string>.That.Contains("state"), A<CancellationToken>._))
			.Returns(stateBytes);

		// Act
		var result = await cb.ExecuteAsync(() => Task.FromResult(99), CancellationToken.None);

		// Assert - Operation should succeed and circuit should transition
		result.ShouldBe(99);
		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("state"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RecordFailureAsync_WhenConsecutiveFailuresExceedThreshold_OpensCircuit()
	{
		// Arrange
		var options = new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 2,
			FailureRatio = 0.9 // High ratio so consecutive failures trigger first
		};
		var cb = CreateCircuitBreaker(options: options);

		// Simulate existing metrics with one failure
		var existingMetrics = new
		{
			SuccessCount = 0L,
			FailureCount = 1L,
			ConsecutiveFailures = 1L,
			ConsecutiveSuccesses = 0L,
			LastFailure = DateTime.UtcNow.AddSeconds(-1),
			LastFailureReason = "Previous error"
		};
		var metricsJson = JsonSerializer.Serialize(existingMetrics);
		var metricsBytes = System.Text.Encoding.UTF8.GetBytes(metricsJson);

		A.CallTo(() => _cache.GetAsync(A<string>.That.Contains("metrics"), A<CancellationToken>._))
			.Returns(metricsBytes);

		// Act - This should exceed threshold and open circuit
		await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("Second failure"));

		// Assert - State should be set to open
		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("state"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RecordFailureAsync_WhenFailureRateExceedsThreshold_OpensCircuit()
	{
		// Arrange
		var options = new DistributedCircuitBreakerOptions
		{
			FailureRatio = 0.5, // 50% failure rate
			ConsecutiveFailureThreshold = 100 // High so we trigger on ratio first
		};
		var cb = CreateCircuitBreaker(options: options);

		// Simulate existing metrics with high failure rate
		var existingMetrics = new
		{
			SuccessCount = 1L, // 1 success
			FailureCount = 1L, // 1 failure - adding 1 more = 2 failures / 3 total > 50%
			ConsecutiveFailures = 0L,
			ConsecutiveSuccesses = 0L
		};
		var metricsJson = JsonSerializer.Serialize(existingMetrics);
		var metricsBytes = System.Text.Encoding.UTF8.GetBytes(metricsJson);

		A.CallTo(() => _cache.GetAsync(A<string>.That.Contains("metrics"), A<CancellationToken>._))
			.Returns(metricsBytes);

		// Act
		await cb.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("Another failure"));

		// Assert - State should be set to open due to failure rate
		A.CallTo(() => _cache.SetAsync(A<string>.That.Contains("state"), A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	#endregion

	#region DisposeAsync Tests

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var cb = CreateCircuitBreaker();

		// Act & Assert - should not throw
		await cb.DisposeAsync();
		await cb.DisposeAsync();

		_circuitBreaker = null; // Prevent double dispose in test cleanup
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIDistributedCircuitBreaker()
	{
		// Arrange
		var cb = CreateCircuitBreaker();

		// Assert
		cb.ShouldBeAssignableTo<IDistributedCircuitBreaker>();
	}

	[Fact]
	public void ImplementsIAsyncDisposable()
	{
		// Arrange
		var cb = CreateCircuitBreaker();

		// Assert
		cb.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion
}
