// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests;

// Existing fixtures observe/deny persistence through IScheduleStore. A real in-memory authority owns
// admission/budget/failure state; accepted fixture writes remain barriers, not invented lease responses.
internal sealed class ScheduleFixtureFailureAuthority(IScheduleStore source, TimeProvider? clock = null) : IScheduleExecutionFailureStore, IDisposable
{
    private readonly InMemoryScheduleStore _authority = new(clock);
    private readonly SemaphoreSlim _mutation = new(1, 1);
    private readonly Dictionary<Guid, ScheduleFailureExecutionAdmission> _owners = [];
    private readonly HashSet<Guid> _seeded = [];

    public void Dispose()
    {
        _mutation.Dispose();
        GC.SuppressFinalize(this);
    }

    public IScheduleExecutionFailureStore RuntimeStore => source is IScheduleStoreSignal signal ? new SignalAuthority(this, signal) : this;

    public async Task<IReadOnlyList<ScheduleExecutionSnapshot>> GetExecutionSnapshotsAsync(CancellationToken cancellationToken)
    {
        var rows = (await source.GetAllAsync(cancellationToken)).ToArray();
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            foreach (var row in rows)
            {
                if (!_seeded.Contains(row.Id))
                {
                    await _authority.StoreAsync(row, cancellationToken);
                    _seeded.Add(row.Id);
                }
            }
            var ids = rows.Select(x => x.Id).ToHashSet();
            return (await _authority.GetExecutionSnapshotsAsync(cancellationToken)).Where(x => ids.Contains(x.Message.Id)).ToArray();
        }
        finally { _mutation.Release(); }
    }

    public async Task<ScheduleFailureExecutionAdmission?> TryAcquireExecutionAsync(Guid id, Guid generation,
        DateTimeOffset occurrence, int requestedMaxAdmissionAttempts, CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var admission = await _authority.TryAcquireExecutionAsync(id, generation, occurrence, requestedMaxAdmissionAttempts, cancellationToken);
            if (admission is not null) { _owners[admission.Lease.OwnerToken] = admission; }
            return admission;
        }
        finally { _mutation.Release(); }
    }

    public Task<ScheduleExecutionLease?> TryAcquireExecutionAsync(Guid id, Guid generation, DateTimeOffset occurrence,
        CancellationToken cancellationToken) => _authority.TryAcquireExecutionAsync(id, generation, occurrence, cancellationToken);

    public async Task<bool> TryCompleteExecutionAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result,
        ScheduleExecutionCompletionDisposition disposition, CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var row = await CurrentOwnedRowAsync(lease, cancellationToken);
            if (row is null || !_owners.TryGetValue(lease.OwnerToken, out var admission)
                || admission.Lease.AdmissionAttempt > admission.CapturedMaxAdmissionAttempts
                || (!result.Disable && (result.NextExecutionUtc is null || result.NextExecutionUtc <= lease.OccurrenceUtc))
                || (disposition != ScheduleExecutionCompletionDisposition.Fulfilled
                    && (!admission.PolicyDispositionAllowed || row.LastExecutionUtc != result.LastExecutionUtc
                        || (disposition == ScheduleExecutionCompletionDisposition.PolicyDisabled && !result.Disable)
                        || (disposition == ScheduleExecutionCompletionDisposition.PolicySkipped && result.Disable))))
            {
                return false;
            }
            var retained = await _authority.GetOccurrenceFailureAsync(lease.ScheduleId, lease.Generation, lease.OccurrenceUtc, row.TenantId, cancellationToken);
            if (retained?.Disposition == ScheduleFailureDisposition.Quarantined
                || (disposition != ScheduleExecutionCompletionDisposition.Fulfilled && retained is not null)) { return false; }
            var projected = ScheduleExecutionSnapshot.Copy(row);
            projected.LastExecutionUtc = result.LastExecutionUtc;
            projected.NextExecutionUtc = result.NextExecutionUtc;
            if (result.Disable) { projected.Enabled = false; }
            await source.StoreAsync(projected, cancellationToken);
            // An accepted fixture write is this test double's commit boundary. Caller cancellation
            // after that acceptance cannot fictionally roll it back before the real authority is updated.
            var completed = await _authority.TryCompleteExecutionAsync(lease, result, disposition, CancellationToken.None);
            if (completed) { _owners.Remove(lease.OwnerToken); }
            return completed;
        }
        finally { _mutation.Release(); }
    }

    public async Task<bool> TryRecordFailureAsync(ScheduleExecutionLease lease, ScheduleOccurrenceFailure failure,
        CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var row = await CurrentOwnedRowAsync(lease, cancellationToken);
            if (row is null || !_owners.TryGetValue(lease.OwnerToken, out var admission)
                || (failure.Disposition == ScheduleFailureDisposition.Quarantined && admission.Lease.AdmissionAttempt <= admission.CapturedMaxAdmissionAttempts)
                || (failure.Disposition == ScheduleFailureDisposition.Retrying && admission.Lease.AdmissionAttempt > admission.CapturedMaxAdmissionAttempts)) { return false; }
            var projected = ScheduleExecutionSnapshot.Copy(row);
            if (failure.Disposition == ScheduleFailureDisposition.Quarantined) { projected.Enabled = false; }
            await source.StoreAsync(projected, cancellationToken);
            return await _authority.TryRecordFailureAsync(lease, failure, CancellationToken.None);
        }
        finally { _mutation.Release(); }
    }

    private async Task<IScheduledMessage?> CurrentOwnedRowAsync(ScheduleExecutionLease lease, CancellationToken cancellationToken)
    {
        if (!_owners.TryGetValue(lease.OwnerToken, out var admission)
            || admission.Lease.StoreId != lease.StoreId || admission.Lease.ScheduleId != lease.ScheduleId
            || admission.Lease.Generation != lease.Generation || admission.Lease.OccurrenceUtc != lease.OccurrenceUtc)
        {
            return null;
        }
        return (await _authority.GetExecutionSnapshotsAsync(cancellationToken))
            .SingleOrDefault(x => x.Generation == lease.Generation && x.Message.Id == lease.ScheduleId && x.Message.NextExecutionUtc == lease.OccurrenceUtc)?.Message;
    }

    public async Task<bool> ReleaseExecutionAsync(ScheduleExecutionLease lease, CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var released = await _authority.ReleaseExecutionAsync(lease, cancellationToken);
            if (released) { _owners.Remove(lease.OwnerToken); }
            return released;
        }
        finally { _mutation.Release(); }
    }

    public Task<bool> TryUpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result, CancellationToken cancellationToken) =>
        _authority.TryUpdateProgressAsync(lease, result, cancellationToken);
    public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken) => _authority.GetAllAsync(cancellationToken);
    public async Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            await _authority.StoreAsync(message, cancellationToken);
            foreach (var token in _owners.Where(pair => pair.Value.Lease.ScheduleId == message.Id).Select(pair => pair.Key).ToArray()) { _owners.Remove(token); }
        }
        finally { _mutation.Release(); }
    }
    public async Task CompleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            await _authority.CompleteAsync(id, cancellationToken);
            foreach (var token in _owners.Where(pair => pair.Value.Lease.ScheduleId == id).Select(pair => pair.Key).ToArray()) { _owners.Remove(token); }
        }
        finally { _mutation.Release(); }
    }
    public Task<ScheduleQuarantineRecoveryReceipt?> TryResumeQuarantinedOccurrenceAsync(ScheduleQuarantineToken token, Guid command,
        CancellationToken cancellationToken) => _authority.TryResumeQuarantinedOccurrenceAsync(token, command, cancellationToken);
    public Task<ScheduleOccurrenceFailureSnapshot?> GetOccurrenceFailureAsync(Guid id, Guid generation, DateTimeOffset occurrence,
        string? tenant, CancellationToken cancellationToken) => _authority.GetOccurrenceFailureAsync(id, generation, occurrence, tenant, cancellationToken);
    public Task<IReadOnlyList<ScheduleOccurrenceFailureSnapshot>> GetQuarantinesAsync(string? tenant, Guid? cursor, int maximum,
        CancellationToken cancellationToken) => _authority.GetQuarantinesAsync(tenant, cursor, maximum, cancellationToken);
    private sealed class SignalAuthority(IScheduleExecutionFailureStore inner, IScheduleStoreSignal signal)
        : IScheduleExecutionFailureStore, IScheduleStoreSignal
    {
        public ValueTask WaitForChangeAsync(TimeSpan timeout, CancellationToken ct) => signal.WaitForChangeAsync(timeout, ct);
        public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken ct) => inner.GetAllAsync(ct);
        public Task StoreAsync(IScheduledMessage row, CancellationToken ct) => inner.StoreAsync(row, ct);
        public Task CompleteAsync(Guid id, CancellationToken ct) => inner.CompleteAsync(id, ct);
        public Task<IReadOnlyList<ScheduleExecutionSnapshot>> GetExecutionSnapshotsAsync(CancellationToken ct) => inner.GetExecutionSnapshotsAsync(ct);
        public Task<ScheduleExecutionLease?> TryAcquireExecutionAsync(Guid id, Guid generation, DateTimeOffset occurrence, CancellationToken ct) => inner.TryAcquireExecutionAsync(id, generation, occurrence, ct);
        public Task<ScheduleFailureExecutionAdmission?> TryAcquireExecutionAsync(Guid id, Guid generation, DateTimeOffset occurrence, int maximum, CancellationToken ct) => inner.TryAcquireExecutionAsync(id, generation, occurrence, maximum, ct);
        public Task<bool> TryCompleteExecutionAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result, ScheduleExecutionCompletionDisposition disposition, CancellationToken ct) => inner.TryCompleteExecutionAsync(lease, result, disposition, ct);
        public Task<bool> TryRecordFailureAsync(ScheduleExecutionLease lease, ScheduleOccurrenceFailure failure, CancellationToken ct) => inner.TryRecordFailureAsync(lease, failure, ct);
        public Task<bool> TryUpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result, CancellationToken ct) => inner.TryUpdateProgressAsync(lease, result, ct);
        public Task<bool> ReleaseExecutionAsync(ScheduleExecutionLease lease, CancellationToken ct) => inner.ReleaseExecutionAsync(lease, ct);
        public Task<ScheduleQuarantineRecoveryReceipt?> TryResumeQuarantinedOccurrenceAsync(ScheduleQuarantineToken token, Guid command, CancellationToken ct) => inner.TryResumeQuarantinedOccurrenceAsync(token, command, ct);
        public Task<ScheduleOccurrenceFailureSnapshot?> GetOccurrenceFailureAsync(Guid id, Guid generation, DateTimeOffset occurrence, string? tenant, CancellationToken ct) => inner.GetOccurrenceFailureAsync(id, generation, occurrence, tenant, ct);
        public Task<IReadOnlyList<ScheduleOccurrenceFailureSnapshot>> GetQuarantinesAsync(string? tenant, Guid? cursor, int maximum, CancellationToken ct) => inner.GetQuarantinesAsync(tenant, cursor, maximum, ct);
    }

}
