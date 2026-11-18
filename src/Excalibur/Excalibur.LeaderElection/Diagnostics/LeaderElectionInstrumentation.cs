// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.LeaderElection.Diagnostics;

/// <summary>Explicit instrument ownership with one owner pin and bounded per-wrapper leases.</summary>
internal sealed class LeaderElectionInstrumentation : IDisposable
{
    private readonly Lock _gate = new();
    private readonly bool _ownsMeter;
    private readonly bool _ownsActivity;
    private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _leases = 1;
    private bool _sealed;
    private Exception? _firstFailure;

    internal LeaderElectionInstrumentation(Meter meter, ActivitySource activity, bool ownsMeter, bool ownsActivity)
    {
        Meter = meter;
        Activity = activity;
        _ownsMeter = ownsMeter;
        _ownsActivity = ownsActivity;
    }

    internal Meter Meter { get; }
    internal ActivitySource Activity { get; }
    internal Task Completion => _completed.Task;

    internal static LeaderElectionInstrumentation Create(IMeterFactory? factory) => new(
        factory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName),
        new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName),
        ownsMeter: factory is null, ownsActivity: true);

    internal InstrumentLease Acquire()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_sealed, this);
            checked { _leases++; }
            return new InstrumentLease(this);
        }
    }

    public void Dispose()
    {
        var close = false;
        lock (_gate)
        {
            if (!_sealed) { _sealed = true; close = --_leases == 0; }
        }
        if (close) { CloseOwnedInstruments(); }
        if (_completed.Task.IsFaulted) { LeaderElectionNotifications.ThrowFirst(_completed.Task.Exception?.InnerException); }
    }

    internal void RecordFailure(Exception failure)
    {
        lock (_gate) { _firstFailure ??= failure; }
    }

    private void Release()
    {
        var close = false;
        lock (_gate) { close = --_leases == 0 && _sealed; }
        if (close) { CloseOwnedInstruments(); }
    }

    private void CloseOwnedInstruments()
    {
        // Foreign disposal never executes under the lease gate.
        if (_ownsActivity)
        {
            try { Activity.Dispose(); }
            catch (Exception failure) { RecordFailure(failure); }
        }
        if (_ownsMeter)
        {
            try { Meter.Dispose(); }
            catch (Exception failure) { RecordFailure(failure); }
        }
        Exception? error;
        lock (_gate) { error = _firstFailure; }
        if (error is null) { _completed.TrySetResult(); }
        else { _completed.TrySetException(error); }
        LeaderElectionNotifications.ThrowFirst(error);
    }

    internal sealed class InstrumentLease : IDisposable
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "This lease borrows the shared owner and releases one reference; disposing the owner here would seal unrelated factory children.")]
        private LeaderElectionInstrumentation? _owner;
        private Task _cleanup = Task.CompletedTask;
        internal InstrumentLease(LeaderElectionInstrumentation owner) => _owner = owner;
        internal Task CleanupTask => _cleanup;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();

        internal void RetainFailedCreationCleanup(Excalibur.Dispatch.LeaderElection.ILeaderElection inner)
        {
            var owner = Volatile.Read(ref _owner) ?? throw new ObjectDisposedException(nameof(InstrumentLease));
            // The lease itself pins the owner while this tracked cleanup is pending.
            _cleanup = LeaderElectionExecution.RunAsync(async () =>
            {
                try
                {
                    try { await inner.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception failure) { owner.RecordFailure(failure); }
                    try { await inner.NotificationsCompleted.ConfigureAwait(false); }
                    catch (Exception failure) { owner.RecordFailure(failure); }
                }
                finally
                {
                    try { Dispose(); }
                    catch (Exception failure) { owner.RecordFailure(failure); }
                }
            });
        }
    }
}
