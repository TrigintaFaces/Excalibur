// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace examples.Handlers;

/// <summary>
///     Sample high-performance batch message handler demonstrating best practices.
/// </summary>
public class SampleHighPerformanceBatchHandler : IMessageBatchHandler
{
	private readonly ILogger<SampleHighPerformanceBatchHandler> _logger;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	///     Initializes a new instance of the <see cref="SampleHighPerformanceBatchHandler" /> class.
	/// </summary>
	/// <param name="logger"> The logger. </param>
	public SampleHighPerformanceBatchHandler(ILogger<SampleHighPerformanceBatchHandler> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		// Pre-configure JSON options for performance
		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false
		};
	}

	/// <summary>
	///     Determines if this handler can process the specified message type.
	/// </summary>
	/// <param name="messageType"> The message type. </param>
	/// <returns> True if the handler can process the message type. </returns>
	public bool CanHandle(string messageType) =>
		messageType.StartsWith("Order", StringComparison.OrdinalIgnoreCase) ||
		messageType.StartsWith("Customer", StringComparison.OrdinalIgnoreCase) ||
		messageType.StartsWith("Product", StringComparison.OrdinalIgnoreCase);

	/// <summary>
	///     Handles a batch of messages with high-performance processing.
	/// </summary>
	/// <param name="messages"> The messages to handle. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	public async Task HandleBatchAsync(IReadOnlyList<ProcessedMessage>? messages)
	{
		if (messages == null || messages.Count == 0)
		{
			return;
		}

		// Group messages by type for optimized processing
		var messageGroups = messages.GroupBy(m => m.MessageType);

		// Process each group in parallel
		var tasks = messageGroups.Select(group => ProcessMessageGroupAsync(group.Key, [.. group]));
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	private async Task ProcessMessageGroupAsync(string messageType, List<ProcessedMessage> messages)
	{
		_logger.LogDebug("Processing {Count} messages of type {Type}", messages.Count, messageType);

		try
		{
			// Route to specific processors based on message type
			switch (messageType.ToLowerInvariant())
			{
				case var type when type.StartsWith("order"):
					await ProcessOrderBatchAsync(messages).ConfigureAwait(false);
					break;

				case var type when type.StartsWith("customer"):
					await ProcessCustomerBatchAsync(messages).ConfigureAwait(false);
					break;

				case var type when type.StartsWith("product"):
					await ProcessProductBatchAsync(messages).ConfigureAwait(false);
					break;

				default:
					_logger.LogWarning("Unknown message type: {Type}", messageType);
					break;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing {Count} messages of type {Type}",
				messages.Count, messageType);
			throw;
		}
	}

	private async Task ProcessOrderBatchAsync(List<ProcessedMessage> messages)
	{
		// Simulate batch processing of orders
		var orders = new List<Order>(messages.Count);

		// Deserialize all messages first
		foreach (var message in messages)
		{
			try
			{
				var order = JsonSerializer.Deserialize<Order>(message.Body, _jsonOptions);
				if (order != null)
				{
					orders.Add(order);
				}
			}
			catch (JsonException ex)
			{
				_logger.LogError(ex, "Failed to deserialize order message: {MessageId}",
					message.Message.MessageId);
			}
		}

		// Process orders in bulk (e.g., batch database insert)
		if (orders.Count > 0)
		{
			await BulkProcessOrdersAsync(orders).ConfigureAwait(false);
		}
	}

	private async Task ProcessCustomerBatchAsync(List<ProcessedMessage> messages)
	{
		// Similar pattern for customer messages
		var customers = new List<Customer>(messages.Count);

		foreach (var message in messages)
		{
			try
			{
				var customer = JsonSerializer.Deserialize<Customer>(message.Body, _jsonOptions);
				if (customer != null)
				{
					customers.Add(customer);
				}
			}
			catch (JsonException ex)
			{
				_logger.LogError(ex, "Failed to deserialize customer message: {MessageId}",
					message.Message.MessageId);
			}
		}

		if (customers.Count > 0)
		{
			await BulkProcessCustomersAsync(customers).ConfigureAwait(false);
		}
	}

	private async Task ProcessProductBatchAsync(List<ProcessedMessage> messages)
	{
		// Similar pattern for product messages
		var products = new List<Product>(messages.Count);

		foreach (var message in messages)
		{
			try
			{
				var product = JsonSerializer.Deserialize<Product>(message.Body, _jsonOptions);
				if (product != null)
				{
					products.Add(product);
				}
			}
			catch (JsonException ex)
			{
				_logger.LogError(ex, "Failed to deserialize product message: {MessageId}",
					message.Message.MessageId);
			}
		}

		if (products.Count > 0)
		{
			await BulkProcessProductsAsync(products).ConfigureAwait(false);
		}
	}

	private async Task BulkProcessOrdersAsync(List<Order> orders)
	{
		// Simulate bulk database operation
		await Task.Delay(10).ConfigureAwait(false); // Simulate I/O

		_logger.LogInformation("Bulk processed {Count} orders", orders.Count);

		// In a real implementation, this would:
		// 1. Use bulk insert with Dapper or EF Core
		// 2. Update cache in batch
		// 3. Publish events in batch
		// 4. Update metrics/analytics
	}

	private async Task BulkProcessCustomersAsync(List<Customer> customers)
	{
		await Task.Delay(10).ConfigureAwait(false); // Simulate I/O
		_logger.LogInformation("Bulk processed {Count} customers", customers.Count);
	}

	private async Task BulkProcessProductsAsync(List<Product> products)
	{
		await Task.Delay(10).ConfigureAwait(false); // Simulate I/O
		_logger.LogInformation("Bulk processed {Count} products", products.Count);
	}

	// Sample domain models
	private class Order
	{
		public string OrderId { get; set; } = string.Empty;
		public string CustomerId { get; set; } = string.Empty;
		public DateTime OrderDate { get; set; }
		public decimal TotalAmount { get; set; }
		public List<OrderItem> Items { get; set; } = [];
	}

	private class OrderItem
	{
		public string ProductId { get; set; } = string.Empty;
		public int Quantity { get; set; }
		public decimal Price { get; set; }
	}

	private class Customer
	{
		public string CustomerId { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public DateTime CreatedDate { get; set; }
	}

	private class Product
	{
		public string ProductId { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Category { get; set; } = string.Empty;
		public decimal Price { get; set; }
		public int StockQuantity { get; set; }
	}
}
