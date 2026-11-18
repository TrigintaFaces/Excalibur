// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;

namespace Excalibur.LeaderElection;

/// <summary>One instance's permanent notification lifetime, independent of resource disposal.</summary>
internal sealed class LeaderElectionNotifications
{
    private readonly Lock _gate = new();
    private readonly Queue<QueueItem> _pending = new();
    private readonly Action<Exception>? _reportFailure;
    private readonly Func<Func<Task>, Task>? _scheduleWorker;
    private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource? _ownershipDrain;
    private int _ownershipPending;
    private Exception? _ownershipFailure;
    private Task _worker = Task.CompletedTask;
    private Task _workerObserver = Task.CompletedTask;
    private int _workers;
    private bool _dispatching;
    private bool _sealed;
    private Action? _pendingDiagnostic;
    private bool _diagnosticQueued;
    private Exception? _firstFailure;
    private static readonly AsyncLocal<CallbackScope?> Scope = new();

    // An injected scheduler must preserve asynchronous dispatch: callbacks cannot run under _gate.
    internal LeaderElectionNotifications(Action<Exception>? reportFailure = null, Func<Func<Task>, Task>? scheduleWorker = null)
    {
        _reportFailure = reportFailure;
        _scheduleWorker = scheduleWorker;
    }
    internal Task NotificationsCompleted => _completed.Task;
    internal Task DrainTask { get { lock (_gate) { return _ownershipDrain?.Task ?? Task.CompletedTask; } } }
    internal Task WorkerTask { get { lock (_gate) { return _worker; } } }
    internal Task WorkerObserverTask { get { lock (_gate) { return _workerObserver; } } }

    internal void ThrowIfReentrant() => ThrowIfNotificationReentrant(NotificationsCompleted);

    internal static void ThrowIfNotificationReentrant(Task domain)
    {
        for (var current = Scope.Value; current is not null; current = current.Parent)
        {
            if (current.Active && ReferenceEquals(current.Domain, domain))
            {
                throw new InvalidOperationException("Notification-reentrant start/drain is refused: it would join its own callback.");
            }
        }
    }

    internal static IDisposable EnterNotificationScope(Task domain)
    {
        var current = new CallbackScope(domain, Scope.Value);
        Scope.Value = current;
        return current;
    }

    internal Task Enqueue(Action callback) => EnqueueCore(callback, ownership: true);

