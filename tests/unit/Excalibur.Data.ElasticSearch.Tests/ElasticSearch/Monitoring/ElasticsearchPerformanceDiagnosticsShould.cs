// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchPerformanceDiagnosticsShould
{
	private readonly ElasticsearchPerformanceDiagnostics _sut;

	public ElasticsearchPerformanceDiagnosticsShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions
		{
			Performance = new PerformanceDiagnosticsOptions
			{
				Enabled = true,
				SamplingRate = 1.0, // Always sample for tests
				SlowOperationThreshold = TimeSpan.FromSeconds(5),
				TrackMemoryUsage = true,
				AnalyzeQueryPerformance = true,
			},
		});

		_sut = new ElasticsearchPerformanceDiagnostics(
			NullLogger<ElasticsearchPerformanceDiagnostics>.Instance,
			options);
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions());
		Should.Throw<ArgumentNullException>(() =>
			new ElasticsearchPerformanceDiagnostics(null!, options));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ElasticsearchPerformanceDiagnostics(
				NullLogger<ElasticsearchPerformanceDiagnostics>.Instance, null!));
	}

	[Fact]
	public void StartOperationReturnsContext()
	{
		using var context = _sut.StartOperation("search", "test-index");
		context.ShouldNotBeNull();
	}

	[Fact]
	public void StartOperationWhenDisabledReturnsNonSampledContext()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions
		{
			Performance = new PerformanceDiagnosticsOptions { Enabled = false },
		});

		var sut = new ElasticsearchPerformanceDiagnostics(
			NullLogger<ElasticsearchPerformanceDiagnostics>.Instance, options);

		using var context = sut.StartOperation("search");
		context.ShouldNotBeNull();
	}

	[Fact]
	public void GetPerformanceMetricsReturnsEmptyInitially()
	{
		var metrics = _sut.GetPerformanceMetrics();
		metrics.ShouldNotBeNull();
		metrics.Count.ShouldBe(0);
	}

	[Fact]
	public void ResetMetricsClearsAll()
	{
		// Let Dispose() call Complete() — do NOT call Complete() explicitly
		using (_sut.StartOperation("search"))
		{
			// Dispose auto-completes
		}

		_sut.ResetMetrics();

		var metrics = _sut.GetPerformanceMetrics();
		metrics.Count.ShouldBe(0);
	}

	[Fact]
	public void ContextCompleteTracksPerformance()
	{
		// Let Dispose() call Complete() to avoid double-counting
		using (_sut.StartOperation("search", "test-index"))
		{
			// Dispose auto-completes
		}

		var metrics = _sut.GetPerformanceMetrics();
		metrics.ShouldContainKey("search");
		metrics["search"].TotalOperations.ShouldBe(1);
		metrics["search"].OperationType.ShouldBe("search");
	}

	[Fact]
	public void ContextDisposeAutoCompletes()
	{
		var context = _sut.StartOperation("index", "test-index");
		context.Dispose();

		var metrics = _sut.GetPerformanceMetrics();
		metrics.ShouldContainKey("index");
	}

	[Fact]
	public void ContextDoubleDisposeDoesNotDuplicateMetrics()
	{
		var context = _sut.StartOperation("delete");
		context.Dispose();
		context.Dispose(); // Second dispose should be no-op

		var metrics = _sut.GetPerformanceMetrics();
		if (metrics.ContainsKey("delete"))
		{
			metrics["delete"].TotalOperations.ShouldBe(1);
		}
	}

	[Fact]
	public void PerformanceMetricsTracksMultipleOperations()
	{
		// Let Dispose() call Complete() to avoid double-counting
		for (var i = 0; i < 5; i++)
		{
			using var context = _sut.StartOperation("search");
			// Dispose auto-completes
		}

		var metrics = _sut.GetPerformanceMetrics();
		metrics["search"].TotalOperations.ShouldBe(5);
		// SuccessRate is 0% because Dispose()-based completion passes null response,
		// and AnalyzePerformance treats null response as non-success (isValid = response != null)
		metrics["search"].SuccessRate.ShouldBe(0.0);
	}

	[Fact]
	public void PerformanceMetricsTracksDuration()
	{
		using (var context = _sut.StartOperation("search"))
		{
			global::Tests.Shared.Infrastructure.TestTiming.Sleep(10); // Small delay to ensure non-zero duration
			// Dispose auto-completes
		}

		var metrics = _sut.GetPerformanceMetrics();
		metrics["search"].AverageDurationMs.ShouldBeGreaterThan(0);
		metrics["search"].MinDurationMs.ShouldBeGreaterThan(0);
		metrics["search"].MaxDurationMs.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void PerformanceMetricsHasLastUpdatedTimestamp()
	{
		var lowerBound = DateTimeOffset.UtcNow;

		using (_sut.StartOperation("search"))
		{
			// Dispose auto-completes
		}

		var metrics = _sut.GetPerformanceMetrics();
		var upperBound = DateTimeOffset.UtcNow;
		metrics["search"].LastUpdated.ShouldBeGreaterThanOrEqualTo(lowerBound);
		metrics["search"].LastUpdated.ShouldBeLessThanOrEqualTo(upperBound);
	}

	[Fact]
	public void MemoryUsageInfoHasCorrectDefaults()
	{
		var info = new ElasticsearchPerformanceDiagnostics.MemoryUsageInfo();
		info.AllocatedBytes.ShouldBe(0);
		info.Gen0Collections.ShouldBe(0);
		info.Gen1Collections.ShouldBe(0);
		info.Gen2Collections.ShouldBe(0);
	}

	[Fact]
	public void MemoryUsageInfoCanSetProperties()
	{
		var info = new ElasticsearchPerformanceDiagnostics.MemoryUsageInfo
		{
			AllocatedBytes = 1024,
			Gen0Collections = 5,
			Gen1Collections = 2,
			Gen2Collections = 1,
		};

		info.AllocatedBytes.ShouldBe(1024);
		info.Gen0Collections.ShouldBe(5);
		info.Gen1Collections.ShouldBe(2);
		info.Gen2Collections.ShouldBe(1);
	}
}
