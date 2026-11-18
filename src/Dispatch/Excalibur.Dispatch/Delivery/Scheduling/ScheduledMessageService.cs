// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0



using Excalibur.Dispatch.Diagnostics;
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
	TimeProvider? timeProvider = null,
	ScheduleDispositionHealth? dispositionHealth = null) : BackgroundService
{
	private readonly IScheduleExecutionFailureStore _failureStore = scheduleStore as IScheduleExecutionFailureStore
		?? throw new ArgumentException("RetainAndQuarantine requires an authoritative failure-capable schedule store.", nameof(scheduleStore));
	private readonly ScheduleDispositionHealth _dispositionHealth = dispositionHealth ?? new ScheduleDispositionHealth();
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
				var executionStore = _failureStore;
				var schedules = (await executionStore.GetExecutionSnapshotsAsync(retrievalCts.Token).ConfigureAwait(false))
					.Select(static snapshot => (Item: snapshot.Message, snapshot.Generation)).ToArray();
				_dispositionHealth.RetainCurrent(schedules.Select(static row => (row.Item.Id, row.Generation, row.Item.NextExecutionUtc)));
				foreach (var (item, generation) in schedules)
				{
					if (item.NextExecutionUtc is { } pendingOccurrence
						&& !_dispositionHealth.CanAttempt(item.Id, generation, pendingOccurrence, _timeProvider.GetUtcNow())) { continue; }
					await ReconcileDispositionAsync(item, generation, stoppingToken).ConfigureAwait(false);
					if (!item.Enabled || item.NextExecutionUtc is null || item.NextExecutionUtc > _timeProvider.GetUtcNow())
					{
						continue;
					}

					var polledOccurrence = item.NextExecutionUtc.Value;
					hadWork = true;
					try
					{
						var failure = await ProcessScheduleAsync(executionStore, item, generation, stoppingToken).ConfigureAwait(false);
						if (failure is not null && _dispositionHealth.Unresolved(item.Id, generation, failure.OccurrenceUtc,
							_timeProvider.GetUtcNow(), _schedulerOptions.PollInterval, 0, ScheduleFailureDisposition.Retrying))
						{
							LogErrorProcessingMessages(failure.Exception);
						}
					}
					catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
					catch (Exception ex)
					{
						// Unknown admission/progress state is degraded, never a successful quarantine.
						// Preserve the polled key even when a detached working copy calculated a later next.
						if (_dispositionHealth.Unresolved(item.Id, generation, polledOccurrence,
							_timeProvider.GetUtcNow(), _schedulerOptions.PollInterval, 0, ScheduleFailureDisposition.Retrying))
						{
							LogErrorProcessingMessages(ex);
						}
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

	private async Task<ScheduleProcessingFailure?> ProcessScheduleAsync(IScheduleExecutionStore? executionStore,
		IScheduledMessage item, Guid generation, CancellationToken stoppingToken)
	{
		var pollNow = _timeProvider.GetUtcNow();
		var workingOccurrence = item.NextExecutionUtc.GetValueOrDefault();
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
				workingOccurrence = occurrence;
				var acquisitionStarted = _timeProvider.GetTimestamp();
				var admission = await _failureStore.TryAcquireExecutionAsync(item.Id, generation, occurrence,
					_schedulerOptions.MaxAdmissionAttemptsPerOccurrence, stoppingToken).ConfigureAwait(false);
				if (admission is null)
				{
					LogAdmissionUnavailable(item.Id, generation, occurrence);
					return null;
				}
				var lease = admission.Lease;
				var retryUnsettled = !admission.PolicyDispositionAllowed;
				if (retryUnsettled) { skip = false; replayAll = replayAll && _cronOptions.MaxMissedExecutions > 0; limit = 1; }

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
					if (lease.AdmissionAttempt > admission.CapturedMaxAdmissionAttempts)
					{
						if (await RecordOccurrenceFailureAsync(item, lease,
							new ScheduleOccurrenceFailure(ScheduleFailureReason.AdmissionBudgetExhausted,
								ScheduleFailureDisposition.Quarantined), occurrenceToken).ConfigureAwait(false))
						{
							LogScheduleRetiredAfterUnsettledAdmissions(item.Id, occurrence, lease.AdmissionAttempt,
								admission.CapturedMaxAdmissionAttempts);
						}
						return null;
					}
					else if (missed && behavior == MissedExecutionBehavior.DisableSchedule && !retryUnsettled)
					{
						item.Enabled = false;
						LogDisablingSchedule(item.Id);
					}
					else
					{
						ScheduleFailureReason? failureReason = null;
						if (!skip)
						{
							try
							{
								catchUpAttempts++;
								failureReason = await ProcessScheduledMessageWithTimeoutAsync(item, lease, occurrenceScope.Value.ServiceProvider, occurrenceToken).ConfigureAwait(false);
								continueReplay = failureReason is null;
							}
							catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
							catch (Exception) when (occurrenceToken.IsCancellationRequested) { return null; }
							catch (Exception ex)
							{
								continueReplay = false;
								failureReason = ex is OperationCanceledException ? ScheduleFailureReason.HandlerTimeout : ScheduleFailureReason.HandlerFailed;
								LogErrorProcessingMessages(ex);
							}
						}
						occurrenceToken.ThrowIfCancellationRequested();
						renewal?.ThrowIfOwnershipLost();
						if (failureReason is { } failed)
						{
							await RecordOccurrenceFailureAsync(item, lease,
								new ScheduleOccurrenceFailure(failed, ScheduleFailureDisposition.Retrying), occurrenceToken).ConfigureAwait(false);
							return null;
						}
                        // Replay follows the persisted occurrence. Ordinary/coalesced schedules retain
                        // completion-based timing and cannot generate an invented historical admission.
                        CalculateNextExecution(item, occurrence, replayAll && !skip && continueReplay);
					}
					occurrenceToken.ThrowIfCancellationRequested();
					renewal?.ThrowIfOwnershipLost();
					var completion = missed && behavior == MissedExecutionBehavior.DisableSchedule && !retryUnsettled
						? ScheduleExecutionCompletionDisposition.PolicyDisabled
						: skip ? ScheduleExecutionCompletionDisposition.PolicySkipped : ScheduleExecutionCompletionDisposition.Fulfilled;
					committed = await _failureStore.TryCompleteExecutionAsync(lease,
						new ScheduleExecutionResult(item.LastExecutionUtc, item.NextExecutionUtc, !item.Enabled), completion, occurrenceToken).ConfigureAwait(false);
					if (committed) { _dispositionHealth.Clear(item.Id, generation, occurrence); }
                    else if (_dispositionHealth.Unresolved(item.Id, generation, occurrence,
                        _timeProvider.GetUtcNow(), _schedulerOptions.PollInterval, 0, ScheduleFailureDisposition.Retrying))
                    {
                        LogErrorProcessingMessages(new InvalidOperationException("Conditional schedule completion was not confirmed; the original occurrence remains unresolved."));
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
				if (!committed || !released || !continueReplay || occurrenceToken.IsCancellationRequested) { return null; }
			}
			return null;
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
		catch (Exception ex) { return new ScheduleProcessingFailure(workingOccurrence, ex); }
		finally
		{
			if (missed) { LogFoundMissedExecutions(catchUpAttempts, item.Id); }
		}
	}

    // One cohesive operation: calculate a future cron/interval occurrence, validate forward
    // movement, or retain the existing explicit configuration-pause behavior.
    private void CalculateNextExecution(IScheduledMessage item, DateTimeOffset occurrence, bool advanceFromOccurrence)
    {
        try
        {
            var from = advanceFromOccurrence ? occurrence : _timeProvider.GetUtcNow();
            if (!string.IsNullOrWhiteSpace(item.CronExpression))
            {
                var timeZone = CronRecurrenceValidation.ResolveStoredTimeZone(item.TimeZoneId);
                item.NextExecutionUtc = cronScheduler.Parse(item.CronExpression, timeZone).GetNextOccurrenceUtc(from);
                if (item.NextExecutionUtc is null) { item.Enabled = false; } // Legitimate finite cron exhaustion.
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
            LogScheduleDisabledAfterFailure(item.Id, new InvalidOperationException("FutureConfigurationBlocked: future configuration is invalid; attempting conditional completion and pause. Repair the retained configuration before resuming.", ex));
        }
    }

	private async Task<ScheduleFailureReason?> ProcessScheduledMessageWithTimeoutAsync(IScheduledMessage item, ScheduleExecutionLease lease, IServiceProvider scopedProvider, CancellationToken cancellationToken)
	{
		// Current and catch-up executions each own their timeout through completion of dispatch.
		using var processCts = CreateTimeoutToken(TimeoutOperationType.Handler, cancellationToken);
		return await ScheduledMessageDispatcher.DispatchAsync(item, lease, scopedProvider, dispatcher, serializer, _timeProvider, logger, processCts.Token).ConfigureAwait(false);
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




	[LoggerMessage(DeliveryEventId.ScheduledAdmissionUnavailable, LogLevel.Debug,
		"Schedule {MessageId} generation {Generation} was due at {Occurrence} but could not be admitted; another instance may hold it, or its stored occurrence no longer matches this snapshot")]
	private partial void LogAdmissionUnavailable(Guid messageId, Guid generation, DateTimeOffset occurrence);

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



	[LoggerMessage(DeliveryEventId.ScheduledNextExecution, LogLevel.Debug,
		"Next execution for schedule {MessageId} calculated as {NextRun} in timezone {TimeZone}")]
	private partial void LogNextExecutionCalculated(Guid messageId, DateTimeOffset? nextRun, string timeZone);

	[LoggerMessage(DeliveryEventId.ScheduledDisabledAfterFailure, LogLevel.Warning,
		"Schedule {ScheduleId} failed and its next execution could not be advanced, so it has been disabled; "
		+ "it will not be retried until it is re-enabled")]
	private partial void LogScheduleDisabledAfterFailure(Guid scheduleId, Exception exception);

	[LoggerMessage(DeliveryEventId.ScheduleRetiredAfterUnsettledAdmissions, LogLevel.Error,
		"Schedule '{ScheduleId}' occurrence {Occurrence} was admitted {Attempt} times without a committed "
		+ "settlement, exceeding the configured bound of {Bound}. The handler was not invoked again and the "
		+ "schedule has been disabled; its executions may have run more than once")]
	private partial void LogScheduleRetiredAfterUnsettledAdmissions(Guid scheduleId, DateTimeOffset occurrence, int attempt, int bound);

	[LoggerMessage(DeliveryEventId.ScheduledTimezoneLookupFailed, LogLevel.Warning,
		"Failed to find timezone {TimeZoneId}, using default")]
	private partial void LogTimezoneLookupFailed(string timeZoneId, Exception ex);
}
