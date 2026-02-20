// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;

using MsOptions = Microsoft.Extensions.Options.Options;

using CircuitState = Excalibur.Dispatch.Resilience.CircuitState;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Additional depth tests for <see cref="DistributedCircuitBreaker"/> covering
/// state transitions, metrics recording, DisposeAsync, and error handling paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class DistributedCircuitBreakerDepthShould : IAsyncDisposable
{
	private readonly IDistributedCache _cache = A.Fake<IDistributedCache>();
	private readonly ILogger<DistributedCircuitBreaker> _logger = A.Fake<ILogger<DistributedCircuitBreaker>>();
	private DistributedCircuitBreaker? _breaker;

	private DistributedCircuitBreaker CreateBreaker(DistributedCircuitBreakerOptions? options = null)
	{
		var opts = MsOptions.Create(options ?? new DistributedCircuitBreakerOptions
		{
			SyncInterval = TimeSpan.FromHours(1), // Disable auto-sync during tests
			BreakDuration = TimeSpan.FromSeconds(30),
			FailureRatio = 0.5,
			MinimumThroughput = 5,
			ConsecutiveFailureThreshold = 3,
			SuccessThresholdToClose = 2,
		});
		_breaker = new DistributedCircuitBreaker("test-breaker", _cache, opts, _logger);
		return _breaker;
	}

	public async ValueTask DisposeAsync()
	{
		if (_breaker != null)
		{
			await _breaker.DisposeAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public void Constructor_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange
		var opts = MsOptions.Create(new DistributedCircuitBreakerOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker(null!, _cache, opts, _logger));
	}

	[Fact]
	public void Constructor_WithNullCache_ThrowsArgumentNullException()
	{
		// Arrange
		var opts = MsOptions.Create(new DistributedCircuitBreakerOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker("test", null!, opts, _logger));
	}

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker("test", _cache, null!, _logger));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var opts = MsOptions.Create(new DistributedCircuitBreakerOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new DistributedCircuitBreaker("test", _cache, opts, null!));
	}

	[Fact]
	public void Name_ReturnsConfiguredName()
	{
		// Arrange
		var breaker = CreateBreaker();

		// Assert
		breaker.Name.ShouldBe("test-breaker");
	}

	[Fact]
	public async Task GetStateAsync_WhenCacheEmpty_ReturnsClosed()
	{
		// Arrange
		var breaker = CreateBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		var state = await breaker.GetStateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		state.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public async Task GetStateAsync_WhenCacheThrows_ReturnsLastKnownState()
	{
		// Arrange
		var breaker = CreateBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Cache failure"));

		// Act
		var state = await breaker.GetStateAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — initial last-known state is Closed
		state.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public async Task ExecuteAsync_WithNullOperation_ThrowsArgumentNullException()
	{
		// Arrange
		var breaker = CreateBreaker();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => breaker.ExecuteAsync<int>(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsync_ClosedState_ExecutesAndReturns()
	{
		// Arrange
		var breaker = CreateBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		var result = await breaker.ExecuteAsync(
			() => Task.FromResult(42),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task RecordSuccessAsync_IncrementsCacheMetrics()
	{
		// Arrange
		var breaker = CreateBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		await breaker.RecordSuccessAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — should have written metrics to cache
		A.CallTo(() => _cache.SetAsync(
			A<string>.That.Contains("metrics"),
			A<byte[]>._,
			A<DistributedCacheEntryOptions>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordFailureAsync_IncrementsCacheMetrics()
	{
		// Arrange
		var breaker = CreateBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		await breaker.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("Test error")).ConfigureAwait(false);

		// Assert — should have written metrics to cache
		A.CallTo(() => _cache.SetAsync(
			A<string>.That.Contains("metrics"),
			A<byte[]>._,
			A<DistributedCacheEntryOptions>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordFailureAsync_WithNullException_StillRecords()
	{
		// Arrange
		var breaker = CreateBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		await breaker.RecordFailureAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _cache.SetAsync(
			A<string>.That.Contains("metrics"),
			A<byte[]>._,
			A<DistributedCacheEntryOptions>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordFailureAsync_WhenCacheThrows_SwallowsException()
	{
		// Arrange
		var breaker = CreateBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Cache failure"));

		// Act — should not throw
		await breaker.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("Error")).ConfigureAwait(false);
	}

	[Fact]
	public async Task RecordSuccessAsync_WhenCacheThrows_SwallowsException()
	{
		// Arrange
		var breaker = CreateBreaker();
		A.CallTo(() => _cache.GetAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Cache failure"));

		// Act — should not throw
		await breaker.RecordSuccessAsync(CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task ResetAsync_ClearsCacheState()
	{
		// Arrange
		var breaker = CreateBreaker();

		// Act
		await breaker.ResetAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert — both state and metrics keys should be removed
		A.CallTo(() => _cache.RemoveAsync(
			A<string>.That.Contains("state"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _cache.RemoveAsync(
			A<string>.That.Contains("metrics"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ResetAsync_WhenCacheThrows_SwallowsException()
	{
		// Arrange
		var breaker = CreateBreaker();
		A.CallTo(() => _cache.RemoveAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Cache failure"));

		// Act — should not throw
		await breaker.ResetAsync(CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var breaker = CreateBreaker();

		// Act & Assert — should not throw
		await breaker.DisposeAsync().ConfigureAwait(false);
		await breaker.DisposeAsync().ConfigureAwait(false);

		// Prevent double-dispose in test cleanup
		_breaker = null;
	}
}
