// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above.

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Messaging.Abstractions.Inbox;
using Excalibur.Dispatch.Messaging.Abstractions.Outbox;
using Excalibur.Dispatch.Messaging.Abstractions.Scheduling;
using Excalibur.Dispatch.Delivery.Inbox.Enhanced;
using Excalibur.Dispatch.Delivery.Outbox.Enhanced;
using Excalibur.Dispatch.Delivery.Scheduling.Enhanced;
using Excalibur.Dispatch.Observability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Excalibur.Dispatch.Examples.EnhancedStores;

/// <summary>
///     Demonstrates comprehensive observability setup for enhanced stores including
///     OpenTelemetry integration, custom dashboards, alerting, and monitoring.
/// </summary>
/// <remarks>
///     This example shows how to:
///     <list type="bullet">
///         <item>Configure OpenTelemetry for enhanced stores</item>
///         <item>Set up custom metrics collection and dashboards</item>
///         <item>Implement alerting based on store performance</item>
///         <item>Create health checks for enhanced stores</item>
///         <item>Monitor cache performance and deduplication rates</item>
///     </list>
/// </remarks>
public static class ObservabilityExample
{
	/// <summary>
	///     Configures comprehensive observability for enhanced stores in production.
	/// </summary>
	public static IServiceCollection ConfigureEnhancedStoreObservability(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// Configure telemetry provider with enhanced store support
		services.AddDispatchTelemetry(options =>
		{
			options.ServiceName = "Enhanced.Store.Service";
			options.ServiceVersion = "1.0.0";
			options.EnableEnhancedStoreObservability = true;
			options.EnableMetrics = true;
			options.EnableTracing = true;
			options.EnableLogging = true;
		});

		// Configure OpenTelemetry with custom exporters
		services.AddOpenTelemetry()
			.WithTracing(builder => builder
				.AddSource(DispatchTelemetryConstants.ActivitySources.InboxEnhanced)
				.AddSource(DispatchTelemetryConstants.ActivitySources.OutboxEnhanced)
				.AddSource(DispatchTelemetryConstants.ActivitySources.ScheduleEnhanced)
				.AddAspNetCoreInstrumentation()
				.AddHttpClientInstrumentation()
				.AddOtlpExporter(options =>
				{
					options.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"]!);
					options.Headers = configuration["OpenTelemetry:Headers"];
				}))
			.WithMetrics(builder => builder
				.AddMeter(DispatchTelemetryConstants.Meters.InboxEnhanced)
				.AddMeter(DispatchTelemetryConstants.Meters.OutboxEnhanced)
				.AddMeter(DispatchTelemetryConstants.Meters.ScheduleEnhanced)
				.AddMeter("enhanced.store.custom.metrics")
				.AddRuntimeInstrumentation()
				.AddProcessInstrumentation()
				.AddPrometheusExporter()
				.AddOtlpExporter());

		// Register enhanced stores with reliability profile for production
		services.AddEnhancedInboxStore(EnhancedInboxOptions.CreateReliabilityProfile());
		services.AddEnhancedOutboxStore(EnhancedOutboxOptions.CreateReliabilityProfile());
		services.AddEnhancedScheduleStore(EnhancedScheduleOptions.CreateReliabilityProfile());

		// Add custom monitoring services
		services.AddSingleton<IEnhancedStoreMonitor, EnhancedStoreMonitor>();
		services.AddSingleton<IAlertingService, AlertingService>();
		services.AddHostedService<EnhancedStoreHealthChecker>();
		services.AddHostedService<PerformanceAnalyzer>();

		// Add health checks for enhanced stores
		services.AddHealthChecks()
			.AddCheck<InboxStoreHealthCheck>("enhanced-inbox")
			.AddCheck<OutboxStoreHealthCheck>("enhanced-outbox")
			.AddCheck<ScheduleStoreHealthCheck>("enhanced-schedule");

		return services;
	}

