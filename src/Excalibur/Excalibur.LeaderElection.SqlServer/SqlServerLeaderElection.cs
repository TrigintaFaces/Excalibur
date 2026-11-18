// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.SqlServer;

/// <summary>SQL Server election with a dedicated application-lock session and monotonic local tenure deadline.</summary>
/// <remarks>Verification checks Exclusive ownership on the original session; retirement joins owned operations before closing that session.</remarks>
public sealed partial class SqlServerLeaderElection : ILeaderElection, IAsyncDisposable
{
    private readonly Lock _runGate = new();
    private readonly LeaderElectionNotifications _notifications;
    private readonly Func<ISqlServerElectionSession> _sessionFactory;
    private readonly string _lockResource;
    private readonly LeaderElectionOptions _options;
    private readonly ILogger<SqlServerLeaderElection> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IMessageFailureClassifier? _failureClassifier;
    private readonly IFencingTokenIssuer? _fencingTokenProvider;
    private const int FencingTokenMintMaxAttempts = 3;
    private StartSlot? _slot;
    private volatile bool _disposed;
    private Task? _disposal;

    /// <summary>Initializes election over a dedicated SQL Server session.</summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <param name="lockResource">The application-lock resource.</param>
    /// <param name="options">Validated election timings and candidate identity.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="failureClassifier">Optional accelerate-only permanent-failure classification.</param>
    /// <param name="fencingTokenProvider">Optional fence minted before leadership is published.</param>
    /// <param name="timeProvider">Monotonic time and timer provider.</param>
    public SqlServerLeaderElection(string connectionString, string lockResource, IOptions<LeaderElectionOptions> options,
        ILogger<SqlServerLeaderElection> logger, IMessageFailureClassifier? failureClassifier = null,
        IFencingTokenIssuer? fencingTokenProvider = null, TimeProvider? timeProvider = null)
        : this(CreateSessionFactory(connectionString), lockResource, options, logger, failureClassifier, fencingTokenProvider, timeProvider)
    {
    }

