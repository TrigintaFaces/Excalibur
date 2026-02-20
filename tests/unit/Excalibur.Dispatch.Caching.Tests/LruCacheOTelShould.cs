// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Caching.Diagnostics;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests verifying OTel metric emission from <see cref="LruCache{TKey,TValue}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class LruCacheOTelShould : IDisposable
{
	private readonly TestMeterFactory _meterFactory = new();
	private readonly MeterListener _listener = new();
	private long _hitCount;
	private long _missCount;
	private long _evictionCount;
	private long _expirationCount;

	public LruCacheOTelShould()
	{
		_listener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Meter.Name == DispatchCachingTelemetryConstants.MeterName)
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		_listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
		{
			switch (instrument.Name)
			{
				case "dispatch.cache.lru.hits":
					Interlocked.Add(ref _hitCount, measurement);
					break;
				case "dispatch.cache.lru.misses":
					Interlocked.Add(ref _missCount, measurement);
					break;
				case "dispatch.cache.lru.evictions":
					Interlocked.Add(ref _evictionCount, measurement);
					break;
				case "dispatch.cache.lru.expirations":
					Interlocked.Add(ref _expirationCount, measurement);
					break;
			}
		});

		_listener.Start();
	}

	public void Dispose()
	{
		_listener.Dispose();
		_meterFactory.Dispose();
	}

	[Fact]
	public void RecordHitCounter_WhenCacheHit()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10, _meterFactory);
		cache.Set("key1", 42);

		// Act
		cache.TryGetValue("key1", out _);
		_listener.RecordObservableInstruments();

		// Assert
		_hitCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void RecordMissCounter_WhenCacheMiss()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10, _meterFactory);

		// Act
		cache.TryGetValue("nonexistent", out _);
		_listener.RecordObservableInstruments();

		// Assert
		_missCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void RecordEvictionCounter_WhenCapacityExceeded()
	{
		// Arrange
		using var cache = new LruCache<string, int>(2, _meterFactory);
		cache.Set("key1", 1);
		cache.Set("key2", 2);

		// Act — this should evict key1
		cache.Set("key3", 3);
		_listener.RecordObservableInstruments();

		// Assert
		_evictionCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void RecordExpirationCounter_WhenItemExpires()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10, _meterFactory, defaultTtl: TimeSpan.FromMilliseconds(1));
		cache.Set("key1", 42);

		// Act — wait for expiry and trigger a lookup
		Thread.Sleep(50);
		cache.TryGetValue("key1", out _);
		_listener.RecordObservableInstruments();

		// Assert
		_expirationCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void RecordMultipleMetrics_DuringNormalUsage()
	{
		// Arrange
		using var cache = new LruCache<string, int>(3, _meterFactory);

		// Act — produce hits, misses, and evictions
		cache.Set("a", 1);
		cache.Set("b", 2);
		cache.Set("c", 3);
		cache.TryGetValue("a", out _); // hit
		cache.TryGetValue("b", out _); // hit
		cache.TryGetValue("missing", out _); // miss
		cache.Set("d", 4); // evict oldest (c, since a and b were accessed)
		_listener.RecordObservableInstruments();

		// Assert
		_hitCount.ShouldBeGreaterThanOrEqualTo(2);
		_missCount.ShouldBeGreaterThanOrEqualTo(1);
		_evictionCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void CreateWithMeterFactory_UsesDispatchCachingMeterName()
	{
		// Arrange & Act
		using var cache = new LruCache<string, int>(10, _meterFactory);
		cache.Set("key", 1);
		cache.TryGetValue("key", out _);
		_listener.RecordObservableInstruments();

		// Assert — hits were recorded via the correct meter
		_hitCount.ShouldBeGreaterThanOrEqualTo(1);
	}
}
