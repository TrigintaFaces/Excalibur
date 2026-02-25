// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

using AzureFunctionsSample.Messages;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureFunctionsSample.Functions;

/// <summary>
/// Queue-triggered Azure Function for processing order events.
/// Demonstrates Dispatch messaging integration with Azure Storage Queues.
/// </summary>
public sealed class QueueOrderProcessor
{
	private readonly IDispatcher _dispatcher;
	private readonly ILogger<QueueOrderProcessor> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="QueueOrderProcessor"/> class.
	/// </summary>
	/// <param name="dispatcher">The Dispatch dispatcher.</param>
	/// <param name="logger">The logger instance.</param>
	public QueueOrderProcessor(IDispatcher dispatcher, ILogger<QueueOrderProcessor> logger)
	{
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Processes order events from Azure Storage Queue.
	/// </summary>
	/// <param name="message">The queue message containing the order event.</param>
	/// <remarks>
	/// <para>
	/// This function triggers when messages arrive in the "order-events" queue.
	/// It demonstrates processing events asynchronously using Excalibur.Dispatch.
	/// </para>
	/// <para>
	/// In production, you might use Azure Service Bus for more advanced features
	/// like dead-letter queues, sessions, and scheduled delivery.
	/// </para>
	/// </remarks>
	[Function("ProcessOrderEvent")]
	public async Task ProcessOrderEventAsync(
		[QueueTrigger("order-events", Connection = "AzureWebJobsStorage")] string message)
	{
		_logger.LogInformation("Queue trigger: Processing order event from queue");

		try
		{
			// Deserialize the event from queue message
			var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message);

			if (orderEvent is null)
			{
				_logger.LogWarning("Invalid queue message: Could not deserialize OrderCreatedEvent");
				return;
			}

			// Create dispatch context
			var context = DispatchContextInitializer.CreateDefaultContext();

			// Dispatch the event using Excalibur messaging
			_ = await _dispatcher.DispatchAsync(orderEvent, context, cancellationToken: default).ConfigureAwait(false);

			_logger.LogInformation(
				"Successfully processed OrderCreatedEvent for order {OrderId}",
				orderEvent.OrderId);
		}
		catch (JsonException ex)
		{
			_logger.LogError(ex, "Failed to deserialize queue message: {Message}", message);
			throw; // Rethrow to trigger retry/dead-letter
		}
	}

	/// <summary>
	/// Processes dead-letter queue messages for failed order events.
	/// </summary>
	/// <param name="message">The dead-letter queue message.</param>
	[Function("ProcessOrderEventDeadLetter")]
	public Task ProcessDeadLetterAsync(
		[QueueTrigger("order-events-poison", Connection = "AzureWebJobsStorage")] string message)
	{
		_logger.LogError(
			"Dead-letter: Failed to process order event after retries. Message: {Message}",
			message);

		// In production:
		// 1. Alert operations team
		// 2. Store for manual review
		// 3. Track in metrics/monitoring

		return Task.CompletedTask;
	}
}