    // Typed internal seam tests actual admission/retirement against controlled SDK completions.
    // The public factory always creates a new hardened physical SQL session for every attempt.
    internal SqlServerLeaderElection(Func<ISqlServerElectionSession> sessionFactory, string lockResource,
        IOptions<LeaderElectionOptions> options, ILogger<SqlServerLeaderElection> logger,
        IMessageFailureClassifier? failureClassifier = null, IFencingTokenIssuer? fencingTokenProvider = null,
        TimeProvider? timeProvider = null)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _lockResource = lockResource ?? throw new ArgumentNullException(nameof(lockResource));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _failureClassifier = failureClassifier;
        _fencingTokenProvider = fencingTokenProvider;
        var validation = new LeaderElectionOptionsValidator().Validate(null, _options);
        if (validation.Failed) { throw new OptionsValidationException(nameof(LeaderElectionOptions), typeof(LeaderElectionOptions), validation.Failures); }
        CandidateId = LeaderCandidateIdentity.Resolve(_options.InstanceId);
        _notifications = new LeaderElectionNotifications(error => LogRenewalError(error, CandidateId));
    }

    private static Func<ISqlServerElectionSession> CreateSessionFactory(string connectionString)
    {
        var hardened = BuildLockConnectionString(connectionString);
        return () => new SqlServerElectionSession(new SqlConnection(hardened));
    }

    private static string BuildLockConnectionString(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return new SqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            ConnectRetryCount = 0,
            Enlist = false
        }.ConnectionString;
    }

    // SQL uses integer units. Clamp before casting, round positive values upward, and leave
    // a one-second command margin for sp_getapplock's finite millisecond lock-wait return.
    private int LockTimeoutMilliseconds => (int)Math.Clamp(Math.Ceiling(_options.RetryInterval.TotalMilliseconds), 1, int.MaxValue);
    private int AcquisitionCommandTimeoutSeconds => (int)Math.Clamp(Math.Ceiling(_options.RetryInterval.TotalSeconds) + 1, 1, int.MaxValue);
    // A proof request starts at R; grace is the remaining normal proof window. Allow that
    // window (rounded up to SQL seconds), while the independent monotonic kernel deadline
    // still retires truth at R+G even if the command or its cancellation has not settled.
    private int VerificationCommandTimeoutSeconds => (int)Math.Clamp(Math.Ceiling(_options.GracePeriod.TotalSeconds), 1, int.MaxValue);

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
            _notifications.EnqueueDiagnostic(LogStarting, CandidateId, _lockResource);
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
            // No new run can be installed after this seal: admission checks under this gate.
            // The common kernel never invokes external user code inline under its gate.
            slot.Run?.Kernel.RequestStop();
            return slot.Shutdown = LeaderElectionExecution.RunAsync(() => StopSlotAsync(slot));
        }
    }

    private async Task StopSlotAsync(StartSlot slot)
    {
        var failures = new List<Exception>();
        try { _notifications.EnqueueDiagnostic(LogStopping, CandidateId); }
        catch (Exception error) { failures.Add(error); }
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
        if (distinct.Length > 1) { throw new AggregateException("SQL Server run shutdown failed.", distinct); }
    }

    private async Task RunActorAsync(Run run)
    {
        try
        {
            await run.Kernel.WaitForNotificationDrainAsync(run.Stop.Token).ConfigureAwait(false);
            while (!run.StopRequested && !run.Stop.IsCancellationRequested)
            {
                var attempt = new Attempt();
                long generation;
                try { generation = run.Kernel.BeginAcquisition(attempt, attempt.Acquire); }
                catch (InvalidOperationException) when (run.StopRequested) { break; }
                var acquisitionStart = _timeProvider.GetTimestamp();
                attempt.Acquire.TryLaunch(async () =>
                {
                    // Creation is inside the already-registered owned operation. A stop that wins
                    // the latch seal cannot be followed by a late connection allocation/open.
                    attempt.Session = _sessionFactory() ?? throw new InvalidOperationException("The SQL session factory returned null.");
                    attempt.Result = await attempt.Session.AcquireAsync(_lockResource, LockTimeoutMilliseconds,
                        AcquisitionCommandTimeoutSeconds, run.Stop.Token).ConfigureAwait(false);
                    if (attempt.Result is 0 or 1)
                    {
                        attempt.Acquired = true;
                        if (_fencingTokenProvider is not null)
                        {
                            attempt.Fence = await FencingTokenMinting.MintWithRetryAsync(_fencingTokenProvider,
                                _lockResource, run.Stop.Token, FencingTokenMintMaxAttempts).ConfigureAwait(false);
                        }
                    }
                });
                try
                {
                    await attempt.Acquire.Completion.ConfigureAwait(false);
                    if (attempt.Acquired && !run.Stop.IsCancellationRequested)
                    {
                        run.Kernel.Publish(generation, acquisitionStart, attempt.Session!, attempt.Fence, _timeProvider.GetUtcNow());
                    }
                    else if (run.Stop.IsCancellationRequested)
                    {
                        run.Kernel.Retire(generation, "stopped");
                    }
                    else
                    {
                        _notifications.EnqueueDiagnostic(LogLockAcquisitionFailed, CandidateId, attempt.Result);
                        run.Kernel.ReportObservation(generation, false, null, "application-lock acquisition was not granted", null, _timeProvider.GetUtcNow());
                        run.Kernel.Retire(generation, "application-lock acquisition was not granted");
                    }
                }
                catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { run.Kernel.Retire(generation, "stopped"); }
                catch (Exception error)
                {
                    if (attempt.Acquired) { _notifications.EnqueueDiagnostic(LogFencingTokenIssueError, error, CandidateId, _lockResource); }
                    else { _notifications.EnqueueDiagnostic(LogLockAcquisitionError, error, CandidateId); }
                    run.Kernel.Retire(generation, "acquisition failed", error);
                    run.Kernel.ReportObservation(generation, false, null,
                        attempt.Acquired ? "fencing token mint failed" : "error during acquisition", error, _timeProvider.GetUtcNow());
                }
                finally { run.Initial.TrySetResult(); }

                var previousRequestStart = acquisitionStart;
                var interval = _options.RenewInterval;
                while (!run.Stop.IsCancellationRequested && run.Kernel.Current is not null)
                {
                    // A healthy round trip consumes R, rather than adding R again after the reply.
                    // An ambiguous reply uses RetryInterval from that SAME actual attempt's start.
                    await LeaderElectionDelay.WaitFromAsync(previousRequestStart, interval, _timeProvider, run.Stop.Token).ConfigureAwait(false);
                    var operation = new OwnedOperationLatch();
                    if (!run.Kernel.TryRegisterOperation(generation, operation)) { break; }
                    var verificationStart = _timeProvider.GetTimestamp();
                    previousRequestStart = verificationStart;
                    var proof = SqlServerSessionProof.Indeterminate;
                    operation.TryLaunch(async () =>
                    {
                        proof = await attempt.Session!.VerifyAsync(_lockResource, VerificationCommandTimeoutSeconds, run.Stop.Token).ConfigureAwait(false);
                    });
                    try
                    {
                        await operation.Completion.ConfigureAwait(false);
                        switch (proof)
                        {
                            case SqlServerSessionProof.Exclusive:
                                run.Kernel.Confirm(generation, verificationStart);
                                interval = _options.RenewInterval;
                                break;
                            case SqlServerSessionProof.Lost:
                                run.Kernel.Retire(generation, "SQL session no longer proves Exclusive ownership");
                                break;
                            default:
                                interval = _options.RetryInterval;
                                break;
                        }
                    }
                    catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { break; }
                    catch (Exception error)
                    {
                        _notifications.EnqueueDiagnostic(LogRenewalError, error, CandidateId);
                        if (_failureClassifier?.Classify(error) == MessageFailureKind.Permanent)
                        {
                            run.Kernel.Retire(generation, "permanent SQL verification failure", error);
                        }
                        interval = _options.RetryInterval;
                    }
                }
                run.Kernel.Retire(generation, run.Stop.IsCancellationRequested ? "stopped" : "tenure ended");
                await run.Kernel.RetirementTask.ConfigureAwait(false);
                run.Kernel.CompleteRetirement(generation);
                await run.Kernel.WaitForNotificationDrainAsync(run.Stop.Token).ConfigureAwait(false);
                // Serial overdue intervals cause one immediate attempt, never a catch-up burst.
                await LeaderElectionDelay.WaitFromAsync(acquisitionStart, _options.RetryInterval, _timeProvider, run.Stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { }
        catch (Exception error)
        {
            run.ActorFailure = error;
            run.Initial.TrySetException(error);
            _ = BeginStop(run.Slot);
        }
        finally { run.Initial.TrySetResult(); }
    }

    private async Task RetireAsync(Run run, RenewalTenureDeadline.Retirement retirement)
    {
        var attempt = (Attempt)retirement.AcquisitionOwner;
        Exception? operationFailure = null;
        if (retirement.OperationCompletion is not null)
        {
            try { await retirement.OperationCompletion.ConfigureAwait(false); }
            catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { }
            catch (Exception error) { operationFailure = error; }
        }
        if (attempt.Session is not { } session) { return; }
        var failures = new List<Exception>();
        Exception? releaseFailure = null;
        // Release is scoped to the original, now-quiescent session. Physical disposal is
        // attempted even after a release failure, before best-effort diagnostics are enqueued.
        try { await session.ReleaseAsync(_lockResource, AcquisitionCommandTimeoutSeconds, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception error) { failures.Add(error); releaseFailure = error; }
        try { await session.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { failures.Add(error); }
        if (operationFailure is not null)
        {
            try { _notifications.EnqueueDiagnostic(LogRenewalError, operationFailure, CandidateId); }
            catch (Exception error) { failures.Add(error); }
        }
        if (releaseFailure is not null)
        {
            try { _notifications.EnqueueDiagnostic(LogLockReleaseError, releaseFailure, CandidateId); }
            catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count == 1) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
        if (failures.Count > 1) { throw new AggregateException("SQL session cleanup failed.", failures); }
        _notifications.EnqueueDiagnostic(LogLockReleased, CandidateId);
    }

    private void Notify(RenewalTenureDeadline.Transition transition)
    {
        var args = new LeaderElectionEventArgs(CandidateId, _lockResource, timestamp: transition.Timestamp);
        InvokeEach(transition.Acquired ? BecameLeader : LostLeadership, args);
        InvokeEach(LeaderChanged, new LeaderChangedEventArgs(transition.Acquired ? transition.PreviousHolder : CandidateId,
            transition.Acquired ? CandidateId : null, _lockResource,
            fencingToken: transition.Acquired ? transition.FencingToken : null, timestamp: transition.Timestamp));
        // Foreign diagnostics cannot suppress either event family.
        if (transition.Acquired)
        {
            _notifications.EnqueueDiagnostic(LogBecameLeader, CandidateId, _lockResource);
            if (transition.FencingToken is { } fence) { _notifications.EnqueueDiagnostic(LogFencingTokenIssued, CandidateId, fence, _lockResource); }
        }
        else { _notifications.EnqueueDiagnostic(LogLostLeadership, CandidateId, _lockResource); }
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
                _lockResource, timestamp: observation.Timestamp));
        }
        if (observation.FailureReason is not null)
        {
            InvokeEach(AcquisitionFailed, new LeaderElectionAcquisitionFailedEventArgs(CandidateId, _lockResource,
                observation.FailureReason, observation.Timestamp, observation.Cause));
        }
    }

    private sealed class Attempt
    {
        internal ISqlServerElectionSession? Session { get; set; }
        internal OwnedOperationLatch Acquire { get; } = new();
        internal int Result { get; set; } = int.MinValue;
        internal bool Acquired { get; set; }
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
        internal Run(SqlServerLeaderElection owner, StartSlot slot)
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
	[LoggerMessage(LeaderElectionEventId.SqlServerStarting, LogLevel.Information,
		"Starting leader election for candidate {CandidateId} on resource {Resource}")]
	partial void LogStarting(string candidateId, string resource);

	[LoggerMessage(LeaderElectionEventId.SqlServerStopping, LogLevel.Information, "Stopping leader election for candidate {CandidateId}")]
	partial void LogStopping(string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerLockAcquisitionFailed, LogLevel.Debug,
		"Failed to acquire lock for {CandidateId}, result: {Result}")]
	partial void LogLockAcquisitionFailed(string candidateId, int result);

	[LoggerMessage(LeaderElectionEventId.SqlServerLockAcquisitionError, LogLevel.Error, "Error acquiring lock for {CandidateId}")]
	partial void LogLockAcquisitionError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerLockReleased, LogLevel.Debug, "Released lock for {CandidateId}")]
	partial void LogLockReleased(string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerLockReleaseError, LogLevel.Warning, "Error releasing lock for {CandidateId}")]
	partial void LogLockReleaseError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerRenewalError, LogLevel.Error, "Error in renewal loop for {CandidateId}")]
	partial void LogRenewalError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionEventId.SqlServerBecameLeader, LogLevel.Information,
		"Candidate {CandidateId} became leader for resource {Resource}")]
	partial void LogBecameLeader(string candidateId, string resource);

	[LoggerMessage(LeaderElectionEventId.SqlServerLostLeadership, LogLevel.Warning,
		"Candidate {CandidateId} lost leadership for resource {Resource}")]
	partial void LogLostLeadership(string candidateId, string resource);

	[LoggerMessage(LeaderElectionEventId.SqlServerFencingTokenIssued, LogLevel.Information,
		"Candidate {CandidateId} issued fencing token {Token} for resource {Resource}")]
	partial void LogFencingTokenIssued(string candidateId, long token, string resource);

	[LoggerMessage(LeaderElectionEventId.SqlServerFencingTokenIssueError, LogLevel.Error,
		"Candidate {CandidateId} failed to mint a fencing token for resource {Resource} after bounded retries; relinquishing leadership (fail-closed) rather than leading with an un-advanced fence")]
	partial void LogFencingTokenIssueError(Exception ex, string candidateId, string resource);
}
