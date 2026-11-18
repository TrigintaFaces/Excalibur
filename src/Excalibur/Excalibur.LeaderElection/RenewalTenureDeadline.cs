// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
namespace Excalibur.LeaderElection;

// Shared local tenure truth and owned retirement. Providers must not maintain a second
// authoritative leadership flag or release clients before registered operations complete.
internal sealed class RenewalTenureDeadline : IAsyncDisposable
{
    private static readonly TimeSpan MaximumTimerDue = TimeSpan.FromMilliseconds(uint.MaxValue - 1d);
    private readonly Lock _gate = new();
    private readonly TimeProvider _clock;
    private readonly LeaderElectionNotifications _notifications;
    private readonly bool _ownsNotifications;
    private readonly TimeSpan _budget;
    private readonly Func<Retirement, Task> _retire;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
        Justification = "The memoized ShutdownCoreAsync owns and awaits exactly one timer.DisposeAsync, including retirement failure paths.")]
    private readonly ITimer _timer;
    private long _generation;
    private object? _acquisitionOwner;
    private OwnedOperationLatch? _operation;
    private PublishedTenure? _current;
    private Phase _phase;
    private bool _stopped;
    private Task _retirement = Task.CompletedTask;
    private readonly Action<Transition>? _notify;
    private readonly Action<Observation>? _observe;
    private Observation? _pendingObservation;
    private string? _observedHolder;
    private readonly Queue<Transition> _transitions = new();
    private Task _notificationDrain;
    private bool _dispatching;
    private Task? _shutdown;
    private Exception? _notificationFailure;

    internal RenewalTenureDeadline(TimeProvider clock, TimeSpan budget, Func<Retirement, Task> retire, Action<Transition>? notify = null, Task? previousNotificationDrain = null, Action<Observation>? observe = null, LeaderElectionNotifications? notifications = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(retire);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(budget, TimeSpan.Zero);
        _clock = clock;
        _notifications = notifications ?? new LeaderElectionNotifications();
        _ownsNotifications = notifications is null;
        _budget = budget;
        _retire = retire;
        _notify = notify;
        _observe = observe;
        _notificationDrain = previousNotificationDrain ?? Task.CompletedTask;
        _timer = clock.CreateTimer(static state => ((RenewalTenureDeadline)state!).CheckDeadline(), this,
            Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // The immutable record IS the publication. All provider getters derive from this property;
    // adapters may not copy the result into a separate mutable leadership field.
    internal PublishedTenure? Current
    {
        get { lock (_gate) { ExpireUnderLock(); return _current; } }
    }
    internal (PublishedTenure? Tenure, string? ObservedHolder) ReadStatus()
    {
        lock (_gate) { ExpireUnderLock(); return (_current, _stopped ? null : _observedHolder); }
    }

    internal void ReportObservation(long generation, bool hasHolder, string? holder, string? failureReason,
        Exception? cause, DateTimeOffset timestamp)
    {
        lock (_gate)
        {
            if (_stopped || generation != _generation) { return; }
            var previous = _observedHolder;
            if (hasHolder) { _observedHolder = holder; }
            if (_observe is null) { return; }
            var pending = _pendingObservation;
            _pendingObservation = new Observation(generation, hasHolder || pending?.HasHolder == true,
                pending?.HasHolder == true ? pending.PreviousHolder : previous,
                hasHolder ? holder : pending?.Holder, failureReason ?? pending?.FailureReason,
                failureReason is not null ? cause : pending?.Cause, timestamp);
            EnsureDispatcherUnderLock();
        }
    }

    internal Task RetirementTask { get { lock (_gate) { return _retirement; } } }
    internal Task NotificationDrainTask { get { lock (_gate) { return _notificationDrain; } } }
    internal Exception? NotificationFailure { get { lock (_gate) { return _notificationFailure; } } }

    internal void ThrowIfNotificationReentrant()
    { _notifications.ThrowIfReentrant(); }

    internal async Task WaitForNotificationDrainAsync(CancellationToken cancellationToken)
    {
        _notifications.ThrowIfReentrant();
        await NotificationDrainTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal long BeginAcquisition(object acquisitionOwner, OwnedOperationLatch? operation = null)
    {
        ArgumentNullException.ThrowIfNull(acquisitionOwner);
        lock (_gate)
        {
            _notifications.ThrowIfReentrant();
            if (_stopped || _phase != Phase.Idle || !_retirement.IsCompletedSuccessfully || !_notificationDrain.IsCompletedSuccessfully)
            {
                throw new InvalidOperationException("Previous retirement is incomplete, failed, or the owner stopped.");
            }
            _phase = Phase.Acquiring;
            _acquisitionOwner = acquisitionOwner;
            _operation = operation;
            return ++_generation;
        }
    }

    internal bool TryRegisterOperation(long generation, OwnedOperationLatch operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_gate)
        {
            ExpireUnderLock();
            if (_stopped || _phase != Phase.Holding || generation != _generation ||
                (_operation is not null && !_operation.Completion.IsCompleted)) { return false; }
            _operation = operation;
            return true;
        }
    }

    internal bool Publish(long generation, long successfulRequestStart, object ownershipProof, long? fencingToken, DateTimeOffset acquiredAt)
    {
        ArgumentNullException.ThrowIfNull(ownershipProof);
        lock (_gate)
        {
            if (_stopped || generation != _generation || _phase != Phase.Acquiring) { return false; }
            var candidate = new PublishedTenure(generation, ownershipProof, fencingToken, acquiredAt, successfulRequestStart);
            var age = _clock.GetElapsedTime(successfulRequestStart);
            if (age < TimeSpan.Zero || age >= _budget)
            {
                RetireUnderLock("acquisition proof expired before publication", null, candidate);
                return false;
            }
            // Arming is preparation, not publication. Keep the candidate local and remain
            // Acquiring until preparation succeeds; failure still retires its ownership proof.
            if (!ArmUnderLock(candidate.ProofRequestStart, candidate)) { return false; }
            // Timer implementations can take time to arm. Do not commit an already-expired proof.
            if (_clock.GetElapsedTime(candidate.ProofRequestStart) >= _budget)
            {
                RetireUnderLock("acquisition proof expired while arming", null, candidate);
                return false;
            }
            // This is the sole successful-publication linearization point. A retirement before
            // it has WasHolding=false and must never emit a LostLeadership notification.
            var previousHolder = _observedHolder;
            _observedHolder = null;
            _current = candidate;
            _phase = Phase.Holding;
            EnqueueTransitionUnderLock(new Transition(generation, true, fencingToken, acquiredAt, "acquired", null, previousHolder));
            return true;
        }
    }

    internal bool Confirm(long generation, long successfulRequestStart)
    {
        lock (_gate)
        {
            ExpireUnderLock();
            if (_stopped || generation != _generation || _phase != Phase.Holding) { return false; }
            var current = _current!;
            var age = _clock.GetElapsedTime(successfulRequestStart);
            if (age < TimeSpan.Zero || age >= _budget ||
                _clock.GetElapsedTime(current.ProofRequestStart, successfulRequestStart) < TimeSpan.Zero) { return false; }
            _current = current with { ProofRequestStart = successfulRequestStart };
            if (!ArmUnderLock(successfulRequestStart)) { return false; }
            ExpireUnderLock();
            return _phase == Phase.Holding;
        }
    }

    internal void Retire(long generation, string reason, Exception? cause = null)
    {
        lock (_gate) { if (generation == _generation) { RetireUnderLock(reason, cause); } }
    }

    internal bool CompleteRetirement(long generation)
    {
        lock (_gate)
        {
            if (generation != _generation || _phase != Phase.Retiring || !_retirement.IsCompletedSuccessfully) { return false; }
            _acquisitionOwner = null;
            _phase = Phase.Idle;
            return true;
        }
    }

    internal void RequestStop()
    {
        lock (_gate)
        {
            _stopped = true;
            _observedHolder = null;
            _pendingObservation = null; // remote observations are coalesced diagnostics, not local ownership transitions.
            RetireUnderLock("stopped", null);
        }
    }

    internal Task StopAsync(CancellationToken cancellationToken)
    {
        RequestStop();
        return ShutdownTask().WaitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        RequestStop();
        return new ValueTask(ShutdownTask());
    }

    private Task ShutdownTask()
    {
        lock (_gate) { return _shutdown ??= LeaderElectionExecution.RunAsync(ShutdownCoreAsync); }
    }

    private async Task ShutdownCoreAsync()
    {
        Exception? failure = null;
        try { await RetirementTask.ConfigureAwait(false); }
        catch (Exception error) { failure = error; }
        try { await _timer.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error)
        {
            failure = failure is null ? error : new AggregateException("Tenure retirement and timer disposal failed.", failure, error);
        }
        finally { if (_ownsNotifications) { _notifications.Seal(); } }
        if (failure is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw(); }
        // Never join the consumer lane: a callback may itself await this resource cleanup.
    }

    private void EnqueueTransitionUnderLock(Transition transition)
    {
        if (_notify is null) { return; }
        _transitions.Enqueue(transition);
        EnsureDispatcherUnderLock();
    }

    private void EnsureDispatcherUnderLock()
    {
        if (_dispatching) { return; }
        _dispatching = true;
        _notificationDrain = _notifications.Enqueue(Dispatch);
    }

    private void Dispatch()
    {
        while (true)
        {
            Transition? transition = null;
            Observation? observation = null;
            lock (_gate)
            {
                if (_transitions.Count != 0) { transition = _transitions.Dequeue(); }
                else if (_pendingObservation is not null) { observation = _pendingObservation; _pendingObservation = null; }
                else { _dispatching = false; return; }
            }
            if (transition is not null) { InvokeSubscribers(_notify!, transition); }
            else { InvokeSubscribers(_observe!, observation!); }
        }
    }

    private void InvokeSubscribers<T>(Action<T> subscribers, T value)
    {
        foreach (var handler in subscribers.GetInvocationList())
        {
            var subscriber = (Action<T>)handler;
            try { subscriber(value); }
            catch (Exception failure)
            {
                lock (_gate) { _notificationFailure ??= failure; }
                _notifications.RecordFailure(failure);
            }
        }
    }

    private void CheckDeadline()
    {
        lock (_gate)
        {
            ExpireUnderLock();
            // Early wakeups and maximum-due chunks must both re-arm; a one-shot callback is
            // not evidence that the monotonic deadline has actually been reached.
            if (_phase == Phase.Holding) { ArmUnderLock(_current!.ProofRequestStart); }
        }
    }

    private void ExpireUnderLock()
    {
        if (_phase == Phase.Holding && _clock.GetElapsedTime(_current!.ProofRequestStart) >= _budget)
        {
            RetireUnderLock("renewal deadline elapsed", null);
        }
    }

    private void RetireUnderLock(string reason, Exception? cause, PublishedTenure? lateAcquisition = null)
    {
        if (_phase is Phase.Idle or Phase.Retiring) { return; }
        var held = _current;
        _current = null;
        _phase = Phase.Retiring;
        // Disarming can itself fail. Local truth has already changed, and cleanup still starts.
        try { _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan); }
        catch (Exception disarmError) { cause ??= disarmError; }
        // Seal at the retirement commit, not later in a scheduled cleanup worker. This is
        // CAS-only; no foreign mutex, user operation or inline continuation runs under _gate.
        var operationCompletion = _operation?.SealAndJoinAsync();
        var retirement = new Retirement(_generation, _acquisitionOwner!, held ?? lateAcquisition,
            held is not null, reason, cause, _operation, operationCompletion);
        _retirement = LeaderElectionExecution.RunAsync(() => _retire(retirement));
        if (held is not null)
        {
            EnqueueTransitionUnderLock(new Transition(_generation, false, held.FencingToken,
                _clock.GetUtcNow(), reason, cause));
        }
    }

    private bool ArmUnderLock(long proofRequestStart, PublishedTenure? unpublishedCandidate = null)
    {
        var remaining = _budget - _clock.GetElapsedTime(proofRequestStart);
        if (remaining <= TimeSpan.Zero)
        {
            RetireUnderLock("renewal deadline elapsed", null, unpublishedCandidate);
            return false;
        }
        var due = remaining >= MaximumTimerDue ? MaximumTimerDue :
            TimeSpan.FromMilliseconds(Math.Max(1L, (remaining.Ticks + TimeSpan.TicksPerMillisecond - 1) / TimeSpan.TicksPerMillisecond));
        try
        {
            if (_timer.Change(due, Timeout.InfiniteTimeSpan)) { return true; }
            RetireUnderLock("deadline timer refused arming", new InvalidOperationException("Timer.Change returned false."), unpublishedCandidate);
        }
        catch (Exception error) { RetireUnderLock("deadline timer failed arming", error, unpublishedCandidate); }
        return false;
    }

    // Deliberately contains no ownership proof, session, connection or borrowed client.
    internal sealed record Transition(long Generation, bool Acquired, long? FencingToken,
        DateTimeOffset Timestamp, string Reason, Exception? Cause, string? PreviousHolder = null);

    internal sealed record Observation(long Generation, bool HasHolder, string? PreviousHolder,
        string? Holder, string? FailureReason, Exception? Cause, DateTimeOffset Timestamp);

    internal sealed record PublishedTenure(long Generation, object OwnershipProof, long? FencingToken,
        DateTimeOffset AcquiredAt, long ProofRequestStart);
    internal sealed record Retirement(long Generation, object AcquisitionOwner, PublishedTenure? Tenure,
        bool WasHolding, string Reason, Exception? Cause, OwnedOperationLatch? Operation, Task? OperationCompletion);
    private enum Phase { Idle, Acquiring, Holding, Retiring }
}

