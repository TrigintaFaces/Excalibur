// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Caching.AdaptiveTtl;
using Microsoft.Extensions.Logging;

namespace Excalibur.Tests.Caching.AdaptiveTtl;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Pattern", "Logging")]
public sealed class RuleBasedAdaptiveTtlLoggingShould
{
	[Fact]
	public void PreserveDecisionsAndMetricsWithDebugDisabled()
	{
		var enabledLogger = new RecordingLogger(true);
		var disabledLogger = new RecordingLogger(false);
		var enabled = new RuleBasedAdaptiveTtlStrategy(enabledLogger, new RuleBasedTtlOptions());
		var disabled = new RuleBasedAdaptiveTtlStrategy(disabledLogger, new RuleBasedTtlOptions());
		foreach (var context in new[] { Context("first", 0.99), Context("first", 0.7), Context("second", 0.01, TimeSpan.FromDays(30)) })
		{
			disabled.CalculateTtl(context).ShouldBe(enabled.CalculateTtl(context));
		}

		var actual = disabled.GetMetrics();
		var expected = enabled.GetMetrics();
		actual.TotalCalculations.ShouldBe(3);
		actual.TotalCalculations.ShouldBe(expected.TotalCalculations);
		actual.TtlIncreases.ShouldBe(expected.TtlIncreases);
		actual.TtlDecreases.ShouldBe(expected.TtlDecreases);
		actual.AverageHitRate.ShouldBe(expected.AverageHitRate);
		actual.AverageAdjustmentFactor.ShouldBe(expected.AverageAdjustmentFactor);
		foreach (var metric in expected.CustomMetrics)
		{
			actual.CustomMetrics[metric.Key].ShouldBe(metric.Value);
		}
		disabledLogger.Records.ShouldBeEmpty();
		enabledLogger.Records.Count.ShouldBe(3);
	}

	[Fact]
	public void PreserveStructuredEventAndCumulativeRuleFlags()
	{
		var logger = new RecordingLogger(true);
		var strategy = new RuleBasedAdaptiveTtlStrategy(logger, new RuleBasedTtlOptions());
		strategy.CalculateTtl(Context("history", 0.99));
		var context = Context("history", 0.7);
		var ttl = strategy.CalculateTtl(context);
		var record = logger.Records[1];
		record.Level.ShouldBe(LogLevel.Debug);
		record.Id.Id.ShouldBe(151900);
		record.Exception.ShouldBeNull();
		record.Fields["Key"].ShouldBe("history");
		record.Fields["Ttl"].ShouldBe(ttl);
		record.Fields["BaseTtl"].ShouldBe(context.BaseTtl);
		record.Fields["RulesApplied"].ShouldBe("R1=True, R2=False, R3=False, R4=False, R5=True, R6=False");
		record.Fields["{OriginalFormat}"].ShouldBe("Calculated rule-based TTL for key {Key}: {Ttl} (base: {BaseTtl}, rules applied: {RulesApplied})");
		record.Message.ShouldContain("history");
		record.Message.ShouldContain("R1=True");
		strategy.CalculateTtl(Context("other", 0.7));
		logger.Records[2].Fields["Key"].ShouldBe("other");
		logger.Records[2].Fields["RulesApplied"].ShouldBe("R1=False, R2=False, R3=False, R4=False, R5=True, R6=False");
	}

	[Fact]
	public void CountRepeatedRulesIndependentlyOfCumulativeLogFlags()
	{
		var logger = new RecordingLogger(true);
		var strategy = new RuleBasedAdaptiveTtlStrategy(logger, new RuleBasedTtlOptions());
		for (var index = 0; index < 100; index++)
		{
			_ = strategy.CalculateTtl(Context("same", 0.99));
		}

		strategy.GetMetrics().TotalCalculations.ShouldBe(100);
		strategy.GetMetrics().TtlIncreases.ShouldBe(100);
		strategy.GetMetrics().TtlDecreases.ShouldBe(100);
		logger.Records.Count.ShouldBe(100);
		logger.Records[^1].Fields["RulesApplied"].ShouldBe("R1=True, R2=False, R3=False, R4=False, R5=True, R6=False");
	}

