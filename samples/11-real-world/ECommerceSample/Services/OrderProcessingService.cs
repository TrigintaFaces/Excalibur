// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Examples.EnhancedStores.ECommerceSample.Infrastructure;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Examples.EnhancedStores.ECommerceSample;

/// <summary>
/// Order processing service demonstrating enhanced inbox store capabilities including advanced deduplication and hot-path optimizations.
/// </summary>
public sealed partial class OrderProcessingService(
	IInboxStore inboxStore,
	InMemoryOrderRepository orderRepository,
	PerformanceMonitor monitor,
	ILogger<OrderProcessingService> logger) : IDisposable
{
	private static readonly string HandlerType =
		typeof(OrderProcessingService).FullName ?? nameof(OrderProcessingService);

	private readonly IInboxStore _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
	private readonly InMemoryOrderRepository _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
	private readonly PerformanceMonitor _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	private readonly ILogger<OrderProcessingService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ActivitySource _activitySource = new("ECommerce.OrderProcessing");

	/// <summary>
	/// Processes an order using enhanced inbox store for deduplication.
	/// </summary>
	public async Task ProcessOrderAsync(OrderCreated order)
	{
		ArgumentNullException.ThrowIfNull(order);
		using var activity = _activitySource.StartActivity("OrderProcessing.ProcessOrder");
		_ = (activity?.SetTag("order.id", order.OrderId));
		_ = (activity?.SetTag("customer.id", order.CustomerId));

		var stopwatch = Stopwatch.StartNew();

		try
		{
			// Serialize order for storage in inbox
			var orderJson = JsonSerializer.Serialize(order);
			var orderBytes = System.Text.Encoding.UTF8.GetBytes(orderJson);

			var metadata = new Dictionary<string, object>
			{
				["customerId"] = order.CustomerId,
				["productId"] = order.ProductId,
				["orderDate"] = order.OrderDate.ToString("O"),
				["totalAmount"] = (order.Price * order.Quantity).ToString("F2", CultureInfo.InvariantCulture)
			};

			// Create inbox entry - enhanced store will handle deduplication
			var inboxEntry = await _inboxStore.CreateEntryAsync(
				order.OrderId,
				HandlerType,
				nameof(OrderCreated),
				orderBytes,
				metadata,
				CancellationToken.None).ConfigureAwait(false);

			LogCreatedInboxEntry(order.OrderId);

			// Process the order business logic
			await ProcessOrderBusinessLogic(order).ConfigureAwait(false);

			// Mark as processed in inbox
			await _inboxStore.MarkProcessedAsync(order.OrderId, HandlerType, CancellationToken.None)
				.ConfigureAwait(false);

			stopwatch.Stop();
			_monitor.RecordOrderProcessed(stopwatch.Elapsed.TotalMilliseconds);

			LogSuccessfullyProcessedOrder(order.OrderId, stopwatch.Elapsed.TotalMilliseconds);

			_ = (activity?.SetStatus(ActivityStatusCode.Ok));
		}
		catch (InvalidOperationException ex) when (ex.Message.Contains("Duplicate message", StringComparison.Ordinal))
		{
			stopwatch.Stop();
			_monitor.RecordDuplicateDetected();

			LogDuplicateOrderDetected(order.OrderId);
			_ = (activity?.SetTag("duplicate_detected", true));
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			await _inboxStore.MarkFailedAsync(
				order.OrderId,
				HandlerType,
				ex.Message,
				CancellationToken.None).ConfigureAwait(false);

			LogFailedToProcessOrder(ex, order.OrderId);
			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			throw;
		}
	}

	/// <summary>
	/// Disposes of resources used by the service.
	/// </summary>
	public void Dispose()
	{
		_activitySource?.Dispose();
	}

	private static decimal CalculateDiscount(decimal totalAmount) =>
		// Simple discount logic for demonstration
		totalAmount switch
		{
			>= 1000m => 0.15m, // 15% discount for orders over $1000
			>= 500m => 0.10m, // 10% discount for orders over $500
			>= 200m => 0.05m, // 5% discount for orders over $200
			_ => 0m // No discount
		};

	[LoggerMessage(1001, LogLevel.Information, "📦 Created inbox entry for order {OrderId}")]
	private partial void LogCreatedInboxEntry(string orderId);

	[LoggerMessage(1002, LogLevel.Information, "✅ Successfully processed order {OrderId} in {ProcessingTime}ms")]
	private partial void LogSuccessfullyProcessedOrder(string orderId, double processingTime);

	[LoggerMessage(1003, LogLevel.Warning, "🔄 Duplicate order detected and rejected: {OrderId}")]
	private partial void LogDuplicateOrderDetected(string orderId);

	[LoggerMessage(1004, LogLevel.Error, "❌ Failed to process order {OrderId}")]
	private partial void LogFailedToProcessOrder(Exception ex, string orderId);

	[LoggerMessage(1005, LogLevel.Information, "💰 Order {OrderId}: ${TotalAmount:F2} -> ${FinalAmount:F2} (discount: {Discount:P})")]
	private partial void LogOrderPricing(string orderId, decimal totalAmount, decimal finalAmount, decimal discount);

	private async Task ProcessOrderBusinessLogic(OrderCreated order)
	{
		// Simulate order validation
		await Task.Delay(RandomNumberGenerator.GetInt32(10, 50)).ConfigureAwait(false);

		if (order.Price <= 0 || order.Quantity <= 0)
		{
			throw new ArgumentException("Invalid order: price and quantity must be positive");
		}

		// Calculate total and apply business rules
		var totalAmount = order.Price * order.Quantity;
		var discountPercentage = CalculateDiscount(totalAmount);
		var finalAmount = totalAmount * (1 - discountPercentage);

		// Save order to repository
		var orderRecord = new OrderRecord
		{
			OrderId = order.OrderId,
			CustomerId = order.CustomerId,
			ProductId = order.ProductId,
			ProductName = order.ProductName,
			UnitPrice = order.Price,
			Quantity = order.Quantity,
			TotalAmount = totalAmount,
			DiscountPercentage = discountPercentage,
			FinalAmount = finalAmount,
			OrderDate = order.OrderDate,
			Status = "Confirmed",
			ProcessedAt = DateTimeOffset.UtcNow
		};

		await _orderRepository.SaveOrderAsync(orderRecord).ConfigureAwait(false);

		LogOrderPricing(order.OrderId, totalAmount, finalAmount, discountPercentage);
	}
}

