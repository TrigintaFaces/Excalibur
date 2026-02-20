// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Caching.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// Implements a rule-based adaptive TTL strategy using simple heuristics.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="RuleBasedAdaptiveTtlStrategy" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
/// <param name="options"> The rule-based TTL options. </param>
public partial class RuleBasedAdaptiveTtlStrategy(
	ILogger<RuleBasedAdaptiveTtlStrategy> logger,
	RuleBasedTtlOptions options) : IAdaptiveTtlStrategy
{
	private readonly ILogger<RuleBasedAdaptiveTtlStrategy> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly RuleBasedTtlOptions options = options ?? throw new ArgumentNullException(nameof(options));

	private readonly ConcurrentDictionary<string, SimpleKeyMetrics> keyMetrics =
		new(StringComparer.Ordinal);

	/// <inheritdoc />
	public TimeSpan CalculateTtl(AdaptiveTtlContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var metrics = keyMetrics.GetOrAdd(context.Key, static _ => new SimpleKeyMetrics());
		var adjustedTtl = context.BaseTtl;

		adjustedTtl = ApplyHitRateRule(context, metrics, adjustedTtl);
		adjustedTtl = ApplyAccessFrequencyRule(context, metrics, adjustedTtl);
		adjustedTtl = ApplyMissCostRule(context, metrics, adjustedTtl);
		adjustedTtl = ApplySystemLoadRule(context, metrics, adjustedTtl);
		adjustedTtl = ApplyTimeOfDayRule(context, metrics, adjustedTtl);
		adjustedTtl = ApplyContentSizeRule(context, metrics, adjustedTtl);
		adjustedTtl = ApplyBounds(metrics, adjustedTtl);

		Interlocked.Increment(ref metrics._totalCalculations);
		metrics.LastTtl = adjustedTtl;
		metrics.LastCalculation = context.CurrentTime;

		var rulesApplied =
			$"R1={metrics.Rule1Applied > 0}, R2={metrics.Rule2Applied > 0}, R3={metrics.Rule3Applied > 0}, R4={metrics.Rule4Applied > 0}, R5={metrics.Rule5Applied > 0}, R6={metrics.Rule6Applied > 0}";
		LogCalculatedTtl(context.Key, adjustedTtl, context.BaseTtl, rulesApplied);

		return adjustedTtl;
	}

	/// <inheritdoc />
	public void UpdateStrategy(CachePerformanceFeedback feedback)
	{
		ArgumentNullException.ThrowIfNull(feedback);

		if (!keyMetrics.TryGetValue(feedback.Key, out var metrics))
		{
			return;
		}

		// Update simple metrics
		if (feedback.IsHit)
		{
			Interlocked.Increment(ref metrics._hits);
			if (feedback.WasStale)
			{
				Interlocked.Increment(ref metrics._staleHits);
			}
		}
		else
		{
			Interlocked.Increment(ref metrics._misses);
		}

		metrics.TotalResponseTime += feedback.ResponseTime;
		metrics.LastUpdate = feedback.Timestamp;

		LogUpdatedMetrics(feedback.Key, metrics.Hits, metrics.Misses, metrics.StaleHits);
	}

	/// <inheritdoc />
	public AdaptiveTtlMetrics GetMetrics()
	{
		long totalCalculations = 0;
		long ttlIncreases = 0;
		long ttlDecreases = 0;
		double totalHitRate = 0;
		var keyCount = 0;

		foreach (var kvp in keyMetrics)
		{
			var metrics = kvp.Value;
			totalCalculations += metrics.TotalCalculations;

			var total = metrics.Hits + metrics.Misses;
			if (total > 0)
			{
				totalHitRate += (double)metrics.Hits / total;
				keyCount++;
			}

			// Count increases/decreases based on rules applied
			ttlIncreases += metrics.Rule1Applied + metrics.Rule2Applied + metrics.Rule3Applied;
			ttlDecreases += metrics.Rule4Applied + metrics.Rule5Applied + metrics.Rule6Applied;
		}

		return new AdaptiveTtlMetrics
		{
			TotalCalculations = totalCalculations,
			TtlIncreases = ttlIncreases,
			TtlDecreases = ttlDecreases,
			AverageHitRate = keyCount > 0 ? totalHitRate / keyCount : 0,
			AverageAdjustmentFactor = 1.0,
			CustomMetrics =
			{
				// Add custom metrics
				["TotalKeys"] = keyMetrics.Count,
				["AverageResponseTimeMs"] = CalculateAverageResponseTime(),
				["StaleHitRate"] = CalculateStaleHitRate(),
				["BoundaryHitRate"] = CalculateBoundaryHitRate(),
			}, // Rule-based doesn't track this precisely
		};
	}

	private TimeSpan ApplyHitRateRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl)
	{
		if (context.HitRate > options.HitRate.HighHitRateThreshold)
		{
			Interlocked.Increment(ref metrics._rule1Applied);
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.HitRate.HighHitRateMultiplier);
		}

		if (context.HitRate < options.HitRate.LowHitRateThreshold)
		{
			Interlocked.Increment(ref metrics._rule1Applied);
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.HitRate.LowHitRateMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplyAccessFrequencyRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl)
	{
		if (context.AccessFrequency > options.Frequency.HighFrequencyThreshold)
		{
			Interlocked.Increment(ref metrics._rule2Applied);
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Frequency.HighFrequencyMultiplier);
		}

		if (context.AccessFrequency < options.Frequency.LowFrequencyThreshold)
		{
			Interlocked.Increment(ref metrics._rule2Applied);
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Frequency.LowFrequencyMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplyMissCostRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl)
	{
		if (context.MissCost > options.Load.ExpensiveMissThreshold)
		{
			Interlocked.Increment(ref metrics._rule3Applied);
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Load.ExpensiveMissMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplySystemLoadRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl)
	{
		if (context.SystemLoad > options.Thresholds.HighLoadThreshold)
		{
			Interlocked.Increment(ref metrics._rule4Applied);
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Load.HighLoadMultiplier);
		}

		if (context.SystemLoad < options.Thresholds.LowLoadThreshold)
		{
			Interlocked.Increment(ref metrics._rule4Applied);
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Load.LowLoadMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplyTimeOfDayRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl)
	{
		var hour = context.CurrentTime.Hour;

		if (hour >= options.TimeOfDay.PeakHoursStart && hour <= options.TimeOfDay.PeakHoursEnd)
		{
			Interlocked.Increment(ref metrics._rule5Applied);
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.TimeOfDay.PeakHoursMultiplier);
		}

		if (hour < options.TimeOfDay.OffHoursStart || hour > options.TimeOfDay.OffHoursEnd)
		{
			Interlocked.Increment(ref metrics._rule5Applied);
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.TimeOfDay.OffHoursMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplyContentSizeRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl)
	{
		if (context.ContentSize > options.Content.LargeContentThreshold)
		{
			Interlocked.Increment(ref metrics._rule6Applied);
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Content.LargeContentMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplyBounds(SimpleKeyMetrics metrics, TimeSpan adjustedTtl)
	{
		if (adjustedTtl < options.MinTtl)
		{
			Interlocked.Increment(ref metrics._boundaryHits);
			return options.MinTtl;
		}

		if (adjustedTtl > options.MaxTtl)
		{
			Interlocked.Increment(ref metrics._boundaryHits);
			return options.MaxTtl;
		}

		return adjustedTtl;
	}

	private double CalculateAverageResponseTime()
	{
		double totalTime = 0;
		long totalRequests = 0;

		foreach (var metrics in keyMetrics.Values)
		{
			if (metrics.Hits + metrics.Misses > 0)
			{
				totalTime += metrics.TotalResponseTime.TotalMilliseconds;
				totalRequests += metrics.Hits + metrics.Misses;
			}
		}

		return totalRequests > 0 ? totalTime / totalRequests : 0;
	}

	private double CalculateStaleHitRate()
	{
		long totalHits = 0;
		long staleHits = 0;

		foreach (var metrics in keyMetrics.Values)
		{
			totalHits += metrics.Hits;
			staleHits += metrics.StaleHits;
		}

		return totalHits > 0 ? (double)staleHits / totalHits : 0;
	}

	private double CalculateBoundaryHitRate()
	{
		long totalCalculations = 0;
		long boundaryHits = 0;

		foreach (var metrics in keyMetrics.Values)
		{
			totalCalculations += metrics.TotalCalculations;
			boundaryHits += metrics.BoundaryHits;
		}

		return totalCalculations > 0 ? (double)boundaryHits / totalCalculations : 0;
	}

	// Source-generated logging methods
	[LoggerMessage(CachingEventId.RuleBasedTtlCalculated, LogLevel.Debug,
		"Calculated rule-based TTL for key {Key}: {Ttl} (base: {BaseTtl}, rules applied: {RulesApplied})")]
	private partial void LogCalculatedTtl(string key, TimeSpan ttl, TimeSpan baseTtl, string rulesApplied);

	[LoggerMessage(CachingEventId.RuleBasedMetricsUpdated, LogLevel.Debug,
		"Updated metrics for key {Key}: hits={Hits}, misses={Misses}, staleHits={StaleHits}")]
	private partial void LogUpdatedMetrics(string key, long hits, long misses, long staleHits);

	/// <summary>
	/// Simple metrics tracked for each key.
	/// </summary>
	private sealed class SimpleKeyMetrics
	{
		// Fields accessed via Interlocked — must be fields, not properties
		public long _totalCalculations;
		public long _hits;
		public long _misses;
		public long _staleHits;
		public long _boundaryHits;
		public long _rule1Applied;
		public long _rule2Applied;
		public long _rule3Applied;
		public long _rule4Applied;
		public long _rule5Applied;
		public long _rule6Applied;

		public long TotalCalculations => Interlocked.Read(ref _totalCalculations);

		public long Hits => Interlocked.Read(ref _hits);

		public long Misses => Interlocked.Read(ref _misses);

		public long StaleHits => Interlocked.Read(ref _staleHits);

		public TimeSpan TotalResponseTime { get; set; }

		public TimeSpan LastTtl { get; set; }

		public DateTimeOffset LastCalculation { get; set; }

		public DateTimeOffset LastUpdate { get; set; }

		public long BoundaryHits => Interlocked.Read(ref _boundaryHits);

		public long Rule1Applied => Interlocked.Read(ref _rule1Applied);

		public long Rule2Applied => Interlocked.Read(ref _rule2Applied);

		public long Rule3Applied => Interlocked.Read(ref _rule3Applied);

		public long Rule4Applied => Interlocked.Read(ref _rule4Applied);

		public long Rule5Applied => Interlocked.Read(ref _rule5Applied);

		public long Rule6Applied => Interlocked.Read(ref _rule6Applied);
	}
}