	/// <summary>
	///     Creates a dashboard configuration for enhanced store metrics.
	/// </summary>
	public static DashboardConfiguration CreateEnhancedStoreDashboard()
	{
		return new DashboardConfiguration
		{
			Name = "Enhanced Stores Performance",
			Panels = new[]
			{
				new DashboardPanel
				{
					Title = "Message Processing Rate",
					Metrics = new[]
					{
						"rate(dispatch_inbox_messages_processed_total[5m])",
						"rate(dispatch_outbox_messages_staged_total[5m])",
						"rate(dispatch_schedule_schedules_stored_total[5m])"
					},
					Type = PanelType.TimeSeries
				},
				new DashboardPanel
				{
					Title = "Duplicate Detection Rate",
					Metrics = new[]
					{
						"rate(dispatch_inbox_duplicates_detected_total[5m])",
						"dispatch_inbox_duplicates_detected_total / dispatch_inbox_messages_processed_total * 100"
					},
					Type = PanelType.Gauge,
					AlertThreshold = 10.0 // Alert if duplicate rate > 10%
				},
				new DashboardPanel
				{
					Title = "Processing Latency",
					Metrics = new[]
					{
						"histogram_quantile(0.50, dispatch_inbox_processing_duration_seconds_bucket)",
						"histogram_quantile(0.95, dispatch_inbox_processing_duration_seconds_bucket)",
						"histogram_quantile(0.99, dispatch_inbox_processing_duration_seconds_bucket)"
					},
					Type = PanelType.TimeSeries
				},
				new DashboardPanel
				{
					Title = "Cache Performance",
					Metrics = new[]
					{
						"enhanced_store_cache_hit_rate",
						"enhanced_store_cache_miss_rate",
						"enhanced_store_cache_size"
					},
					Type = PanelType.Stat
				},
				new DashboardPanel
				{
					Title = "Pending Messages",
					Metrics = new[]
					{
						"dispatch_inbox_pending_messages",
						"dispatch_outbox_pending_messages",
						"dispatch_schedule_pending_schedules"
					},
					Type = PanelType.Gauge,
					AlertThreshold = 1000.0 // Alert if pending > 1000
				}
			}
		};
	}
}

/// <summary>
///     Custom monitoring service for enhanced stores that tracks performance and alerts on anomalies.
/// </summary>
public interface IEnhancedStoreMonitor
{
	Task<StorePerformanceReport> GeneratePerformanceReportAsync(TimeSpan period);
	Task<IEnumerable<PerformanceAlert>> CheckAlertsAsync();
	void RecordCustomMetric(string name, double value, IDictionary<string, object?> tags);
}

/// <summary>
///     Implementation of enhanced store monitoring with custom metrics and alerting.
/// </summary>
public sealed class EnhancedStoreMonitor : IEnhancedStoreMonitor
{
	private readonly Meter _customMeter;
	private readonly Counter<long> _customOperations;
	private readonly Histogram<double> _cacheHitRate;
	private readonly ObservableGauge<long> _memoryUsage;
	private readonly ILogger<EnhancedStoreMonitor> _logger;
	private readonly IAlertingService _alerting;

	public EnhancedStoreMonitor(
		ILogger<EnhancedStoreMonitor> logger,
		IAlertingService alerting)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_alerting = alerting ?? throw new ArgumentNullException(nameof(alerting));

