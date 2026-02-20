// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Background service that processes scheduled messages with timezone-aware cron support.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ScheduledMessageService" /> class. </remarks>
public partial class ScheduledMessageService(
	IScheduleStore scheduleStore,
	IDispatcher dispatcher,
	IJsonSerializer serializer,
	ICronScheduler cronScheduler,
	IOptions<SchedulerOptions> options,
	IOptions<CronScheduleOptions> cronOptions,
	ILogger<ScheduledMessageService> logger) : BackgroundService
{
	private readonly TimeSpan _pollInterval = options.Value.PollInterval;
	private readonly CronScheduleOptions _cronOptions = cronOptions.Value;

	/// <inheritdoc />
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await base.StopAsync(cancellationToken).ConfigureAwait(false);

		if (scheduleStore is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:Members annotated with RequiresUnreferencedCodeAttribute may break with trimming",
			Justification = "Scheduled message processing relies on runtime type resolution and serializer configuration.")]
	[UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "Scheduled message processing uses runtime deserialization; AOT users should opt out of scheduling features.")]
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogServiceStarted();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var schedules = await scheduleStore.GetAllAsync(stoppingToken).ConfigureAwait(false);
				foreach (var item in schedules)
				{
					if (!item.Enabled || item.NextExecutionUtc is null || item.NextExecutionUtc > DateTimeOffset.UtcNow)
					{
						continue;
					}

					// Check for missed executions
					if (ShouldHandleMissedExecution(item))
					{
						await HandleMissedExecutionsAsync(item, stoppingToken).ConfigureAwait(false);
					}

					// Process the current execution
					await ProcessScheduledMessageAsync(item, stoppingToken).ConfigureAwait(false);

					// Calculate next execution time
					await UpdateNextExecutionTimeAsync(item, stoppingToken).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				LogErrorProcessingMessages(ex);
			}

			await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
		}

		LogServiceStopped();
	}

	private bool ShouldHandleMissedExecution(IScheduledMessage item)
	{
		var behavior = item.MissedExecutionBehavior ?? _cronOptions.MissedExecutionBehavior;
		return behavior != MissedExecutionBehavior.SkipMissed &&
			   item is { LastExecutionUtc: not null, NextExecutionUtc: not null } &&
			   item.NextExecutionUtc.Value < DateTimeOffset.UtcNow.Subtract(_pollInterval);
	}

	[RequiresUnreferencedCode("Uses dynamic type loading")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Delivery.ScheduledMessageService.ProcessScheduledMessageAsync(IScheduledMessage, CancellationToken)")]
	private async Task HandleMissedExecutionsAsync(IScheduledMessage item, CancellationToken cancellationToken)
	{
		var behavior = item.MissedExecutionBehavior ?? _cronOptions.MissedExecutionBehavior;

		if (behavior == MissedExecutionBehavior.DisableSchedule)
		{
			LogDisablingSchedule(item.Id);
			item.Enabled = false;
			await scheduleStore.StoreAsync(item, cancellationToken).ConfigureAwait(false);
			return;
		}

		if (!string.IsNullOrWhiteSpace(item.CronExpression) && item.LastExecutionUtc.HasValue)
		{
			var timeZone = GetTimeZone(item.TimeZoneId);
			var cronExpr = cronScheduler.Parse(item.CronExpression, timeZone);

			if (cronScheduler is CronScheduler scheduler)
			{
				var missedExecutions = scheduler.GetMissedExecutions(cronExpr, item.LastExecutionUtc.Value, DateTimeOffset.UtcNow).ToList();

				if (missedExecutions.Count != 0)
				{
					LogFoundMissedExecutions(missedExecutions.Count, item.Id);

					switch (behavior)
					{
						case MissedExecutionBehavior.ExecuteLatestMissed:
							// Execute only the most recent missed execution
							_ = missedExecutions[^1];
							await ProcessScheduledMessageAsync(item, cancellationToken).ConfigureAwait(false);
							break;

						case MissedExecutionBehavior.ExecuteAllMissed:
							// Execute all missed executions
							foreach (var _ in missedExecutions)
							{
								await ProcessScheduledMessageAsync(item, cancellationToken).ConfigureAwait(false);
							}

							break;

						case MissedExecutionBehavior.SkipMissed:
						case MissedExecutionBehavior.DisableSchedule:
							break;

						default:
							// Unknown behavior, skip missed executions
							LogUnknownBehavior(behavior, item.Id);
							break;
					}
				}
			}
		}
	}

	[RequiresUnreferencedCode("Uses DeserializeAsync with runtime type resolution")]
	[RequiresDynamicCode("Calls Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer.DeserializeAsync(String, Type)")]
	private async Task ProcessScheduledMessageAsync(IScheduledMessage item, CancellationToken cancellationToken)
	{
		var type = MessageTypeRegistry.GetType(item.MessageName);
		if (type is null)
		{
			LogUnknownMessageType(item.MessageName);
			return;
		}

		var message = await serializer.DeserializeAsync(item.MessageBody, type).ConfigureAwait(false);
		if (message is null)
		{
			LogDeserializationFailed(item.Id);
			return;
		}

		var context = DispatchContextInitializer.CreateDefaultContext();
		context.CorrelationId = item.CorrelationId;
		context.TraceParent = item.TraceParent;
		context.TenantId = item.TenantId;
		context.UserId = item.UserId;

		// Add timezone information to context if available
		if (!string.IsNullOrEmpty(item.TimeZoneId))
		{
			context.Items["ScheduleTimeZone"] = item.TimeZoneId;
		}

		switch (message)
		{
			case IDispatchAction action:
				_ = await dispatcher.DispatchAsync(action, context, cancellationToken).ConfigureAwait(false);
				break;

			case IDispatchEvent evt:
				_ = await dispatcher.DispatchAsync(evt, context, cancellationToken).ConfigureAwait(false);
				break;

			default:
				LogUnsupportedMessageType(message.GetType().Name);
				break;
		}

		// Update last execution time
		item.LastExecutionUtc = DateTimeOffset.UtcNow;
	}

	private async Task UpdateNextExecutionTimeAsync(IScheduledMessage item, CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(item.CronExpression))
		{
			var timeZone = GetTimeZone(item.TimeZoneId);
			var cronExpr = cronScheduler.Parse(item.CronExpression, timeZone);
			item.NextExecutionUtc = cronExpr.GetNextOccurrenceUtc(DateTimeOffset.UtcNow);

			if (_cronOptions.EnableDetailedLogging)
			{
				LogNextExecutionCalculated(item.Id, item.NextExecutionUtc, timeZone.Id);
			}
		}
		else if (item.Interval is not null)
		{
			item.NextExecutionUtc = DateTimeOffset.UtcNow.Add(item.Interval.Value);
		}
		else
		{
			// One-time schedule - disable after execution
			item.Enabled = false;
		}

		await scheduleStore.StoreAsync(item, cancellationToken).ConfigureAwait(false);
	}

	private TimeZoneInfo GetTimeZone(string? timeZoneId)
	{
		if (string.IsNullOrEmpty(timeZoneId))
		{
			return _cronOptions.DefaultTimeZone;
		}

		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
		}
		catch (Exception ex)
		{
			LogTimezoneLookupFailed(timeZoneId, ex);
			return _cronOptions.DefaultTimeZone;
		}
	}

	// Source-generated logging methods (Sprint 360 - EventId Migration Phase 1)
	[LoggerMessage(DeliveryEventId.ScheduledUnknownMessageType, LogLevel.Warning,
		"Unknown scheduled message type {Type}")]
	private partial void LogUnknownMessageType(string type);

	[LoggerMessage(DeliveryEventId.ScheduledDeserializationFailed, LogLevel.Warning,
		"Failed to deserialize scheduled message {MessageId}")]
	private partial void LogDeserializationFailed(Guid messageId);

	[LoggerMessage(DeliveryEventId.ScheduledServiceStarting, LogLevel.Information,
		"EnhancedScheduledMessageService started with timezone support.")]
	private partial void LogServiceStarted();

	[LoggerMessage(DeliveryEventId.ScheduledProcessingError, LogLevel.Error,
		"Error processing scheduled messages")]
	private partial void LogErrorProcessingMessages(Exception ex);

	[LoggerMessage(DeliveryEventId.ScheduledServiceStopping, LogLevel.Information,
		"EnhancedScheduledMessageService stopped.")]
	private partial void LogServiceStopped();

	[LoggerMessage(DeliveryEventId.ScheduledDisabled, LogLevel.Warning,
		"Disabling schedule {MessageId} due to missed executions")]
	private partial void LogDisablingSchedule(Guid messageId);

	[LoggerMessage(DeliveryEventId.ScheduledMissedExecutions, LogLevel.Warning,
		"Found {Count} missed executions for schedule {MessageId}")]
	private partial void LogFoundMissedExecutions(int count, Guid messageId);

	[LoggerMessage(DeliveryEventId.ScheduledUnknownBehavior, LogLevel.Warning,
		"Unknown missed execution behavior {Behavior} for schedule {MessageId}")]
	private partial void LogUnknownBehavior(MissedExecutionBehavior behavior, Guid messageId);

	[LoggerMessage(DeliveryEventId.ScheduledUnsupportedMessageType, LogLevel.Warning,
		"Message type {Type} is not supported for scheduling")]
	private partial void LogUnsupportedMessageType(string type);

	[LoggerMessage(DeliveryEventId.ScheduledNextExecution, LogLevel.Debug,
		"Next execution for schedule {MessageId} calculated as {NextRun} in timezone {TimeZone}")]
	private partial void LogNextExecutionCalculated(Guid messageId, DateTimeOffset? nextRun, string timeZone);

	[LoggerMessage(DeliveryEventId.ScheduledTimezoneLookupFailed, LogLevel.Warning,
		"Failed to find timezone {TimeZoneId}, using default")]
	private partial void LogTimezoneLookupFailed(string timeZoneId, Exception ex);
}