    private Task EnqueueCore(Action callback, bool ownership)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (_gate)
        {
            if (_sealed) { throw new InvalidOperationException("Notification publication is permanently sealed."); }
            if (ownership)
            {
                if (_ownershipPending == 0 && _ownershipFailure is null)
                {
                    _ownershipDrain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                _ownershipPending++;
            }
            _pending.Enqueue(new QueueItem(callback, ownership));
            if (!_dispatching)
            {
                _dispatching = true;
                _workers++;
                // A blocked synchronous consumer must not occupy the resource actor's execution lane.
                try
                {
                    Func<Task> dispatch = () => LeaderElectionExecution.InvokeAsync(DispatchAsync);
                    _worker = _scheduleWorker is null
                        ? Task.Factory.StartNew(dispatch, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap()
                        : _scheduleWorker(dispatch);
                    _workerObserver = ObserveWorkerAsync(_worker);
                }
                catch (Exception failure)
                {
                    _workers--;
                    _dispatching = false;
                    _pending.Clear();
                    _ownershipPending = 0;
                    _firstFailure ??= failure;
                    FailOwnershipUnderLock(failure);
                    CompleteIfTerminalUnderLock();
                    throw;
                }
            }
            return _ownershipDrain?.Task ?? Task.CompletedTask;
        }
    }

    internal void Seal()
    {
        lock (_gate)
        {
            _sealed = true;
            CompleteIfTerminalUnderLock();
        }
    }

    internal void EnqueueDiagnostic(Action diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        lock (_gate)
        {
            if (_sealed) { return; }
            // Diagnostics can be emitted on every renewal. Retain one pending slot, not one
            // closure per renewal behind a blocked consumer or logger. Ownership events do not coalesce.
            _pendingDiagnostic = diagnostic;
            if (_diagnosticQueued) { return; }
            _diagnosticQueued = true;
            _ = EnqueueCore(DispatchDiagnostic, ownership: false);
        }
    }

    internal void EnqueueDiagnostic<T1>(Action<T1> write, T1 first) =>
        EnqueueDiagnostic(() => write(first));
    internal void EnqueueDiagnostic<T1, T2>(Action<T1, T2> write, T1 first, T2 second) =>
        EnqueueDiagnostic(() => write(first, second));
    internal void EnqueueDiagnostic<T1, T2, T3>(Action<T1, T2, T3> write, T1 first, T2 second, T3 third) =>
        EnqueueDiagnostic(() => write(first, second, third));
    internal void EnqueueDiagnostic<T1, T2, T3, T4>(Action<T1, T2, T3, T4> write, T1 first, T2 second, T3 third, T4 fourth) =>
        EnqueueDiagnostic(() => write(first, second, third, fourth));

    private void DispatchDiagnostic()
    {
        Action? diagnostic;
        lock (_gate)
        {
            diagnostic = _pendingDiagnostic;
            _pendingDiagnostic = null;
            _diagnosticQueued = false;
        }
        diagnostic?.Invoke();
    }

    internal void RecordFailure(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        StoreFailure(failure);
        ReportFailure(failure);
    }

    internal void InvokeEach<T>(EventHandler<T>? subscribers, object sender, T args) where T : EventArgs
    {
        if (subscribers is null) { return; }
        List<Exception>? failures = null;
        foreach (var entry in subscribers.GetInvocationList())
        {
            try { ((EventHandler<T>)entry)(sender, args); }
            catch (Exception failure)
            {
                StoreFailure(failure);
                (failures ??= []).Add(failure);
            }
        }
        // No instrumentation or diagnostic exception can prevent another subscriber from being tried.
        if (failures is not null)
        {
            foreach (var failure in failures) { ReportFailure(failure); }
        }
    }

    internal static Exception? InvokeAll<T>(EventHandler<T>? subscribers, object sender, T args) where T : EventArgs
    {
        Exception? first = null;
        if (subscribers is null) { return null; }
        foreach (var entry in subscribers.GetInvocationList())
        {
            try { ((EventHandler<T>)entry)(sender, args); }
            catch (Exception failure) { first ??= failure; }
        }
        return first;
    }

    internal static void ThrowFirst(Exception? failure)
    {
        if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }

    private void StoreFailure(Exception failure)
    {
        lock (_gate) { _firstFailure ??= failure; }
    }

    private void ReportFailure(Exception failure)
    {
        if (_reportFailure is null) { return; }
        EnqueueDiagnostic(() =>
        {
            try { _reportFailure(failure); }
            catch (Exception diagnosticFailure) { StoreFailure(diagnosticFailure); }
        });
    }

    private Task DispatchAsync()
    {
        while (true)
        {
            QueueItem next;
            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    _dispatching = false;
                    return Task.CompletedTask;
                }
                next = _pending.Dequeue();
            }
            try
            {
                using var scope = EnterNotificationScope(NotificationsCompleted);
                try { next.Callback(); }
                catch (Exception failure) { RecordFailure(failure); }
            }
            finally
            {
                if (next.Ownership)
                {
                    lock (_gate)
                    {
                        _ownershipPending--;
                        if (_ownershipPending == 0 && _ownershipFailure is null) { _ownershipDrain?.TrySetResult(); }
                    }
                }
            }
        }
    }

    private async Task ObserveWorkerAsync(Task worker)
    {
        Exception? failure = null;
        try { await worker.ConfigureAwait(false); }
        catch (Exception error) { failure = error; }
        lock (_gate)
        {
            if (failure is not null)
            {
                _firstFailure ??= failure;
                // A fatal infrastructure/context failure poisons this instance's ownership barrier,
                // even when dispatch returned or another worker already owns a newer queue.
                FailOwnershipUnderLock(failure);
                if (ReferenceEquals(_worker, worker) && _dispatching)
                {
                    // The execution environment failed before normal callback dispatch could finish.
                    // Fail this lifetime rather than retaining an unreachable queue or claiming success.
                    foreach (var item in _pending)
                    {
                        if (item.Ownership) { _ownershipPending--; }
                    }
                    _pending.Clear();
                    _dispatching = false;
                }
            }
            _workers--;
            CompleteIfTerminalUnderLock();
        }
    }

    private void FailOwnershipUnderLock(Exception failure)
    {
        _ownershipFailure ??= failure;
        if (_ownershipDrain is null || _ownershipDrain.Task.IsCompletedSuccessfully)
        {
            _ownershipDrain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        _ownershipDrain.TrySetException(_ownershipFailure);
    }

    private readonly record struct QueueItem(Action Callback, bool Ownership);

    private void CompleteIfTerminalUnderLock()
    {
        if (!_sealed || _dispatching || _pending.Count != 0 || _workers != 0) { return; }
        if (_firstFailure is { } failure) { _completed.TrySetException(failure); }
        else { _completed.TrySetResult(); }
    }

    private sealed class CallbackScope(Task domain, CallbackScope? parent) : IDisposable
    {
        private int _active = 1;
        internal Task Domain { get; } = domain;
        internal CallbackScope? Parent { get; } = parent;
        internal bool Active => Volatile.Read(ref _active) != 0;
        public void Dispose()
        {
            Volatile.Write(ref _active, 0);
            if (ReferenceEquals(Scope.Value, this)) { Scope.Value = Parent; }
        }
    }
}
