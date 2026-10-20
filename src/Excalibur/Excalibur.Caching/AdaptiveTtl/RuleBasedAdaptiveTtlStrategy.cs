// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Numerics;

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
	private readonly int _maxTrackedKeys = ValidateCapacity(options);
	private readonly Lock _admissionLock = new();
	private readonly Queue<string> _admissionOrder = new();
	private long _totalCalculations;
	private long _ttlIncreases;
	private long _ttlDecreases;
	private long _boundaryHits;
	private long _hits;
	private long _misses;
	private long _staleHits;
	private long _responseTicks;

	private readonly ConcurrentDictionary<string, SimpleKeyMetrics> keyMetrics =
		new(StringComparer.Ordinal);

	/// <inheritdoc />
	public TimeSpan CalculateTtl(AdaptiveTtlContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		var metrics = GetOrCreateMetrics(context.Key);
		var adjustedTtl = context.BaseTtl;
		uint rules = 0;
		var boundaryHit = false;
		try
		{
			adjustedTtl = ApplyHitRateRule(context, metrics, adjustedTtl, ref rules);
			adjustedTtl = ApplyAccessFrequencyRule(context, metrics, adjustedTtl, ref rules);
			adjustedTtl = ApplyMissCostRule(context, metrics, adjustedTtl, ref rules);
			adjustedTtl = ApplySystemLoadRule(context, metrics, adjustedTtl, ref rules);
			adjustedTtl = ApplyTimeOfDayRule(context, metrics, adjustedTtl, ref rules);
			adjustedTtl = ApplyContentSizeRule(context, metrics, adjustedTtl, ref rules);
			adjustedTtl = ApplyBounds(adjustedTtl, ref boundaryHit);

			Interlocked.Increment(ref _totalCalculations);
		}
		finally
		{
			// Preserve partial rule counts even when later TTL math throws. The
			// legacy groups are rules 1-3 and 4-6, irrespective of multiplier sign.
			if ((rules & 7) != 0)
			{
				Interlocked.Add(ref _ttlIncreases, BitOperations.PopCount(rules & 7));
			}
			if ((rules >> 3) != 0)
			{
				Interlocked.Add(ref _ttlDecreases, BitOperations.PopCount(rules >> 3));
			}
			if (boundaryHit)
			{
				Interlocked.Increment(ref _boundaryHits);
			}
		}

		if (_logger.IsEnabled(LogLevel.Debug))
		{
			var rulesApplied =
				$"R1={metrics.Rule1Applied > 0}, R2={metrics.Rule2Applied > 0}, R3={metrics.Rule3Applied > 0}, R4={metrics.Rule4Applied > 0}, R5={metrics.Rule5Applied > 0}, R6={metrics.Rule6Applied > 0}";
			LogCalculatedTtl(context.Key, adjustedTtl, context.BaseTtl, rulesApplied);
		}

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
			Interlocked.Increment(ref _hits);
			if (feedback.WasStale)
			{
				Interlocked.Increment(ref metrics._staleHits);
				Interlocked.Increment(ref _staleHits);
			}
		}
		else
		{
			Interlocked.Increment(ref metrics._misses);
			Interlocked.Increment(ref _misses);
		}

		Interlocked.Add(ref _responseTicks, feedback.ResponseTime.Ticks);

		LogUpdatedMetrics(feedback.Key, metrics.Hits, metrics.Misses, metrics.StaleHits);
	}

	/// <inheritdoc />
	public AdaptiveTtlMetrics GetMetrics()
	{
		double totalHitRate = 0;
		var keyCount = 0;
		var snapshot = keyMetrics.Values;

		foreach (var metrics in snapshot)
		{
			var total = metrics.Hits + metrics.Misses;
			if (total > 0)
			{
				totalHitRate += (double)metrics.Hits / total;
				keyCount++;
			}
		}
		var totalCalculations = Interlocked.Read(ref _totalCalculations);
		var hits = Interlocked.Read(ref _hits);
		var requests = hits + Interlocked.Read(ref _misses);

		return new AdaptiveTtlMetrics
		{
			TotalCalculations = totalCalculations,
			TtlIncreases = Interlocked.Read(ref _ttlIncreases),
			TtlDecreases = Interlocked.Read(ref _ttlDecreases),
			AverageHitRate = keyCount > 0 ? totalHitRate / keyCount : 0,
			AverageAdjustmentFactor = 1.0,
			CustomMetrics =
			{
				// Add custom metrics
				["TotalKeys"] = snapshot.Count,
				["AverageResponseTimeMs"] = requests > 0 ? TimeSpan.FromTicks(Interlocked.Read(ref _responseTicks)).TotalMilliseconds / requests : 0,
				["StaleHitRate"] = hits > 0 ? (double)Interlocked.Read(ref _staleHits) / hits : 0,
				["BoundaryHitRate"] = totalCalculations > 0 ? (double)Interlocked.Read(ref _boundaryHits) / totalCalculations : 0,
			}, // Rule-based doesn't track this precisely
		};
	}

	private TimeSpan ApplyHitRateRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl, ref uint rules)
	{
		if (context.HitRate > options.HitRate.HighHitRateThreshold)
		{
			metrics.MarkRule(1);
			rules |= 1;
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.HitRate.HighHitRateMultiplier);
		}

		if (context.HitRate < options.HitRate.LowHitRateThreshold)
		{
			metrics.MarkRule(1);
			rules |= 1;
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.HitRate.LowHitRateMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplyAccessFrequencyRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl, ref uint rules)
	{
		if (context.AccessFrequency > options.Frequency.HighFrequencyThreshold)
		{
			metrics.MarkRule(2);
			rules |= 2;
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Frequency.HighFrequencyMultiplier);
		}

		if (context.AccessFrequency < options.Frequency.LowFrequencyThreshold)
		{
			metrics.MarkRule(2);
			rules |= 2;
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Frequency.LowFrequencyMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplyMissCostRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl, ref uint rules)
	{
		if (context.MissCost > options.Load.ExpensiveMissThreshold)
		{
			metrics.MarkRule(4);
			rules |= 4;
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Load.ExpensiveMissMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplySystemLoadRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl, ref uint rules)
	{
		if (context.SystemLoad > options.Thresholds.HighLoadThreshold)
		{
			metrics.MarkRule(8);
			rules |= 8;
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Load.HighLoadMultiplier);
		}

		if (context.SystemLoad < options.Thresholds.LowLoadThreshold)
		{
			metrics.MarkRule(8);
			rules |= 8;
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Load.LowLoadMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplyTimeOfDayRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl, ref uint rules)
	{
		var hour = context.CurrentTime.Hour;

		if (hour >= options.TimeOfDay.PeakHoursStart && hour <= options.TimeOfDay.PeakHoursEnd)
		{
			metrics.MarkRule(16);
			rules |= 16;
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.TimeOfDay.PeakHoursMultiplier);
		}

		if (hour < options.TimeOfDay.OffHoursStart || hour > options.TimeOfDay.OffHoursEnd)
		{
			metrics.MarkRule(16);
			rules |= 16;
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.TimeOfDay.OffHoursMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplyContentSizeRule(AdaptiveTtlContext context, SimpleKeyMetrics metrics, TimeSpan adjustedTtl, ref uint rules)
	{
		if (context.ContentSize > options.Content.LargeContentThreshold)
		{
			metrics.MarkRule(32);
			rules |= 32;
			return TimeSpan.FromMilliseconds(adjustedTtl.TotalMilliseconds * options.Content.LargeContentMultiplier);
		}

		return adjustedTtl;
	}

	private TimeSpan ApplyBounds(TimeSpan adjustedTtl, ref bool boundaryHit)
	{
		if (adjustedTtl < options.MinTtl)
		{
			boundaryHit = true;
			return options.MinTtl;
		}

		if (adjustedTtl > options.MaxTtl)
		{
			boundaryHit = true;
			return options.MaxTtl;
		}

		return adjustedTtl;
	}

	private SimpleKeyMetrics GetOrCreateMetrics(string key)
	{
		if (keyMetrics.TryGetValue(key, out var existing))
		{
			return existing;
		}

		lock (_admissionLock)
		{
			if (keyMetrics.TryGetValue(key, out existing))
			{
				return existing;
			}

			if (_admissionOrder.Count >= _maxTrackedKeys)
			{
				_ = keyMetrics.TryRemove(_admissionOrder.Dequeue(), out _);
			}

			var created = new SimpleKeyMetrics();
			keyMetrics[key] = created;
			_admissionOrder.Enqueue(key);
			return created;
		}
	}

	private static int ValidateCapacity(RuleBasedTtlOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		var capacity = options.MaxTrackedKeys;
		ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1, nameof(options.MaxTrackedKeys));
		return capacity;
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
		public long _hits;
		public long _misses;
		public long _staleHits;
		public int _rulesApplied;

		public long Hits => Interlocked.Read(ref _hits);

		public long Misses => Interlocked.Read(ref _misses);

		public long StaleHits => Interlocked.Read(ref _staleHits);

		public int Rule1Applied => Volatile.Read(ref _rulesApplied) & 1;

		public int Rule2Applied => Volatile.Read(ref _rulesApplied) & 2;

		public int Rule3Applied => Volatile.Read(ref _rulesApplied) & 4;

		public int Rule4Applied => Volatile.Read(ref _rulesApplied) & 8;

		public int Rule5Applied => Volatile.Read(ref _rulesApplied) & 16;

		public int Rule6Applied => Volatile.Read(ref _rulesApplied) & 32;

		public void MarkRule(int rule)
		{
			// Flags are cumulative for this admission, independently of per-call
			// global rule counts. Preserve the flag even if subsequent TTL math fails.
			if ((Volatile.Read(ref _rulesApplied) & rule) == 0)
			{
				_ = Interlocked.Or(ref _rulesApplied, rule);
			}
		}
	}
}
