// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Performance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
///     Tests for the <see cref="PerformanceBenchmark" /> class.
/// </summary>
[Collection("Performance Tests")]
[Trait("Category", "Unit")]
public sealed class PerformanceBenchmarkShould
{
	private static PerformanceSnapshot CreateEmptySnapshot() =>
		new()
		{
			MiddlewareMetrics = new Dictionary<string, ComponentMetrics>(),
			PipelineMetrics = new PipelineMetrics
			{
				TotalExecutions = 0,
				TotalDuration = TimeSpan.Zero,
				AverageDuration = TimeSpan.Zero,
				AverageMiddlewareCount = 0,
				TotalMemoryAllocated = 0,
				AverageMemoryPerExecution = 0,
			},
			BatchMetrics = new Dictionary<string, BatchProcessingMetrics>(),
			HandlerMetrics = new HandlerRegistryMetrics
			{
				TotalLookups = 0,
				TotalLookupTime = TimeSpan.Zero,
				AverageLookupTime = TimeSpan.Zero,
				AverageHandlersPerLookup = 0,
				CacheHits = 0,
				CacheMisses = 0,
				CacheHitRate = 0,
			},
			QueueMetrics = new Dictionary<string, QueueMetrics>(),
			Timestamp = DateTimeOffset.UtcNow,
		};

	[Fact]
	public void ThrowForNullMetricsCollector() =>
		Should.Throw<ArgumentNullException>(() => new PerformanceBenchmark(null!, NullLogger<PerformanceBenchmark>.Instance));

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() => new PerformanceBenchmark(A.Fake<IPerformanceMetricsCollector>(), null!));

	[Fact]
	public async Task ThrowForInvalidIterations()
	{
		var collector = A.Fake<IPerformanceMetricsCollector>();
		var sut = new PerformanceBenchmark(collector, NullLogger<PerformanceBenchmark>.Instance);

		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => sut.RunComprehensiveBenchmarkAsync(CancellationToken.None, iterations: 0)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RunBenchmarkSuccessfully()
	{
		var collector = A.Fake<IPerformanceMetricsCollector>();
		A.CallTo(() => collector.GetSnapshot()).Returns(CreateEmptySnapshot());

		var sut = new PerformanceBenchmark(collector, NullLogger<PerformanceBenchmark>.Instance);

		var results = await sut.RunComprehensiveBenchmarkAsync(CancellationToken.None, iterations: 10).ConfigureAwait(false);

		results.ShouldNotBeNull();
		results.Iterations.ShouldBe(10);
		results.TotalDuration.ShouldBeGreaterThan(TimeSpan.Zero);
		results.MessagesPerSecond.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task RecordPipelineExecutions()
	{
		var collector = A.Fake<IPerformanceMetricsCollector>();
		A.CallTo(() => collector.GetSnapshot()).Returns(CreateEmptySnapshot());

		var sut = new PerformanceBenchmark(collector, NullLogger<PerformanceBenchmark>.Instance);

		_ = await sut.RunComprehensiveBenchmarkAsync(CancellationToken.None, iterations: 10).ConfigureAwait(false);

		A.CallTo(() => collector.RecordPipelineExecution(
			A<int>._, A<TimeSpan>._, A<long>._)).MustHaveHappened();
	}
}
