// Copyright (c) Nexus Dynamics. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Framework;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Triggers;
using Microsoft.Extensions.Logging;

namespace Examples.CloudNative.Serverless.GoogleCloudFunctions.FirestoreTriggers
{
	/// <summary>
	/// Example Firestore function that tracks order status changes.
	/// </summary>
	public class OrderTrackingFunction : FirestoreFunction<Order>
	{
		private readonly ILogger<OrderTrackingFunction> _logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="OrderTrackingFunction"/> class.
		/// </summary>
		public OrderTrackingFunction()
		{
			_logger = GetLogger().AsGeneric<OrderTrackingFunction>();
		}

		/// <summary>
		/// Configures the trigger for order tracking.
		/// </summary>
		protected override FirestoreTriggerOptions ConfigureTriggerOptions()
		{
			return new FirestoreTriggerOptions
			{
				// Only process documents in the orders collection
				AllowedCollections = new() { "orders" },

				// Track field changes to monitor status updates
				TrackFieldChanges = true,

				// Set path template to extract order ID
				PathTemplate = "orders/{orderId}",

				// Only process creates and updates
				AllowedChangeTypes = new()
 {
 FirestoreChangeType.Create,
 FirestoreChangeType.Update
 }
			};
		}

		/// <summary>
		/// Processes typed order changes.
		/// </summary>
		protected override async Task ProcessTypedChangeAsync(
		Order? before,
		Order? after,
		FirestoreChangeType changeType,
		string documentId,
		GoogleCloudFunctionExecutionContext context,
		CancellationToken cancellationToken)
		{
			switch (changeType)
			{
				case FirestoreChangeType.Create:
					await HandleOrderCreated(after!, documentId, context, cancellationToken);
					break;

				case FirestoreChangeType.Update:
					await HandleOrderUpdated(before!, after!, documentId, context, cancellationToken);
					break;
			}
		}

		private async Task HandleOrderCreated(
		Order order,
		string orderId,
		GoogleCloudFunctionExecutionContext context,
		CancellationToken cancellationToken)
		{
			_logger.LogInformation("New order created: {OrderId}, Customer: {CustomerId}, Total: ${Total:F2}",
			orderId, order.CustomerId, order.TotalAmount);

			context.TrackMetric("orders.created", 1);
			context.TrackMetric("orders.value", order.TotalAmount);
			context.TrackMetric($"orders.paymentMethod.{order.PaymentMethod}", 1);

			// Send order confirmation
			await SendOrderConfirmation(order, orderId, cancellationToken);

			// Initialize order tracking
			await InitializeOrderTracking(orderId, order.CustomerId, cancellationToken);

			// Process payment if auto-charge is enabled
			if (order.PaymentMethod == "saved_card" && order.Status == OrderStatus.Pending)
			{
				await ProcessPayment(orderId, order.TotalAmount, cancellationToken);
			}

			// Track order source
			if (!string.IsNullOrEmpty(order.Source))
			{
				context.TrackMetric($"orders.source.{order.Source}", 1);
			}
		}

		private async Task HandleOrderUpdated(
		Order before,
		Order after,
		string orderId,
		GoogleCloudFunctionExecutionContext context,
		CancellationToken cancellationToken)
		{
			context.TrackMetric("orders.updated", 1);

			// Check for status change
			if (before.Status != after.Status)
			{
				_logger.LogInformation("Order {OrderId} status changed from {OldStatus} to {NewStatus}",
				orderId, before.Status, after.Status);

				await HandleStatusChange(orderId, before.Status, after.Status, after, context, cancellationToken);
				context.TrackMetric($"orders.status.{after.Status}", 1);
			}

			// Check for shipping update
			if (before.ShippingInfo?.TrackingNumber != after.ShippingInfo?.TrackingNumber &&
			!string.IsNullOrEmpty(after.ShippingInfo?.TrackingNumber))
			{
				_logger.LogInformation("Order {OrderId} shipped with tracking: {TrackingNumber}",
				orderId, after.ShippingInfo.TrackingNumber);

				await SendShippingNotification(orderId, after, cancellationToken);
				context.TrackMetric("orders.shipped", 1);
			}

			// Check for refund
			if (!before.IsRefunded && after.IsRefunded)
			{
				await ProcessRefund(orderId, after.RefundAmount ?? 0, cancellationToken);
				context.TrackMetric("orders.refunded", 1);
				context.TrackMetric("orders.refund.amount", after.RefundAmount ?? 0);
			}
		}

