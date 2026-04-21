// Outbox Processor Background Service

using Excalibur.Dispatch.Abstractions;

namespace MultiProviderQueueProcessor.Infrastructure;

/// <summary>
/// Background service that processes the unified outbox.
/// Publishes events to configured transport providers.
/// </summary>
/// <remarks>
/// <para>
/// This is a simplified sample implementation. In production, use the framework's
/// <c>OutboxBackgroundService</c> via
/// <c>services.AddExcalibur(x =&gt; x.AddOutbox(o =&gt; o.EnableBackgroundProcessing()))</c>
/// (the canonical builder path per ADR-321/325) which provides retry policies,
/// circuit breakers, multi-transport support, and metrics.
/// </para>
/// </remarks>
public sealed class OutboxProcessorService(
	IOutboxStore outboxStore,
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
		var messages = await outboxStore.GetUnsentMessagesAsync(_batchSize, cancellationToken);
		var messageList = messages.ToList();

		if (messageList.Count == 0)
		{
			return;
		}

		logger.LogDebug("Processing {Count} pending outbox messages", messageList.Count);

		foreach (var message in messageList)
		{
			try
			{
				// In a real implementation:
				// 1. Deserialize message.Payload to the appropriate event type
				// 2. Publish to the configured transport (Azure Service Bus, Kafka, RabbitMQ, etc.)
				// 3. Handle transport-specific publishing options

				logger.LogInformation(
					"Publishing outbox message {MessageId} - Type: {MessageType}",
					message.Id,
					message.MessageType);

				// Mark as sent
				await outboxStore.MarkSentAsync(message.Id, cancellationToken);

				logger.LogDebug("Published outbox message {MessageId}", message.Id);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to publish outbox message {MessageId}", message.Id);

				await outboxStore.MarkFailedAsync(message.Id, ex.Message, message.RetryCount + 1, cancellationToken);
			}
		}
	}
}