		_customMeter = new Meter("enhanced.store.custom.metrics", "1.0.0");
		_customOperations = _customMeter.CreateCounter<long>("enhanced_store_custom_operations_total");
		_cacheHitRate = _customMeter.CreateHistogram<double>("enhanced_store_cache_hit_rate");
		_memoryUsage = _customMeter.CreateObservableGauge<long>("enhanced_store_memory_usage_bytes",
			() => GC.GetTotalMemory(false));
	}

	public async Task<StorePerformanceReport> GeneratePerformanceReportAsync(TimeSpan period)
	{
		_logger.LogInformation("Generating performance report for period {Period}", period);

		// Simulate collecting metrics over the specified period
		await Task.Delay(100); // Simulate async work

		var report = new StorePerformanceReport
		{
			Period = period,
			GeneratedAt = DateTimeOffset.UtcNow,
			InboxMetrics = new StoreMetrics
			{
				MessagesProcessed = Random.Shared.Next(10000, 50000),
				DuplicatesDetected = Random.Shared.Next(100, 500),
				AverageProcessingTime = TimeSpan.FromMilliseconds(Random.Shared.Next(50, 200)),
				CacheHitRate = Random.Shared.NextDouble() * 0.3 + 0.7, // 70-100%
				ErrorRate = Random.Shared.NextDouble() * 0.02 // 0-2%
			},
			OutboxMetrics = new StoreMetrics
			{
				MessagesProcessed = Random.Shared.Next(8000, 40000),
				DuplicatesDetected = Random.Shared.Next(50, 200),
				AverageProcessingTime = TimeSpan.FromMilliseconds(Random.Shared.Next(30, 150)),
				CacheHitRate = Random.Shared.NextDouble() * 0.2 + 0.8, // 80-100%
				ErrorRate = Random.Shared.NextDouble() * 0.01 // 0-1%
			},
			ScheduleMetrics = new StoreMetrics
			{
				MessagesProcessed = Random.Shared.Next(5000, 25000),
				DuplicatesDetected = Random.Shared.Next(25, 100),
				AverageProcessingTime = TimeSpan.FromMilliseconds(Random.Shared.Next(20, 100)),
				CacheHitRate = Random.Shared.NextDouble() * 0.25 + 0.75, // 75-100%
				ErrorRate = Random.Shared.NextDouble() * 0.005 // 0-0.5%
			}
		};

		// Record cache hit rates
		_cacheHitRate.Record(report.InboxMetrics.CacheHitRate, new KeyValuePair<string, object?>("store", "inbox"));
		_cacheHitRate.Record(report.OutboxMetrics.CacheHitRate, new KeyValuePair<string, object?>("store", "outbox"));
		_cacheHitRate.Record(report.ScheduleMetrics.CacheHitRate, new KeyValuePair<string, object?>("store", "schedule"));

		_logger.LogInformation("Performance report generated successfully");
		return report;
	}

	public async Task<IEnumerable<PerformanceAlert>> CheckAlertsAsync()
	{
		var alerts = new List<PerformanceAlert>();

		// Generate sample performance report for alerting
		var report = await GeneratePerformanceReportAsync(TimeSpan.FromMinutes(5));

		// Check for various alert conditions
		if (report.InboxMetrics.ErrorRate > 0.05) // > 5% error rate
		{
			alerts.Add(new PerformanceAlert
			{
				Severity = AlertSeverity.Critical,
				Store = "Inbox",
				Metric = "ErrorRate",
				Value = report.InboxMetrics.ErrorRate,
				Threshold = 0.05,
				Message = $"Inbox error rate ({report.InboxMetrics.ErrorRate:P}) exceeds threshold (5%)"
			});
		}

		if (report.InboxMetrics.CacheHitRate < 0.7) // < 70% cache hit rate
		{
			alerts.Add(new PerformanceAlert
			{
				Severity = AlertSeverity.Warning,
				Store = "Inbox",
				Metric = "CacheHitRate",
				Value = report.InboxMetrics.CacheHitRate,
				Threshold = 0.7,
				Message = $"Inbox cache hit rate ({report.InboxMetrics.CacheHitRate:P}) below optimal threshold (70%)"
			});
		}

		if (report.InboxMetrics.AverageProcessingTime > TimeSpan.FromMilliseconds(500))
		{
			alerts.Add(new PerformanceAlert
			{
				Severity = AlertSeverity.Warning,
				Store = "Inbox",
				Metric = "ProcessingTime",
				Value = report.InboxMetrics.AverageProcessingTime.TotalMilliseconds,
				Threshold = 500,
				Message = $"Inbox processing time ({report.InboxMetrics.AverageProcessingTime.TotalMilliseconds}ms) exceeds threshold (500ms)"
			});
		}

		// Process alerts through alerting service
		foreach (var alert in alerts)
		{
			await _alerting.SendAlertAsync(alert);
		}

		return alerts;
	}

	public void RecordCustomMetric(string name, double value, IDictionary<string, object?> tags)
	{
		_customOperations.Add(1, tags.ToArray());
		_logger.LogDebug("Recorded custom metric {MetricName} with value {Value}", name, value);
	}
}

/// <summary>
///     Alerting service for sending notifications about enhanced store performance issues.
/// </summary>
public interface IAlertingService
{
	Task SendAlertAsync(PerformanceAlert alert);
	Task<IEnumerable<PerformanceAlert>> GetActiveAlertsAsync();
}

/// <summary>
///     Implementation of alerting service with multiple notification channels.
/// </summary>
public sealed class AlertingService : IAlertingService
{
	private readonly ILogger<AlertingService> _logger;
	private readonly List<PerformanceAlert> _activeAlerts = new();

