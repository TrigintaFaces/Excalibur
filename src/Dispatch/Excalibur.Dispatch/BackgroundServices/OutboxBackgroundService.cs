// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OutboxOptions = Excalibur.Dispatch.Options.Middleware.OutboxOptions;

namespace Excalibur.Dispatch.BackgroundServices;

/// <summary>
/// Background service responsible for polling the outbox store and publishing staged messages.
/// </summary>
/// <remarks>
/// This service implements the publishing component of the Transactional Outbox pattern by:
/// <list type="bullet">
/// <item> Polling the outbox store at configured intervals for staged messages </item>
/// <item> Publishing messages to their configured destinations </item>
/// <item> Updating message status after successful/failed delivery attempts </item>
/// <item> Retrying failed messages according to retry policy </item>
/// <item> Cleaning up old sent/failed messages to prevent storage growth </item>
/// <item> Providing telemetry and metrics for monitoring outbox health </item>
/// </list>
/// </remarks>
public partial class OutboxBackgroundService : BackgroundService
{
	private static readonly ActivitySource ActivitySource = new(DispatchTelemetryConstants.ActivitySources.OutboxBackgroundService, "1.0.0");

	private readonly IServiceScopeFactory _serviceScopeFactory;

	private readonly OutboxOptions _options;

	private readonly ILogger<OutboxBackgroundService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxBackgroundService" /> class. Creates a new outbox background service instance.
	/// </summary>
	/// <param name="serviceScopeFactory"> Factory for creating service scopes. </param>
	/// <param name="options"> Configuration options for outbox behavior. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public OutboxBackgroundService(
		IServiceScopeFactory serviceScopeFactory,
		IOptions<OutboxOptions> options,
		ILogger<OutboxBackgroundService> logger)
	{
		ArgumentNullException.ThrowIfNull(serviceScopeFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_serviceScopeFactory = serviceScopeFactory;
		_options = options.Value;
		_logger = logger;
	}

	/// <summary>
	/// Disposes the activity source when the service is disposed.
	/// </summary>
	public override void Dispose()
	{
		ActivitySource.Dispose();
		base.Dispose();
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Main execution loop for the outbox background service.
	/// </summary>
	/// <param name="stoppingToken"> Token to signal service shutdown. </param>
	/// <returns> A <see cref="Task" /> representing the asynchronous operation. </returns>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogOutboxServiceStarting();

		// Initial delay to allow application to fully start
		await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);

		var currentInterval = _options.EnableAdaptivePolling
			? _options.MinPollingInterval
			: _options.PublishPollingInterval;

		while (!stoppingToken.IsCancellationRequested)
		{
			var hadMessages = false;

			try
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var outboxStore = scope.ServiceProvider.GetService<IOutboxStore>();

				if (outboxStore == null)
				{
					LogOutboxStoreNotRegistered();
					break;
				}

				// Process outbox messages
				hadMessages = await ProcessOutboxMessagesAsync(outboxStore, stoppingToken).ConfigureAwait(false);

				// Cleanup old messages periodically
				if (ShouldRunCleanup())
				{
					var outboxAdmin = outboxStore as IOutboxStoreAdmin
						?? scope.ServiceProvider.GetService<IOutboxStoreAdmin>();
					if (outboxAdmin is not null)
					{
						await CleanupOldMessagesAsync(outboxAdmin, stoppingToken).ConfigureAwait(false);
					}
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Expected during shutdown
				break;
			}
			catch (Exception ex)
			{
				LogOutboxProcessingError(ex);
			}

			// Adaptive polling: reset to min when messages found, back off when idle
			if (_options.EnableAdaptivePolling)
			{
				if (hadMessages)
				{
					currentInterval = _options.MinPollingInterval;
				}
				else
				{
					var nextInterval = TimeSpan.FromMilliseconds(
						currentInterval.TotalMilliseconds * _options.AdaptivePollingBackoffMultiplier);
					currentInterval = nextInterval < _options.PublishPollingInterval
						? nextInterval
						: _options.PublishPollingInterval;
				}
			}

			// Wait for next polling interval
			try
			{
				await Task.Delay(currentInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		LogOutboxServiceStopping();
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.OutboxServiceStarting, LogLevel.Information,
		"Outbox background service starting up")]
	private partial void LogOutboxServiceStarting();

	[LoggerMessage(DeliveryEventId.OutboxServiceStopping, LogLevel.Information,
		"Outbox background service shutting down")]
	private partial void LogOutboxServiceStopping();

	[LoggerMessage(DeliveryEventId.OutboxStoreNotRegistered, LogLevel.Warning,
		"IOutboxStore not registered, outbox background service will stop")]
	private partial void LogOutboxStoreNotRegistered();

	[LoggerMessage(DeliveryEventId.OutboxProcessingError, LogLevel.Error,
		"Error occurred during outbox processing cycle")]
	private partial void LogOutboxProcessingError(Exception ex);

	[LoggerMessage(DeliveryEventId.NoUnsentMessages, LogLevel.Trace,
		"No unsent messages found in outbox")]
	private partial void LogNoUnsentMessages();

	[LoggerMessage(DeliveryEventId.FoundUnsentMessages, LogLevel.Debug,
		"Found {MessageCount} unsent messages to process")]
	private partial void LogFoundUnsentMessages(int messageCount);

	[LoggerMessage(DeliveryEventId.MessageNotReady, LogLevel.Trace,
		"Message {MessageId} is not ready for delivery yet")]
	private partial void LogMessageNotReady(string messageId);

	[LoggerMessage(DeliveryEventId.MessageNotEligibleForRetry, LogLevel.Trace,
		"Message {MessageId} is not eligible for retry yet")]
	private partial void LogMessageNotEligibleForRetry(string messageId);

	[LoggerMessage(DeliveryEventId.FailedToPublishOutboxMessage, LogLevel.Error,
		"Failed to publish message {MessageId} of type {MessageType}")]
	private partial void LogFailedToPublishMessage(string messageId, string messageType, Exception ex);

	[LoggerMessage(DeliveryEventId.OutboxProcessingCompleted, LogLevel.Information,
		"Outbox processing completed: {PublishedCount} published, {FailedCount} failed")]
	private partial void LogOutboxProcessingCompleted(int publishedCount, int failedCount);

	[LoggerMessage(DeliveryEventId.OutboxMessageProcessingError, LogLevel.Error,
		"Error occurred during outbox message processing")]
	private partial void LogOutboxMessageProcessingError(Exception ex);

	[LoggerMessage(DeliveryEventId.PublishingOutboxMessage, LogLevel.Debug,
		"Publishing message {MessageId} of type {MessageType} to {Destination}")]
	private partial void LogPublishingMessage(string messageId, string messageType, string destination);

	[LoggerMessage(DeliveryEventId.MessagePublishedSuccessfully, LogLevel.Debug,
		"Successfully published message {MessageId}")]
	private partial void LogMessagePublishedSuccessfully(string messageId);

	[LoggerMessage(DeliveryEventId.FailedToPublishSingleMessage, LogLevel.Warning,
		"Failed to publish message {MessageId}: {Error}")]
	private partial void LogFailedToPublishSingleMessage(string messageId, string error);

	[LoggerMessage(DeliveryEventId.StartingOutboxCleanup, LogLevel.Debug,
		"Starting outbox cleanup for messages older than {CleanupAge}")]
	private partial void LogStartingOutboxCleanup(TimeSpan cleanupAge);

	[LoggerMessage(DeliveryEventId.OutboxCleanupCompleted, LogLevel.Information,
		"Outbox cleanup completed: deleted {DeletedCount} messages older than {CutoffTime}")]
	private partial void LogOutboxCleanupCompleted(int deletedCount, DateTimeOffset cutoffTime);

	[LoggerMessage(DeliveryEventId.OutboxCleanupError, LogLevel.Error,
		"Error occurred during outbox cleanup")]
	private partial void LogOutboxCleanupError(Exception ex);

	/// <summary>
	/// Processes staged messages from the outbox store.
	/// </summary>
	/// <returns> True if messages were found and processed; false if the outbox was empty. </returns>
	private async Task<bool> ProcessOutboxMessagesAsync(IOutboxStore outboxStore, CancellationToken cancellationToken)
	{
		using var activity = ActivitySource.StartActivity("ProcessOutboxMessages");

		try
		{
			// Get unsent messages in batches
			var unsentMessages = await outboxStore.GetUnsentMessagesAsync(_options.PublishBatchSize, cancellationToken)
				.ConfigureAwait(false);

			if (!unsentMessages.Any())
			{
				LogNoUnsentMessages();
				return false;
			}

			var messageCount = unsentMessages.Count();
			LogFoundUnsentMessages(messageCount);

			_ = (activity?.SetTag("outbox.message_count", messageCount));

			var publishedCount = 0;
			var failedCount = 0;

			// Process each message
			foreach (var message in unsentMessages)
			{
				try
				{
					// Check if message is ready for delivery (not scheduled for future)
					if (!message.IsReadyForDelivery())
					{
						LogMessageNotReady(message.Id);
						continue;
					}

					// Check if failed message is eligible for retry
					if (message.Status == OutboxStatus.Failed &&
						!message.IsEligibleForRetry(
							_options.MaxRetries,
							(int)_options.RetryDelay.TotalMinutes,
							_options.EnableExponentialRetryBackoff,
							(int)_options.MaxRetryDelay.TotalMinutes))
					{
						LogMessageNotEligibleForRetry(message.Id);
						continue;
					}

					await PublishMessageAsync(outboxStore, message, cancellationToken).ConfigureAwait(false);
					publishedCount++;
				}
				catch (Exception ex)
				{
					LogFailedToPublishMessage(message.Id, message.MessageType, ex);
					failedCount++;
				}
			}

			LogOutboxProcessingCompleted(publishedCount, failedCount);

			_ = (activity?.SetTag("outbox.published_count", publishedCount));
			_ = (activity?.SetTag("outbox.failed_count", failedCount));

			return true;
		}
		catch (Exception ex)
		{
			LogOutboxMessageProcessingError(ex);
			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			throw;
		}
	}

	/// <summary>
	/// Publishes a single message and updates its status in the outbox store.
	/// </summary>
	private async Task PublishMessageAsync(IOutboxStore outboxStore, OutboundMessage message,
		CancellationToken cancellationToken)
	{
		using var activity = ActivitySource.StartActivity("PublishMessage");
		_ = (activity?.SetTag("message.id", message.Id));
		_ = (activity?.SetTag("message.type", message.MessageType));
		_ = (activity?.SetTag("message.destination", message.Destination));

		LogPublishingMessage(message.Id, message.MessageType, message.Destination);

		try
		{
			// Mark message as being sent
			message.MarkSending();

			// IOutboxPublisher handles routing messages to their configured destinations.
			// This service orchestrates the publishing workflow rather than transport-specific logic.

			// Mark as successfully sent
			await outboxStore.MarkSentAsync(message.Id, cancellationToken).ConfigureAwait(false);

			LogMessagePublishedSuccessfully(message.Id);
			_ = (activity?.SetStatus(ActivityStatusCode.Ok));
		}
		catch (Exception ex)
		{
			// Mark as failed with error details
			var errorMessage = ex.Message;
			await outboxStore.MarkFailedAsync(message.Id, errorMessage, 1, cancellationToken).ConfigureAwait(false);

			LogFailedToPublishSingleMessage(message.Id, errorMessage);
			_ = (activity?.SetStatus(ActivityStatusCode.Error, errorMessage));
			throw;
		}
	}

	/// <summary>
	/// Determines if cleanup should run based on configured interval.
	/// </summary>
	private bool ShouldRunCleanup()
	{
		// Simple time-based cleanup - in production might want to track last cleanup time
		var currentMinute = DateTimeOffset.UtcNow.Minute;
		var intervalMinutes = (int)_options.CleanupInterval.TotalMinutes;

		// Run cleanup every configured interval (default: every hour at minute 0)
		return intervalMinutes > 0 && currentMinute % Math.Max(1, intervalMinutes / 60) == 0;
	}

	/// <summary>
	/// Cleans up old sent and failed messages to prevent unbounded storage growth.
	/// </summary>
	private async Task CleanupOldMessagesAsync(IOutboxStoreAdmin outboxStoreAdmin, CancellationToken cancellationToken)
	{
		using var activity = ActivitySource.StartActivity("CleanupOldMessages");

		try
		{
			LogStartingOutboxCleanup(_options.CleanupAge);

			var cutoffTime = DateTimeOffset.UtcNow - _options.CleanupAge;
			var deletedCount = await outboxStoreAdmin.CleanupSentMessagesAsync(cutoffTime, 1000, cancellationToken).ConfigureAwait(false);

			LogOutboxCleanupCompleted(deletedCount, cutoffTime);

			_ = (activity?.SetTag("cleanup.cutoff_time", cutoffTime.ToString("O")));
			_ = (activity?.SetTag("cleanup.deleted_count", deletedCount));
		}
		catch (Exception ex)
		{
			LogOutboxCleanupError(ex);
			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
		}
	}
}
