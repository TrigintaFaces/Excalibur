// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Examples.EnhancedStores.ECommerceSample.Infrastructure;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Examples.EnhancedStores.ECommerceSample;

/// <summary>
/// Health check for enhanced stores functionality. Validates that all enhanced stores are operational and performing within acceptable limits.
/// </summary>
public sealed partial class EnhancedStoreHealthCheck(
	IInboxStore inboxStore,
	IOutboxStore outboxStore,
	IScheduleStore scheduleStore,
	ILogger<EnhancedStoreHealthCheck> logger) : IHealthCheck
{
	private static readonly string HandlerType =
		typeof(EnhancedStoreHealthCheck).FullName ?? nameof(EnhancedStoreHealthCheck);

	private readonly IInboxStore _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
	private readonly IOutboxStore _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
	private readonly IScheduleStore _scheduleStore = scheduleStore ?? throw new ArgumentNullException(nameof(scheduleStore));
	private readonly ILogger<EnhancedStoreHealthCheck> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var stopwatch = Stopwatch.StartNew();
			var healthData = new Dictionary<string, object>();

			// Check inbox store health
			var inboxHealth = await CheckInboxStoreHealthAsync(cancellationToken).ConfigureAwait(false);
			healthData["inbox"] = inboxHealth;

			// Check outbox store health
			var outboxHealth = await CheckOutboxStoreHealthAsync(cancellationToken).ConfigureAwait(false);
			healthData["outbox"] = outboxHealth;

			// Check schedule store health
			var scheduleHealth = await CheckScheduleStoreHealthAsync(cancellationToken).ConfigureAwait(false);
			healthData["schedule"] = scheduleHealth;

			stopwatch.Stop();
			healthData["total_check_time_ms"] = stopwatch.Elapsed.TotalMilliseconds;

			// Determine overall health status
			var overallStatus = DetermineOverallStatus(inboxHealth, outboxHealth, scheduleHealth);
			var description = CreateHealthDescription(inboxHealth, outboxHealth, scheduleHealth);

			LogHealthCheckCompleted(stopwatch.Elapsed.TotalMilliseconds, overallStatus);

			return new HealthCheckResult(overallStatus, description, data: healthData);
		}
		catch (Exception ex)
		{
			LogHealthCheckFailed(ex);
			return HealthCheckResult.Unhealthy("Enhanced stores health check failed", ex);
		}
	}

	private static HealthStatus DetermineOverallStatus(
		StoreHealthInfo inbox,
		StoreHealthInfo outbox,
		StoreHealthInfo schedule)
	{
		if (inbox.Status == HealthStatus.Unhealthy ||
			outbox.Status == HealthStatus.Unhealthy ||
			schedule.Status == HealthStatus.Unhealthy)
		{
			return HealthStatus.Unhealthy;
		}

		if (inbox.Status == HealthStatus.Degraded ||
			outbox.Status == HealthStatus.Degraded ||
			schedule.Status == HealthStatus.Degraded)
		{
			return HealthStatus.Degraded;
		}

		return HealthStatus.Healthy;
	}

	private static string CreateHealthDescription(
		StoreHealthInfo inbox,
		StoreHealthInfo outbox,
		StoreHealthInfo schedule)
	{
		var descriptions = new List<string>();

		if (inbox.Status == HealthStatus.Healthy)
		{
			descriptions.Add($"Inbox: OK ({inbox.ResponseTime.TotalMilliseconds:F1}ms)");
		}
		else
		{
			descriptions.Add($"Inbox: {inbox.Status} - {inbox.ErrorMessage}");
		}

		if (outbox.Status == HealthStatus.Healthy)
		{
			descriptions.Add($"Outbox: OK ({outbox.ResponseTime.TotalMilliseconds:F1}ms)");
		}
		else
		{
			descriptions.Add($"Outbox: {outbox.Status} - {outbox.ErrorMessage}");
		}

		if (schedule.Status == HealthStatus.Healthy)
		{
			descriptions.Add($"Schedule: OK ({schedule.ResponseTime.TotalMilliseconds:F1}ms)");
		}
		else
		{
			descriptions.Add($"Schedule: {schedule.Status} - {schedule.ErrorMessage}");
		}

		return string.Join("; ", descriptions);
	}

	[LoggerMessage(1001, LogLevel.Debug, "🏥 Enhanced stores health check completed in {ElapsedMs}ms with status {Status}")]
	private partial void LogHealthCheckCompleted(double elapsedMs, HealthStatus status);

	[LoggerMessage(1002, LogLevel.Error, "❌ Enhanced stores health check failed")]
	private partial void LogHealthCheckFailed(Exception ex);

	private async Task<StoreHealthInfo> CheckInboxStoreHealthAsync(CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();

		try
		{
			// Test basic inbox operations
			var testMessageId = $"health-check-{Guid.NewGuid()}";
			var testPayload = System.Text.Encoding.UTF8.GetBytes("health check");

			// Create test entry
			var testMetadata = new Dictionary<string, object> { ["test"] = "health-check" };
			_ = await _inboxStore.CreateEntryAsync(
				testMessageId,
				HandlerType,
				"HealthCheck",
				testPayload,
				testMetadata,
				cancellationToken).ConfigureAwait(false);

			// Verify retrieval
			var retrieved = await _inboxStore.GetEntryAsync(testMessageId, HandlerType, cancellationToken)
				.ConfigureAwait(false);
			if (retrieved == null)
			{
				return new StoreHealthInfo
				{
					Status = HealthStatus.Unhealthy,
					ResponseTime = stopwatch.Elapsed,
					ErrorMessage = "Test entry could not be retrieved"
				};
			}

			// Mark as processed
			await _inboxStore.MarkProcessedAsync(testMessageId, HandlerType, cancellationToken)
				.ConfigureAwait(false);

			stopwatch.Stop();

			return new StoreHealthInfo { Status = HealthStatus.Healthy, ResponseTime = stopwatch.Elapsed, OperationsPerformed = 3 };
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			return new StoreHealthInfo { Status = HealthStatus.Unhealthy, ResponseTime = stopwatch.Elapsed, ErrorMessage = ex.Message };
		}
	}

	private async Task<StoreHealthInfo> CheckOutboxStoreHealthAsync(CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();

		try
		{
			// Test basic outbox operations
			var testMessageId = $"health-check-{Guid.NewGuid()}";
			var testPayload = System.Text.Encoding.UTF8.GetBytes("health check");

			// Stage test message
			var outboundMessage = new OutboundMessage(
				"HealthCheck",
				testPayload,
				"health-check-destination")
			{ Id = testMessageId };
			await _outboxStore.StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);

			// Get unsent messages to verify staging worked
			var unsentMessages = await _outboxStore.GetUnsentMessagesAsync(10, cancellationToken)
				.ConfigureAwait(false);
			if (!unsentMessages.Any(m => m.Id == testMessageId))
			{
				return new StoreHealthInfo
				{
					Status = HealthStatus.Unhealthy,
					ResponseTime = stopwatch.Elapsed,
					ErrorMessage = "Test message could not be retrieved from unsent messages"
				};
			}

			// Mark as sent
			await _outboxStore.MarkSentAsync(testMessageId, cancellationToken).ConfigureAwait(false);

			stopwatch.Stop();

			return new StoreHealthInfo { Status = HealthStatus.Healthy, ResponseTime = stopwatch.Elapsed, OperationsPerformed = 3 };
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			return new StoreHealthInfo { Status = HealthStatus.Unhealthy, ResponseTime = stopwatch.Elapsed, ErrorMessage = ex.Message };
		}
	}

	private async Task<StoreHealthInfo> CheckScheduleStoreHealthAsync(CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();

		try
		{
			// Test basic schedule operations
			var testScheduleId = $"health-check-{Guid.NewGuid()}";
			var testPayload = System.Text.Encoding.UTF8.GetBytes("health check");
			var executeAt = DateTimeOffset.UtcNow.AddMinutes(1);

			// Store test schedule
			var scheduledMessage = new ScheduledMessage
			{
				Id = Guid.NewGuid(),
				MessageName = "HealthCheck",
				MessageBody = System.Text.Encoding.UTF8.GetString(testPayload),
				NextExecutionUtc = executeAt,
				Enabled = true,
				CronExpression = string.Empty
			};
			await _scheduleStore.StoreAsync(scheduledMessage, CancellationToken.None).ConfigureAwait(false);

			// Get all schedules to verify storage worked
			var allSchedules = await _scheduleStore.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
			if (!allSchedules.Any(s => s.Id == scheduledMessage.Id))
			{
				return new StoreHealthInfo
				{
					Status = HealthStatus.Unhealthy,
					ResponseTime = stopwatch.Elapsed,
					ErrorMessage = "Test schedule could not be retrieved from stored schedules"
				};
			}

			// Mark as completed
			await _scheduleStore.CompleteAsync(scheduledMessage.Id, CancellationToken.None).ConfigureAwait(false);

			stopwatch.Stop();

			return new StoreHealthInfo { Status = HealthStatus.Healthy, ResponseTime = stopwatch.Elapsed, OperationsPerformed = 3 };
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			return new StoreHealthInfo { Status = HealthStatus.Unhealthy, ResponseTime = stopwatch.Elapsed, ErrorMessage = ex.Message };
		}
	}
}

