// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0



using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection;
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
	IServiceScopeFactory scopeFactory,
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
				var executionStore = scheduleStore as IScheduleExecutionStore;
				var schedules = executionStore is not null
					? (await executionStore.GetExecutionSnapshotsAsync(retrievalCts.Token).ConfigureAwait(false))
						.Select(static snapshot => (Item: snapshot.Message, snapshot.Generation))
					: (await scheduleStore.GetAllAsync(retrievalCts.Token).ConfigureAwait(false))
						.Select(static item => (Item: (IScheduledMessage)ScheduleExecutionSnapshot.Copy(item), Generation: Guid.Empty));
				foreach (var (item, generation) in schedules)
				{
					if (!item.Enabled || item.NextExecutionUtc is null || item.NextExecutionUtc > _timeProvider.GetUtcNow())
					{
						continue;
					}

					hadWork = true;
					try
					{
						await ProcessScheduleAsync(executionStore, item, generation, stoppingToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
					catch (Exception ex)
					{
						// One row cannot starve the remaining inventory. Authority/storage failures
						// never fall back to a second administrative write.
						LogErrorProcessingMessages(ex);
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
				using var wait = new TimeoutTokenScope(delay, _timeProvider, stoppingToken);
				try
				{
					// Cancel the actual signal read at the provider deadline; never leave an orphaned read.
					await scheduleSignal.WaitForChangeAsync(Timeout.InfiniteTimeSpan, wait.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested && wait.Token.IsCancellationRequested)
				{
					// The polling deadline is a normal wake-up; host cancellation still propagates.
				}
			}
			else
			{
				await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
			}
		}

		LogServiceStopped();
	}

	private async Task ProcessScheduleAsync(IScheduleExecutionStore? executionStore,
		IScheduledMessage item, Guid generation, CancellationToken stoppingToken)
	{
		var pollNow = _timeProvider.GetUtcNow();
		var recurring = !string.IsNullOrWhiteSpace(item.CronExpression) || item.Interval is not null;
		var tolerance = _schedulerOptions.PollInterval > _cronOptions.ExecutionToleranceWindow
			? _schedulerOptions.PollInterval : _cronOptions.ExecutionToleranceWindow;
		var missed = recurring && item.NextExecutionUtc < pollNow.Subtract(tolerance);
		var behavior = item.MissedExecutionBehavior ?? _cronOptions.MissedExecutionBehavior;
		if (missed && !Enum.IsDefined(behavior)) { LogUnknownBehavior(behavior, item.Id); }
		var replayAll = missed && behavior == MissedExecutionBehavior.ExecuteAllMissed;
		var skip = missed && (behavior == MissedExecutionBehavior.SkipMissed
			|| (replayAll && _cronOptions.MaxMissedExecutions == 0));
		var limit = replayAll && !skip ? _cronOptions.MaxMissedExecutions : 1;

		var catchUpAttempts = 0;
		var renewalStore = executionStore as IScheduleExecutionLeaseRenewal;
		var leaseDuration = renewalStore is not null ? ScheduleExecutionRenewal.ValidateDuration(renewalStore) : TimeSpan.Zero;
		try
		{
			for (var attempt = 0; attempt < limit && item.Enabled && item.NextExecutionUtc is { } occurrence
				&& occurrence <= pollNow; attempt++)
			{
				var acquisitionStarted = _timeProvider.GetTimestamp();
				var lease = executionStore is not null
					? await executionStore.TryAcquireExecutionAsync(item.Id, generation, occurrence, stoppingToken).ConfigureAwait(false)
					: null;
				if (executionStore is not null && lease is null) { return; }
				var occurrenceToken = stoppingToken;
				var committed = false;
				var released = executionStore is null;
				var continueReplay = true;
				AsyncServiceScope? occurrenceScope = null;
				try
				{
					// The admitted occurrence owns its dependencies through dispatch and fenced settlement.
					// Catch-up iterations deliberately create distinct scopes; none captures host-root services.
					occurrenceScope = scopeFactory.CreateAsyncScope();
					await using var renewal = renewalStore is not null && lease is not null
						? new ScheduleExecutionRenewal(renewalStore, lease, leaseDuration, acquisitionStarted,
							_timeProvider, LogErrorProcessingMessages, stoppingToken) : null;
					occurrenceToken = renewal?.Token ?? stoppingToken;
					occurrenceToken.ThrowIfCancellationRequested();
					renewal?.ThrowIfOwnershipLost();
					if (missed && behavior == MissedExecutionBehavior.DisableSchedule)
					{
						item.Enabled = false;
						LogDisablingSchedule(item.Id);
					}
					else
					{
						if (!skip)
						{
							try
							{
								catchUpAttempts++;
								continueReplay = await ProcessScheduledMessageWithTimeoutAsync(item, occurrenceScope.Value.ServiceProvider, occurrenceToken).ConfigureAwait(false);
							}
							catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
							catch (Exception) when (occurrenceToken.IsCancellationRequested) { return; }
							catch (Exception ex) { continueReplay = false; LogErrorProcessingMessages(ex); }
						}
						occurrenceToken.ThrowIfCancellationRequested();
						renewal?.ThrowIfOwnershipLost();
						try
						{
							// Replay follows the persisted occurrence. Ordinary/coalesced schedules retain
							// completion-based timing and cannot generate an invented historical admission.
							var from = replayAll && !skip && continueReplay ? occurrence : _timeProvider.GetUtcNow();
							if (!string.IsNullOrWhiteSpace(item.CronExpression))
							{
								var timeZone = GetTimeZone(item.TimeZoneId);
								item.NextExecutionUtc = cronScheduler.Parse(item.CronExpression, timeZone).GetNextOccurrenceUtc(from);
								if (_cronOptions.EnableDetailedLogging) { LogNextExecutionCalculated(item.Id, item.NextExecutionUtc, timeZone.Id); }
							}
							else if (item.Interval is { } interval) { item.NextExecutionUtc = from.Add(interval); }
							else { item.Enabled = false; }
							if (item.Enabled && (item.NextExecutionUtc is null || item.NextExecutionUtc <= occurrence))
							{
								throw new InvalidOperationException("The schedule did not produce a later occurrence.");
							}
						}
						catch (Exception ex)
						{
							item.Enabled = false;
							LogScheduleDisabledAfterFailure(item.Id, ex);
						}
					}
					occurrenceToken.ThrowIfCancellationRequested();
					renewal?.ThrowIfOwnershipLost();
					if (executionStore is not null)
					{
						committed = await executionStore.TryUpdateProgressAsync(lease!,
							new ScheduleExecutionResult(item.LastExecutionUtc, item.NextExecutionUtc, !item.Enabled), occurrenceToken).ConfigureAwait(false);
					}
					else
					{
						await scheduleStore.StoreAsync(item, stoppingToken).ConfigureAwait(false);
						committed = true;
					}
				}
				finally
				{
					try
					{
						// Shutdown must release ownership without advancing the retained due occurrence.
						// This deadline is independent of the stopped host and the scheduler's test clock.
						if (executionStore is not null && lease is not null)
						{
							using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
							try
							{
								released = await executionStore.ReleaseExecutionAsync(lease, cleanup.Token)
									.WaitAsync(cleanup.Token).ConfigureAwait(false);
							}
							catch (Exception ex) { LogErrorProcessingMessages(ex); }
						}
					}
					finally
					{
						if (occurrenceScope is { } ownedScope)
						{
							await ownedScope.DisposeAsync().ConfigureAwait(false);
						}
					}
				}
				if (!committed || !released || !continueReplay || occurrenceToken.IsCancellationRequested) { return; }
			}
		}
		finally
		{
			if (missed) { LogFoundMissedExecutions(catchUpAttempts, item.Id); }
		}
	}

	private async Task<bool> ProcessScheduledMessageWithTimeoutAsync(IScheduledMessage item, IServiceProvider scopedProvider, CancellationToken cancellationToken)
	{
		// Current and catch-up executions each own their timeout through completion of dispatch.
		using var processCts = CreateTimeoutToken(TimeoutOperationType.Handler, cancellationToken);
		return await ProcessScheduledMessageAsync(item, scopedProvider, processCts.Token).ConfigureAwait(false);
	}

	private async Task<bool> ProcessScheduledMessageAsync(IScheduledMessage item, IServiceProvider scopedProvider, CancellationToken cancellationToken)
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
			return false;
		}

		var message = await serializer.DeserializeAsync(item.MessageBody, type).ConfigureAwait(false);
		if (message is null)
		{
			LogDeserializationFailed(item.Id);
			return false;
		}

		var context = DispatchContextInitializer.CreateDefaultContext(scopedProvider);
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

		IMessageResult? result;
		switch (message)
		{
			case IDispatchAction action:
				result = await dispatcher.DispatchAsync(action, context, cancellationToken).ConfigureAwait(false);
				break;

			case IDispatchEvent evt:
				result = await dispatcher.DispatchAsync(evt, context, cancellationToken).ConfigureAwait(false);
				break;

			case IDispatchMessage dispatchMessage:
				result = await dispatcher.DispatchAsync(dispatchMessage, context, cancellationToken).ConfigureAwait(false);
				break;

			default:
				LogUnsupportedMessageType(message.GetType().Name);
				return false;
		}

		// A handler may swallow cancellation and return success. The processing deadline
		// still owns this attempt, so never record success after its token has expired.
		cancellationToken.ThrowIfCancellationRequested();
		if (result is null || !result.Succeeded)
		{
			// Returned failures follow the same per-row policy as thrown failures. A cancelled
			// dispatch token retains host-shutdown and handler-deadline ownership at the caller.
			throw new InvalidOperationException(result?.ErrorMessage ?? result?.ProblemDetails?.Detail
				?? "Scheduled message dispatch did not succeed.");
		}

		// Update last execution time
		item.LastExecutionUtc = _timeProvider.GetUtcNow();
		return true;
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
	private TimeoutTokenScope CreateTimeoutToken(TimeoutOperationType operationType, CancellationToken parentToken)
	{
		var timeout = timePolicy is not null && timePolicy.ShouldApplyTimeout(operationType, null)
			? timePolicy.GetTimeoutFor(operationType)
			: Timeout.InfiniteTimeSpan;
		return new TimeoutTokenScope(timeout, _timeProvider, parentToken);
	}

	private sealed class TimeoutTokenScope : IDisposable
	{
		private readonly CancellationTokenSource? _deadline;
		private readonly CancellationTokenSource _linked;

		public TimeoutTokenScope(TimeSpan timeout, TimeProvider clock, CancellationToken parentToken)
		{
			if (timeout == Timeout.InfiniteTimeSpan)
			{
				_linked = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
				return;
			}

			_deadline = new CancellationTokenSource(timeout, clock);
			try
			{
				_linked = CancellationTokenSource.CreateLinkedTokenSource(parentToken, _deadline.Token);
			}
			catch
			{
				_deadline.Dispose();
				throw;
			}
		}

		public CancellationToken Token => _linked.Token;

		public void Dispose()
		{
			_linked.Dispose();
			_deadline?.Dispose();
		}
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
		"Attempted {Count} catch-up executions for schedule {MessageId} in this poll")]
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
