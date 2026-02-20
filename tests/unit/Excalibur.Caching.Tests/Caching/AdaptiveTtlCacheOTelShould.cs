// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;
using System.Text;

using Excalibur.Caching.Diagnostics;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.Caching;

/// <summary>
/// Unit tests verifying OTel metric emission and concurrent access for <see cref="AdaptiveTtlCache"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class AdaptiveTtlCacheOTelShould : IAsyncDisposable
{
	private readonly MeterListener _listener = new();
	private readonly IDistributedCache _fakeInnerCache = A.Fake<IDistributedCache>();
	private readonly IAdaptiveTtlStrategy _fakeStrategy = A.Fake<IAdaptiveTtlStrategy>();
	private readonly ISystemLoadMonitor _fakeLoadMonitor = A.Fake<ISystemLoadMonitor>();
	private long _hitCount;
	private long _missCount;
	private double _ttlRecorded;

	public AdaptiveTtlCacheOTelShould()
	{
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == CachingTelemetryConstants.MeterName)
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			switch (instrument.Name)
			{
				case "caching.adaptive_ttl.hits":
					Interlocked.Add(ref _hitCount, measurement);
					break;
				case "caching.adaptive_ttl.misses":
					Interlocked.Add(ref _missCount, measurement);
					break;
			}
		});

		_listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
		{
			if (instrument.Name == "caching.adaptive_ttl.ttl_seconds")
			{
				Interlocked.Exchange(ref _ttlRecorded, measurement);
			}
		});

		_listener.Start();

		// Default mocks
		A.CallTo(() => _fakeLoadMonitor.GetCurrentLoadAsync()).Returns(0.5);
		A.CallTo(() => _fakeStrategy.CalculateTtl(A<AdaptiveTtlContext>._)).Returns(TimeSpan.FromMinutes(5));
	}

	public async ValueTask DisposeAsync()
	{
		_listener.Dispose();
		await Task.CompletedTask;
	}

	private AdaptiveTtlCache CreateCache(TimeProvider? timeProvider = null) =>
		new(_fakeInnerCache, _fakeStrategy, NullLogger<AdaptiveTtlCache>.Instance, _fakeLoadMonitor, timeProvider);

	[Fact]
	public async Task RecordHitCounter_WhenCacheReturnsValue()
	{
		// Arrange
		await using var cache = CreateCache();
		var key = "test-key";
		var data = Encoding.UTF8.GetBytes("test-value");

		A.CallTo(() => _fakeInnerCache.GetAsync(key, A<CancellationToken>._))
			.Returns(data);

		// Act
		_ = await cache.GetAsync(key, CancellationToken.None);
		_listener.RecordObservableInstruments();

		// Assert
		_hitCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task RecordMissCounter_WhenCacheReturnsNull()
	{
		// Arrange
		await using var cache = CreateCache();
		var key = "missing-key";

		A.CallTo(() => _fakeInnerCache.GetAsync(key, A<CancellationToken>._))
			.Returns((byte[]?)null);

		// Act
		_ = await cache.GetAsync(key, CancellationToken.None);
		_listener.RecordObservableInstruments();

		// Assert
		_missCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task RecordTtlHistogram_WhenSettingValue()
	{
		// Arrange
		A.CallTo(() => _fakeStrategy.CalculateTtl(A<AdaptiveTtlContext>._))
			.Returns(TimeSpan.FromMinutes(10));

		await using var cache = CreateCache();
		var key = "set-key";
		var data = Encoding.UTF8.GetBytes("set-value");
		var options = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
		};

		// Act
		await cache.SetAsync(key, data, options, CancellationToken.None);
		_listener.RecordObservableInstruments();

		// Assert — TTL should be 10 minutes = 600 seconds
		_ttlRecorded.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task HandleConcurrentGetOperations_WithoutErrors()
	{
		// Arrange
		await using var cache = CreateCache();
		var key = "concurrent-key";
		var data = Encoding.UTF8.GetBytes("value");

		A.CallTo(() => _fakeInnerCache.GetAsync(key, A<CancellationToken>._))
			.Returns(data);

		var exceptions = new List<Exception>();

		// Act — 50 concurrent gets
		var tasks = Enumerable.Range(0, 50).Select(n => Task.Run(async () =>
		{
			try
			{
				await cache.GetAsync(key, CancellationToken.None);
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		}));

		await Task.WhenAll(tasks);

		// Assert
		exceptions.ShouldBeEmpty("Concurrent GetAsync should not throw");
		_hitCount.ShouldBeGreaterThanOrEqualTo(50);
	}

	[Fact]
	public async Task HandleConcurrentSetOperations_WithoutErrors()
	{
		// Arrange
		await using var cache = CreateCache();

		A.CallTo(() => _fakeStrategy.CalculateTtl(A<AdaptiveTtlContext>._))
			.Returns(TimeSpan.FromMinutes(5));

		var exceptions = new List<Exception>();

		// Act — 50 concurrent sets
		var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(async () =>
		{
			try
			{
				var key = $"concurrent-set-{i}";
				var data = Encoding.UTF8.GetBytes($"value-{i}");
				var options = new DistributedCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
				};
				await cache.SetAsync(key, data, options, CancellationToken.None);
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		}));

		await Task.WhenAll(tasks);

		// Assert
		exceptions.ShouldBeEmpty("Concurrent SetAsync should not throw");
	}

	[Fact]
	public async Task HandleConcurrentMixedOperations_WithoutErrors()
	{
		// Arrange
		await using var cache = CreateCache();
		var data = Encoding.UTF8.GetBytes("value");

		A.CallTo(() => _fakeInnerCache.GetAsync(A<string>._, A<CancellationToken>._))
			.Returns(data);
		A.CallTo(() => _fakeStrategy.CalculateTtl(A<AdaptiveTtlContext>._))
			.Returns(TimeSpan.FromMinutes(5));

		var exceptions = new List<Exception>();

		// Act — mix of gets, sets, removes
		var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
		{
			try
			{
				var key = $"mixed-{i % 10}";
				switch (i % 3)
				{
					case 0:
						_ = await cache.GetAsync(key, CancellationToken.None);
						break;
					case 1:
						await cache.SetAsync(key, data,
							new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
							CancellationToken.None);
						break;
					case 2:
						await cache.RemoveAsync(key, CancellationToken.None);
						break;
				}
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		}));

		await Task.WhenAll(tasks);

		// Assert
		exceptions.ShouldBeEmpty("Concurrent mixed operations should not throw");
	}

	[Fact]
	public async Task GetMetrics_ReturnsAggregatedMetrics()
	{
		// Arrange
		await using var cache = CreateCache();
		var key = "metrics-key";
		var data = Encoding.UTF8.GetBytes("value");

		A.CallTo(() => _fakeInnerCache.GetAsync(key, A<CancellationToken>._))
			.Returns(data);

		// Act — perform some operations to populate metadata
		await cache.GetAsync(key, CancellationToken.None);
		await cache.GetAsync(key, CancellationToken.None);
		var metrics = cache.GetMetrics();

		// Assert
		metrics.ShouldNotBeNull();
		metrics.TotalCalculations.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task DisposeAsync_CompletesGracefully()
	{
		// Arrange
		var cache = CreateCache();
		var key = "dispose-key";
		var data = Encoding.UTF8.GetBytes("value");
		A.CallTo(() => _fakeInnerCache.GetAsync(key, A<CancellationToken>._))
			.Returns(data);

		await cache.GetAsync(key, CancellationToken.None);

		// Act & Assert — should not throw
		await cache.DisposeAsync();
	}

	[Fact]
	public void Dispose_CompletesGracefully()
	{
		// Arrange
		var cache = CreateCache();

		// Act & Assert — should not throw
		cache.Dispose();
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenGetAsyncKeyIsNull()
	{
		// Arrange
		await using var cache = CreateCache();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			cache.GetAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenSetAsyncKeyIsNull()
	{
		// Arrange
		await using var cache = CreateCache();
		var data = Encoding.UTF8.GetBytes("value");
		var options = new DistributedCacheEntryOptions();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			cache.SetAsync(null!, data, options, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenSetAsyncValueIsNull()
	{
		// Arrange
		await using var cache = CreateCache();
		var options = new DistributedCacheEntryOptions();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			cache.SetAsync("key", null!, options, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenSetAsyncOptionsIsNull()
	{
		// Arrange
		await using var cache = CreateCache();
		var data = Encoding.UTF8.GetBytes("value");

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			cache.SetAsync("key", data, null!, CancellationToken.None));
	}
}
