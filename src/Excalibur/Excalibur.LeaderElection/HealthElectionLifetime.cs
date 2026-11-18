// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.LeaderElection;

// Owns actual work, not caller waits. No native or consumer code executes under _gate.
internal sealed class HealthElectionLifetime(
    Func<CancellationToken, Task> start,
    Func<CancellationToken, Task> stop,
    Func<ValueTask> dispose,
    Func<CancellationToken, Task> remove,
    Action? completeDisposal = null,
    Func<Task>? afterWorkCleanup = null,
    Action? beforeStart = null)
{
    private static readonly AsyncLocal<NativeScope?> ActiveNative = new();
    private static readonly AsyncLocal<Work?> CurrentWork = new();
    private readonly Lock _gate = new();
    private readonly HashSet<Work> _active = [];
    private Task? _starting;
    private Task? _stopping;
    private Task? _disposal;
    private volatile bool _disposed;

    internal Task StartAsync(CancellationToken token)
    {
        beforeStart?.Invoke();
        ThrowIfNativeReentrant();
        return StartCoreAsync(token);
    }

    private async Task StartCoreAsync(CancellationToken caller)
    {
        while (true)
        {
            Work? launch = null;
            Task pending;
            bool stopping;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                stopping = _stopping is not null && !_stopping.IsCompletedSuccessfully;
                if (stopping) { pending = _stopping!; }
                else
                {
                    if (_starting is null)
                    {
                        launch = new Work(this, true, CancellationToken.None, isStart: true);
                        _active.Add(launch);
                        _starting = launch.Completion.Task;
                    }
                    pending = _starting;
                }
            }
            if (launch is not null) { _ = ExecuteAsync(launch, start, null); }
            try { await pending.WaitAsync(caller).ConfigureAwait(false); }
            catch (OperationCanceledException) when (caller.IsCancellationRequested)
            {
                if (!stopping) { _ = BeginStop(pending); }
                throw;
            }
            if (!stopping) { return; }
        }
    }

    internal Task MutateAsync(Func<CancellationToken, Task> sdk, Func<Task>? authority, CancellationToken token) =>
        RunAsync(true, ct => NativeAsync(sdk, ct), authority, token);

    internal Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> sdk, CancellationToken token) =>
        ReadCoreAsync(sdk, token);

    private async Task<T> ReadCoreAsync<T>(Func<CancellationToken, Task<T>> sdk, CancellationToken token)
    {
        T result = default!;
        await RunAsync(false, async ct => { result = await NativeAsync(sdk, ct).ConfigureAwait(false); }, null, token).ConfigureAwait(false);
        return result;
    }

    private async Task RunAsync(bool mutation, Func<CancellationToken, Task> body, Func<Task>? authority, CancellationToken caller)
    {
        Work work;
        while (true)
        {
            Task? pending;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                pending = mutation ? _stopping : null;
                if (pending is null || pending.IsCompletedSuccessfully)
                {
                    work = new Work(this, mutation, caller);
                    _active.Add(work); // Completion is registered before the first native instruction.
                    break;
                }
            }
            await pending.WaitAsync(caller).ConfigureAwait(false);
        }
        _ = ExecuteAsync(work, body, authority);
        await work.Completion.Task.WaitAsync(caller).ConfigureAwait(false);
    }

    private async Task ExecuteAsync(Work work, Func<CancellationToken, Task> body, Func<Task>? authority)
    {
        Exception? failure = null;
        var previousWork = CurrentWork.Value;
        CurrentWork.Value = work;
        try
        {
            work.Token.ThrowIfCancellationRequested();
            await body(work.Token).ConfigureAwait(false);
            // Retain authority until it settles; Stop cannot reopen a newer generation.
            if (authority is not null) { await authority().ConfigureAwait(false); }
        }
        catch (Exception error) { failure = error; }
        finally { CurrentWork.Value = previousWork; }
        try { await work.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); }
        // Internal deterministic scheduler seam; production wrappers leave it unset.
        try { if (afterWorkCleanup is not null) { await afterWorkCleanup().ConfigureAwait(false); } }
        catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); }
        TaskCompletionSource? rollback = null;
        Work[] snapshot = [];
        lock (_gate)
        {
            _active.Remove(work);
            // Failed DDL/start can retry only after registered rollback reaches quiescence.
            if (work.IsStart && failure is not null && ReferenceEquals(_starting, work.Completion.Task) && !_disposed)
            { SealStopUnderGate(out rollback, out snapshot); }
            if (failure is OperationCanceledException canceled && work.Token.IsCancellationRequested && canceled.CancellationToken == work.Token)
            { work.Completion.TrySetCanceled(canceled.CancellationToken); }
            else if (failure is not null) { work.Completion.TrySetException(failure); }
            else { work.Completion.TrySetResult(); }
        }
        if (rollback is not null) { _ = StopCoreAsync(snapshot, rollback); }
    }

    internal Task StopAsync(CancellationToken caller)
    {
        ThrowIfNativeReentrant();
        return BeginStop(null).WaitAsync(caller);
    }

    private Task BeginStop(Task? expectedStart)
    {
        TaskCompletionSource? completion;
        Work[] snapshot;
        Task result;
        lock (_gate)
        {
            if (expectedStart is not null && (_disposed || !ReferenceEquals(_starting, expectedStart)))
            { return Task.CompletedTask; } // Never let an old waiter stop a newer intent.
            ObjectDisposedException.ThrowIf(_disposed, this);
            SealStopUnderGate(out completion, out snapshot);
            result = _stopping!;
        }
        if (completion is not null) { _ = StopCoreAsync(snapshot, completion); }
        return result;
    }

    private void SealStopUnderGate(out TaskCompletionSource? completion, out Work[] snapshot)
    {
        completion = null; snapshot = [];
        if (_stopping is null || _stopping.IsCompletedSuccessfully)
        {
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _stopping = completion.Task;
            _starting = null;
            snapshot = _active.Where(static work => work.Mutation).ToArray();
        }
    }

    private async Task StopCoreAsync(Work[] snapshot, TaskCompletionSource completion)
    {
        var errors = new List<Exception>();
        // The first call retires current local truth before waiting for slow health SDK work.
        var first = InvokeStop();
        await CancelAndJoinAsync(snapshot, errors).ConfigureAwait(false);
        await ObserveAsync(first, errors).ConfigureAwait(false);
        await ObserveAsync(InvokeStop(), errors).ConfigureAwait(false);
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(6));
            await NativeAsync(remove, cleanup.Token).ConfigureAwait(false);
        }
        catch (Exception error) { errors.Add(error); }
        Complete(completion, errors);
    }

    private Task InvokeStop()
    {
        lock (_gate) { if (_disposed) { return Task.CompletedTask; } }
        try { return StopIgnoringTerminalRaceAsync(); }
        catch (ObjectDisposedException) when (IsDisposed) { return Task.CompletedTask; }
    }

    private async Task StopIgnoringTerminalRaceAsync()
    {
        try { await stop(CancellationToken.None).ConfigureAwait(false); }
        catch (ObjectDisposedException) when (IsDisposed) { }
    }

    internal bool DisposalRequested => IsDisposed;

    private bool IsDisposed { get { lock (_gate) { return _disposed; } } }

    internal ValueTask DisposeAsync()
    {
        ThrowIfNativeReentrant();
        TaskCompletionSource? completion = null;
        Work[] snapshot = [];
        Task? pendingStop = null;
        Task result;
        lock (_gate)
        {
            if (_disposal is null)
            {
                _disposed = true;
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposal = completion.Task;
                snapshot = _active.ToArray();
                pendingStop = _stopping;
            }
            result = _disposal;
        }
        if (completion is not null) { _ = DisposeCoreAsync(snapshot, pendingStop, completion); }
        return new ValueTask(result);
    }

    private async Task DisposeCoreAsync(Work[] snapshot, Task? pendingStop, TaskCompletionSource completion)
    {
        var errors = new List<Exception>();
        Task inner;
        try { inner = dispose().AsTask(); } // Immediate retirement, before any health drain.
        catch (Exception error) { inner = Task.FromException(error); }
        await CancelAndJoinAsync(snapshot, errors).ConfigureAwait(false);
        if (pendingStop is not null) { await ObserveAsync(pendingStop, errors).ConfigureAwait(false); }
        await ObserveAsync(inner, errors).ConfigureAwait(false);
        try { completeDisposal?.Invoke(); }
        catch (Exception error) { errors.Add(error); }
        Complete(completion, errors);
    }

    private static async Task CancelAndJoinAsync(Work[] snapshot, List<Exception> errors)
    {
        foreach (var work in snapshot)
        {
            try { await work.CancelForShutdownAsync().ConfigureAwait(false); }
            catch (Exception error) { errors.Add(error); }
        }
        foreach (var work in snapshot)
        {
            try { await work.Completion.Task.ConfigureAwait(false); }
            catch (OperationCanceledException error) when (work.ShutdownCancellationRequested && error.CancellationToken == work.Token) { }
            catch (Exception error) { errors.Add(error); }
        }
    }

    private static async Task ObserveAsync(Task task, List<Exception> errors)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
    }

    private static void Complete(TaskCompletionSource completion, List<Exception> errors)
    {
        var unique = errors.Distinct<Exception>(ReferenceEqualityComparer.Instance).ToArray();
        if (unique.Length == 0) { completion.TrySetResult(); }
        else if (unique.Length == 1) { completion.TrySetException(unique[0]); }
        else { completion.TrySetException(new AggregateException(unique)); }
    }

    internal async Task NativeAsync(Func<CancellationToken, Task> body, CancellationToken token)
    {
        var previous = ActiveNative.Value;
        var scope = new NativeScope(this, previous);
        ActiveNative.Value = scope;
        var owner = CurrentWork.Value;
        var retained = owner is not null && ReferenceEquals(owner.Owner, this) && owner.TryRegisterScope(scope);
        try { await body(token).ConfigureAwait(false); }
        finally { if (!retained) { scope.Active = false; } ActiveNative.Value = previous; }
    }

    private async Task<T> NativeAsync<T>(Func<CancellationToken, Task<T>> body, CancellationToken token)
    {
        T result = default!;
        await NativeAsync(async ct => { result = await body(ct).ConfigureAwait(false); }, token).ConfigureAwait(false);
        return result;
    }

    private void ThrowIfNativeReentrant()
    {
        for (var scope = ActiveNative.Value; scope is not null; scope = scope.Parent)
        {
            if (scope.Active && ReferenceEquals(scope.Owner, this))
            { throw new InvalidOperationException("Lifecycle reentry from this owner's active health SDK operation would wait for itself."); }
        }
    }

    private sealed class NativeScope(HealthElectionLifetime owner, NativeScope? parent)
    {
        internal HealthElectionLifetime Owner { get; } = owner;
        internal NativeScope? Parent { get; } = parent;
        internal volatile bool Active = true;
    }
    private sealed class Work : IAsyncDisposable
    {
        private readonly Lock _sourceGate = new();
        private Task? _cancellation;
        private bool _finishing;
        private readonly List<NativeScope> _scopes = [];
        internal Work(HealthElectionLifetime owner, bool mutation, CancellationToken caller, bool isStart = false)
        {
            Owner = owner;
            Mutation = mutation;
            IsStart = isStart;
            Source = CancellationTokenSource.CreateLinkedTokenSource(caller);
            Token = Source.Token;
        }
        internal HealthElectionLifetime Owner { get; }
        internal bool Mutation { get; }
        internal bool IsStart { get; }
        private CancellationTokenSource Source { get; }
        internal CancellationToken Token { get; }
        internal volatile bool ShutdownCancellationRequested;
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool TryRegisterScope(NativeScope scope)
        {
            lock (_sourceGate)
            {
                if (_finishing || !ReferenceEquals(scope.Owner, Owner)) { return false; }
                _scopes.Add(scope); return true;
            }
        }
        internal Task CancelForShutdownAsync()
        {
            lock (_sourceGate)
            {
                // Body is already settled when finishing is set. Still join Completion externally.
                if (_finishing) { return Task.CompletedTask; }
                if (_cancellation is not null) { return _cancellation; }
                if (!Token.IsCancellationRequested) { ShutdownCancellationRequested = true; }
                return _cancellation = Source.CancelAsync();
            }
        }
        public async ValueTask DisposeAsync()
        {
            Task? cancellation;
            lock (_sourceGate) { _finishing = true; cancellation = _cancellation; }
            try { if (cancellation is not null) { await cancellation.ConfigureAwait(false); } }
            finally
            {
                try { Source.Dispose(); }
                finally { lock (_sourceGate) { foreach (var scope in _scopes) { scope.Active = false; } } }
            }
        }
    }
}
