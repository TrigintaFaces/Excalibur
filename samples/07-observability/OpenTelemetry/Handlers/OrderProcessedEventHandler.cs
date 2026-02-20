// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Observability.Metrics;

using OpenTelemetry.Trace;

using OpenTelemetrySample.Messages;

namespace OpenTelemetrySample.Handlers;

/// <summary>
/// Handles order processed events with OpenTelemetry tracing.
/// </summary>
public sealed class OrderProcessedEventHandler : IEventHandler<OrderProcessedEvent>
{
	private readonly ILogger<OrderProcessedEventHandler> _logger;

	public OrderProcessedEventHandler(ILogger<OrderProcessedEventHandler> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task HandleAsync(OrderProcessedEvent eventMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventMessage);

		// Create a child span for handler processing
		using var activity = DispatchActivitySource.Instance.StartActivity("HandleOrderProcessed");
		_ = (activity?.SetTag("order.id", eventMessage.OrderId));
		_ = (activity?.SetTag("handler.type", nameof(OrderProcessedEventHandler)));

		_logger.LogInformation(
			"Handling order {OrderId} for customer {CustomerId}, amount: {Amount:C}",
			eventMessage.OrderId,
			eventMessage.CustomerId,
			eventMessage.Amount);

		try
		{
			// Simulate processing with nested spans
			await SimulateValidationAsync(eventMessage, cancellationToken).ConfigureAwait(false);
			await SimulatePersistenceAsync(eventMessage, cancellationToken).ConfigureAwait(false);
			await SimulateNotificationAsync(eventMessage, cancellationToken).ConfigureAwait(false);

			_ = (activity?.SetStatus(ActivityStatusCode.Ok));
			_logger.LogInformation("Order {OrderId} handled successfully", eventMessage.OrderId);
		}
		catch (Exception ex)
		{
			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			activity?.RecordException(ex);
			_logger.LogError(ex, "Failed to handle order {OrderId}", eventMessage.OrderId);
			throw;
		}
	}

	private static async Task SimulateValidationAsync(OrderProcessedEvent order, CancellationToken cancellationToken)
	{
		using var activity = DispatchActivitySource.Instance.StartActivity("ValidateOrder");
		_ = (activity?.SetTag("validation.type", "business_rules"));

		// Simulate validation work
		await Task.Delay(10, cancellationToken).ConfigureAwait(false);

		_ = (activity?.AddEvent(new ActivityEvent("ValidationPassed")));
	}

	private static async Task SimulatePersistenceAsync(OrderProcessedEvent order, CancellationToken cancellationToken)
	{
		using var activity = DispatchActivitySource.Instance.StartActivity("PersistOrder");
		_ = (activity?.SetTag("db.system", "postgresql"));
		_ = (activity?.SetTag("db.operation", "INSERT"));
		_ = (activity?.SetTag("db.name", "orders"));

		// Simulate database work
		await Task.Delay(25, cancellationToken).ConfigureAwait(false);

		_ = (activity?.AddEvent(new ActivityEvent("OrderPersisted", tags: new ActivityTagsCollection { ["order.id"] = order.OrderId, })));
	}

	private static async Task SimulateNotificationAsync(OrderProcessedEvent order, CancellationToken cancellationToken)
	{
		using var activity = DispatchActivitySource.Instance.StartActivity("SendNotification");
		_ = (activity?.SetTag("notification.type", "email"));
		_ = (activity?.SetTag("notification.recipient", order.CustomerId));

		// Simulate sending notification
		await Task.Delay(15, cancellationToken).ConfigureAwait(false);

		_ = (activity?.AddEvent(new ActivityEvent("NotificationSent")));
	}
}
