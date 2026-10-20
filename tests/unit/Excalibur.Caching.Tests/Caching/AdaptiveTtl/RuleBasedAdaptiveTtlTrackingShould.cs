// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

using Excalibur.Caching.AdaptiveTtl;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Caching.AdaptiveTtl;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Concurrency")]
public sealed class RuleBasedAdaptiveTtlTrackingShould
{
	[Fact]
	public async Task KeepAReadmittedEntryIndependentOfAnOlderCalculation()
	{
		using var logger = new BlockingLogger();
		var strategy = new RuleBasedAdaptiveTtlStrategy(logger, new RuleBasedTtlOptions { MaxTrackedKeys = 1 });
		var oldContext = new AdaptiveTtlContext
		{
			Key = "first", BaseTtl = TimeSpan.FromMinutes(5), HitRate = 1,
			CurrentTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
		};
		var pending = Task.Run(() => strategy.CalculateTtl(oldContext));
		object? replacement = null;
		try
		{
			logger.Entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
			var old = Map(strategy)["first"];
			_ = strategy.CalculateTtl(Context("second"));
			_ = strategy.CalculateTtl(Context("first"));
			replacement = Map(strategy)["first"];
			replacement.ShouldNotBeSameAs(old);
			strategy.GetMetrics().TotalCalculations.ShouldBe(3);
		}
		finally
		{
			logger.Release.Set();
		}

		_ = await pending.WaitAsync(TimeSpan.FromSeconds(10));
		Map(strategy)["first"].ShouldBeSameAs(replacement);
		Map(strategy).Count.ShouldBe(1);
		(((int)replacement!.GetType().GetField("_rulesApplied")!.GetValue(replacement)!) & 1).ShouldBe(0);
		strategy.GetMetrics().TotalCalculations.ShouldBe(3);
		strategy.GetMetrics().TtlIncreases.ShouldBe(2);
	}