	public AlertingService(ILogger<AlertingService> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task SendAlertAsync(PerformanceAlert alert)
	{
		_logger.LogWarning("ALERT: {Severity} - {Store} {Metric}: {Message}",
			alert.Severity, alert.Store, alert.Metric, alert.Message);

		// Add to active alerts if not already present
		if (!_activeAlerts.Any(a => a.Store == alert.Store && a.Metric == alert.Metric))
		{
			_activeAlerts.Add(alert);
		}

		// In a real implementation, this would send notifications via:
		// - Email
		// - Slack/Teams
		// - PagerDuty
		// - SMS
		// - Webhook endpoints

		await Task.Delay(10); // Simulate async notification sending
	}

	public Task<IEnumerable<PerformanceAlert>> GetActiveAlertsAsync()
	{
		// In a real implementation, this would query persistent storage
		return Task.FromResult<IEnumerable<PerformanceAlert>>(_activeAlerts.ToList());
	}
}

/// <summary>
///     Background service that continuously monitors enhanced store health.
/// </summary>
public sealed class EnhancedStoreHealthChecker : BackgroundService
{
	private readonly IEnhancedStoreMonitor _monitor;
	private readonly ILogger<EnhancedStoreHealthChecker> _logger;
	private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

	public EnhancedStoreHealthChecker(
		IEnhancedStoreMonitor monitor,
		ILogger<EnhancedStoreHealthChecker> logger)
	{
		_monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Enhanced store health checker started");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await _monitor.CheckAlertsAsync();
				await Task.Delay(_checkInterval, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				_logger.LogInformation("Enhanced store health checker stopping");
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during health check");
				await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
			}
		}
	}
}

/// <summary>
///     Background service that analyzes enhanced store performance trends.
/// </summary>
public sealed class PerformanceAnalyzer : BackgroundService
{
	private readonly IEnhancedStoreMonitor _monitor;
	private readonly ILogger<PerformanceAnalyzer> _logger;
	private readonly TimeSpan _analysisInterval = TimeSpan.FromMinutes(15);

	public PerformanceAnalyzer(
		IEnhancedStoreMonitor monitor,
		ILogger<PerformanceAnalyzer> logger)
	{
		_monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Performance analyzer started");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var report = await _monitor.GeneratePerformanceReportAsync(TimeSpan.FromMinutes(15));
				AnalyzePerformanceTrends(report);

				await Task.Delay(_analysisInterval, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				_logger.LogInformation("Performance analyzer stopping");
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during performance analysis");
				await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
			}
		}
	}

	private void AnalyzePerformanceTrends(StorePerformanceReport report)
	{
		_logger.LogInformation("Performance Analysis for {Period}:", report.Period);
		_logger.LogInformation("  Inbox: {Messages} messages, {Duplicates} duplicates, {AvgTime}ms avg, {CacheHit:P} cache hit",
			report.InboxMetrics.MessagesProcessed,
			report.InboxMetrics.DuplicatesDetected,
			report.InboxMetrics.AverageProcessingTime.TotalMilliseconds,
			report.InboxMetrics.CacheHitRate);

		_logger.LogInformation("  Outbox: {Messages} messages, {Duplicates} duplicates, {AvgTime}ms avg, {CacheHit:P} cache hit",
			report.OutboxMetrics.MessagesProcessed,
			report.OutboxMetrics.DuplicatesDetected,
			report.OutboxMetrics.AverageProcessingTime.TotalMilliseconds,
			report.OutboxMetrics.CacheHitRate);

		_logger.LogInformation("  Schedule: {Messages} schedules, {Duplicates} duplicates, {AvgTime}ms avg, {CacheHit:P} cache hit",
			report.ScheduleMetrics.MessagesProcessed,
			report.ScheduleMetrics.DuplicatesDetected,
			report.ScheduleMetrics.AverageProcessingTime.TotalMilliseconds,
			report.ScheduleMetrics.CacheHitRate);

		// Record custom metrics for trend analysis
		_monitor.RecordCustomMetric("inbox.messages_per_minute",
			report.InboxMetrics.MessagesProcessed / report.Period.TotalMinutes,
			new Dictionary<string, object?> { ["store"] = "inbox" });

		_monitor.RecordCustomMetric("outbox.messages_per_minute",
			report.OutboxMetrics.MessagesProcessed / report.Period.TotalMinutes,
			new Dictionary<string, object?> { ["store"] = "outbox" });

		_monitor.RecordCustomMetric("schedule.schedules_per_minute",
			report.ScheduleMetrics.MessagesProcessed / report.Period.TotalMinutes,
			new Dictionary<string, object?> { ["store"] = "schedule" });
	}
}

