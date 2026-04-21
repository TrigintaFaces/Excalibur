// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Examples.EnhancedStores.ECommerceSample.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Examples.EnhancedStores.ECommerceSample;

/// <summary>
/// Background service that processes inbox entries containing order messages. Demonstrates continuous processing with enhanced inbox store.
/// </summary>
public sealed partial class OrderProcessorHostedService(
	IServiceProvider serviceProvider,
	ILogger<OrderProcessorHostedService> logger) : BackgroundService
{
	private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	private readonly ILogger<OrderProcessorHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ActivitySource _activitySource = new("ECommerce.OrderProcessing");

	public override void Dispose()
	{
		_activitySource?.Dispose();
		base.Dispose();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogOrderProcessorStarted();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				using var scope = _serviceProvider.CreateScope();
				var inboxStore = scope.ServiceProvider.GetRequiredService<IInboxStore>();
				var orderService = scope.ServiceProvider.GetRequiredService<OrderProcessingService>();

				await ProcessPendingOrdersAsync(inboxStore, orderService, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogOrderProcessorError(ex);
				await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
			}

			// Wait before next processing cycle
			await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
		}

		LogOrderProcessorStopped();
	}

	[LoggerMessage(1001, LogLevel.Information, "?? Order processor started - monitoring for pending orders...")]
	private partial void LogOrderProcessorStarted();

	[LoggerMessage(1002, LogLevel.Error, "? Error in order processor - will retry")]
	private partial void LogOrderProcessorError(Exception ex);

	[LoggerMessage(1003, LogLevel.Information, "?? Order processor stopped")]
	private partial void LogOrderProcessorStopped();

	[LoggerMessage(1004, LogLevel.Debug, "?? Inbox processing skipped - GetPendingAsync not available in interface")]
	private partial void LogInboxProcessingSkipped();

	private Task ProcessPendingOrdersAsync(
		IInboxStore inboxStore,
		OrderProcessingService orderService,
		CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("OrderProcessor.ProcessPending");

		// Note: IInboxStore interface doesn't have GetPendingAsync method For this sample, we'll skip inbox processing since the interface
		// is limited In a real implementation, you would need to extend the interface or use a different approach
		LogInboxProcessingSkipped();
		_ = (activity?.SetTag("pending_count", 0));
		return Task.CompletedTask;
	}
}

/// <summary>
/// Background service that processes outbox entries for email notifications. Demonstrates batch processing with enhanced outbox store.
/// </summary>
public sealed partial class NotificationProcessorHostedService(
	IServiceProvider serviceProvider,
	ILogger<NotificationProcessorHostedService> logger) : BackgroundService
{
	private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	private readonly ILogger<NotificationProcessorHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ActivitySource _activitySource = new("ECommerce.OrderProcessing");

	public override void Dispose()
	{
		_activitySource?.Dispose();
		base.Dispose();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogNotificationProcessorStarted();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				using var scope = _serviceProvider.CreateScope();
				var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
				var emailService = scope.ServiceProvider.GetRequiredService<InMemoryEmailService>();

				await ProcessPendingNotificationsAsync(outboxStore, emailService, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogNotificationProcessorError(ex);
			}

			// Wait before next processing cycle
			await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);
		}

		LogNotificationProcessorStopped();
	}

	[LoggerMessage(1001, LogLevel.Information, "?? Notification processor started - monitoring for pending emails...")]
	private partial void LogNotificationProcessorStarted();

	[LoggerMessage(1002, LogLevel.Error, "❌ Error in notification processor - will retry")]
	private partial void LogNotificationProcessorError(Exception ex);

	[LoggerMessage(1003, LogLevel.Information, "?? Notification processor stopped")]
	private partial void LogNotificationProcessorStopped();

	[LoggerMessage(1004, LogLevel.Debug, "?? Processing {StagedCount} staged email notifications")]
	private partial void LogProcessingStagedNotifications(int stagedCount);

	[LoggerMessage(1005, LogLevel.Debug, "?? Sent notification {MessageId}")]
	private partial void LogSentNotification(string messageId);

	[LoggerMessage(1006, LogLevel.Error, "❌ Failed to send notification {MessageId}")]
	private partial void LogFailedToSendNotification(Exception ex, string messageId);

	[LoggerMessage(1007, LogLevel.Information, "?? Sent {SentCount} email notifications")]
	private partial void LogSentNotifications(int sentCount);

	private async Task ProcessPendingNotificationsAsync(
		IOutboxStore outboxStore,
		InMemoryEmailService emailService,
		CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("NotificationProcessor.ProcessPending");

		// This sample simulates reading staged notifications and sending them
		var stagedMessages = await emailService.GetPendingEmailsAsync().ConfigureAwait(false);
		LogProcessingStagedNotifications(stagedMessages.Count());

		var sentCount = 0;
		foreach (var message in stagedMessages)
		{
			try
			{
				var notification = new EmailNotification
				{
					ToEmail = message.ToEmail,
					Subject = message.Subject,
					Body = message.Body,
					NotificationType = message.NotificationType
				};
				await emailService.SendEmailAsync(notification).ConfigureAwait(false);
				LogSentNotification(message.EmailId);
				sentCount++;
			}
			catch (Exception ex)
			{
				LogFailedToSendNotification(ex, message.EmailId);
			}
		}

		if (sentCount > 0)
		{
			LogSentNotifications(sentCount);
		}

		_ = (activity?.SetTag("sent_count", sentCount));
	}
}

