// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;

using Excalibur.Outbox.Diagnostics;
using Excalibur.Outbox.Health;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Outbox;

/// <summary>
/// Configuration options for the outbox background service.
/// </summary>
public class OutboxProcessingOptions
{
	/// <summary>
	/// Gets or sets the interval between polling cycles.
	/// </summary>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the maximum number of retries for failed messages.
	/// </summary>
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets whether to process scheduled messages.
	/// </summary>
	public bool ProcessScheduledMessages { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to retry failed messages.
	/// </summary>
	public bool RetryFailedMessages { get; set; } = true;

	/// <summary>
	/// Gets or sets whether this instance is enabled.
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the drain timeout in seconds for graceful shutdown.
	/// </summary>
	/// <value>The drain timeout in seconds. Default is 30.</value>
	/// <remarks>
	/// When the service is stopping, this timeout controls how long to wait for
	/// in-flight processing to complete before forcing shutdown.
	/// </remarks>
	public int DrainTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets the drain timeout as a <see cref="TimeSpan"/>.
	/// </summary>
	/// <value>The drain timeout duration.</value>
	public TimeSpan DrainTimeout => TimeSpan.FromSeconds(DrainTimeoutSeconds);
}

/// <summary>
/// Background service that processes outbox messages for reliable delivery.
/// </summary>
/// <remarks>
/// This service polls the outbox store for pending messages and publishes them
/// to the message bus. It handles retries for failed messages and processes
/// scheduled messages when they become due.
/// </remarks>
public partial class OutboxBackgroundService : BackgroundService
{
	private readonly IOutboxPublisher _publisher;
	private readonly IOptions<OutboxProcessingOptions> _options;
	private readonly BackgroundServiceHealthState? _healthState;
	private readonly ILogger<OutboxBackgroundService> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OutboxBackgroundService"/> class.
	/// </summary>
	/// <param name="publisher">The outbox publisher for message delivery.</param>
	/// <param name="options">The processing options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="healthState">Optional health state tracker for health check integration.</param>
	public OutboxBackgroundService(
		IOutboxPublisher publisher,
		IOptions<OutboxProcessingOptions> options,
		ILogger<OutboxBackgroundService> logger,
		BackgroundServiceHealthState? healthState = null)
	{
		_publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_healthState = healthState;
	}

	/// <inheritdoc/>
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		_healthState?.MarkStopped();

		var drainTimeout = _options.Value.DrainTimeout;
		using var drainCts = new CancellationTokenSource(drainTimeout);
		using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken, drainCts.Token);

