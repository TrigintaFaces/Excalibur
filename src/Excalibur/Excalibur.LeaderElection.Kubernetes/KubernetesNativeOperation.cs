// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Runtime.ExceptionServices;

namespace Excalibur.LeaderElection.Kubernetes;

// One bounded scope per actual SDK transaction, not an ever-growing list on a long-lived tenure.
internal static class KubernetesNativeOperation
{
    private static readonly AsyncLocal<Scope?> Active = new();
    internal static void ThrowIfActive(KubernetesLeaderElection owner)
    {
        for (var scope = Active.Value; scope is not null; scope = scope.Parent)
        {
            if (scope.IsActive && ReferenceEquals(scope.Owner, owner))
            { throw new InvalidOperationException("Lifecycle reentry from this election's active SDK operation would join itself."); }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Invocation disposal is awaited exactly once after all operation outcomes, and joins the upstream forwarder and actual cancellation callbacks. Its failure is aggregated with the SDK failure; await using would rethrow the memoized disposal failure and mask that aggregate.")]
    internal static async Task<T> RunAsync<T>(KubernetesLeaderElection owner, Func<CancellationToken, Task<T>> operation,
        CancellationToken parent, Action<Exception>? cleanupFailure = null, Action? admitPhase = null)
    {
        var invocation = new Invocation(parent);
        var previous = Active.Value;
        var scope = new Scope(owner, previous, admitPhase);
        Active.Value = scope;
        T result = default!;
        Exception? failure = null;
        try { invocation.Token.ThrowIfCancellationRequested(); result = await operation(invocation.Token).ConfigureAwait(false); }
        catch (Exception error) { failure = error; }
        finally { Active.Value = previous; }
        try { await invocation.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error)
        {
            cleanupFailure?.Invoke(error);
            failure = failure is null ? error : new AggregateException(failure, error);
        }
        finally { scope.IsActive = false; }
        if (failure is OperationCanceledException canceled && invocation.Token.IsCancellationRequested && canceled.CancellationToken == invocation.Token)
        { throw new OperationCanceledException("Owned Kubernetes SDK operation cancelled.", canceled, parent); }
        if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
        return result;
    }

    internal static void AdmitPhase()
    {
        var scope = Active.Value;
        if (scope is null) { return; }
        if (!scope.IsActive) { throw new InvalidOperationException("A completed native owner cannot admit a new SDK phase."); }
        scope.Admission?.Invoke();
    }

    private sealed class Scope(KubernetesLeaderElection owner, Scope? parent, Action? admission)
    {
        internal KubernetesLeaderElection Owner { get; } = owner;
        internal Scope? Parent { get; } = parent;
        internal Action? Admission { get; } = admission;
        internal volatile bool IsActive = true;
    }

    private sealed class Invocation : IAsyncDisposable
    {
        private readonly CancellationTokenSource _source = new();
        private readonly Lock _gate = new();
        private readonly CancellationTokenRegistration _registration;
        private Task? _cancellation;
        private Task? _completion;
        internal Invocation(CancellationToken parent)
        {
            Token = _source.Token;
            _registration = parent.Register(static state => ((Invocation)state!).RequestCancellation(), this);
        }
        internal CancellationToken Token { get; }
        private void RequestCancellation()
        {
            lock (_gate) { _cancellation ??= _source.CancelAsync(); }
        }
        internal Task JoinAsync()
        {
            lock (_gate) { return _completion ??= JoinCoreAsync(); }
        }
        private async Task JoinCoreAsync()
        {
            // Join/remove the sole upstream forwarder before observing its cancellation task,
            // so Source.Dispose cannot race a late CancelAsync or a native cancellation callback.
            try
            {
                await _registration.DisposeAsync().ConfigureAwait(false);
                Task? cancellation; lock (_gate) { cancellation = _cancellation; }
                if (cancellation is not null) { await cancellation.ConfigureAwait(false); }
            }
            finally { _source.Dispose(); }
        }
        public ValueTask DisposeAsync() => new(JoinAsync());
    }
}