/// <summary>
/// Background service that processes scheduled inventory checks.
/// </summary>
public sealed partial class InventoryCheckProcessor(
	IServiceProvider serviceProvider,
	ILogger<InventoryCheckProcessor> logger) : BackgroundService
{
	private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	private readonly ILogger<InventoryCheckProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ActivitySource _activitySource = new("ECommerce.Inventory");

	public override void Dispose()
	{
		_activitySource?.Dispose();
		base.Dispose();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogInventoryCheckProcessorStarted();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				using var scope = _serviceProvider.CreateScope();
				var scheduleStore = scope.ServiceProvider.GetRequiredService<IScheduleStore>();
				var inventoryService = scope.ServiceProvider.GetRequiredService<InventoryService>();

				await ExecuteScheduledChecksAsync(scheduleStore, inventoryService, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogInventoryCheckProcessorError(ex);
			}

			await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
		}

		LogInventoryCheckProcessorStopped();
	}

	private static string ExtractProductId(string scheduleId)
	{
		// Extract product ID from schedule ID format: "inventory-check-{productId}-{timestamp}"
		var parts = scheduleId.Split('-');
		return parts.Length >= 4 ? string.Join("-", parts[2..^1]) : "unknown";
	}

	[LoggerMessage(1001, LogLevel.Information, "?? Inventory check processor started - monitoring for scheduled checks...")]
	private partial void LogInventoryCheckProcessorStarted();

	[LoggerMessage(1002, LogLevel.Error, "❌ Error in inventory check processor - will retry")]
	private partial void LogInventoryCheckProcessorError(Exception ex);

	[LoggerMessage(1003, LogLevel.Information, "?? Inventory check processor stopped")]
	private partial void LogInventoryCheckProcessorStopped();

	[LoggerMessage(1004, LogLevel.Debug, "?? Executing {ReadyCount} scheduled inventory checks")]
	private partial void LogExecutingScheduledChecks(int readyCount);

	[LoggerMessage(1005, LogLevel.Debug, "?? Executed inventory check {ScheduleId}")]
	private partial void LogExecutedInventoryCheck(Guid scheduleId);

	[LoggerMessage(1006, LogLevel.Error, "? Failed to execute inventory check {ScheduleId}")]
	private partial void LogFailedToExecuteInventoryCheck(Exception ex, Guid scheduleId);

	[LoggerMessage(1007, LogLevel.Information, "?? Executed {ExecutedCount} inventory checks")]
	private partial void LogExecutedInventoryChecks(int executedCount);

	private async Task ExecuteScheduledChecksAsync(
		IScheduleStore scheduleStore,
		InventoryService inventoryService,
		CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("InventoryProcessor.ExecuteScheduled");

		// Get schedules ready for execution
		var allSchedules = await scheduleStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
		var readySchedules = allSchedules
			.Where(s => s.NextExecutionUtc.HasValue && s.NextExecutionUtc.Value <= DateTimeOffset.UtcNow)
			.Take(15)
			.ToList();

		if (readySchedules.Count == 0)
		{
			_ = (activity?.SetTag("ready_count", 0));
			return;
		}

		_ = (activity?.SetTag("ready_count", readySchedules.Count));
		LogExecutingScheduledChecks(readySchedules.Count);

		var executedCount = 0;
		foreach (var schedule in readySchedules)
		{
			try
			{
				// Simulate executing the scheduled inventory check In a real scenario, you'd deserialize the payload and execute the check
				var scheduleIdString = schedule.Id.ToString();
				var check = new ScheduledInventoryCheck
				{
					ScheduleId = scheduleIdString,
					ProductId = ExtractProductId(scheduleIdString),
					ExecuteAt = schedule.NextExecutionUtc ?? DateTimeOffset.UtcNow,
					CheckType = "StockLevel"
				};

				await inventoryService.ExecuteInventoryCheckAsync(check).ConfigureAwait(false);

				// Mark as executed (completed)
				await scheduleStore.CompleteAsync(schedule.Id, CancellationToken.None).ConfigureAwait(false);
				executedCount++;

				LogExecutedInventoryCheck(schedule.Id);
			}
			catch (Exception ex)
			{
				LogFailedToExecuteInventoryCheck(ex, schedule.Id);
				// For failed executions, we complete the schedule to remove it
				await scheduleStore.CompleteAsync(schedule.Id, CancellationToken.None).ConfigureAwait(false);
			}
		}

		if (executedCount > 0)
		{
			LogExecutedInventoryChecks(executedCount);
		}

		_ = (activity?.SetTag("executed_count", executedCount));
	}
}

