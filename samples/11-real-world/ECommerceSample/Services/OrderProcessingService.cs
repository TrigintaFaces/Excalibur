// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Examples.ECommerceSample;

/// <summary>How a submitted order was resolved.</summary>
public enum OrderProcessingOutcome
{
	/// <summary>The order was accepted, persisted, and its confirmation staged in the outbox.</summary>
	Processed,

	/// <summary>The inbox already held this order for this handler, so the submission was suppressed.</summary>
	Duplicate,

	/// <summary>The order was rejected by business validation and the inbox entry was marked failed.</summary>
	Failed
}

/// <summary>
/// Processes orders exactly once per handler using the inbox store, and stages the customer confirmation in
/// the outbox as part of the same flow.
/// </summary>
public sealed partial class OrderProcessingService(
	IInboxStore inboxStore,
	InMemoryOrderRepository orderRepository,
	NotificationService notificationService,
	PerformanceMonitor monitor,
	ILogger<OrderProcessingService> logger)
{
	/// <summary>
	/// The deduplication scope orders are keyed under, together with the order identifier.
	/// </summary>
	/// <remarks>
	/// A stable literal, deliberately: deriving it from a type name would silently reopen the deduplication
	/// window the first time the class is renamed, and every entry already written would stop matching.
	/// </remarks>
	public const string HandlerType = "ECommerce.OrderProcessing.OrderHandler";

	/// <summary>The status a successfully processed order is persisted with.</summary>
	public const string ConfirmedStatus = "Confirmed";

	private readonly IInboxStore _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
	private readonly InMemoryOrderRepository _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));

	private readonly NotificationService _notificationService =
		notificationService ?? throw new ArgumentNullException(nameof(notificationService));

	private readonly PerformanceMonitor _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	private readonly ILogger<OrderProcessingService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Processes an order, suppressing a repeat submission of an order this handler has already seen.
	/// </summary>
	public async Task<OrderProcessingOutcome> ProcessOrderAsync(OrderCreated order, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(order);

		using var activity = SampleTelemetry.Orders.StartActivity("OrderProcessing.ProcessOrder");
		_ = activity?.SetTag("order.id", order.OrderId);
		_ = activity?.SetTag("customer.id", order.CustomerId);

		var payload = JsonSerializer.SerializeToUtf8Bytes(order);
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["customerId"] = order.CustomerId,
			["productId"] = order.ProductId,
			["orderDate"] = order.OrderDate.ToString("O", CultureInfo.InvariantCulture),
			["totalAmount"] = (order.Price * order.Quantity).ToString("F2", CultureInfo.InvariantCulture)
		};

		// Creating the entry IS the claim: the store admits one (messageId, handlerType) pair and documents an
		// InvalidOperationException for any later one. Claiming before the work runs -- rather than checking a
		// flag first and acting on it afterwards -- is what makes the suppression hold when two submissions
		// arrive at once.
		try
		{
			_ = await _inboxStore
				.CreateEntryAsync(order.OrderId, HandlerType, nameof(OrderCreated), payload, metadata, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			_monitor.RecordDuplicateSuppressed();
			_ = activity?.SetTag("order.duplicate", true);
			LogDuplicateOrderSuppressed(order.OrderId);
			return OrderProcessingOutcome.Duplicate;
		}

		LogClaimedOrder(order.OrderId);

		try
		{
			var record = BuildOrderRecord(order);
			await _orderRepository.SaveOrderAsync(record).ConfigureAwait(false);

			// Staged inside the claim, so a confirmation exists for every persisted order and for no other.
			await _notificationService.QueueOrderConfirmationAsync(record, cancellationToken).ConfigureAwait(false);

			await _inboxStore.MarkProcessedAsync(order.OrderId, HandlerType, cancellationToken).ConfigureAwait(false);

			_monitor.RecordOrderProcessed();
			LogProcessedOrder(order.OrderId, record.FinalAmount);
			_ = activity?.SetStatus(ActivityStatusCode.Ok);

			return OrderProcessingOutcome.Processed;
		}
		catch (Exception ex)
		{
			// Recording the failure must not replace it. If the inbox write itself throws, the original
			// error is what the operator needs, and a bookkeeping fault must not take the host down on a
			// path whose whole job is to survive a bad order.
			try
			{
				await _inboxStore.MarkFailedAsync(order.OrderId, HandlerType, ex.Message, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception recordingFailure)
			{
				LogFailedToRecordFailure(recordingFailure, order.OrderId);
			}

			_monitor.RecordOrderFailed();
			LogFailedOrder(ex, order.OrderId);
			_ = activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

			return OrderProcessingOutcome.Failed;
		}
	}

	private static decimal DiscountFor(decimal totalAmount) =>
		totalAmount switch
		{
			>= 1000m => 0.15m,
			>= 500m => 0.10m,
			>= 200m => 0.05m,
			_ => 0m
		};

	private static OrderRecord BuildOrderRecord(OrderCreated order)
	{
		if (order.Price <= 0 || order.Quantity <= 0)
		{
			throw new ArgumentException($"Order '{order.OrderId}' is invalid: price and quantity must be positive.", nameof(order));
		}

		var totalAmount = order.Price * order.Quantity;
		var discount = DiscountFor(totalAmount);

		return new OrderRecord
		{
			OrderId = order.OrderId,
			CustomerId = order.CustomerId,
			ProductId = order.ProductId,
			ProductName = order.ProductName,
			UnitPrice = order.Price,
			Quantity = order.Quantity,
			TotalAmount = totalAmount,
			DiscountPercentage = discount,
			FinalAmount = totalAmount * (1 - discount),
			OrderDate = order.OrderDate,
			Status = ConfirmedStatus,
			ProcessedAt = DateTimeOffset.UtcNow
		};
	}

	[LoggerMessage(1001, LogLevel.Information, "Claimed inbox entry for order {OrderId}")]
	private partial void LogClaimedOrder(string orderId);

	[LoggerMessage(1002, LogLevel.Information, "Processed order {OrderId} for {FinalAmount}")]
	private partial void LogProcessedOrder(string orderId, decimal finalAmount);

	[LoggerMessage(1003, LogLevel.Warning, "Duplicate order {OrderId} suppressed by the inbox")]
	private partial void LogDuplicateOrderSuppressed(string orderId);

	[LoggerMessage(1004, LogLevel.Error, "Order {OrderId} failed and was marked failed in the inbox")]
	private partial void LogFailedOrder(Exception ex, string orderId);

	[LoggerMessage(1005, LogLevel.Error, "Could not record the failure of order {OrderId} in the inbox")]
	private partial void LogFailedToRecordFailure(Exception ex, string orderId);
}

