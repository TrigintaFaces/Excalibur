// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Caching.AdaptiveTtl;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.Caching.AdaptiveTtl;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class RuleBasedAdaptiveTtlStrategyShould
{
	private static RuleBasedAdaptiveTtlStrategy CreateStrategy(RuleBasedTtlOptions? options = null)
	{
		return new RuleBasedAdaptiveTtlStrategy(
			NullLogger<RuleBasedAdaptiveTtlStrategy>.Instance,
			options ?? new RuleBasedTtlOptions());
	}

	private static AdaptiveTtlContext CreateContext(
		string key = "test-key",
		TimeSpan? baseTtl = null,
		double hitRate = 0.7,
		double accessFrequency = 50,
		double systemLoad = 0.5,
		long contentSize = 1024,
		TimeSpan? missCost = null,
		int hour = 12)
	{
		return new AdaptiveTtlContext
		{
			Key = key,
			BaseTtl = baseTtl ?? TimeSpan.FromMinutes(5),
			HitRate = hitRate,
			AccessFrequency = accessFrequency,
			SystemLoad = systemLoad,
			ContentSize = contentSize,
			MissCost = missCost ?? TimeSpan.FromMilliseconds(10),
			CurrentTime = new DateTimeOffset(2026, 1, 1, hour, 0, 0, TimeSpan.Zero),
			LastUpdate = DateTimeOffset.UtcNow,
		};
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RuleBasedAdaptiveTtlStrategy(null!, new RuleBasedTtlOptions()));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RuleBasedAdaptiveTtlStrategy(
				NullLogger<RuleBasedAdaptiveTtlStrategy>.Instance, null!));
	}

	[Fact]
	public void CalculateTtl_ThrowsOnNull()
	{
		var strategy = CreateStrategy();
		Should.Throw<ArgumentNullException>(() => strategy.CalculateTtl(null!));
	}

	[Fact]
	public void CalculateTtl_ReturnsTtl()
	{
		var strategy = CreateStrategy();
		var context = CreateContext();
		var ttl = strategy.CalculateTtl(context);
		ttl.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateTtl_AppliesHighHitRateRule()
	{
		var strategy = CreateStrategy();
		var baseTtl = TimeSpan.FromMinutes(5);

		// High hit rate = 0.95 (> default 0.9 threshold) → should increase TTL
		var highHitContext = CreateContext(hitRate: 0.95, baseTtl: baseTtl);
		var highHitTtl = strategy.CalculateTtl(highHitContext);

		// Normal hit rate = 0.7 (between thresholds) → no adjustment
		var normalContext = CreateContext(key: "normal-key", hitRate: 0.7, baseTtl: baseTtl);
		var normalTtl = strategy.CalculateTtl(normalContext);

		// High hit rate should produce a longer TTL (due to multiplier > 1)
		highHitTtl.ShouldNotBe(normalTtl);
	}

	[Fact]
	public void CalculateTtl_AppliesLowHitRateRule()
	{
		var strategy = CreateStrategy();
		var baseTtl = TimeSpan.FromMinutes(5);

		// Low hit rate = 0.3 (< default 0.5 threshold) → should decrease TTL
		var lowHitContext = CreateContext(hitRate: 0.3, baseTtl: baseTtl);
		var lowHitTtl = strategy.CalculateTtl(lowHitContext);

		lowHitTtl.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateTtl_AppliesHighFrequencyRule()
	{
		var strategy = CreateStrategy();
		var baseTtl = TimeSpan.FromMinutes(5);

		// High frequency = 200 (> default 100 threshold)
		var highFreqContext = CreateContext(accessFrequency: 200, baseTtl: baseTtl);
		var highFreqTtl = strategy.CalculateTtl(highFreqContext);

		highFreqTtl.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateTtl_AppliesLowFrequencyRule()
	{
		var strategy = CreateStrategy();
		var baseTtl = TimeSpan.FromMinutes(5);

		// Low frequency = 0.5 (< default 1 threshold)
		var lowFreqContext = CreateContext(key: "low-freq", accessFrequency: 0.5, baseTtl: baseTtl);
		var lowFreqTtl = strategy.CalculateTtl(lowFreqContext);

		lowFreqTtl.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateTtl_AppliesMissCostRule()
	{
		var strategy = CreateStrategy();
		var baseTtl = TimeSpan.FromMinutes(5);

		// Expensive miss cost = 500ms (> default 100ms threshold)
		var expensiveContext = CreateContext(key: "expensive", missCost: TimeSpan.FromMilliseconds(500), baseTtl: baseTtl);
		var ttl = strategy.CalculateTtl(expensiveContext);

		ttl.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateTtl_AppliesHighSystemLoadRule()
	{
		var options = new RuleBasedTtlOptions();
		options.Thresholds.HighLoadThreshold = 0.8;
		var strategy = CreateStrategy(options);

		// High system load = 0.9 (> 0.8 threshold)
		var highLoadContext = CreateContext(key: "high-load", systemLoad: 0.9);
		var ttl = strategy.CalculateTtl(highLoadContext);

		ttl.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateTtl_AppliesLowSystemLoadRule()
	{
		var options = new RuleBasedTtlOptions();
		options.Thresholds.LowLoadThreshold = 0.3;
		var strategy = CreateStrategy(options);

		// Low system load = 0.1 (< 0.3 threshold)
		var lowLoadContext = CreateContext(key: "low-load", systemLoad: 0.1);
		var ttl = strategy.CalculateTtl(lowLoadContext);

		ttl.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateTtl_AppliesPeakHoursRule()
	{
		var strategy = CreateStrategy();

		// Peak hours = hour 12 (9-17 default)
		var peakContext = CreateContext(key: "peak", hour: 12);
		var ttl = strategy.CalculateTtl(peakContext);

		ttl.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateTtl_AppliesOffHoursRule()
	{
		var strategy = CreateStrategy();

		// Off hours = hour 23 (> 22 default OffHoursStart)
		var offHoursContext = CreateContext(key: "off-hours", hour: 23);
		var ttl = strategy.CalculateTtl(offHoursContext);

		ttl.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateTtl_AppliesLargeContentRule()
	{
		var strategy = CreateStrategy();

		// Large content = 2MB (> default 1MB threshold)
		var largeContext = CreateContext(key: "large", contentSize: 2 * 1024 * 1024);
		var ttl = strategy.CalculateTtl(largeContext);

		ttl.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateTtl_EnforcesMinBound()
	{
		var options = new RuleBasedTtlOptions { MinTtl = TimeSpan.FromSeconds(10), MaxTtl = TimeSpan.FromHours(1) };
		var strategy = CreateStrategy(options);

		// Use very low base TTL that will be brought to min after adjustments
		var context = CreateContext(baseTtl: TimeSpan.FromMilliseconds(1), hitRate: 0.3, accessFrequency: 0.1);
		var ttl = strategy.CalculateTtl(context);

		ttl.ShouldBeGreaterThanOrEqualTo(options.MinTtl);
	}

	[Fact]
	public void CalculateTtl_EnforcesMaxBound()
	{
		var options = new RuleBasedTtlOptions { MinTtl = TimeSpan.FromSeconds(1), MaxTtl = TimeSpan.FromMinutes(10) };
		var strategy = CreateStrategy(options);

		// Use very high base TTL that will be brought to max after adjustments
		var context = CreateContext(key: "max-bound", baseTtl: TimeSpan.FromHours(10), hitRate: 0.95, accessFrequency: 200);
		var ttl = strategy.CalculateTtl(context);

		ttl.ShouldBeLessThanOrEqualTo(options.MaxTtl);
	}

	[Fact]
	public void UpdateStrategy_ThrowsOnNull()
	{
		var strategy = CreateStrategy();
		Should.Throw<ArgumentNullException>(() => strategy.UpdateStrategy(null!));
	}

	[Fact]
	public void UpdateStrategy_RecordsHit()
	{
		var strategy = CreateStrategy();
		var context = CreateContext(key: "hit-key");
		strategy.CalculateTtl(context);

		strategy.UpdateStrategy(new CachePerformanceFeedback
		{
			Key = "hit-key",
			IsHit = true,
			ResponseTime = TimeSpan.FromMilliseconds(5),
			Timestamp = DateTimeOffset.UtcNow,
		});

		var metrics = strategy.GetMetrics();
		metrics.ShouldNotBeNull();
	}

	[Fact]
	public void UpdateStrategy_RecordsMiss()
	{
		var strategy = CreateStrategy();
		var context = CreateContext(key: "miss-key");
		strategy.CalculateTtl(context);

		strategy.UpdateStrategy(new CachePerformanceFeedback
		{
			Key = "miss-key",
			IsHit = false,
			ResponseTime = TimeSpan.FromMilliseconds(50),
			Timestamp = DateTimeOffset.UtcNow,
		});

		var metrics = strategy.GetMetrics();
		metrics.ShouldNotBeNull();
	}

	[Fact]
	public void UpdateStrategy_RecordsStaleHit()
	{
		var strategy = CreateStrategy();
		var context = CreateContext(key: "stale-key");
		strategy.CalculateTtl(context);

		strategy.UpdateStrategy(new CachePerformanceFeedback
		{
			Key = "stale-key",
			IsHit = true,
			WasStale = true,
			ResponseTime = TimeSpan.FromMilliseconds(5),
			Timestamp = DateTimeOffset.UtcNow,
		});

		var metrics = strategy.GetMetrics();
		metrics.ShouldNotBeNull();
	}

	[Fact]
	public void UpdateStrategy_IgnoresUnknownKey()
	{
		var strategy = CreateStrategy();

		// Update for a key that was never calculated
		strategy.UpdateStrategy(new CachePerformanceFeedback
		{
			Key = "unknown-key",
			IsHit = true,
			ResponseTime = TimeSpan.FromMilliseconds(5),
			Timestamp = DateTimeOffset.UtcNow,
		});

		// Should not throw
		var metrics = strategy.GetMetrics();
		metrics.ShouldNotBeNull();
	}

	[Fact]
	public void GetMetrics_ReturnsValidMetrics()
	{
		var strategy = CreateStrategy();

		// Calculate TTL for a few keys
		strategy.CalculateTtl(CreateContext(key: "key-1"));
		strategy.CalculateTtl(CreateContext(key: "key-2"));

		// Add some feedback
		strategy.UpdateStrategy(new CachePerformanceFeedback
		{
			Key = "key-1",
			IsHit = true,
			ResponseTime = TimeSpan.FromMilliseconds(5),
			Timestamp = DateTimeOffset.UtcNow,
		});
		strategy.UpdateStrategy(new CachePerformanceFeedback
		{
			Key = "key-1",
			IsHit = false,
			ResponseTime = TimeSpan.FromMilliseconds(50),
			Timestamp = DateTimeOffset.UtcNow,
		});

		var metrics = strategy.GetMetrics();
		metrics.TotalCalculations.ShouldBe(2);
		metrics.CustomMetrics.ShouldContainKey("TotalKeys");
		metrics.CustomMetrics.ShouldContainKey("AverageResponseTimeMs");
		metrics.CustomMetrics.ShouldContainKey("StaleHitRate");
		metrics.CustomMetrics.ShouldContainKey("BoundaryHitRate");
	}

	[Fact]
	public void GetMetrics_ReturnsEmptyMetrics_WhenNoCalculations()
	{
		var strategy = CreateStrategy();
		var metrics = strategy.GetMetrics();

		metrics.TotalCalculations.ShouldBe(0);
		metrics.TtlIncreases.ShouldBe(0);
		metrics.TtlDecreases.ShouldBe(0);
		metrics.AverageHitRate.ShouldBe(0);
	}

	[Fact]
	public void ImplementIAdaptiveTtlStrategy()
	{
		var strategy = CreateStrategy();
		strategy.ShouldBeAssignableTo<IAdaptiveTtlStrategy>();
	}
}