		try
		{
			await base.StopAsync(combinedCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (drainCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			LogDrainTimeoutExceeded(drainTimeout);
		}
	}

	/// <inheritdoc/>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Value.Enabled)
		{
			LogBackgroundServiceDisabled();
			return;
		}

		_healthState?.MarkStarted();
		LogBackgroundServiceStarting(_options.Value.PollingInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessOutboxAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				// Normal shutdown, don't log as error
				break;
			}
			catch (Exception ex)
			{
				LogBackgroundServiceError(ex);
				BackgroundServiceMetrics.RecordProcessingError(
					BackgroundServiceTypes.Outbox,
					ex.GetType().Name);
			}

			try
			{
				await Task.Delay(_options.Value.PollingInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		_healthState?.MarkStopped();
		LogBackgroundServiceStopped();
	}

	private async Task ProcessOutboxAsync(CancellationToken cancellationToken)
	{
		var stopwatch = ValueStopwatch.StartNew();
		var totalSuccess = 0;
		var totalFailure = 0;

		// Process pending messages
		var pendingResult = await _publisher.PublishPendingMessagesAsync(cancellationToken).ConfigureAwait(false);
		totalSuccess += pendingResult.SuccessCount;
		totalFailure += pendingResult.FailureCount;

		if (pendingResult.SuccessCount > 0 || pendingResult.FailureCount > 0)
		{
			LogProcessedPendingMessages(
					pendingResult.SuccessCount,
					pendingResult.FailureCount);

			BackgroundServiceMetrics.RecordMessagesProcessed(
				BackgroundServiceTypes.Outbox,
				BackgroundServiceOperations.Pending,
				pendingResult.SuccessCount);

			BackgroundServiceMetrics.RecordMessagesFailed(
				BackgroundServiceTypes.Outbox,
				BackgroundServiceOperations.Pending,
				pendingResult.FailureCount);
		}

		// Process scheduled messages if enabled
		if (_options.Value.ProcessScheduledMessages)
		{
			var scheduledResult = await _publisher.PublishScheduledMessagesAsync(cancellationToken).ConfigureAwait(false);
			totalSuccess += scheduledResult.SuccessCount;
			totalFailure += scheduledResult.FailureCount;

			if (scheduledResult.SuccessCount > 0 || scheduledResult.FailureCount > 0)
			{
				LogProcessedScheduledMessages(
						scheduledResult.SuccessCount,
						scheduledResult.FailureCount);

				BackgroundServiceMetrics.RecordMessagesProcessed(
					BackgroundServiceTypes.Outbox,
					BackgroundServiceOperations.Scheduled,
					scheduledResult.SuccessCount);

				BackgroundServiceMetrics.RecordMessagesFailed(
					BackgroundServiceTypes.Outbox,
					BackgroundServiceOperations.Scheduled,
					scheduledResult.FailureCount);
			}
		}

		// Retry failed messages if enabled
		if (_options.Value.RetryFailedMessages)
		{
			var retryResult = await _publisher.RetryFailedMessagesAsync(
				_options.Value.MaxRetries,
				cancellationToken).ConfigureAwait(false);
			totalSuccess += retryResult.SuccessCount;
			totalFailure += retryResult.FailureCount;

			if (retryResult.SuccessCount > 0 || retryResult.FailureCount > 0)
			{
				LogRetriedFailedMessages(
						retryResult.SuccessCount,
						retryResult.FailureCount);

				BackgroundServiceMetrics.RecordMessagesProcessed(
					BackgroundServiceTypes.Outbox,
					BackgroundServiceOperations.Retry,
					retryResult.SuccessCount);

				BackgroundServiceMetrics.RecordMessagesFailed(
					BackgroundServiceTypes.Outbox,
					BackgroundServiceOperations.Retry,
					retryResult.FailureCount);
			}
		}

		// Record cycle metrics
		var result = totalFailure > 0
			? (totalSuccess > 0 ? BackgroundServiceResults.Partial : BackgroundServiceResults.Error)
			: (totalSuccess > 0 ? BackgroundServiceResults.Success : BackgroundServiceResults.Empty);

		BackgroundServiceMetrics.RecordProcessingCycle(
			BackgroundServiceTypes.Outbox, result);

		BackgroundServiceMetrics.RecordProcessingDuration(
			BackgroundServiceTypes.Outbox, stopwatch.Elapsed.TotalMilliseconds);

		// Update health state
		_healthState?.RecordCycle(totalSuccess, totalFailure);
	}

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceDisabled, LogLevel.Information,
			"Outbox background service is disabled.")]
	private partial void LogBackgroundServiceDisabled();

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceStarting, LogLevel.Information,
			"Outbox background service starting with polling interval {PollingInterval}.")]
	private partial void LogBackgroundServiceStarting(TimeSpan pollingInterval);

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceError, LogLevel.Error,
			"Error processing outbox messages.")]
	private partial void LogBackgroundServiceError(Exception exception);

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceStopped, LogLevel.Information,
			"Outbox background service stopped.")]
	private partial void LogBackgroundServiceStopped();

	[LoggerMessage(OutboxEventId.OutboxBackgroundProcessedPending, LogLevel.Debug,
			"Processed pending messages: {SuccessCount} succeeded, {FailureCount} failed.")]
	private partial void LogProcessedPendingMessages(int successCount, int failureCount);

	[LoggerMessage(OutboxEventId.OutboxBackgroundProcessedScheduled, LogLevel.Debug,
			"Processed scheduled messages: {SuccessCount} succeeded, {FailureCount} failed.")]
	private partial void LogProcessedScheduledMessages(int successCount, int failureCount);

	[LoggerMessage(OutboxEventId.OutboxBackgroundRetriedFailed, LogLevel.Debug,
			"Retried failed messages: {SuccessCount} succeeded, {FailureCount} failed.")]
	private partial void LogRetriedFailedMessages(int successCount, int failureCount);

	[LoggerMessage(OutboxEventId.OutboxBackgroundServiceDrainTimeout, LogLevel.Warning,
			"Outbox background service drain timeout exceeded ({DrainTimeout}).")]
	private partial void LogDrainTimeoutExceeded(TimeSpan drainTimeout);
}
