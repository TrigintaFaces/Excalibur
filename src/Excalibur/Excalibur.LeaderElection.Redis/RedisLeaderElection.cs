// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Security.Cryptography;
using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Excalibur.LeaderElection.Redis;

/// <summary>Redis election with tenure-unique ownership and a monotonic local deadline.</summary>
/// <remarks>Uses tenure-specific ownership proofs and joins owned operations before remote cleanup.</remarks>
public sealed partial class RedisLeaderElection : ILeaderElection, IAsyncDisposable
{
    private static readonly System.Buffers.SearchValues<char> OwnerHexCharacters = System.Buffers.SearchValues.Create("0123456789ABCDEF");
    private readonly Lock _runGate = new();
    private readonly LeaderElectionNotifications _notifications;
    private readonly IConnectionMultiplexer _redis;
    private readonly string _lockKey;
    private readonly LeaderElectionOptions _options;
    private readonly ILogger<RedisLeaderElection> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IMessageFailureClassifier? _failureClassifier;
    private readonly IFencingTokenIssuer? _fencingTokenProvider;
    private const int FencingTokenMintMaxAttempts = 3;
    private StartSlot? _slot;
    private volatile bool _disposed;
    private Task? _disposal;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisLeaderElection"/> class.
	/// </summary>
	/// <param name="redis">The Redis connection multiplexer.</param>
	/// <param name="lockKey">The Redis key for the leader lock (e.g., "myapp:leader").</param>
	/// <param name="options">The leader election options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="context">
	/// An optional <see cref="RedisLeaderElectionContext"/> bundling the opt-in cross-cutting collaborators
	/// (failure classifier for accelerated self-demotion, fencing-token provider for monotonic token
	/// issuance). Defaults to <see langword="null"/> (grace-only, no fencing — fully backward compatible).
	/// </param>
	/// <param name="timeProvider">
	/// Optional time provider used for event timestamps. Defaults to <see cref="TimeProvider.System"/>.
	/// Inject a controllable provider to make emitted timestamps deterministic in tests.
	/// </param>
    public RedisLeaderElection(IConnectionMultiplexer redis, string lockKey, IOptions<LeaderElectionOptions> options,
        ILogger<RedisLeaderElection> logger, RedisLeaderElectionContext? context = null, TimeProvider? timeProvider = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _lockKey = lockKey ?? throw new ArgumentNullException(nameof(lockKey));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _failureClassifier = context?.FailureClassifier;
        _fencingTokenProvider = context?.FencingTokenProvider;
        var validation = new LeaderElectionOptionsValidator().Validate(null, _options);
        if (validation.Failed) { throw new OptionsValidationException(nameof(LeaderElectionOptions), typeof(LeaderElectionOptions), validation.Failures); }
        CandidateId = LeaderCandidateIdentity.Resolve(_options.InstanceId);
        _notifications = new LeaderElectionNotifications(error => LogRenewalWarning(error, CandidateId));
    }

