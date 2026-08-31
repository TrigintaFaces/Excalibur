// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Background service that processes scheduled messages with timezone-aware cron support.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ScheduledMessageService" /> class. </remarks>
internal sealed partial class ScheduledMessageService(
	IScheduleStore scheduleStore,
	IDispatcher dispatcher,
	DispatchJsonSerializer serializer,
	ICronScheduler cronScheduler,
	IOptions<SchedulerOptions> options,
	IOptions<CronScheduleOptions> cronOptions,
	ILogger<ScheduledMessageService> logger,
	ITimePolicy? timePolicy = null,
	ITimeoutMonitor? timeoutMonitor = null,
	TimeProvider? timeProvider = null) : BackgroundService
{
	private readonly SchedulerOptions _schedulerOptions = options.Value;
	private readonly CronScheduleOptions _cronOptions = cronOptions.Value;

	// due-check, missed-execution replay, and Last/NextExecutionUtc reads go through TimeProvider so
	// the scheduling boundaries are deterministic under test (default TimeProvider.System is transparent to
	// existing DI and callers).
	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

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
		var scheduleSignal = scheduleStore as IScheduleStoreSignal;
		var currentPollInterval = PollingIntervalCalculator.GetInitialInterval(
			_schedulerOptions.EnableAdaptivePolling,
			_schedulerOptions.MinPollingInterval,
			_schedulerOptions.PollInterval);

		while (!stoppingToken.IsCancellationRequested)
		{
			var hadWork = false;

			try
			{
				// Apply timeout to schedule retrieval when ITimePolicy is available
				using var retrievalCts = CreateTimeoutToken(TimeoutOperationType.Database, stoppingToken);
				var schedules = await scheduleStore.GetAllAsync(retrievalCts.Token).ConfigureAwait(false);
				foreach (var item in schedules)
				{
					if (!item.Enabled || item.NextExecutionUtc is null || item.NextExecutionUtc > _timeProvider.GetUtcNow())
					{
						continue;
					}

					hadWork = true;

					// One schedule row must never be able to stop the others. A row whose type no longer exists, whose body no
					// longer deserializes, or whose handler throws is the expected steady state for a durable scheduler -- rows
					// outlive the code that created them. Letting that escape the loop aborts the scan before
					// UpdateNextExecutionTimeAsync runs, so the same row is still due on the next poll and every later row is
					// starved permanently, for every message and tenant, with no path back.
					try
					{
						// Check for missed executions
						if (ShouldHandleMissedExecution(item))
						{
							await HandleMissedExecutionsAsync(item, stoppingToken).ConfigureAwait(false);
						}

						// Process the current execution with optional timeout.
						//
						// CA2000 is suppressed here, and only here, as a measured false positive: the token
						// source below IS disposed on every path by the `using var` (the compiler emits the
						// try/finally), exactly as the sibling `retrievalCts` above is -- which the analyzer
						// does not flag. What changed is that the callee now owns a disposable of its own
						// (the tenant scope), and that costs CA2000's interprocedural dataflow enough
						// precision to report this call site conservatively. Four alternative shapes were
						// built and measured against it -- `using var` at the callee's top, an explicit
						// try/finally in the callee, the scope pushed one call deeper, and a `using` block --
						// and all four reproduce it; so does rewriting this statement into the explicit
						// try/finally with a null-out that the diagnostic's own message recommends. There is
						// no code shape that satisfies the rule, so the alternative to suppressing is
						// dropping a tenant-correctness fix for an analyzer artifact.
#pragma warning disable CA2000 // Dispose objects before losing scope
						using var processCts = CreateTimeoutToken(TimeoutOperationType.Handler, stoppingToken);
#pragma warning restore CA2000
						await ProcessScheduledMessageAsync(item, processCts.Token).ConfigureAwait(false);

						// Calculate next execution time
						await UpdateNextExecutionTimeAsync(item, stoppingToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
					{
						throw;
					}
					catch (Exception ex)
					{
						LogErrorProcessingMessages(ex);

						// A row that failed must still ADVANCE. Without this it stays due, so it is
						// re-processed and re-logged on every poll for the life of the process: unbounded
						// log volume and wasted work from a row that can never succeed. Durable rows
						// outliving the code that created them is the expected steady state for a
						// scheduler, so this is reachable in normal operation, not an exceptional path.
						await AdvanceOrDisableAsync(item, stoppingToken).ConfigureAwait(false);
					}
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (TimeoutException ex)
			{
				LogTimeoutDuringProcessing(ex);
			}
			catch (Exception ex)
			{
				LogErrorProcessingMessages(ex);
			}

			currentPollInterval = PollingIntervalCalculator.GetNextInterval(
				currentPollInterval,
				hadWork,
				_schedulerOptions.EnableAdaptivePolling,
				_schedulerOptions.MinPollingInterval,
				_schedulerOptions.PollInterval,
				_schedulerOptions.AdaptivePollingBackoffMultiplier);

			var delay = PollingIntervalCalculator.ApplyJitter(
				currentPollInterval,
				_schedulerOptions.PollingJitterRatio);
			if (!hadWork && scheduleSignal is not null)
			{
				await scheduleSignal.WaitForChangeAsync(delay, stoppingToken).ConfigureAwait(false);
			}
			else
			{
				await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
			}
		}

		LogServiceStopped();
	}

	private bool ShouldHandleMissedExecution(IScheduledMessage item)
	{
		var behavior = item.MissedExecutionBehavior ?? _cronOptions.MissedExecutionBehavior;
		return behavior != MissedExecutionBehavior.SkipMissed &&
			   item is { LastExecutionUtc: not null, NextExecutionUtc: not null } &&
			   item.NextExecutionUtc.Value < _timeProvider.GetUtcNow().Subtract(_schedulerOptions.PollInterval);
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
				switch (behavior)
				{
					case MissedExecutionBehavior.ExecuteLatestMissed:
						{
							var missedExecutionCount = 0;
							foreach (var _ in scheduler.GetMissedExecutions(cronExpr, item.LastExecutionUtc.Value, _timeProvider.GetUtcNow()))
							{
								missedExecutionCount++;
							}

							if (missedExecutionCount > 0)
							{
								LogFoundMissedExecutions(missedExecutionCount, item.Id);
								await ProcessScheduledMessageAsync(item, cancellationToken).ConfigureAwait(false);
							}

							break;
						}

					case MissedExecutionBehavior.ExecuteAllMissed:
						{
							var missedExecutionCount = 0;
							foreach (var _ in scheduler.GetMissedExecutions(cronExpr, item.LastExecutionUtc.Value, _timeProvider.GetUtcNow()))
							{
								missedExecutionCount++;
								await ProcessScheduledMessageAsync(item, cancellationToken).ConfigureAwait(false);
							}

							if (missedExecutionCount > 0)
							{
								LogFoundMissedExecutions(missedExecutionCount, item.Id);
							}

							break;
						}

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

	[RequiresUnreferencedCode("Uses DeserializeAsync with runtime type resolution")]
	[RequiresDynamicCode("Calls DispatchJsonSerializer.DeserializeAsync(String, Type)")]
	private async Task ProcessScheduledMessageAsync(IScheduledMessage item, CancellationToken cancellationToken)
	{
		// Establish the schedule's tenant as ambient for the whole dispatch, at the point of use. Stamping
		// the identity feature alone is not enough: it is read from the message context, whereas every
		// ITenantContext-reading store a handler touches reads the ambient holder, and nothing on this path
		// wrote it. A scheduled message therefore ran with the poller's ambient tenant -- normally none --
		// so a handler's writes landed untenanted while the context claimed the right tenant. Establishing
		// it here rather than at the caller makes the caller's scope irrelevant, so no future caller can
		// reintroduce the gap.
		//
		// The term is read back off a stored schedule, so it goes through the total store-read conversion.
		// A raw null CLEARS the ambient, and a cleared ambient means "no tenant was established" -- which a
		// multi-tenant store fails closed on. An untenanted schedule is a different state and binds the
		// reserved untenanted term.
		using var tenantScope = TenantContextHolder.BeginScope(
			KeyedTenantPartition.FromStoredValue(item.TenantId).TenantId);

		if (!MessageTypeRegistry.TryGetType(item.MessageName, out var type))
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

		var identityFeature = context.GetOrCreateIdentityFeature();
		identityFeature.TraceParent = item.TraceParent;
		identityFeature.TenantId = item.TenantId;
		identityFeature.UserId = item.UserId;

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
		item.LastExecutionUtc = _timeProvider.GetUtcNow();
	}

	/// <summary>
	/// Moves a failed row off the current poll: to its next scheduled occurrence if the schedule can be
	/// advanced, otherwise disabled so it stops being due at all.
	/// </summary>
	/// <remarks>
	/// The give-up is logged ONCE, at the point the row is disabled. A row whose schedule cannot be
	/// computed (an unparseable cron, for instance) would otherwise remain due forever and re-log on
	/// every poll, which is the volume this exists to bound.
	/// </remarks>
	private async Task AdvanceOrDisableAsync(IScheduledMessage item, CancellationToken cancellationToken)
	{
		try
		{
			await UpdateNextExecutionTimeAsync(item, cancellationToken).ConfigureAwait(false);
			return;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogScheduleDisabledAfterFailure(item.Id, ex);
		}

		try
		{
			item.Enabled = false;
			await scheduleStore.StoreAsync(item, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			// The store itself is unavailable. Nothing further can be done from here; the row stays due
			// and the next poll will try again, which is correct while the store is down.
			LogErrorProcessingMessages(ex);
		}
	}

	private async Task UpdateNextExecutionTimeAsync(IScheduledMessage item, CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(item.CronExpression))
		{
			var timeZone = GetTimeZone(item.TimeZoneId);
			var cronExpr = cronScheduler.Parse(item.CronExpression, timeZone);
			item.NextExecutionUtc = cronExpr.GetNextOccurrenceUtc(_timeProvider.GetUtcNow());

			if (_cronOptions.EnableDetailedLogging)
			{
				LogNextExecutionCalculated(item.Id, item.NextExecutionUtc, timeZone.Id);
			}
		}
		else if (item.Interval is not null)
		{
			item.NextExecutionUtc = _timeProvider.GetUtcNow().Add(item.Interval.Value);
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

	/// <summary>
	/// Creates a timeout-aware cancellation token when <see cref="ITimePolicy"/> is registered.
	/// When no time policy is available, returns a token linked only to the parent.
	/// </summary>
	private CancellationTokenSource CreateTimeoutToken(TimeoutOperationType operationType, CancellationToken parentToken)
	{
		if (timePolicy is null || !timePolicy.ShouldApplyTimeout(operationType, null))
		{
			return CancellationTokenSource.CreateLinkedTokenSource(parentToken);
		}

		var timeout = timePolicy.GetTimeoutFor(operationType);
		var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
		cts.CancelAfter(timeout);
		return cts;
	}

	// Source-generated logging methods
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

	[LoggerMessage(DeliveryEventId.ScheduledTimeoutDuringProcessing, LogLevel.Warning,
		"Timeout occurred during scheduled message processing")]
	private partial void LogTimeoutDuringProcessing(Exception ex);

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

	[LoggerMessage(DeliveryEventId.ScheduledDisabledAfterFailure, LogLevel.Warning,
		"Schedule {ScheduleId} failed and its next execution could not be advanced, so it has been disabled; "
		+ "it will not be retried until it is re-enabled")]
	private partial void LogScheduleDisabledAfterFailure(Guid scheduleId, Exception exception);

	[LoggerMessage(DeliveryEventId.ScheduledTimezoneLookupFailed, LogLevel.Warning,
		"Failed to find timezone {TimeZoneId}, using default")]
	private partial void LogTimezoneLookupFailed(string timeZoneId, Exception ex);
}
