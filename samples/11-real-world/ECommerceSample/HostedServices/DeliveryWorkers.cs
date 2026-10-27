// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Text.Json;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Examples.ECommerceSample;

/// <summary>
/// Drains the outbox: claims staged confirmations, sends them, and reports the outcome back to the store.
/// </summary>
/// <remarks>
/// A send that throws is reported with <c>MarkFailedAsync</c> rather than swallowed. The store then holds the
/// message back for its failure-backoff window and hands it to a later pass, so a transient failure costs a
/// delay and not the notification. Nothing is removed from the outbox until the store has been told the send
/// succeeded.
/// </remarks>
public sealed partial class NotificationDrainService(
	IOutboxStore outboxStore,
	InMemoryEmailService emailService,
	PerformanceMonitor monitor,
	ILogger<NotificationDrainService> logger) : BackgroundService
{
	private const int BatchSize = 20;
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

	private readonly IOutboxStore _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
	private readonly InMemoryEmailService _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
	private readonly PerformanceMonitor _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
	private readonly ILogger<NotificationDrainService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogStarted();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await DrainOnceAsync(stoppingToken).ConfigureAwait(false);
				await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogDrainCycleFailed(ex);
			}
		}

		LogStopped();
	}

	private async Task DrainOnceAsync(CancellationToken cancellationToken)
	{
		using var activity = SampleTelemetry.Orders.StartActivity("NotificationDrain.Drain");

		var claimed = await _outboxStore.GetUnsentMessagesAsync(BatchSize, cancellationToken).ConfigureAwait(false);

		var sent = 0;
		foreach (var message in claimed)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				var notification = JsonSerializer.Deserialize<EmailNotification>(message.Payload)
					?? throw new InvalidOperationException($"Outbox message '{message.Id}' carried an empty payload.");

				await _emailService.SendEmailAsync(notification, cancellationToken).ConfigureAwait(false);
				await _outboxStore.MarkSentAsync(message.Id, cancellationToken).ConfigureAwait(false);

				_monitor.RecordNotificationSent();
				sent++;
				LogSent(message.Id);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				await _outboxStore
					.MarkFailedAsync(message.Id, ex.Message, message.RetryCount + 1, cancellationToken)
					.ConfigureAwait(false);

				_monitor.RecordNotificationSendRetry();
				LogSendFailed(message.Id, ex.Message);
			}
		}

		_ = activity?.SetTag("sent_count", sent);
	}

	[LoggerMessage(1001, LogLevel.Information, "Notification drain started")]
	private partial void LogStarted();

	[LoggerMessage(1002, LogLevel.Information, "Notification drain stopped")]
	private partial void LogStopped();

	[LoggerMessage(1003, LogLevel.Debug, "Sent outbox message {MessageId}")]
	private partial void LogSent(string messageId);

	[LoggerMessage(1004, LogLevel.Warning, "Send failed for outbox message {MessageId}, returned for retry: {Error}")]
	private partial void LogSendFailed(string messageId, string error);

    [LoggerMessage(1005, LogLevel.Error, "Notification drain cycle failed")]
	private partial void LogDrainCycleFailed(Exception ex);
}

/// <summary>
/// Executes due inventory checks from the schedule store and completes them.
/// </summary>
/// <remarks>
/// The check to run is deserialized from the stored message body. Completion is reported through
/// <c>CompleteAsync</c>, after which the schedule is no longer due; stores differ in whether they retain the
/// completed row, so the due predicate reads the enabled flag rather than assuming the row disappears.
/// </remarks>
public sealed partial class InventoryCheckProcessor(
	IScheduleStore scheduleStore,
	InventoryService inventoryService,
	ILogger<InventoryCheckProcessor> logger) : BackgroundService
{
	private const int BatchSize = 20;
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

	private readonly IScheduleStore _scheduleStore = scheduleStore ?? throw new ArgumentNullException(nameof(scheduleStore));

	private readonly InventoryService _inventoryService =
		inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));

	private readonly ILogger<InventoryCheckProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Whether a stored schedule is an inventory check that is due now. Also used by the run's assertions to
	/// confirm nothing was left behind.
	/// </summary>
	public static bool IsDue(IScheduledMessage schedule)
	{
		ArgumentNullException.ThrowIfNull(schedule);

		return schedule.Enabled
			&& string.Equals(schedule.MessageName, InventoryService.ScheduledMessageName, StringComparison.Ordinal)
			&& schedule.NextExecutionUtc.HasValue
			&& schedule.NextExecutionUtc.Value <= DateTimeOffset.UtcNow;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogStarted();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ExecuteDueChecksAsync(stoppingToken).ConfigureAwait(false);
				await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogCycleFailed(ex);
			}
		}

		LogStopped();
	}

	private async Task ExecuteDueChecksAsync(CancellationToken cancellationToken)
	{
		using var activity = SampleTelemetry.Inventory.StartActivity("InventoryProcessor.ExecuteDue");

		var all = await _scheduleStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
		var due = all.Where(IsDue).Take(BatchSize).ToList();

		_ = activity?.SetTag("due_count", due.Count);

		foreach (var schedule in due)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				var check = JsonSerializer.Deserialize<ScheduledInventoryCheck>(schedule.MessageBody)
					?? throw new InvalidOperationException($"Schedule '{schedule.Id}' carried an empty inventory-check body.");

				await _inventoryService.ExecuteInventoryCheckAsync(check, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				LogCheckFailed(ex, schedule.Id);
			}
			finally
			{
				// Completed either way: a check whose payload cannot be read will not become readable on a
				// later pass, and leaving it due would spin this loop forever.
				await _scheduleStore.CompleteAsync(schedule.Id, CancellationToken.None).ConfigureAwait(false);
			}
		}
	}

	[LoggerMessage(1001, LogLevel.Information, "Inventory check processor started")]
	private partial void LogStarted();

	[LoggerMessage(1002, LogLevel.Information, "Inventory check processor stopped")]
	private partial void LogStopped();

	[LoggerMessage(1003, LogLevel.Error, "Inventory check {ScheduleId} failed")]
	private partial void LogCheckFailed(Exception ex, Guid scheduleId);

	[LoggerMessage(1004, LogLevel.Error, "Inventory check cycle failed")]
	private partial void LogCycleFailed(Exception ex);
}