		private async Task HandleStatusChange(
		string orderId,
		OrderStatus oldStatus,
		OrderStatus newStatus,
		Order order,
		GoogleCloudFunctionExecutionContext context,
		CancellationToken cancellationToken)
		{
			// Send status update notification
			await SendStatusUpdateNotification(orderId, order.CustomerId, oldStatus, newStatus, cancellationToken);

			// Handle specific status transitions
			switch ((oldStatus, newStatus))
			{
				case (OrderStatus.Pending, OrderStatus.Processing):
					await NotifyWarehouse(orderId, order.Items, cancellationToken);
					break;

				case (OrderStatus.Processing, OrderStatus.Shipped):
					await UpdateInventory(order.Items, cancellationToken);
					await ChargeShipping(orderId, order.ShippingInfo?.Cost ?? 0, cancellationToken);
					break;

				case (OrderStatus.Shipped, OrderStatus.Delivered):
					await RecordDelivery(orderId, DateTime.UtcNow, cancellationToken);
					await SendReviewRequest(orderId, order.CustomerId, cancellationToken);
					break;

				case (_, OrderStatus.Cancelled):
					await HandleCancellation(orderId, order, cancellationToken);
					break;
			}

			// Track status transition time
			var transitionKey = $"orders.transition.{oldStatus}_to_{newStatus}";
			context.TrackMetric(transitionKey, 1);
		}

		// Helper methods (simulated operations)
		private Task SendOrderConfirmation(Order order, string orderId, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Sending order confirmation for {OrderId}", orderId);
			return Task.Delay(100, cancellationToken);
		}

		private Task InitializeOrderTracking(string orderId, string customerId, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Initializing tracking for order {OrderId}", orderId);
			return Task.Delay(50, cancellationToken);
		}

		private Task ProcessPayment(string orderId, decimal amount, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Processing payment of ${Amount:F2} for order {OrderId}", amount, orderId);
			return Task.Delay(200, cancellationToken);
		}

		private Task SendStatusUpdateNotification(string orderId, string customerId, OrderStatus oldStatus, OrderStatus newStatus, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Notifying customer {CustomerId} of status change for order {OrderId}", customerId, orderId);
			return Task.Delay(100, cancellationToken);
		}

		private Task NotifyWarehouse(string orderId, List<OrderItem> items, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Notifying warehouse about order {OrderId} with {ItemCount} items", orderId, items.Count);
			return Task.Delay(150, cancellationToken);
		}

		private Task UpdateInventory(List<OrderItem> items, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Updating inventory for {ItemCount} items", items.Count);
			return Task.Delay(100, cancellationToken);
		}

		private Task ChargeShipping(string orderId, decimal shippingCost, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Charging shipping ${Cost:F2} for order {OrderId}", shippingCost, orderId);
			return Task.Delay(100, cancellationToken);
		}

		private Task SendShippingNotification(string orderId, Order order, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Sending shipping notification for order {OrderId}", orderId);
			return Task.Delay(100, cancellationToken);
		}

		private Task RecordDelivery(string orderId, DateTime deliveryTime, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Recording delivery for order {OrderId} at {DeliveryTime}", orderId, deliveryTime);
			return Task.Delay(50, cancellationToken);
		}

		private Task SendReviewRequest(string orderId, string customerId, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Sending review request for order {OrderId} to customer {CustomerId}", orderId, customerId);
			return Task.Delay(100, cancellationToken);
		}