/// <summary>
/// Stages customer notifications in the outbox. Nothing here sends mail: the message is handed to the outbox
/// and a separate worker drains it, which is what makes the send survivable and retryable.
/// </summary>
public sealed partial class NotificationService(
	IOutboxStore outboxStore,
	PerformanceMonitor monitor,
	ILogger<NotificationService> logger)
{
	/// <summary>The outbox destination confirmations are staged against.</summary>
	public const string Destination = "email-notifications";

	private readonly IOutboxStore _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
	private readonly PerformanceMonitor _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	private readonly ILogger<NotificationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>Stages the order confirmation for a persisted order.</summary>
	public async Task QueueOrderConfirmationAsync(OrderRecord order, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(order);

		using var activity = SampleTelemetry.Orders.StartActivity("NotificationService.StageConfirmation");
		_ = activity?.SetTag("order.id", order.OrderId);
		_ = activity?.SetTag("email.to", order.CustomerId);

		var notification = new EmailNotification
		{
			ToEmail = order.CustomerId,
			Subject = $"Order {order.OrderId} confirmed",
			Body = string.Create(
				CultureInfo.InvariantCulture,
				$"Thank you. Order {order.OrderId} for {order.Quantity} x {order.ProductName} totalling {order.FinalAmount:F2} is confirmed."),
			NotificationType = "OrderConfirmation"
		};

		var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["orderId"] = order.OrderId,
			["emailTo"] = notification.ToEmail,
			["emailType"] = notification.NotificationType
		};

		// The message id is derived from the order, so re-staging the same confirmation is refused by the
		// store rather than producing a second e-mail.
		var message = new OutboundMessage(nameof(EmailNotification), JsonSerializer.SerializeToUtf8Bytes(notification), Destination, metadata)
		{
			Id = $"order-confirmation-{order.OrderId}"
		};

		await _outboxStore.StageMessageAsync(message, cancellationToken).ConfigureAwait(false);

		_monitor.RecordNotificationStaged();
		LogStagedConfirmation(order.OrderId, notification.ToEmail);
		_ = activity?.SetStatus(ActivityStatusCode.Ok);
	}

	[LoggerMessage(1001, LogLevel.Information, "Staged confirmation for order {OrderId} to {CustomerEmail}")]
	private partial void LogStagedConfirmation(string orderId, string customerEmail);
}

