// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode
#pragma warning disable CA2213 // Fields disposed transitively via _sut.Dispose()

using Excalibur.Data.ElasticSearch.Monitoring;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchMonitoringServiceShould : IDisposable
{
	private readonly ElasticsearchMetrics _metrics;
	private readonly ElasticsearchActivitySource _activitySource;
	private readonly ElasticsearchRequestLogger _requestLogger;
	private readonly ElasticsearchPerformanceDiagnostics _perfDiagnostics;
	private readonly ElasticsearchMonitoringService _sut;

	public ElasticsearchMonitoringServiceShould()
	{
		_metrics = new ElasticsearchMetrics();
		_activitySource = new ElasticsearchActivitySource();

		var monitoringOptions = new ElasticsearchMonitoringOptions
		{
			Enabled = true,
			Level = MonitoringLevel.Standard,
			Tracing = new TracingOptions { Enabled = true, RecordRequestResponse = true, RecordResilienceDetails = true },
			RequestLogging = new RequestLoggingOptions { Enabled = true },
			Performance = new PerformanceDiagnosticsOptions { Enabled = true, SamplingRate = 1.0 },
		};

		var options = Microsoft.Extensions.Options.Options.Create(monitoringOptions);

		_requestLogger = new ElasticsearchRequestLogger(
			NullLogger<ElasticsearchRequestLogger>.Instance, options);

		_perfDiagnostics = new ElasticsearchPerformanceDiagnostics(
			NullLogger<ElasticsearchPerformanceDiagnostics>.Instance, options);

		_sut = new ElasticsearchMonitoringService(
			_metrics,
			_activitySource,
			_requestLogger,
			_perfDiagnostics,
			options,
			NullLogger<ElasticsearchMonitoringService>.Instance);
	}

	[Fact]
	public void ThrowWhenMetricsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ElasticsearchMonitoringService(
			null!,
			_activitySource,
			_requestLogger,
			_perfDiagnostics,
			Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions()),
			NullLogger<ElasticsearchMonitoringService>.Instance));
	}

	[Fact]
	public void ThrowWhenActivitySourceIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ElasticsearchMonitoringService(
			_metrics,
			null!,
			_requestLogger,
			_perfDiagnostics,
			Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions()),
			NullLogger<ElasticsearchMonitoringService>.Instance));
	}

	[Fact]
	public void ThrowWhenRequestLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ElasticsearchMonitoringService(
			_metrics,
			_activitySource,
			null!,
			_perfDiagnostics,
			Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions()),
			NullLogger<ElasticsearchMonitoringService>.Instance));
	}

	[Fact]
	public void ThrowWhenPerfDiagnosticsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ElasticsearchMonitoringService(
			_metrics,
			_activitySource,
			_requestLogger,
			null!,
			Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions()),
			NullLogger<ElasticsearchMonitoringService>.Instance));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ElasticsearchMonitoringService(
			_metrics,
			_activitySource,
			_requestLogger,
			_perfDiagnostics,
			null!,
			NullLogger<ElasticsearchMonitoringService>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ElasticsearchMonitoringService(
			_metrics,
			_activitySource,
			_requestLogger,
			_perfDiagnostics,
			Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions()),
			null!));
	}

	[Fact]
	public void StartOperationReturnsContext()
	{
		using var context = _sut.StartOperation("search", new { Query = "test" }, "test-index");
		context.ShouldNotBeNull();
	}

	[Fact]
	public void StartOperationWhenDisabledReturnsNonMonitoringContext()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions { Enabled = false });
		using var sut = new ElasticsearchMonitoringService(
			new ElasticsearchMetrics(),
			new ElasticsearchActivitySource(),
			new ElasticsearchRequestLogger(NullLogger<ElasticsearchRequestLogger>.Instance, options),
			new ElasticsearchPerformanceDiagnostics(NullLogger<ElasticsearchPerformanceDiagnostics>.Instance, options),
			options,
			NullLogger<ElasticsearchMonitoringService>.Instance);

		using var context = sut.StartOperation("search", new { });
		context.ShouldNotBeNull();
	}

	[Fact]
	public void RecordRetryAttemptDoesNotThrow()
	{
		_sut.RecordRetryAttempt(
			"search", 1, 3, TimeSpan.FromMilliseconds(100),
			new TimeoutException("test"), "test-index");
	}

	[Fact]
	public void RecordRetryAttemptWhenDisabledDoesNothing()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions { Enabled = false });
		using var sut = new ElasticsearchMonitoringService(
			new ElasticsearchMetrics(),
			new ElasticsearchActivitySource(),
			new ElasticsearchRequestLogger(NullLogger<ElasticsearchRequestLogger>.Instance, options),
			new ElasticsearchPerformanceDiagnostics(NullLogger<ElasticsearchPerformanceDiagnostics>.Instance, options),
			options,
			NullLogger<ElasticsearchMonitoringService>.Instance);

		sut.RecordRetryAttempt("search", 1, 3, TimeSpan.FromMilliseconds(100),
			new TimeoutException("test"));
	}

	[Fact]
	public void RecordCircuitBreakerStateChangeDoesNotThrow()
	{
		_sut.RecordCircuitBreakerStateChange("closed", "open", "search", "threshold exceeded");
	}

	[Fact]
	public void RecordCircuitBreakerStateChangeWhenDisabledDoesNothing()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions { Enabled = false });
		using var sut = new ElasticsearchMonitoringService(
			new ElasticsearchMetrics(),
			new ElasticsearchActivitySource(),
			new ElasticsearchRequestLogger(NullLogger<ElasticsearchRequestLogger>.Instance, options),
			new ElasticsearchPerformanceDiagnostics(NullLogger<ElasticsearchPerformanceDiagnostics>.Instance, options),
			options,
			NullLogger<ElasticsearchMonitoringService>.Instance);

		sut.RecordCircuitBreakerStateChange("closed", "open");
	}

	[Fact]
	public void RecordPermanentFailureDoesNotThrow()
	{
		_sut.RecordPermanentFailure("search", new TimeoutException("timeout"), "test-index");
	}

	[Fact]
	public void RecordPermanentFailureWhenDisabledDoesNothing()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchMonitoringOptions { Enabled = false });
		using var sut = new ElasticsearchMonitoringService(
			new ElasticsearchMetrics(),
			new ElasticsearchActivitySource(),
			new ElasticsearchRequestLogger(NullLogger<ElasticsearchRequestLogger>.Instance, options),
			new ElasticsearchPerformanceDiagnostics(NullLogger<ElasticsearchPerformanceDiagnostics>.Instance, options),
			options,
			NullLogger<ElasticsearchMonitoringService>.Instance);

		sut.RecordPermanentFailure("search", new TimeoutException("test"));
	}

	[Fact]
	public void GetPerformanceMetricsReturnsResults()
	{
		var metrics = _sut.GetPerformanceMetrics();
		metrics.ShouldNotBeNull();
	}

	[Fact]
	public void ResetPerformanceMetricsClearsAll()
	{
		_sut.ResetPerformanceMetrics();
		var metrics = _sut.GetPerformanceMetrics();
		metrics.Count.ShouldBe(0);
	}

	[Fact]
	public void DisposeDoesNotThrow()
	{
		_sut.Dispose();
	}

	[Fact]
	public void DoubleDisposeDoesNotThrow()
	{
		_sut.Dispose();
		_sut.Dispose();
	}

	public void Dispose()
	{
		_sut.Dispose();
	}
}
