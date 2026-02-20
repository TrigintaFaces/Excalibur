// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// Background service that automatically cleans up old dead letter messages based on retention policy.
/// </summary>
public partial class PoisonMessageCleanupService : BackgroundService
{
	private readonly IDeadLetterStore _deadLetterStore;
	private readonly IPoisonMessageHandler _poisonMessageHandler;
	private readonly PoisonMessageOptions _options;
	private readonly ILogger<PoisonMessageCleanupService> _logger;
	private readonly ActivitySource _activitySource;

	/// <summary>
	/// Initializes a new instance of the <see cref="PoisonMessageCleanupService" /> class.
	/// </summary>
	public PoisonMessageCleanupService(
		IDeadLetterStore deadLetterStore,
		IPoisonMessageHandler poisonMessageHandler,
		IOptions<PoisonMessageOptions> options,
		ILogger<PoisonMessageCleanupService> logger)
	{
		ArgumentNullException.ThrowIfNull(deadLetterStore);
		ArgumentNullException.ThrowIfNull(poisonMessageHandler);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_deadLetterStore = deadLetterStore;
		_poisonMessageHandler = poisonMessageHandler;
		_options = options.Value;
		_logger = logger;
		_activitySource = new ActivitySource(DispatchTelemetryConstants.ActivitySources.PoisonMessageCleanup);
	}

	/// <summary>
	/// Disposes resources used by the background service.
	/// </summary>
	public override void Dispose()
	{
		_activitySource?.Dispose();
		base.Dispose();
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.EnableAutoCleanup)
		{
			LogPoisonMessageAutoCleanupDisabled();
			return;
		}

		LogPoisonMessageCleanupServiceStarted(_options.AutoCleanupInterval, _options.DeadLetterRetentionPeriod);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(_options.AutoCleanupInterval, stoppingToken).ConfigureAwait(false);

				if (stoppingToken.IsCancellationRequested)
				{
					break;
				}

				await PerformCleanupAsync(stoppingToken).ConfigureAwait(false);
				await CheckAlertThresholdAsync(stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected when cancellation is requested
				break;
			}
			catch (Exception ex)
			{
				LogErrorDuringPoisonMessageCleanup(ex);

				// Wait a bit before retrying to avoid tight error loops
				try
				{
					await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}

		LogPoisonMessageCleanupServiceStopped();
	}

	/// <summary>
	/// Performs the cleanup of old dead letter messages.
	/// </summary>
	private async Task PerformCleanupAsync(CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("CleanupOldMessages");

		try
		{
			var retentionDays = (int)_options.DeadLetterRetentionPeriod.TotalDays;
			var cleanedCount = await _deadLetterStore.CleanupOldMessagesAsync(retentionDays, cancellationToken)
				.ConfigureAwait(false);

			if (cleanedCount > 0)
			{
				LogCleanedUpDeadLetterMessages(cleanedCount, retentionDays);
			}

			_ = (activity?.SetTag("cleanup.count", cleanedCount));
			_ = (activity?.SetTag("cleanup.retention_days", retentionDays));
		}
		catch (Exception ex)
		{
			LogFailedToCleanupOldDeadLetterMessages(ex);
			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
			throw;
		}
	}

	/// <summary>
	/// Checks if the alert threshold has been exceeded.
	/// </summary>
	private async Task CheckAlertThresholdAsync(CancellationToken cancellationToken)
	{
		if (!_options.EnableAlerting)
		{
			return;
		}

		using var activity = _activitySource.StartActivity("CheckAlertThreshold");

		try
		{
			var statistics = await _poisonMessageHandler.GetStatisticsAsync(cancellationToken)
				.ConfigureAwait(false);

			if (statistics.RecentCount >= _options.AlertThreshold)
			{
				LogPoisonMessageAlertThresholdExceeded(statistics.RecentCount, _options.AlertThreshold, _options.AlertTimeWindow);

				// Log details about the most common issues
				if (statistics.MessagesByType.Count != 0)
				{
					var topTypes = statistics.MessagesByType
						.OrderByDescending(static kvp => kvp.Value)
						.Take(5);

					foreach (var (messageType, count) in topTypes)
					{
						LogTopPoisonMessageType(messageType, count);
					}
				}

				if (statistics.MessagesByReason.Count != 0)
				{
					var topReasons = statistics.MessagesByReason
						.OrderByDescending(static kvp => kvp.Value)
						.Take(5);

					foreach (var (reason, count) in topReasons)
					{
						LogTopPoisonReason(reason, count);
					}
				}

				_ = (activity?.SetTag("alert.triggered", value: true));
				_ = (activity?.SetTag("alert.recent_count", statistics.RecentCount));
				_ = (activity?.SetTag("alert.threshold", _options.AlertThreshold));
			}
		}
		catch (Exception ex)
		{
			LogFailedToCheckAlertThreshold(ex);
			_ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
		}
	}

	// Source-generated logging methods
	[LoggerMessage(DeliveryEventId.PoisonCleanupDisabled, LogLevel.Information,
		"Poison message auto-cleanup is disabled")]
	private partial void LogPoisonMessageAutoCleanupDisabled();

	[LoggerMessage(DeliveryEventId.PoisonCleanupStarting, LogLevel.Information,
		"Poison message cleanup service started with interval {CleanupInterval} and retention period {RetentionPeriod}")]
	private partial void LogPoisonMessageCleanupServiceStarted(TimeSpan cleanupInterval, TimeSpan retentionPeriod);

	[LoggerMessage(DeliveryEventId.PoisonCleanupCycleError, LogLevel.Error,
		"Error during poison message cleanup")]
	private partial void LogErrorDuringPoisonMessageCleanup(Exception ex);

	[LoggerMessage(DeliveryEventId.PoisonCleanupStopping, LogLevel.Information,
		"Poison message cleanup service stopped")]
	private partial void LogPoisonMessageCleanupServiceStopped();

	[LoggerMessage(DeliveryEventId.PoisonCleanupCompleted, LogLevel.Information,
		"Cleaned up {CleanedCount} dead letter messages older than {RetentionDays} days")]
	private partial void LogCleanedUpDeadLetterMessages(int cleanedCount, int retentionDays);

	[LoggerMessage(DeliveryEventId.PoisonCleanupError, LogLevel.Error,
		"Failed to cleanup old dead letter messages")]
	private partial void LogFailedToCleanupOldDeadLetterMessages(Exception ex);

	[LoggerMessage(DeliveryEventId.PoisonAlertThresholdExceeded, LogLevel.Warning,
		"Poison message alert threshold exceeded: {RecentCount} messages in {TimeWindow} (threshold: {Threshold})")]
	private partial void LogPoisonMessageAlertThresholdExceeded(int recentCount, int threshold, TimeSpan timeWindow);

	[LoggerMessage(DeliveryEventId.PoisonAlertTopMessageType, LogLevel.Warning,
		"Top poison message type: {MessageType} ({Count} occurrences)")]
	private partial void LogTopPoisonMessageType(string messageType, int count);

	[LoggerMessage(DeliveryEventId.PoisonAlertTopReason, LogLevel.Warning,
		"Top poison reason: {Reason} ({Count} occurrences)")]
	private partial void LogTopPoisonReason(string reason, int count);

	[LoggerMessage(DeliveryEventId.PoisonAlertCheckError, LogLevel.Error,
		"Failed to check alert threshold")]
	private partial void LogFailedToCheckAlertThreshold(Exception ex);
}