/// <summary>
/// Schedules inventory checks in the schedule store and executes them when the worker hands them back.
/// </summary>
public sealed partial class InventoryService(
	IScheduleStore scheduleStore,
	InMemoryInventoryRepository inventoryRepository,
	PerformanceMonitor monitor,
	ILogger<InventoryService> logger)
{
	/// <summary>The message name scheduled inventory checks are stored under.</summary>
	public const string ScheduledMessageName = nameof(ScheduledInventoryCheck);

	private readonly IScheduleStore _scheduleStore = scheduleStore ?? throw new ArgumentNullException(nameof(scheduleStore));

	private readonly InMemoryInventoryRepository _inventoryRepository =
		inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));

	private readonly PerformanceMonitor _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	private readonly ILogger<InventoryService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>Schedules a stock-level check for a product.</summary>
	public async Task ScheduleInventoryCheckAsync(string productId, DateTimeOffset executeAt, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(productId);

		using var activity = SampleTelemetry.Inventory.StartActivity("InventoryService.ScheduleCheck");
		_ = activity?.SetTag("product.id", productId);

		var check = new ScheduledInventoryCheck
		{
			ProductId = productId,
			CheckType = "StockLevel",
			ExecuteAt = executeAt
		};

		// Everything the worker needs to run the check travels in the message body. The schedule identifier
		// is an opaque handle for completion and carries no business meaning.
		var scheduled = new ScheduledMessage
		{
			Id = Guid.NewGuid(),
			MessageName = ScheduledMessageName,
			MessageBody = JsonSerializer.Serialize(check),
			NextExecutionUtc = executeAt,
			Enabled = true,
			CronExpression = string.Empty
		};

		await _scheduleStore.StoreAsync(scheduled, cancellationToken).ConfigureAwait(false);

		_monitor.RecordInventoryCheckScheduled();
		LogScheduledCheck(productId, executeAt);
		_ = activity?.SetStatus(ActivityStatusCode.Ok);
	}

	/// <summary>Runs a due inventory check and records the result.</summary>
	public async Task ExecuteInventoryCheckAsync(ScheduledInventoryCheck check, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(check);

		using var activity = SampleTelemetry.Inventory.StartActivity("InventoryService.ExecuteCheck");
		_ = activity?.SetTag("product.id", check.ProductId);

		var currentStock = await _inventoryRepository.GetStockLevelAsync(check.ProductId).ConfigureAwait(false);
		var item = await _inventoryRepository.GetInventoryItemAsync(check.ProductId).ConfigureAwait(false);
		var reorderRecommended = item is not null && currentStock <= item.ReorderLevel;

		await _inventoryRepository.UpdateInventoryCheckAsync(
			check.ProductId,
			new InventoryCheckResult
			{
				ProductId = check.ProductId,
				CheckDate = DateTimeOffset.UtcNow,
				StockLevel = currentStock,
				ReorderRecommended = reorderRecommended,
				CheckType = check.CheckType
			}).ConfigureAwait(false);

		_monitor.RecordInventoryCheckExecuted();
		LogExecutedCheck(check.ProductId, currentStock, reorderRecommended);
		_ = activity?.SetStatus(ActivityStatusCode.Ok);
	}

	[LoggerMessage(1001, LogLevel.Information, "Scheduled inventory check for {ProductId} at {ExecuteAt}")]
	private partial void LogScheduledCheck(string productId, DateTimeOffset executeAt);

	[LoggerMessage(1002, LogLevel.Information, "Inventory check for {ProductId}: stock={Stock}, reorder={Reorder}")]
	private partial void LogExecutedCheck(string productId, int stock, bool reorder);
}