	[Fact]
	public void RejectInvalidCapacityThroughRegisteredTypedValidation()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDistributedMemoryCache();
		_ = services.AddAdaptiveTtlCache(options => options.MaxTrackedKeys = 0);
		using var provider = services.BuildServiceProvider();
		var validator = provider.GetServices<IValidateOptions<RuleBasedTtlOptions>>()
			.Single(candidate => candidate.GetType().Name == "RuleBasedTtlOptionsValidator");
		validator.Validate(null, new RuleBasedTtlOptions { MaxTrackedKeys = 0 }).Failed.ShouldBeTrue();
		Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<RuleBasedTtlOptions>>().Value);
	}

	[Fact]
	public void BoundBothTheMapAndAdmissionQueueUnderUniqueKeyChurn()
	{
		var strategy = Create(3);
		for (var index = 0; index < 100_000; index++)
		{
			_ = strategy.CalculateTtl(Context($"key-{index}"));
			Count(strategy, "keyMetrics").ShouldBeLessThanOrEqualTo(3);
			Count(strategy, "_admissionOrder").ShouldBeLessThanOrEqualTo(3);
		}

		strategy.GetMetrics().TotalCalculations.ShouldBe(100_000);
		strategy.GetMetrics().CustomMetrics["TotalKeys"].ShouldBe(3);
	}

	[Fact]
	public void CaptureCapacityAtConstructionAndEvictInAdmissionOrder()
	{
		var options = new RuleBasedTtlOptions { MaxTrackedKeys = 2 };
		var strategy = new RuleBasedAdaptiveTtlStrategy(NullLogger<RuleBasedAdaptiveTtlStrategy>.Instance, options);
		_ = strategy.CalculateTtl(Context("first"));
		_ = strategy.CalculateTtl(Context("second"));
		_ = strategy.CalculateTtl(Context("first"));
		options.MaxTrackedKeys = 1000;
		_ = strategy.CalculateTtl(Context("third"));

		var map = Map(strategy);
		map.Contains("first").ShouldBeFalse();
		map.Contains("second").ShouldBeTrue();
		map.Contains("third").ShouldBeTrue();
		map.Count.ShouldBe(2);
		strategy.GetMetrics().TotalCalculations.ShouldBe(4);
	}

	[Fact]
	public void PreserveLifetimeAcceptedFeedbackWhileResettingTheTrackedCohort()
	{
		var strategy = Create(1);
		_ = strategy.CalculateTtl(Context("first"));
		strategy.UpdateStrategy(new CachePerformanceFeedback
		{
			Key = "first", IsHit = true, WasStale = true, ResponseTime = TimeSpan.FromMilliseconds(10),
		});
		_ = strategy.CalculateTtl(Context("second"));
		strategy.UpdateStrategy(new CachePerformanceFeedback
		{
			Key = "second", IsHit = false, ResponseTime = TimeSpan.FromMilliseconds(30),
		});
		strategy.UpdateStrategy(new CachePerformanceFeedback
		{
			Key = "first", IsHit = true, ResponseTime = TimeSpan.FromHours(1),
		});

		var metrics = strategy.GetMetrics();
		metrics.TotalCalculations.ShouldBe(2);
		metrics.AverageHitRate.ShouldBe(0);
		metrics.CustomMetrics["AverageResponseTimeMs"].ShouldBe(20);
		metrics.CustomMetrics["StaleHitRate"].ShouldBe(1);
		metrics.CustomMetrics["TotalKeys"].ShouldBe(1);
		Map(strategy).Contains("first").ShouldBeFalse();
		_ = strategy.CalculateTtl(Context("first"));
		strategy.GetMetrics().TotalCalculations.ShouldBe(3);
		strategy.GetMetrics().AverageHitRate.ShouldBe(0);
	}

	[Fact]
	public void PreserveRuleApplicationsWhenLaterMathFails()
	{
		var strategy = Create(1);
		var context = new AdaptiveTtlContext { Key = "overflow", BaseTtl = TimeSpan.MaxValue, HitRate = 1 };
		Should.Throw<OverflowException>(() => strategy.CalculateTtl(context));
		strategy.GetMetrics().TotalCalculations.ShouldBe(0);
		strategy.GetMetrics().TtlIncreases.ShouldBe(1);
		_ = strategy.CalculateTtl(Context("replacement"));
		strategy.GetMetrics().TtlIncreases.ShouldBe(1);
	}

	[Fact]
	public void PreserveTtlDecisionsRegardlessOfDiagnosticEviction()
	{
		var small = Create(1);
		var large = Create(1024);
		for (var index = 0; index < 300; index++)
		{
			var context = new AdaptiveTtlContext
			{
				Key = $"key-{index % 17}", BaseTtl = TimeSpan.FromSeconds(1 + index),
				HitRate = index % 2 == 0 ? 0.99 : 0.1, AccessFrequency = index,
				SystemLoad = index % 2 == 0 ? 0.95 : 0.05,
				MissCost = TimeSpan.FromMilliseconds(index), ContentSize = index * 10000,
				CurrentTime = new DateTimeOffset(2026, 1, 1, index % 24, 0, 0, TimeSpan.Zero),
			};
			small.CalculateTtl(context).ShouldBe(large.CalculateTtl(context));
		}

		small.GetMetrics().TotalCalculations.ShouldBe(large.GetMetrics().TotalCalculations);
		small.GetMetrics().TtlIncreases.ShouldBe(large.GetMetrics().TtlIncreases);
		small.GetMetrics().TtlDecreases.ShouldBe(large.GetMetrics().TtlDecreases);
		small.GetMetrics().CustomMetrics["BoundaryHitRate"].ShouldBe(large.GetMetrics().CustomMetrics["BoundaryHitRate"]);
	}

	[Fact]
	public void KeepLifetimeCountsUnderConcurrentChurnAndMetricReads()
	{
		var strategy = Create(16);
		Parallel.For(0, 10_000, index =>
		{
			_ = strategy.CalculateTtl(Context($"key-{index}"));
			strategy.GetMetrics().CustomMetrics["TotalKeys"].ShouldBeLessThanOrEqualTo(16);
		});

		strategy.GetMetrics().TotalCalculations.ShouldBe(10_000);
		strategy.GetMetrics().TtlDecreases.ShouldBe(10_000);
		Count(strategy, "keyMetrics").ShouldBe(16);
		Count(strategy, "_admissionOrder").ShouldBe(16);
	}

	[Fact]
	public void PreserveConcurrentAcceptedFeedbackTotals()
	{
		var strategy = Create(1);
		_ = strategy.CalculateTtl(Context("same"));
		Parallel.For(0, 10_000, index => strategy.UpdateStrategy(new CachePerformanceFeedback
		{
			Key = "same", IsHit = index % 2 == 0, WasStale = index % 4 == 0,
			ResponseTime = TimeSpan.FromMilliseconds(2),
		}));

		var metrics = strategy.GetMetrics();
		metrics.AverageHitRate.ShouldBe(0.5);
		metrics.CustomMetrics["StaleHitRate"].ShouldBe(0.5);
		metrics.CustomMetrics["AverageResponseTimeMs"].ShouldBe(2);
	}

	[Fact]
	public void AdmitASharedKeyOnlyOnceUnderContention()
	{
		var strategy = Create(1);
		Parallel.For(0, 10_000, _ => strategy.CalculateTtl(Context("same")));
		Count(strategy, "keyMetrics").ShouldBe(1);
		Count(strategy, "_admissionOrder").ShouldBe(1);
		strategy.GetMetrics().TotalCalculations.ShouldBe(10_000);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void RejectInvalidCapacityForDirectConstructionAndAnnotations(int capacity)
	{
		var options = new RuleBasedTtlOptions { MaxTrackedKeys = capacity };
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new RuleBasedAdaptiveTtlStrategy(NullLogger<RuleBasedAdaptiveTtlStrategy>.Instance, options));
		var results = new List<ValidationResult>();
		Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true).ShouldBeFalse();
		results.ShouldContain(result => result.MemberNames.Contains(nameof(RuleBasedTtlOptions.MaxTrackedKeys)));
	}

	private static RuleBasedAdaptiveTtlStrategy Create(int capacity) =>
		new(NullLogger<RuleBasedAdaptiveTtlStrategy>.Instance, new RuleBasedTtlOptions { MaxTrackedKeys = capacity });

	private static AdaptiveTtlContext Context(string key) => new()
	{
		Key = key, BaseTtl = TimeSpan.FromMinutes(5), HitRate = 0.7, AccessFrequency = 10,
		SystemLoad = 0.5, CurrentTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
	};

	private static IDictionary Map(RuleBasedAdaptiveTtlStrategy strategy) => (IDictionary)typeof(RuleBasedAdaptiveTtlStrategy)
		.GetField("keyMetrics", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(strategy)!;

	private static int Count(RuleBasedAdaptiveTtlStrategy strategy, string field) =>
		((ICollection)typeof(RuleBasedAdaptiveTtlStrategy).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(strategy)!).Count;

	private sealed class BlockingLogger : ILogger<RuleBasedAdaptiveTtlStrategy>, IDisposable
	{
		private int _blockNext = 1;

		public ManualResetEventSlim Entered { get; } = new();
		public ManualResetEventSlim Release { get; } = new();

		public bool IsEnabled(LogLevel logLevel)
		{
			if (Interlocked.Exchange(ref _blockNext, 0) == 1)
			{
				Entered.Set();
				if (!Release.Wait(TimeSpan.FromSeconds(10)))
				{
					throw new TimeoutException("Blocked diagnostic logger was not released.");
				}
			}
			return logLevel == LogLevel.Debug;
		}

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
		}

		public void Dispose()
		{
			Entered.Dispose();
			Release.Dispose();
		}
	}
}
