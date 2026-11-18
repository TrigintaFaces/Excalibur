// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Delivery;

internal sealed partial class InMemoryScheduleStore
{
    private readonly Dictionary<(Guid Id, Guid Generation, DateTimeOffset Occurrence), ScheduleOccurrenceFailureSnapshot> _failures = [];
    private readonly Dictionary<Guid, ScheduleQuarantineRecoveryReceipt> _recoveries = [];

    /// <inheritdoc />
    public Task<ScheduleFailureExecutionAdmission?> TryAcquireExecutionAsync(Guid scheduleId, Guid generation,
        DateTimeOffset occurrenceUtc, int requestedMaxAdmissionAttempts, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedMaxAdmissionAttempts);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanAcquire(scheduleId, generation, occurrenceUtc, out var entry))
            {
                return Task.FromResult<ScheduleFailureExecutionAdmission?>(null);
            }
            if (entry.MaxAdmissionAttempts is null && entry.AdmissionAttempts != 0)
            {
                throw new InvalidOperationException("This occurrence has consumed legacy admissions without a captured budget; explicit repair is required.");
            }
            if (entry.MaxAdmissionAttempts is null) { _ = CronRecurrenceValidation.Validate(entry.Message); }
            var selection = entry.MaxAdmissionAttempts is null
                ? ScheduleAdmissionPolicySelection.CapturedNew : ScheduleAdmissionPolicySelection.UsedExisting;
            var budget = entry.MaxAdmissionAttempts ?? requestedMaxAdmissionAttempts;
            var lease = GrantUnderLock(scheduleId, generation, occurrenceUtc, entry with { MaxAdmissionAttempts = budget });
            return Task.FromResult<ScheduleFailureExecutionAdmission?>(new(lease, budget, selection, PolicyDispositionAllowed(scheduleId, _store[scheduleId], occurrenceUtc)));
        }
    }

    /// <inheritdoc />
    public Task<bool> TryCompleteExecutionAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result,
        ScheduleExecutionCompletionDisposition disposition, CancellationToken cancellationToken) =>
        UpdateProgressAsync(lease, result, disposition, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryRecordFailureAsync(ScheduleExecutionLease lease, ScheduleOccurrenceFailure failure,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(failure);
        if (!Enum.IsDefined(failure.Reason) || !Enum.IsDefined(failure.Disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(failure));
        }
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Matches(lease, out var entry) || entry.ProgressCommitted || entry.MaxAdmissionAttempts is not { } budget)
            {
                return Task.FromResult(false);
            }
            var key = (lease.ScheduleId, lease.Generation, lease.OccurrenceUtc);
            _failures.TryGetValue(key, out var previous);
            if (entry.FailureOwner == lease.OwnerToken && previous is not null)
            {
                return Task.FromResult(entry.FailureRequest == failure);
            }
            if (previous?.Disposition == ScheduleFailureDisposition.Quarantined
                || (failure.Disposition == ScheduleFailureDisposition.Quarantined && entry.AdmissionAttempts <= budget)
                || (failure.Disposition == ScheduleFailureDisposition.Retrying && entry.AdmissionAttempts > budget))
            {
                return Task.FromResult(false);
            }
            var token = new ScheduleQuarantineToken(_storeId, previous?.Token.QuarantineId ?? Guid.NewGuid(),
                checked((previous?.Token.Version ?? 0) + 1), lease.ScheduleId, lease.Generation, lease.OccurrenceUtc);
            var snapshot = new ScheduleOccurrenceFailureSnapshot(token, failure.Disposition == ScheduleFailureDisposition.Quarantined
                ? previous?.Reason ?? failure.Reason : failure.Reason, failure.Disposition,
                entry.AdmissionAttempts, budget, _clock.GetUtcNow(), entry.Message);
            var message = ScheduleExecutionSnapshot.Copy(entry.Message);
            if (failure.Disposition == ScheduleFailureDisposition.Quarantined) { message.Enabled = false; }
            _failures[key] = snapshot;
            _store[lease.ScheduleId] = entry with { Message = message, FailureOwner = lease.OwnerToken, FailureRequest = failure };
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<ScheduleQuarantineRecoveryReceipt?> TryResumeQuarantinedOccurrenceAsync(
        ScheduleQuarantineToken expectedQuarantineToken, Guid recoveryCommandId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedQuarantineToken);
        if (recoveryCommandId == Guid.Empty) { throw new ArgumentException("A recovery command is required.", nameof(recoveryCommandId)); }
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_recoveries.TryGetValue(recoveryCommandId, out var replay))
            {
                return Task.FromResult<ScheduleQuarantineRecoveryReceipt?>(
                    replay.OriginalQuarantine == expectedQuarantineToken ? replay : null);
            }
            var token = expectedQuarantineToken;
            var key = (token.ScheduleId, token.Generation, token.OccurrenceUtc);
            if (token.StoreId != _storeId || !_failures.TryGetValue(key, out var failure) || failure.Token != token
                || failure.Disposition != ScheduleFailureDisposition.Quarantined || failure.RecoveryReceipt is not null
                || !_store.TryGetValue(token.ScheduleId, out var entry) || entry.Generation != token.Generation
                || entry.Message.NextExecutionUtc != token.OccurrenceUtc || entry.Message.Enabled)
            {
                return Task.FromResult<ScheduleQuarantineRecoveryReceipt?>(null);
            }
            var generation = Guid.NewGuid();
            var receipt = new ScheduleQuarantineRecoveryReceipt(recoveryCommandId, token, generation,
                token.OccurrenceUtc, failure.MaxAdmissionAttempts, _clock.GetUtcNow());
            var message = ScheduleExecutionSnapshot.Copy(failure.Message);
            message.Enabled = true;
            message.NextExecutionUtc = token.OccurrenceUtc;
            _store[token.ScheduleId] = new Entry(message, generation, MaxAdmissionAttempts: failure.MaxAdmissionAttempts, RecoveryLineage: true);
            _failures[key] = new ScheduleOccurrenceFailureSnapshot(failure.Token, failure.Reason, failure.Disposition,
                failure.AdmissionAttempts, failure.MaxAdmissionAttempts, failure.RecordedUtc, failure.Message, receipt);
            _recoveries[recoveryCommandId] = receipt;
            _changeChannel.Writer.TryWrite(true);
            return Task.FromResult<ScheduleQuarantineRecoveryReceipt?>(receipt);
        }
    }

    /// <inheritdoc />
    public Task<ScheduleOccurrenceFailureSnapshot?> GetOccurrenceFailureAsync(Guid scheduleId, Guid generation,
        DateTimeOffset occurrenceUtc, string? tenantId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _failures.TryGetValue((scheduleId, generation, occurrenceUtc), out var failure);
            return Task.FromResult(failure is not null && string.Equals(failure.Message.TenantId, tenantId, StringComparison.Ordinal)
                ? failure : null);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScheduleOccurrenceFailureSnapshot>> GetQuarantinesAsync(string? tenantId,
        Guid? afterQuarantineId, int maximumCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumCount, 256);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cursor = afterQuarantineId?.ToString("N");
            return Task.FromResult<IReadOnlyList<ScheduleOccurrenceFailureSnapshot>>(_failures.Values
                .Where(f => f.Disposition == ScheduleFailureDisposition.Quarantined
                    && string.Equals(f.Message.TenantId, tenantId, StringComparison.Ordinal)
                    && (cursor is null || string.CompareOrdinal(f.Token.QuarantineId.ToString("N"), cursor) > 0))
                .OrderBy(f => f.Token.QuarantineId.ToString("N"), StringComparer.Ordinal).Take(maximumCount).ToArray());
        }
    }

    private bool PolicyDispositionAllowed(Guid id, Entry entry, DateTimeOffset occurrence) => entry.AdmissionAttempts == 1
        && !_failures.ContainsKey((id, entry.Generation, occurrence))
        && !entry.RecoveryLineage;

    private bool HasUnresolvedFailure(Guid id, Entry entry) => entry.Message.NextExecutionUtc is { } occurrence
        && _failures.TryGetValue((id, entry.Generation, occurrence), out var failure) && failure.RecoveryReceipt is null;

    private void QuarantineForAdministrativeCompletion(Guid id, Entry entry)
    {
        var key = (id, entry.Generation, entry.Message.NextExecutionUtc!.Value);
        _failures.TryGetValue(key, out var failure);
        if (failure?.Disposition == ScheduleFailureDisposition.Quarantined) { return; }
        var token = new ScheduleQuarantineToken(_storeId, failure?.Token.QuarantineId ?? Guid.NewGuid(),
            checked((failure?.Token.Version ?? 0) + 1), id, entry.Generation, entry.Message.NextExecutionUtc.Value);
        _failures[key] = new ScheduleOccurrenceFailureSnapshot(token,
            failure?.Reason ?? ScheduleFailureReason.AdministrativeCancellation, ScheduleFailureDisposition.Quarantined,
            entry.AdmissionAttempts, entry.MaxAdmissionAttempts ?? failure!.MaxAdmissionAttempts,
            _clock.GetUtcNow(), failure?.Message ?? entry.Message);
        var message = ScheduleExecutionSnapshot.Copy(entry.Message);
        message.Enabled = false;
        _store[id] = entry with { Message = message, Owner = null };
        _changeChannel.Writer.TryWrite(true);
    }
}