// Construct/register this latch BEFORE admission. The adapter retirement worker must join it;
// owning an object without joining its latch does not establish client quiescence.
internal sealed class OwnedOperationLatch
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    // Ready=0, Launched=1, SealedUnlaunched=2, SealedLaunched=3. No lock is borrowed by
    // the kernel. Completion never executes user continuations inline.
    private int _state;
    internal Task Completion => _completion.Task;

    internal bool TryLaunch(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0) { return false; }
        // The launch linearization won before any subsequent seal. Even if scheduling is
        // delayed, retirement owns this completion until the actual SDK operation finishes.
        _ = LeaderElectionExecution.RunAsync(async () =>
        {
            try { await operation().ConfigureAwait(false); _completion.TrySetResult(); }
            catch (Exception error) { _completion.TrySetException(error); }
        });
        return true;
    }

    internal Task SealAndJoinAsync()
    {
        while (true)
        {
            var observed = Volatile.Read(ref _state);
            if (observed is 2 or 3) { return _completion.Task; }
            if (observed == 0)
            {
                if (Interlocked.CompareExchange(ref _state, 2, 0) != 0) { continue; }
                _completion.TrySetResult();
                return _completion.Task;
            }
            if (Interlocked.CompareExchange(ref _state, 3, 1) == 1) { return _completion.Task; }
        }
    }
}
