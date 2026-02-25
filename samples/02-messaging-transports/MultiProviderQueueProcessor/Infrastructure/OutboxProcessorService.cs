// Outbox Processor Background Service

using Excalibur.EventSourcing.Outbox;

namespace MultiProviderQueueProcessor.Infrastructure;

/// <summary>
/// Background service that processes the event-sourced outbox.
/// Publishes events to configured transport providers.
/// </summary>
/// <remarks>
/// <para>
/// This is a simplified sample implementation. In production, you would:
/// <list type="bullet">
/// <item>Deserialize the EventData JSON to the appropriate domain event type</item>
/// <item>Use the configured transport to publish to message queues</item>
/// <item>Handle poison messages and dead-letter queues</item>
/// <item>Implement proper retry policies with circuit breakers</item>
/// </list>
/// </para>
/// </remarks>
public sealed class OutboxProcessorService(
	IEventSourcedOutboxStore outboxStore,
	ILogger<OutboxProcessorService> logger) : BackgroundService
{
	private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
	private readonly int _batchSize = 100;

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		logger.LogInformation("Outbox processor starting");

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessPendingMessagesAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Expected during shutdown
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Error processing outbox messages");
			}

			await Task.Delay(_pollInterval, stoppingToken);
		}

		logger.LogInformation("Outbox processor stopped");
	}

	private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
	{
		var messages = await outboxStore.GetPendingAsync(_batchSize, cancellationToken);

		if (messages.Count == 0)
		{
			return;
		}

		logger.LogDebug("Processing {Count} pending outbox messages", messages.Count);

		foreach (var message in messages)
		{
			try
			{
				// In a real implementation:
				// 1. Deserialize message.EventData to the appropriate event type using IEventSerializer
				// 2. Publish to the configured transport (Azure Service Bus, Kafka, RabbitMQ, etc.)
				// 3. Handle transport-specific publishing options

				logger.LogInformation(
					"Publishing outbox message {MessageId} - AggregateType: {AggregateType}, EventType: {EventType}",
					message.Id,
					message.AggregateType,
					message.EventType);

				// Mark as published
				await outboxStore.MarkAsPublishedAsync(message.Id, null, cancellationToken);

				logger.LogDebug(
					"Published outbox message {MessageId} for aggregate {AggregateId}",
					message.Id,
					message.AggregateId);
			}
			catch (Exception ex)
			{
				logger.LogError(
					ex,
					"Failed to publish outbox message {MessageId}",
					message.Id);

				// Increment retry count for exponential backoff
				await outboxStore.IncrementRetryCountAsync(message.Id, cancellationToken);
			}
		}
	}
}