/// <summary>
/// Notification service demonstrating enhanced outbox store capabilities including batch staging and exponential backoff.
/// </summary>
public sealed partial class NotificationService(
	IOutboxStore outboxStore,
	PerformanceMonitor monitor,
	ILogger<NotificationService> logger) : IDisposable
{
	private readonly IOutboxStore _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
	private readonly PerformanceMonitor _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	private readonly ILogger<NotificationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ActivitySource _activitySource = new("ECommerce.OrderProcessing");

	/// <summary>
	/// Queues a welcome email using enhanced outbox store for reliable delivery.
	/// </summary>
	public async Task QueueWelcomeEmailAsync(string customerEmail)
	{
		var notification = new EmailNotification
		{
			ToEmail = customerEmail,
			Subject = "Welcome to Our Store!",
			Body = $"Hello {customerEmail}! Thank you for joining our store. Enjoy shopping!",
			NotificationType = "Welcome"
		};

		await QueueEmailAsync(notification).ConfigureAwait(false);
	}

	/// <summary>
	/// Queues a promotional email using enhanced outbox store.
	/// </summary>
	public async Task QueuePromotionalEmailAsync(string customerEmail, string promotion)
	{
		var notification = new EmailNotification
		{
			ToEmail = customerEmail,
			Subject = $"Special Offer: {promotion}",
			Body = $"Hi {customerEmail}! Don't miss out on our latest promotion: {promotion}. Shop now!",
			NotificationType = "Promotional"
		};

		await QueueEmailAsync(notification).ConfigureAwait(false);
	}

	/// <summary>
	/// Disposes of resources used by the service.
	/// </summary>
	public void Dispose()
	{
		_activitySource?.Dispose();
	}

	[LoggerMessage(1001, LogLevel.Information, "📧 Queued {EmailType} email for {CustomerEmail}")]
	private partial void LogQueuedEmail(string emailType, string customerEmail);

	[LoggerMessage(1002, LogLevel.Error, "❌ Failed to queue email for {CustomerEmail}")]
	private partial void LogFailedToQueueEmail(Exception ex, string customerEmail);

	private async Task QueueEmailAsync(EmailNotification notification)
	{
		using var activity = _activitySource.StartActivity("NotificationService.QueueEmail");
		_ = (activity?.SetTag("email.to", notification.ToEmail));
		_ = (activity?.SetTag("email.type", notification.NotificationType));

		try
		{
			var messageId = $"email-{Guid.NewGuid()}";
			var payload = JsonSerializer.SerializeToUtf8Bytes(notification);

			var metadata = new Dictionary<string, object>
			{
				["emailTo"] = notification.ToEmail,
				["emailType"] = notification.NotificationType,
				["queuedAt"] = notification.QueuedAt.ToString("O")
			};

			// Stage message in outbox - enhanced store will handle batching
			var outboundMessage = new OutboundMessage(
				nameof(EmailNotification),
				payload,
				"email-notifications",
				metadata)
			{ Id = messageId };

			await _outboxStore.StageMessageAsync(outboundMessage, CancellationToken.None)
				.ConfigureAwait(false);

			_monitor.RecordEmailQueued();

			LogQueuedEmail(notification.NotificationType, notification.ToEmail);

			_ = (activity?.SetStatus(ActivityStatusCode.Ok));
		}
		catch (Exception ex)
		{
			LogFailedToQueueEmail(ex, notification.ToEmail);
			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			throw;
		}
	}
}