		private Task HandleCancellation(string orderId, Order order, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Handling cancellation for order {OrderId}", orderId);
			return Task.Delay(200, cancellationToken);
		}

		private Task ProcessRefund(string orderId, decimal refundAmount, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Processing refund of ${Amount:F2} for order {OrderId}", refundAmount, orderId);
			return Task.Delay(300, cancellationToken);
		}
	}

	/// <summary>
	/// Order model.
	/// </summary>
	public class Order
	{
		/// <summary>
		/// Gets or sets the order ID.
		/// </summary>
		public string? Id { get; set; }

		/// <summary>
		/// Gets or sets the customer ID.
		/// </summary>
		public string CustomerId { get; set; } = null!;

		/// <summary>
		/// Gets or sets the order status.
		/// </summary>
		public OrderStatus Status { get; set; } = OrderStatus.Pending;

		/// <summary>
		/// Gets or sets the order items.
		/// </summary>
		public List<OrderItem> Items { get; set; } = new();

		/// <summary>
		/// Gets or sets the total amount.
		/// </summary>
		public decimal TotalAmount { get; set; }

		/// <summary>
		/// Gets or sets the payment method.
		/// </summary>
		public string PaymentMethod { get; set; } = null!;

		/// <summary>
		/// Gets or sets the shipping information.
		/// </summary>
		public ShippingInfo? ShippingInfo { get; set; }

		/// <summary>
		/// Gets or sets the order source.
		/// </summary>
		public string? Source { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the order is refunded.
		/// </summary>
		public bool IsRefunded { get; set; }

		/// <summary>
		/// Gets or sets the refund amount.
		/// </summary>
		public decimal? RefundAmount { get; set; }

		/// <summary>
		/// Gets or sets the created timestamp.
		/// </summary>
		public DateTime CreatedAt { get; set; }

		/// <summary>
		/// Gets or sets the updated timestamp.
		/// </summary>
		public DateTime UpdatedAt { get; set; }
	}

	/// <summary>
	/// Order item model.
	/// </summary>
	public class OrderItem
	{
		/// <summary>
		/// Gets or sets the product ID.
		/// </summary>
		public string ProductId { get; set; } = null!;

		/// <summary>
		/// Gets or sets the product name.
		/// </summary>
		public string ProductName { get; set; } = null!;

		/// <summary>
		/// Gets or sets the quantity.
		/// </summary>
		public int Quantity { get; set; }

		/// <summary>
		/// Gets or sets the unit price.
		/// </summary>
		public decimal UnitPrice { get; set; }

		/// <summary>
		/// Gets or sets the total price.
		/// </summary>
		public decimal TotalPrice => Quantity * UnitPrice;
	}

	/// <summary>
	/// Shipping information.
	/// </summary>
	public class ShippingInfo
	{
		/// <summary>
		/// Gets or sets the shipping address.
		/// </summary>
		public string Address { get; set; } = null!;

		/// <summary>
		/// Gets or sets the shipping method.
		/// </summary>
		public string Method { get; set; } = null!;

		/// <summary>
		/// Gets or sets the shipping cost.
		/// </summary>
		public decimal Cost { get; set; }

		/// <summary>
		/// Gets or sets the tracking number.
		/// </summary>
		public string? TrackingNumber { get; set; }

		/// <summary>
		/// Gets or sets the carrier.
		/// </summary>
		public string? Carrier { get; set; }
	}

	/// <summary>
	/// Order status enumeration.
	/// </summary>
	public enum OrderStatus
	{
		/// <summary>
		/// Order is pending.
		/// </summary>
		Pending,

		/// <summary>
		/// Order is being processed.
		/// </summary>
		Processing,

		/// <summary>
		/// Order has been shipped.
		/// </summary>
		Shipped,

		/// <summary>
		/// Order has been delivered.
		/// </summary>
		Delivered,

		/// <summary>
		/// Order has been cancelled.
		/// </summary>
		Cancelled
	}
}