/// <summary>
/// Background service that collects and reports performance metrics. Demonstrates monitoring capabilities across enhanced stores.
/// </summary>
public sealed partial class MetricsReportingService(
	PerformanceMonitor monitor,
	ILogger<MetricsReportingService> logger) : BackgroundService
{
	private readonly PerformanceMonitor _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	private readonly ILogger<MetricsReportingService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogMetricsReportingServiceStarted();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ReportCurrentMetricsAsync().ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogErrorReportingMetrics(ex);
			}

			// Report metrics every 30 seconds
			await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
		}

		LogMetricsReportingServiceStopped();
	}

	[LoggerMessage(1001, LogLevel.Information, "?? Metrics reporting service started")]
	private partial void LogMetricsReportingServiceStarted();

	[LoggerMessage(1002, LogLevel.Error, "? Error reporting metrics")]
	private partial void LogErrorReportingMetrics(Exception ex);

	[LoggerMessage(1003, LogLevel.Information, "?? Metrics reporting service stopped")]
	private partial void LogMetricsReportingServiceStopped();

	[LoggerMessage(1004, LogLevel.Information,
		"?? Performance Report: Orders={OrdersProcessed}, Duplicates={DuplicatesDetected}, Emails={EmailsQueued}, Checks={InventoryChecksScheduled}, AvgTime={AverageProcessingTime:F1}ms, CacheHit={CacheHitRate:P1}")]
	private partial void LogPerformanceReport(int ordersProcessed, int duplicatesDetected, int emailsQueued, int inventoryChecksScheduled,
		double averageProcessingTime, double cacheHitRate);

	private async Task ReportCurrentMetricsAsync()
	{
		var metrics = await _monitor.GetCurrentMetricsAsync().ConfigureAwait(false);

		// Log metrics in structured format for monitoring systems (structured template, no concatenation)
		LogPerformanceReport(
			(int)metrics.OrdersProcessed,
			(int)metrics.DuplicatesDetected,
			(int)metrics.EmailsQueued,
			(int)metrics.InventoryChecksScheduled,
			metrics.AverageProcessingTime,
			metrics.CacheHitRate);

		// Simulate publishing metrics to external monitoring system In production, you might send to Prometheus, CloudWatch, etc.
		await Task.Delay(10).ConfigureAwait(false); // Simulate network call
	}
}
