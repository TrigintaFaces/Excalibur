// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Examples.EnhancedStores.ECommerceSample.Infrastructure;

/// <summary>
/// In-memory order repository for sample application.
/// </summary>
public sealed class InMemoryOrderRepository(ILogger<InMemoryOrderRepository> logger)
{
	private static readonly Action<ILogger, string, Exception?> LogOrderSaved =
		LoggerMessage.Define<string>(
			LogLevel.Debug,
			new EventId(1, "OrderSaved"),
			"💾 Saved order {OrderId} to repository");

	private readonly ConcurrentDictionary<string, OrderRecord> _orders = new();
	private readonly ILogger<InMemoryOrderRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public Task SaveOrderAsync(OrderRecord order)
	{
		ArgumentNullException.ThrowIfNull(order);
		_ = _orders.AddOrUpdate(order.OrderId, order, (_, _) => order);
		LogOrderSaved(_logger, order.OrderId, null);
		return Task.CompletedTask;
	}

	public Task<OrderRecord?> GetOrderAsync(string orderId)
	{
		_ = _orders.TryGetValue(orderId, out var order);
		return Task.FromResult(order);
	}

	public Task<IEnumerable<OrderRecord>> GetOrdersByCustomerAsync(string customerId)
	{
		var customerOrders = _orders.Values
			.Where(o => o.CustomerId == customerId)
			.OrderByDescending(o => o.OrderDate)
			.AsEnumerable();

		return Task.FromResult(customerOrders);
	}

	public Task<IEnumerable<OrderRecord>> GetAllOrdersAsync() => Task.FromResult(_orders.Values.AsEnumerable());

	public int GetTotalOrderCount() => _orders.Count;
}

/// <summary>
/// In-memory email service for sample application.
/// </summary>
public sealed class InMemoryEmailService(ILogger<InMemoryEmailService> logger)
{
	private static readonly Action<ILogger, string, string, string, Exception?> LogEmailSent =
		LoggerMessage.Define<string, string, string>(
			LogLevel.Information,
			new EventId(1, "EmailSent"),
			"📤 Sent {EmailType} email to {CustomerEmail}: {Subject}");

	private static readonly Action<ILogger, string, string, Exception?> LogEmailQueued =
		LoggerMessage.Define<string, string>(
			LogLevel.Debug,
			new EventId(2, "EmailQueued"),
			"📬 Queued email for {CustomerEmail}: {Subject}");

	private readonly ConcurrentQueue<EmailRecord> _sentEmails = new();
	private readonly ConcurrentQueue<EmailRecord> _pendingEmails = new();
	private readonly ILogger<InMemoryEmailService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	public async Task SendEmailAsync(EmailNotification notification)
	{
		ArgumentNullException.ThrowIfNull(notification);
		// Simulate email sending delay
		await Task.Delay(RandomNumberGenerator.GetInt32(50, 200)).ConfigureAwait(false);

		var emailRecord = new EmailRecord
		{
			EmailId = Guid.NewGuid().ToString(),
			ToEmail = notification.ToEmail,
			Subject = notification.Subject,
			Body = notification.Body,
			NotificationType = notification.NotificationType,
			SentAt = DateTimeOffset.UtcNow,
			Status = "Sent"
		};

		_sentEmails.Enqueue(emailRecord);
		LogEmailSent(_logger, notification.NotificationType, notification.ToEmail, notification.Subject, null);
	}

	public Task QueueEmailAsync(EmailNotification notification)
	{
		ArgumentNullException.ThrowIfNull(notification);
		var emailRecord = new EmailRecord
		{
			EmailId = Guid.NewGuid().ToString(),
			ToEmail = notification.ToEmail,
			Subject = notification.Subject,
			Body = notification.Body,
			NotificationType = notification.NotificationType,
			SentAt = DateTimeOffset.UtcNow,
			Status = "Queued"
		};

		_pendingEmails.Enqueue(emailRecord);
		LogEmailQueued(_logger, notification.ToEmail, notification.Subject, null);
		return Task.CompletedTask;
	}

	public Task<IEnumerable<EmailRecord>> GetSentEmailsAsync() => Task.FromResult(_sentEmails.AsEnumerable());

	public Task<IEnumerable<EmailRecord>> GetPendingEmailsAsync() => Task.FromResult(_pendingEmails.AsEnumerable());

	public async Task ProcessPendingEmailsAsync()
	{
		var emailsToProcess = new List<EmailRecord>();

		// Dequeue up to 10 pending emails
		for (var i = 0; i < 10 && _pendingEmails.TryDequeue(out var email); i++)
		{
			emailsToProcess.Add(email);
		}

		// Process emails in batch
		var processTasks = emailsToProcess.Select(async email =>
		{
			var notification = new EmailNotification
			{
				ToEmail = email.ToEmail,
				Subject = email.Subject,
				Body = email.Body,
				NotificationType = email.NotificationType
			};

			await SendEmailAsync(notification).ConfigureAwait(false);
		});

		await Task.WhenAll(processTasks).ConfigureAwait(false);
	}

	public int GetSentEmailCount() => _sentEmails.Count;

	public int GetPendingEmailCount() => _pendingEmails.Count;
}