/// <summary>
/// Inventory service demonstrating enhanced schedule store capabilities including duplicate detection and execution time indexing.
/// </summary>
public sealed partial class InventoryService(
	IScheduleStore scheduleStore,
	InMemoryInventoryRepository inventoryRepository,
	PerformanceMonitor monitor,
	ILogger<InventoryService> logger) : IDisposable
{
	private readonly IScheduleStore _scheduleStore = scheduleStore ?? throw new ArgumentNullException(nameof(scheduleStore));

	private readonly InMemoryInventoryRepository _inventoryRepository =
		inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));

	private readonly PerformanceMonitor _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	private readonly ILogger<InventoryService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly ActivitySource _activitySource = new("ECommerce.OrderProcessing");

	/// <summary>
	/// Schedules an inventory check using enhanced schedule store with duplicate detection.
	/// </summary>
	public async Task ScheduleInventoryCheckAsync(string productId, DateTimeOffset executeAt)
	{
		using var activity = _activitySource.StartActivity("InventoryService.ScheduleCheck");
		_ = (activity?.SetTag("product.id", productId));
		_ = (activity?.SetTag("execute.at", executeAt.ToString("O")));

		try
		{
			var scheduleId = $"inventory-check-{productId}-{executeAt:yyyyMMddHHmm}";

			var scheduledCheck = new ScheduledInventoryCheck
			{
				ScheduleId = scheduleId,
				ProductId = productId,
				ExecuteAt = executeAt,
				CheckType = "StockLevel"
			};

			var payload = JsonSerializer.SerializeToUtf8Bytes(scheduledCheck);

			var metadata = new Dictionary<string, object>
			{
				["productId"] = productId,
				["checkType"] = scheduledCheck.CheckType,
				["scheduledAt"] = DateTimeOffset.UtcNow.ToString("O")
			};

			// Schedule using enhanced store - will handle duplicates and indexing
			var scheduledMessage = new ScheduledMessage
			{
				Id = Guid.NewGuid(),
				MessageName = nameof(ScheduledInventoryCheck),
				MessageBody = System.Text.Encoding.UTF8.GetString(payload),
				NextExecutionUtc = executeAt,
				Enabled = true,
				CronExpression = string.Empty
			};

			await _scheduleStore.StoreAsync(scheduledMessage, CancellationToken.None).ConfigureAwait(false);

			_monitor.RecordInventoryCheckScheduled();

			LogScheduledInventoryCheck(productId, executeAt);

			_ = (activity?.SetStatus(ActivityStatusCode.Ok));
		}
		catch (Exception ex)
		{
			LogFailedToScheduleInventoryCheck(ex, productId);
			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			throw;
		}
	}

	/// <summary>
	/// Executes scheduled inventory checks.
	/// </summary>
	public async Task ExecuteInventoryCheckAsync(ScheduledInventoryCheck check)
	{
		ArgumentNullException.ThrowIfNull(check);
		using var activity = _activitySource.StartActivity("InventoryService.ExecuteCheck");
		_ = (activity?.SetTag("product.id", check.ProductId));

		try
		{
			// Simulate inventory check logic
			await Task.Delay(RandomNumberGenerator.GetInt32(100, 500)).ConfigureAwait(false);

			var currentStock = await _inventoryRepository.GetStockLevelAsync(check.ProductId).ConfigureAwait(false);
			var recommendedReorder = currentStock < 10;

			await _inventoryRepository.UpdateInventoryCheckAsync(check.ProductId,
				new InventoryCheckResult
				{
					ProductId = check.ProductId,
					CheckDate = DateTimeOffset.UtcNow,
					StockLevel = currentStock,
					ReorderRecommended = recommendedReorder,
					CheckType = check.CheckType
				}).ConfigureAwait(false);

			LogInventoryCheckCompleted(check.ProductId, currentStock, recommendedReorder);

			_ = (activity?.SetStatus(ActivityStatusCode.Ok));
		}
		catch (Exception ex)
		{
			LogFailedToExecuteInventoryCheck(ex, check.ProductId);
			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			throw;
		}
	}

	/// <summary>
	/// Disposes of resources used by the service.
	/// </summary>
	public void Dispose()
	{
		_activitySource?.Dispose();
	}

	[LoggerMessage(1001, LogLevel.Information, "📅 Scheduled inventory check for product {ProductId} at {ExecuteAt}")]
	private partial void LogScheduledInventoryCheck(string productId, DateTimeOffset executeAt);

	[LoggerMessage(1002, LogLevel.Error, "❌ Failed to schedule inventory check for product {ProductId}")]
	private partial void LogFailedToScheduleInventoryCheck(Exception ex, string productId);

	[LoggerMessage(1003, LogLevel.Information, "📊 Inventory check completed for {ProductId}: Stock={Stock}, Reorder={Reorder}")]
	private partial void LogInventoryCheckCompleted(string productId, int stock, bool reorder);

	[LoggerMessage(1004, LogLevel.Error, "❌ Failed to execute inventory check for product {ProductId}")]
	private partial void LogFailedToExecuteInventoryCheck(Exception ex, string productId);
}