    /// <inheritdoc/>
    public string CandidateId { get; }
    /// <inheritdoc/>
    public Task NotificationsCompleted => _notifications.NotificationsCompleted;
    /// <inheritdoc/>
    public bool IsLeader => CurrentRun()?.Kernel.Current is not null;
    /// <inheritdoc/>
    public Leadership? CurrentLeadership
    {
        get
        {
            var held = CurrentRun()?.Kernel.Current;
            return held is null ? null : new Leadership(held.FencingToken, held.AcquiredAt);
        }
    }
    /// <inheritdoc/>
    public string? CurrentLeaderId
    {
        get
        {
            var run = CurrentRun();
            if (run is null) { return null; }
            var status = run.Kernel.ReadStatus();
            return status.Tenure is not null ? CandidateId : status.ObservedHolder;
        }
    }
    /// <inheritdoc/>
    public event EventHandler<LeaderElectionEventArgs>? BecameLeader;
    /// <inheritdoc/>
    public event EventHandler<LeaderElectionEventArgs>? LostLeadership;
    /// <inheritdoc/>
    public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;
    /// <inheritdoc/>
    public event EventHandler<LeaderElectionAcquisitionFailedEventArgs>? AcquisitionFailed;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _notifications.ThrowIfReentrant();
        StartSlot slot;
        lock (_runGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_slot is { Shutdown: { } shutdown } && !shutdown.IsCompletedSuccessfully)
            {
                throw new InvalidOperationException("The previous run has not reached successful client quiescence.");
            }
            if (_slot is { Shutdown: null } existing) { slot = existing; }
            else
            {
                // A pending start owns only one cancellation/lifecycle slot. It allocates NO
                // kernel, timer, run or dispatcher until all previous callbacks have drained.
                var previousNotifications = _slot?.Run?.Kernel.NotificationDrainTask ?? _slot?.PreviousNotifications ?? Task.CompletedTask;
                slot = new StartSlot(previousNotifications);
                _slot = slot;
                _notifications.EnqueueDiagnostic(LogStarting, CandidateId, _lockKey);
                slot.Worker = LeaderElectionExecution.RunAsync(() => AdmitRunAsync(slot));
            }
        }
        try { await slot.Initial.Task.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _ = BeginStop(slot); // retained; a canceled waiter does not abandon a pending launch.
            throw;
        }
    }

    private async Task AdmitRunAsync(StartSlot slot)
    {
        try
        {
            await slot.PreviousNotifications.WaitAsync(slot.Stop.Token).ConfigureAwait(false);
            Run run;
            lock (_runGate)
            {
                if (slot.StopRequested || _disposed || !ReferenceEquals(_slot, slot))
                {
                    throw new OperationCanceledException(slot.Stop.Token);
                }
                run = new Run(this, slot);
                slot.Run = run;
                run.Actor = LeaderElectionExecution.RunAsync(() => RunActorAsync(run));
            }
            await run.Initial.Task.ConfigureAwait(false);
            slot.Initial.TrySetResult();
        }
        catch (OperationCanceledException) when (slot.StopRequested)
        {
            slot.Initial.TrySetCanceled(slot.Stop.Token);
        }
        catch (Exception failure)
        {
            slot.Initial.TrySetException(failure);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        StartSlot? slot;
        lock (_runGate) { ObjectDisposedException.ThrowIf(_disposed, this); slot = _slot; }
        return slot is null ? Task.CompletedTask : BeginStop(slot).WaitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        lock (_runGate)
        {
            _disposed = true;
            // Permanent disposal seals the same admitted start slot before its async stop worker runs.
            if (_slot is { } slot) { slot.StopRequested = true; }
            return new ValueTask(_disposal ??= LeaderElectionExecution.RunAsync(DisposeResourcesAsync));
        }
    }

    private async Task DisposeResourcesAsync()
    {
        try
        {
            StartSlot? slot;
            lock (_runGate) { slot = _slot; }
            if (slot is not null) { await BeginStop(slot).ConfigureAwait(false); }
        }
        finally { _notifications.Seal(); }
    }

    private Run? CurrentRun() { lock (_runGate) { return _slot?.Run; } }

    private Task BeginStop(StartSlot slot)
    {
        lock (_runGate)
        {
            if (slot.Shutdown is not null) { return slot.Shutdown; }
            slot.StopRequested = true;
            _notifications.EnqueueDiagnostic(LogStopping, CandidateId);
            // No new run can be installed after this seal: admission checks under this gate.
            // The common kernel never invokes external user code inline under its gate.
            slot.Run?.Kernel.RequestStop();
            return slot.Shutdown = LeaderElectionExecution.RunAsync(() => StopSlotAsync(slot));
        }
    }

    private static async Task StopSlotAsync(StartSlot slot)
    {
        var failures = new List<Exception>();
        try { await slot.Stop.CancelAsync().ConfigureAwait(false); }
        catch (Exception error) { failures.Add(error); }
        try { await slot.Worker.ConfigureAwait(false); }
        catch (Exception error) { failures.Add(error); }
        if (slot.Run is { } run)
        {
            try { await run.Actor.ConfigureAwait(false); }
            catch (Exception error) { failures.Add(error); }
            try { await run.Kernel.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { failures.Add(error); }
            if (run.ActorFailure is not null) { failures.Add(run.ActorFailure); }
        }
        slot.Stop.Dispose();
        var distinct = failures.Distinct<Exception>(ReferenceEqualityComparer.Instance).ToArray();
        if (distinct.Length == 1) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(distinct[0]).Throw(); }
        if (distinct.Length > 1) { throw new AggregateException("Redis run shutdown failed.", distinct); }
    }

    private async Task RunActorAsync(Run run)
    {
        try
        {
            await run.Kernel.WaitForNotificationDrainAsync(run.Stop.Token).ConfigureAwait(false);
            while (!run.StopRequested && !run.Stop.IsCancellationRequested)
            {
                var attempt = new Attempt("excalibur:v1:" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + ":" + CandidateId);
                long generation;
                try { generation = run.Kernel.BeginAcquisition(attempt, attempt.Acquire); }
                catch (InvalidOperationException) when (run.StopRequested) { break; }
                var start = _timeProvider.GetTimestamp();
                // Refused only when a stop already sealed the latch; the stop arm below reports it.
                _ = attempt.Acquire.TryLaunch(async () =>
                {
                    attempt.Acquired = await _redis.GetDatabase().StringSetAsync(_lockKey, attempt.Owner,
                        _options.LeaseDuration, When.NotExists).ConfigureAwait(false);
                    if (!attempt.Acquired)
                    {
                        var holder = await _redis.GetDatabase().StringGetAsync(_lockKey).ConfigureAwait(false);
                        attempt.OtherLeader = holder.HasValue ? DecodeDisplayIdentity(holder.ToString()) : null;
                    }
                    if (attempt.Acquired && _fencingTokenProvider is not null)
                    {
                        attempt.Fence = await FencingTokenMinting.MintWithRetryAsync(_fencingTokenProvider,
                            _lockKey, run.Stop.Token, FencingTokenMintMaxAttempts).ConfigureAwait(false);
                    }
                });
                try
                {
                    await attempt.Acquire.Completion.ConfigureAwait(false);
                    if (attempt.Acquired && !run.Stop.IsCancellationRequested)
                    {
                        if (!run.Kernel.Publish(generation, start, attempt.Owner, attempt.Fence, _timeProvider.GetUtcNow()))
                        {
                            run.Kernel.ReportObservation(generation, false, null, "acquisition proof expired before publication", null, _timeProvider.GetUtcNow());
                        }
                    }
                    else if (run.StopRequested || run.Stop.IsCancellationRequested)
                    {
                        // A stop seals the acquisition latch synchronously inside BeginStop, BEFORE it
                        // schedules the token cancellation, so the actor can resume here with the token
                        // still uncancelled. Reading StopRequested -- which BeginStop sets under the run
                        // gate before sealing -- is what makes this deterministic: without it a clean
                        // shutdown is logged as a contention failure that nobody lost.
                        run.Kernel.Retire(generation, "stopped");
                    }
                    else
                    {
                        _notifications.EnqueueDiagnostic(LogLockAcquisitionFailed, CandidateId, attempt.OtherLeader);
                        run.Kernel.ReportObservation(generation, true, attempt.OtherLeader, "lost the acquisition race", null, _timeProvider.GetUtcNow());
                        run.Kernel.Retire(generation, "acquisition race lost");
                    }
                }
                catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { run.Kernel.Retire(generation, "stopped"); }
                catch (Exception error)
                {
                    run.Kernel.Retire(generation, "acquisition failed", error);
                    run.Kernel.ReportObservation(generation, false, null,
                        attempt.Acquired ? "fencing token mint failed" : "error during acquisition", error, _timeProvider.GetUtcNow());
                    if (attempt.Acquired) { _notifications.EnqueueDiagnostic(LogFencingTokenIssueError, error, CandidateId, _lockKey); }
                    else { _notifications.EnqueueDiagnostic(LogLockAcquisitionError, error, CandidateId); }
                }
                finally { run.Initial.TrySetResult(); }

                var renewalDelay = _options.RenewInterval;
                var cadenceStart = start;
                while (!run.Stop.IsCancellationRequested && run.Kernel.Current is not null)
                {
                    await LeaderElectionDelay.WaitFromAsync(cadenceStart, renewalDelay, _timeProvider, run.Stop.Token).ConfigureAwait(false);
                    var operation = new OwnedOperationLatch();
                    if (!run.Kernel.TryRegisterOperation(generation, operation)) { break; }
                    var renewalStart = _timeProvider.GetTimestamp();
                    cadenceStart = renewalStart;
                    var renewed = false;
                    // Refused only when a stop already sealed the latch; the loop exits on StopRequested.
                    _ = operation.TryLaunch(async () =>
                    {
                        const string script = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('pexpire', KEYS[1], ARGV[2]) else return 0 end";
                        var result = await _redis.GetDatabase().ScriptEvaluateAsync(script, [_lockKey],
                            [attempt.Owner, (long)_options.LeaseDuration.TotalMilliseconds]).ConfigureAwait(false);
                        renewed = (long)result == 1;
                    });
                    try
                    {
                        await operation.Completion.ConfigureAwait(false);
                        if (renewed)
                        {
                            run.Kernel.Confirm(generation, renewalStart);
                            renewalDelay = _options.RenewInterval;
                        }
                        else { run.Kernel.Retire(generation, "Redis owner proof no longer matches"); }
                    }
                    catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { break; }
                    catch (Exception error)
                    {
                        _notifications.EnqueueDiagnostic(LogRenewalError, error, CandidateId);
                        if (_failureClassifier?.Classify(error) == MessageFailureKind.Permanent)
                        {
                            run.Kernel.Retire(generation, "permanent renewal failure", error);
                        }
                        // Retry inside the grace portion rather than spending another full R before
                        // the next attempt. A transient at default R=G=5 otherwise cannot recover by B=10.
                        renewalDelay = _options.RetryInterval;
                        // Ambiguous/unrelated cancellation does not advance the deadline or stop the actor.
                    }
                }
                run.Kernel.Retire(generation, run.Stop.IsCancellationRequested ? "stopped" : "tenure ended");
                await run.Kernel.RetirementTask.ConfigureAwait(false);
                run.Kernel.CompleteRetirement(generation);
                await run.Kernel.WaitForNotificationDrainAsync(run.Stop.Token).ConfigureAwait(false);
                await LeaderElectionDelay.WaitFromAsync(cadenceStart, _options.RetryInterval, _timeProvider, run.Stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { }
        catch (Exception error)
        {
            run.ActorFailure = error;
            run.Initial.TrySetException(error);
            _ = BeginStop(run.Slot); // another task owns the join; the actor never joins itself.
        }
        finally { run.Initial.TrySetResult(); }
    }

    private async Task RetireAsync(Run run, RenewalTenureDeadline.Retirement retirement)
    {
        var attempt = (Attempt)retirement.AcquisitionOwner;
        try
        {
            if (retirement.OperationCompletion is not null)
            {
                try { await retirement.OperationCompletion.ConfigureAwait(false); }
                catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { }
                catch (Exception error) { _notifications.EnqueueDiagnostic(LogRenewalError, error, CandidateId); }
            }
        }
        finally
        {
            await ReleaseAcquisitionAsync(attempt).ConfigureAwait(false);
        }
    }

    private async Task ReleaseAcquisitionAsync(Attempt attempt)
    {
        // Even a failed/unknown SET might have reached Redis. Exact old nonce cleanup is safe;
        // using public CandidateId here could delete a later tenure with the same display ID.
        const string script = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
        try
        {
            var released = await _redis.GetDatabase().ScriptEvaluateAsync(script, [_lockKey], [attempt.Owner]).ConfigureAwait(false);
            if ((int)released == 1) { _notifications.EnqueueDiagnostic(LogLockReleased, CandidateId); }
        }
        catch (Exception error)
        {
            _notifications.EnqueueDiagnostic(LogLockReleaseError, error, CandidateId);
            throw;
        }
    }

    private void Notify(RenewalTenureDeadline.Transition transition)
    {
        var args = new LeaderElectionEventArgs(CandidateId, _lockKey, timestamp: transition.Timestamp);
        InvokeEach(transition.Acquired ? BecameLeader : LostLeadership, args);
        InvokeEach(LeaderChanged, new LeaderChangedEventArgs(transition.Acquired ? transition.PreviousHolder : CandidateId,
            transition.Acquired ? CandidateId : null, _lockKey,
            fencingToken: transition.Acquired ? transition.FencingToken : null, timestamp: transition.Timestamp));
        // Preserve both event families before queuing bounded best-effort diagnostics.
        if (transition.Acquired)
        {
            _notifications.EnqueueDiagnostic(LogBecameLeader, CandidateId, _lockKey);
            if (transition.FencingToken is { } fence) { _notifications.EnqueueDiagnostic(LogFencingTokenIssued, CandidateId, fence, _lockKey); }
        }
        else { _notifications.EnqueueDiagnostic(LogLostLeadership, CandidateId, _lockKey); }
    }

    private void InvokeEach<T>(EventHandler<T>? subscribers, T args) where T : EventArgs
    {
        _notifications.InvokeEach(subscribers, this, args);
    }

    private void NotifyObservation(RenewalTenureDeadline.Observation observation)
    {
        if (observation.HasHolder && !string.Equals(observation.PreviousHolder, observation.Holder, StringComparison.Ordinal))
        {
            InvokeEach(LeaderChanged, new LeaderChangedEventArgs(observation.PreviousHolder, observation.Holder,
                _lockKey, timestamp: observation.Timestamp));
        }
        if (observation.FailureReason is not null)
        {
            InvokeEach(AcquisitionFailed, new LeaderElectionAcquisitionFailedEventArgs(CandidateId, _lockKey,
                observation.FailureReason, observation.Timestamp, observation.Cause));
        }
    }

    private static string DecodeDisplayIdentity(string value)
    {
        const string prefix = "excalibur:v1:";
        if (value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length + 64 &&
            value[prefix.Length + 64] == ':' && value.AsSpan(prefix.Length, 64).IndexOfAnyExcept(OwnerHexCharacters) < 0)
        {
            return value[(prefix.Length + 65)..];
        }
        return value; // legacy/unknown values are display-only; they never match this tenure's nonce.
    }

    private sealed class Attempt(string owner)
    {
        internal string Owner { get; } = owner;
        internal OwnedOperationLatch Acquire { get; } = new();
        internal bool Acquired { get; set; }
        internal string? OtherLeader { get; set; }
        internal long? Fence { get; set; }
    }

    private sealed class StartSlot(Task previousNotifications)
    {
        internal Task PreviousNotifications { get; } = previousNotifications;
        internal CancellationTokenSource Stop { get; } = new();
        internal TaskCompletionSource Initial { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Worker { get; set; } = Task.CompletedTask;
        internal Run? Run { get; set; }
        internal Task? Shutdown { get; set; }
        internal volatile bool StopRequested;
    }

    private sealed class Run
    {
        internal Run(RedisLeaderElection owner, StartSlot slot)
        {
            Slot = slot;
            Kernel = new RenewalTenureDeadline(owner._timeProvider, owner._options.RenewInterval + owner._options.GracePeriod,
                retirement => owner.RetireAsync(this, retirement), owner.Notify,
                observe: owner.NotifyObservation, notifications: owner._notifications);
        }
        internal StartSlot Slot { get; }
        internal CancellationTokenSource Stop => Slot.Stop;
        internal bool StopRequested => Slot.StopRequested;
        internal TaskCompletionSource Initial { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal RenewalTenureDeadline Kernel { get; }
        internal Task Actor { get; set; } = Task.CompletedTask;
        internal Exception? ActorFailure { get; set; }
    }
	[LoggerMessage(LeaderElectionEventId.RedisStarting, LogLevel.Information,
		"Starting leader election for candidate {CandidateId} on key {Key}")]
	partial void LogStarting(string candidateId, string key);

	[LoggerMessage(LeaderElectionEventId.RedisStopping, LogLevel.Information, "Stopping leader election for candidate {CandidateId}")]
	partial void LogStopping(string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisLockAcquisitionFailed, LogLevel.Debug,
		"Failed to acquire lock for {CandidateId}, current leader: {Leader}")]
	partial void LogLockAcquisitionFailed(string candidateId, string? leader);

	[LoggerMessage(LeaderElectionEventId.RedisLockAcquisitionError, LogLevel.Error, "Error acquiring lock for {CandidateId}")]
	partial void LogLockAcquisitionError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisLockReleased, LogLevel.Debug, "Released lock for {CandidateId}")]
	partial void LogLockReleased(string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisLockReleaseError, LogLevel.Warning, "Error releasing lock for {CandidateId}")]
	partial void LogLockReleaseError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisRenewalError, LogLevel.Error, "Error in renewal loop for {CandidateId}")]
	partial void LogRenewalError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisRenewalWarning, LogLevel.Warning, "Error renewing lease for {CandidateId}")]
	partial void LogRenewalWarning(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.RedisBecameLeader, LogLevel.Information, "Candidate {CandidateId} became leader for key {Key}")]
	partial void LogBecameLeader(string candidateId, string key);

	[LoggerMessage(LeaderElectionEventId.RedisFencingTokenIssued, LogLevel.Information,
		"Candidate {CandidateId} issued fencing token {Token} for resource {Resource}")]
	partial void LogFencingTokenIssued(string candidateId, long token, string resource);

	[LoggerMessage(LeaderElectionEventId.RedisFencingTokenIssueError, LogLevel.Error,
		"Candidate {CandidateId} failed to mint a fencing token for resource {Resource} after bounded retries; relinquishing leadership (fail-closed) rather than leading with an un-advanced fence")]
	partial void LogFencingTokenIssueError(Exception ex, string candidateId, string resource);

	[LoggerMessage(LeaderElectionEventId.RedisLostLeadership, LogLevel.Warning, "Candidate {CandidateId} lost leadership for key {Key}")]
	partial void LogLostLeadership(string candidateId, string key);
}