	[Fact]
	public void PreserveRuleFlagWhenSubsequentTtlMathThrows()
	{
		var logger = new RecordingLogger(true);
		var strategy = new RuleBasedAdaptiveTtlStrategy(logger, new RuleBasedTtlOptions());
		Should.Throw<OverflowException>(() => strategy.CalculateTtl(Context("same", 0.99, TimeSpan.MaxValue)));
		logger.Records.ShouldBeEmpty();
		strategy.GetMetrics().TotalCalculations.ShouldBe(0);
		strategy.GetMetrics().TtlIncreases.ShouldBe(1);
		_ = strategy.CalculateTtl(Context("same", 0.7));
		logger.Records.Single().Fields["RulesApplied"].ShouldBe("R1=True, R2=False, R3=False, R4=False, R5=True, R6=False");
		strategy.GetMetrics().TotalCalculations.ShouldBe(1);
		strategy.GetMetrics().TtlIncreases.ShouldBe(1);
	}

	[Fact]
	public void RetainDifferentConcurrentRuleFlagsWithoutLosingPerCallCounts()
	{
		var logger = new RecordingLogger(false);
		var options = new RuleBasedTtlOptions
		{
			TimeOfDay = { PeakHoursStart = 10, PeakHoursEnd = 11, OffHoursStart = 0, OffHoursEnd = 23 },
		};
		var strategy = new RuleBasedAdaptiveTtlStrategy(logger, options);
		var contexts = Enumerable.Range(0, 6).Select(_ => Context("shared", 0.7)).ToArray();
		contexts[0].HitRate = 1;
		contexts[1].AccessFrequency = 1000;
		contexts[2].MissCost = TimeSpan.FromDays(1);
		contexts[3].SystemLoad = 1;
		contexts[4].CurrentTime = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
		contexts[5].ContentSize = long.MaxValue;
		_ = Parallel.For(0, 6000, index => strategy.CalculateTtl(contexts[index % contexts.Length]));
		strategy.GetMetrics().TotalCalculations.ShouldBe(6000);
		strategy.GetMetrics().TtlIncreases.ShouldBe(3000);
		strategy.GetMetrics().TtlDecreases.ShouldBe(3000);
		logger.Enabled = true;
		_ = strategy.CalculateTtl(Context("shared", 0.7));
		logger.Records.Single().Fields["RulesApplied"].ShouldBe("R1=True, R2=True, R3=True, R4=True, R5=True, R6=True");
		strategy.GetMetrics().TotalCalculations.ShouldBe(6001);
		strategy.GetMetrics().TtlIncreases.ShouldBe(3000);
		strategy.GetMetrics().TtlDecreases.ShouldBe(3000);
	}

	[Fact]
	public void PropagateLoggingFailureAfterUpdatingCalculationMetrics()
	{
		var failure = new InvalidOperationException("logger failure");
		var strategy = new RuleBasedAdaptiveTtlStrategy(new RecordingLogger(true) { Failure = failure }, new RuleBasedTtlOptions());
		Should.Throw<InvalidOperationException>(() => strategy.CalculateTtl(Context("key", 0.99))).ShouldBeSameAs(failure);
		strategy.GetMetrics().TotalCalculations.ShouldBe(1);
	}

	private static AdaptiveTtlContext Context(string key, double hitRate, TimeSpan? baseTtl = null) => new()
	{
		Key = key,
		BaseTtl = baseTtl ?? TimeSpan.FromMinutes(5),
		HitRate = hitRate,
		AccessFrequency = 50,
		SystemLoad = 0.5,
		ContentSize = 1024,
		MissCost = TimeSpan.FromMilliseconds(10),
		CurrentTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
		LastUpdate = new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero),
	};

	private sealed record LogRecord(LogLevel Level, EventId Id, Exception? Exception, Dictionary<string, object?> Fields, string Message);

	private sealed class RecordingLogger(bool enabled) : ILogger<RuleBasedAdaptiveTtlStrategy>
	{
		public List<LogRecord> Records { get; } = [];
		public bool Enabled { get; set; } = enabled;
		public Exception? Failure { get; init; }
		public bool IsEnabled(LogLevel logLevel) => Enabled && logLevel == LogLevel.Debug;
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (Failure is not null)
			{
				throw Failure;
			}
			var fields = ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(pair => pair.Key, pair => pair.Value);
			Records.Add(new LogRecord(logLevel, eventId, exception, fields, formatter(state, exception)));
		}
	}
}
