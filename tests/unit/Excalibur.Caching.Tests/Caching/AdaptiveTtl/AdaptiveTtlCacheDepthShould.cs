// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Caching.Tests.AdaptiveTtl;

/// <summary>
/// Depth coverage tests for <see cref="AdaptiveTtlCache"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AdaptiveTtlCacheDepthShould : IAsyncDisposable
{
	private readonly IDistributedCache _innerCache;
	private readonly IAdaptiveTtlStrategy _strategy;
	private readonly ISystemLoadMonitor _loadMonitor;
	private readonly AdaptiveTtlCache _sut;

	public AdaptiveTtlCacheDepthShould()
	{
		_innerCache = A.Fake<IDistributedCache>();
		_strategy = A.Fake<IAdaptiveTtlStrategy>();
		_loadMonitor = A.Fake<ISystemLoadMonitor>();

		A.CallTo(() => _strategy.CalculateTtl(A<AdaptiveTtlContext>._))
			.Returns(TimeSpan.FromMinutes(10));
		A.CallTo(() => _loadMonitor.GetCurrentLoadAsync())
			.Returns(Task.FromResult(0.5));

		_sut = new AdaptiveTtlCache(
			_innerCache,
			_strategy,
			NullLogger<AdaptiveTtlCache>.Instance,
			_loadMonitor);
	}

	[Fact]
	public void ThrowWhenInnerCacheIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new AdaptiveTtlCache(
			null!,
			_strategy,
			NullLogger<AdaptiveTtlCache>.Instance,
			_loadMonitor));
	}

	[Fact]
	public void ThrowWhenStrategyIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new AdaptiveTtlCache(
			_innerCache,
			null!,
			NullLogger<AdaptiveTtlCache>.Instance,
			_loadMonitor));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new AdaptiveTtlCache(
			_innerCache,
			_strategy,
			null!,
			_loadMonitor));
	}

	[Fact]
	public void ThrowWhenLoadMonitorIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new AdaptiveTtlCache(
			_innerCache,
			_strategy,
			NullLogger<AdaptiveTtlCache>.Instance,
			null!));
	}

	[Fact]
	public async Task GetAsyncDelegatesToInnerCache()
	{
		// Arrange
		var expected = Encoding.UTF8.GetBytes("value");
		A.CallTo(() => _innerCache.GetAsync("key", A<CancellationToken>._))
			.Returns(Task.FromResult<byte[]?>(expected));

		// Act
		var result = await _sut.GetAsync("key", CancellationToken.None);

		// Assert
		result.ShouldBe(expected);
		A.CallTo(() => _innerCache.GetAsync("key", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetAsyncReturnNullOnMiss()
	{
		// Arrange
		A.CallTo(() => _innerCache.GetAsync("miss-key", A<CancellationToken>._))
			.Returns(Task.FromResult<byte[]?>(null));

		// Act
		var result = await _sut.GetAsync("miss-key", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetAsyncThrowOnNullOrEmptyKey()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() => _sut.GetAsync(null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentNullException>(() => _sut.GetAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsyncPropagatesExceptionFromInnerCache()
	{
		// Arrange
		A.CallTo(() => _innerCache.GetAsync("error-key", A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("cache error"));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.GetAsync("error-key", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsyncUpdatesFeedbackOnStrategy()
	{
		// Arrange
		A.CallTo(() => _innerCache.GetAsync("feedback-key", A<CancellationToken>._))
			.Returns(Task.FromResult<byte[]?>(new byte[] { 1 }));

		// Act
		await _sut.GetAsync("feedback-key", CancellationToken.None);

		// Assert
		A.CallTo(() => _strategy.UpdateStrategy(A<CachePerformanceFeedback>.That.Matches(f =>
			f.Key == "feedback-key" && f.IsHit)))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SetAsyncDelegatesToInnerCacheWithAdaptiveTtl()
	{
		// Arrange
		var data = Encoding.UTF8.GetBytes("test-value");
		var options = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
		};

		// Act
		await _sut.SetAsync("set-key", data, options, CancellationToken.None);

		// Assert
		A.CallTo(() => _innerCache.SetAsync("set-key", data, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SetAsyncThrowOnNullOrEmptyKey()
	{
		// Arrange
		var data = new byte[] { 1 };
		var options = new DistributedCacheEntryOptions();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.SetAsync(null!, data, options, CancellationToken.None));
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.SetAsync("", data, options, CancellationToken.None));
	}

	[Fact]
	public async Task SetAsyncThrowOnNullValue()
	{
		// Arrange
		var options = new DistributedCacheEntryOptions();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.SetAsync("key", null!, options, CancellationToken.None));
	}

	[Fact]
	public async Task SetAsyncThrowOnNullOptions()
	{
		// Arrange
		var data = new byte[] { 1 };

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.SetAsync("key", data, null!, CancellationToken.None));
	}

	[Fact]
	public async Task SetAsyncUseShorterOfOriginalAndAdaptiveTtl()
	{
		// Arrange
		var data = new byte[] { 1 };
		var shortOriginalTtl = TimeSpan.FromMinutes(2);
		var options = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = shortOriginalTtl,
		};

		// Strategy returns 10 minutes, but original is 2 minutes, so 2 should win
		A.CallTo(() => _strategy.CalculateTtl(A<AdaptiveTtlContext>._))
			.Returns(TimeSpan.FromMinutes(10));

		// Act
		await _sut.SetAsync("key", data, options, CancellationToken.None);

		// Assert
		A.CallTo(() => _innerCache.SetAsync("key", data,
			A<DistributedCacheEntryOptions>.That.Matches(o =>
				o.AbsoluteExpirationRelativeToNow == shortOriginalTtl),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RefreshAsyncDelegatesToInnerCache()
	{
		// Act
		await _sut.RefreshAsync("refresh-key", CancellationToken.None);

		// Assert
		A.CallTo(() => _innerCache.RefreshAsync("refresh-key", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RefreshAsyncThrowOnNullOrEmptyKey()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.RefreshAsync(null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.RefreshAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task RemoveAsyncDelegatesToInnerCache()
	{
		// Act
		await _sut.RemoveAsync("remove-key", CancellationToken.None);

		// Assert
		A.CallTo(() => _innerCache.RemoveAsync("remove-key", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RemoveAsyncThrowOnNullOrEmptyKey()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.RemoveAsync(null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.RemoveAsync("", CancellationToken.None));
	}

	[Fact]
	public void GetMetricsReturnValidMetrics()
	{
		// Act
		var metrics = _sut.GetMetrics();

		// Assert
		metrics.ShouldNotBeNull();
		metrics.TotalCalculations.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public async Task GetMetricsReturnMetricsAfterAccess()
	{
		// Arrange
		A.CallTo(() => _innerCache.GetAsync("metrics-key", A<CancellationToken>._))
			.Returns(Task.FromResult<byte[]?>(new byte[] { 1 }));

		await _sut.GetAsync("metrics-key", CancellationToken.None);

		// Act
		var metrics = _sut.GetMetrics();

		// Assert
		metrics.ShouldNotBeNull();
		metrics.TotalCalculations.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void DisposeDoesNotThrowOnSecondCall()
	{
		// Act
		_sut.Dispose();
		_sut.Dispose(); // Should not throw

		// Assert - no exception means success
	}

	[Fact]
	public async Task DisposeAsyncDoesNotThrowOnSecondCall()
	{
		// Act
		await _sut.DisposeAsync();
		await _sut.DisposeAsync(); // Should not throw

		// Assert - no exception means success
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
	}
}