/// <summary>
/// Health check for business logic components. Validates that core business services are operational.
/// </summary>
public sealed partial class BusinessLogicHealthCheck(
	InMemoryOrderRepository orderRepository,
	InMemoryEmailService emailService,
	InMemoryInventoryRepository inventoryRepository,
	ILogger<BusinessLogicHealthCheck> logger) : IHealthCheck
{
	private readonly InMemoryOrderRepository _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
	private readonly InMemoryEmailService _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));

	private readonly InMemoryInventoryRepository _inventoryRepository =
		inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));

	private readonly ILogger<BusinessLogicHealthCheck> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var stopwatch = Stopwatch.StartNew();
			var healthData = new Dictionary<string, object>();

			// Check order repository
			var orderCount = _orderRepository.GetTotalOrderCount();
			healthData["total_orders"] = orderCount;

			// Check email service
			var sentEmailCount = _emailService.GetSentEmailCount();
			var pendingEmailCount = _emailService.GetPendingEmailCount();
			healthData["sent_emails"] = sentEmailCount;
			healthData["pending_emails"] = pendingEmailCount;

			// Check inventory repository
			var allInventory = await _inventoryRepository.GetAllInventoryAsync().ConfigureAwait(false);
			var lowStockItems = await _inventoryRepository.GetLowStockItemsAsync().ConfigureAwait(false);
			healthData["inventory_items"] = allInventory.Count();
			healthData["low_stock_items"] = lowStockItems.Count();

			// Test a simple operation on each component
			var testOrder = new OrderRecord
			{
				OrderId = $"health-check-{Guid.NewGuid()}",
				CustomerId = "health-check@example.com",
				ProductId = "health-check-product",
				ProductName = "Health Check Product",
				UnitPrice = 1.00m,
				Quantity = 1,
				TotalAmount = 1.00m,
				DiscountPercentage = 0,
				FinalAmount = 1.00m,
				OrderDate = DateTimeOffset.UtcNow,
				Status = "HealthCheck"
			};

			await _orderRepository.SaveOrderAsync(testOrder).ConfigureAwait(false);

			stopwatch.Stop();
			healthData["check_duration_ms"] = stopwatch.Elapsed.TotalMilliseconds;

			var description = $"Business logic healthy: {orderCount} orders, {sentEmailCount} emails sent, " +
							  $"{allInventory.Count()} inventory items ({lowStockItems.Count()} low stock)";

			LogBusinessLogicHealthCheckCompleted(stopwatch.Elapsed.TotalMilliseconds);

			return HealthCheckResult.Healthy(description, healthData);
		}
		catch (Exception ex)
		{
			LogBusinessLogicHealthCheckFailed(ex);
			return HealthCheckResult.Unhealthy("Business logic health check failed", ex);
		}
	}

	[LoggerMessage(1001, LogLevel.Debug, "🏥 Business logic health check completed in {ElapsedMs}ms")]
	private partial void LogBusinessLogicHealthCheckCompleted(double elapsedMs);

	[LoggerMessage(1002, LogLevel.Error, "❌ Business logic health check failed")]
	private partial void LogBusinessLogicHealthCheckFailed(Exception ex);
}

/// <summary>
/// Information about the health status of an individual store.
/// </summary>
public sealed class StoreHealthInfo
{
	public HealthStatus Status { get; init; } = HealthStatus.Unhealthy;
	public TimeSpan ResponseTime { get; init; }
	public int OperationsPerformed { get; init; }
	public string? ErrorMessage { get; init; }
}