/// <summary>
/// In-memory inventory repository for sample application.
/// </summary>
public sealed class InMemoryInventoryRepository
{
	private static readonly Action<ILogger, string, int, Exception?> LogInventoryCheckUpdated =
		LoggerMessage.Define<string, int>(
			LogLevel.Debug,
			new EventId(1, "InventoryCheckUpdated"),
			"📊 Updated inventory check for {ProductId}: Stock={Stock}");

	private static readonly Action<ILogger, int, Exception?> LogInventoryInitialized =
		LoggerMessage.Define<int>(
			LogLevel.Information,
			new EventId(2, "InventoryInitialized"),
			"📦 Initialized inventory with {ProductCount} products");

	private readonly ConcurrentDictionary<string, InventoryItem> _inventory = new();
	private readonly ConcurrentDictionary<string, List<InventoryCheckResult>> _checkHistory = new();
	private readonly ILogger<InMemoryInventoryRepository> _logger;

	public InMemoryInventoryRepository(ILogger<InMemoryInventoryRepository> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		InitializeInventory();
	}

	public Task<int> GetStockLevelAsync(string productId)
	{
		if (_inventory.TryGetValue(productId, out var item))
		{
			// Simulate stock fluctuation
			var fluctuation = RandomNumberGenerator.GetInt32(-2, 3);
			var currentStock = Math.Max(0, item.StockLevel + fluctuation);

			// Update the stock level
			item.StockLevel = currentStock;

			return Task.FromResult(currentStock);
		}

		return Task.FromResult(0);
	}

	public Task UpdateInventoryCheckAsync(string productId, InventoryCheckResult checkResult)
	{
		ArgumentNullException.ThrowIfNull(checkResult);
		_ = _checkHistory.AddOrUpdate(
			productId,
			[checkResult],
			(_, existing) =>
			{
				existing.Add(checkResult);
				// Keep last 50 check results
				if (existing.Count > 50)
				{
					existing.RemoveAt(0);
				}

				return existing;
			});

		LogInventoryCheckUpdated(_logger, productId, checkResult.StockLevel, null);

		return Task.CompletedTask;
	}

	public Task<InventoryItem?> GetInventoryItemAsync(string productId)
	{
		_ = _inventory.TryGetValue(productId, out var item);
		return Task.FromResult(item);
	}

	public Task<IEnumerable<InventoryItem>> GetAllInventoryAsync() => Task.FromResult(_inventory.Values.AsEnumerable());

	public Task<IEnumerable<InventoryCheckResult>> GetCheckHistoryAsync(string productId)
	{
		if (_checkHistory.TryGetValue(productId, out var history))
		{
			return Task.FromResult(history.AsEnumerable());
		}

		return Task.FromResult(Enumerable.Empty<InventoryCheckResult>());
	}

	public Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync()
	{
		var lowStockItems = _inventory.Values
			.Where(static item => item.StockLevel <= item.ReorderLevel);

		return Task.FromResult(lowStockItems);
	}

	private void InitializeInventory()
	{
		var products = new[]
		{
			new InventoryItem
			{
				ProductId = "laptop-pro-15",
				ProductName = """
				              Laptop Pro 15"
				              """,
				StockLevel = 25,
				ReorderLevel = 5
			},
			new InventoryItem { ProductId = "wireless-mouse", ProductName = "Wireless Gaming Mouse", StockLevel = 150, ReorderLevel = 20 },
			new InventoryItem
			{
				ProductId = "mechanical-keyboard", ProductName = "Mechanical Keyboard", StockLevel = 75, ReorderLevel = 15
			},
			new InventoryItem { ProductId = "usb-c-hub", ProductName = "USB-C Hub 8-in-1", StockLevel = 40, ReorderLevel = 10 },
			new InventoryItem { ProductId = "monitor-4k-27", ProductName = "27\" 4K Monitor", StockLevel = 12, ReorderLevel = 3 }
		};

		foreach (var product in products)
		{
			_ = _inventory.TryAdd(product.ProductId, product);
		}

		LogInventoryInitialized(_logger, products.Length, null);
	}
}

// Data model records for the repositories

public sealed record OrderRecord
{
	public required string OrderId { get; init; }
	public required string CustomerId { get; init; }
	public required string ProductId { get; init; }
	public required string ProductName { get; init; }
	public required decimal UnitPrice { get; init; }
	public required int Quantity { get; init; }
	public required decimal TotalAmount { get; init; }
	public required decimal DiscountPercentage { get; init; }
	public required decimal FinalAmount { get; init; }
	public required DateTimeOffset OrderDate { get; init; }
	public required string Status { get; init; }
	public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record EmailRecord
{
	public required string EmailId { get; init; }
	public required string ToEmail { get; init; }
	public required string Subject { get; init; }
	public required string Body { get; init; }
	public required string NotificationType { get; init; }
	public required DateTimeOffset SentAt { get; init; }
	public required string Status { get; init; }
}

public sealed class InventoryItem
{
	public required string ProductId { get; init; }
	public required string ProductName { get; init; }
	public int StockLevel { get; set; }
	public required int ReorderLevel { get; init; }
	public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record InventoryCheckResult
{
	public required string ProductId { get; init; }
	public required DateTimeOffset CheckDate { get; init; }
	public required int StockLevel { get; init; }
	public required bool ReorderRecommended { get; init; }
	public required string CheckType { get; init; }
}