// Supporting data models for observability

public sealed record StorePerformanceReport
{
	public required TimeSpan Period { get; init; }
	public required DateTimeOffset GeneratedAt { get; init; }
	public required StoreMetrics InboxMetrics { get; init; }
	public required StoreMetrics OutboxMetrics { get; init; }
	public required StoreMetrics ScheduleMetrics { get; init; }
}

public sealed record StoreMetrics
{
	public required long MessagesProcessed { get; init; }
	public required long DuplicatesDetected { get; init; }
	public required TimeSpan AverageProcessingTime { get; init; }
	public required double CacheHitRate { get; init; }
	public required double ErrorRate { get; init; }
}

public sealed record PerformanceAlert
{
	public required AlertSeverity Severity { get; init; }
	public required string Store { get; init; }
	public required string Metric { get; init; }
	public required double Value { get; init; }
	public required double Threshold { get; init; }
	public required string Message { get; init; }
	public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public enum AlertSeverity
{
	Info,
	Warning,
	Critical
}

public sealed record DashboardConfiguration
{
	public required string Name { get; init; }
	public required DashboardPanel[] Panels { get; init; }
}

public sealed record DashboardPanel
{
	public required string Title { get; init; }
	public required string[] Metrics { get; init; }
	public required PanelType Type { get; init; }
	public double? AlertThreshold { get; init; }
}

public enum PanelType
{
	TimeSeries,
	Gauge,
	Stat,
	Table
}

// Health check implementations for enhanced stores

public sealed class InboxStoreHealthCheck : IHealthCheck
{
	private readonly IInboxStore _inboxStore;
	private readonly ILogger<InboxStoreHealthCheck> _logger;

	public InboxStoreHealthCheck(IInboxStore inboxStore, ILogger<InboxStoreHealthCheck> logger)
	{
		_inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			// Perform a simple health check operation
			var testMessageId = $"health-check-{Guid.NewGuid()}";
			var isProcessed = await _inboxStore.IsAlreadyProcessedAsync(testMessageId, cancellationToken);

			return HealthCheckResult.Healthy("Enhanced inbox store is responsive");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Enhanced inbox store health check failed");
			return HealthCheckResult.Unhealthy("Enhanced inbox store is not responsive", ex);
		}
	}
}

public sealed class OutboxStoreHealthCheck : IHealthCheck
{
	private readonly IOutboxStore _outboxStore;
	private readonly ILogger<OutboxStoreHealthCheck> _logger;

	public OutboxStoreHealthCheck(IOutboxStore outboxStore, ILogger<OutboxStoreHealthCheck> logger)
	{
		_outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			// Check for pending messages as a health indicator
			var pendingMessages = await _outboxStore.GetPendingMessagesAsync(1, cancellationToken);

			return HealthCheckResult.Healthy("Enhanced outbox store is responsive");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Enhanced outbox store health check failed");
			return HealthCheckResult.Unhealthy("Enhanced outbox store is not responsive", ex);
		}
	}
}

public sealed class ScheduleStoreHealthCheck : IHealthCheck
{
	private readonly IScheduleStore _scheduleStore;
	private readonly ILogger<ScheduleStoreHealthCheck> _logger;

	public ScheduleStoreHealthCheck(IScheduleStore scheduleStore, ILogger<ScheduleStoreHealthCheck> logger)
	{
		_scheduleStore = scheduleStore ?? throw new ArgumentNullException(nameof(scheduleStore));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			// Check for schedules ready for execution as a health indicator
			var readySchedules = await _scheduleStore.GetReadyForExecutionAsync(1, cancellationToken);

			return HealthCheckResult.Healthy("Enhanced schedule store is responsive");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Enhanced schedule store health check failed");
			return HealthCheckResult.Unhealthy("Enhanced schedule store is not responsive", ex);
		}
	}
}