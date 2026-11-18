// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

internal sealed partial class ScheduledMessageService
{
    private sealed record ScheduleProcessingFailure(DateTimeOffset OccurrenceUtc, Exception Exception);
    private async Task ReconcileDispositionAsync(IScheduledMessage item, Guid generation, CancellationToken cancellationToken)
    {
        if (item.NextExecutionUtc is not { } occurrence || !_dispositionHealth.IsPending(item.Id, generation, occurrence)) { return; }
        try
        {
            var failure = await _failureStore.GetOccurrenceFailureAsync(item.Id, generation, occurrence,
                item.TenantId, cancellationToken).ConfigureAwait(false);
            if (failure is not null && _dispositionHealth.Confirms(failure))
            {
                _dispositionHealth.Clear(item.Id, generation, occurrence);
            }
            else if (_dispositionHealth.DeferReconciliation(item.Id, generation, occurrence,
                _timeProvider.GetUtcNow(), _schedulerOptions.PollInterval))
            {
                LogErrorProcessingMessages(new InvalidOperationException($"PendingDispositionUnresolved for schedule {item.Id}; provider state is not confirmed."));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            if (_dispositionHealth.DeferReconciliation(item.Id, generation, occurrence,
                _timeProvider.GetUtcNow(), _schedulerOptions.PollInterval)) { LogErrorProcessingMessages(ex); }
            // One already-eligible cycle may still attempt a mutation; future cycles honor the new backoff.
        }
    }

    private async Task<bool> RecordOccurrenceFailureAsync(IScheduledMessage item, ScheduleExecutionLease lease,
        ScheduleOccurrenceFailure failure, CancellationToken cancellationToken)
    {
        try
        {
            if (await _failureStore.TryRecordFailureAsync(lease, failure, cancellationToken).ConfigureAwait(false))
            {
                _dispositionHealth.Clear(item.Id, lease.Generation, lease.OccurrenceUtc);
                return true;
            }
            var observed = await _failureStore.GetOccurrenceFailureAsync(item.Id, lease.Generation,
                lease.OccurrenceUtc, item.TenantId, cancellationToken).ConfigureAwait(false);
            if (observed is not null && observed.AdmissionAttempts >= lease.AdmissionAttempt
                && observed.Disposition == failure.Disposition)
            {
                _dispositionHealth.Clear(item.Id, lease.Generation, lease.OccurrenceUtc);
                return true;
            }
            if (_dispositionHealth.Unresolved(item.Id, lease.Generation, lease.OccurrenceUtc,
                _timeProvider.GetUtcNow(), _schedulerOptions.PollInterval, lease.AdmissionAttempt, failure.Disposition))
            {
                LogErrorProcessingMessages(new InvalidOperationException(
                    $"PendingDispositionUnresolved for schedule {item.Id}, generation {lease.Generation}, occurrence {lease.OccurrenceUtc:O}; failure recording did not confirm current authority."));
            }
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _dispositionHealth.Unresolved(item.Id, lease.Generation, lease.OccurrenceUtc,
                _timeProvider.GetUtcNow(), _schedulerOptions.PollInterval, lease.AdmissionAttempt, failure.Disposition);
            throw;
        }
        catch (Exception ex)
        {
            if (_dispositionHealth.Unresolved(item.Id, lease.Generation, lease.OccurrenceUtc,
                _timeProvider.GetUtcNow(), _schedulerOptions.PollInterval, lease.AdmissionAttempt, failure.Disposition)) { LogErrorProcessingMessages(ex); }
            // No progress/reset. A late successful mutation is reconciled by the next exact provider observation.
            return false;
        }
    }
}
