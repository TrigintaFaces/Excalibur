// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Data;
using System.Data.Common;
using System.Globalization;
using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Postgres.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Excalibur.LeaderElection.Postgres;

/// <summary>Postgres session advisory-lock election with a monotonic local deadline.</summary>
/// <remarks>Uses tenure-specific ownership proofs and joins owned operations before remote cleanup.</remarks>
public sealed partial class PostgresLeaderElection : ILeaderElection, IAsyncDisposable
{
    private readonly Lock _runGate = new();
    private readonly LeaderElectionNotifications _notifications;
    private readonly Func<string, DbConnection> _sessionFactory;
    private readonly PostgresLeaderElectionOptions _pgOptions;
    private readonly string _connectionString;
    private readonly string _resource;
    private readonly LeaderElectionOptions _electionOptions;
    private readonly ILogger<PostgresLeaderElection> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IMessageFailureClassifier? _failureClassifier;
    private readonly IFencingTokenIssuer? _fencingTokenProvider;
    private const int FencingTokenMintMaxAttempts = 3;
    private StartSlot? _slot;
    private volatile bool _disposed;
    private Task? _disposal;

    /// <summary>Initializes a dedicated-session Postgres leader election.</summary>
    /// <param name="pgOptions">Postgres connection and lock options.</param>
    /// <param name="electionOptions">Local timing and candidate options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="fencingTokenProvider">Optional monotonic fencing provider.</param>
    /// <param name="timeProvider">Optional monotonic clock.</param>
    public PostgresLeaderElection(IOptions<PostgresLeaderElectionOptions> pgOptions,
        IOptions<LeaderElectionOptions> electionOptions, ILogger<PostgresLeaderElection> logger,
        IFencingTokenIssuer? fencingTokenProvider = null, TimeProvider? timeProvider = null)
        : this(pgOptions, electionOptions, logger, fencingTokenProvider, timeProvider, null, null) { }

    /// <summary>Initializes a Postgres candidate with shared failure classification.</summary>
    /// <param name="pgOptions">Postgres connection and lock options.</param>
    /// <param name="electionOptions">Local timing and candidate options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="fencingTokenProvider">Optional monotonic fencing provider.</param>
    /// <param name="timeProvider">Optional monotonic clock.</param>
    /// <param name="context">Optional cross-cutting failure classification.</param>
    public PostgresLeaderElection(IOptions<PostgresLeaderElectionOptions> pgOptions,
        IOptions<LeaderElectionOptions> electionOptions, ILogger<PostgresLeaderElection> logger,
        IFencingTokenIssuer? fencingTokenProvider, TimeProvider? timeProvider, PostgresLeaderElectionContext? context)
        : this(pgOptions, electionOptions, logger, fencingTokenProvider, timeProvider, context, null) { }

    // The factory transfers a new dedicated closed connection. It is an internal deterministic
    // protocol-test seam, not a promise that arbitrary wrappers can preserve backend ownership.
    internal PostgresLeaderElection(IOptions<PostgresLeaderElectionOptions> pgOptions,
        IOptions<LeaderElectionOptions> electionOptions, ILogger<PostgresLeaderElection> logger,
        IFencingTokenIssuer? fencingTokenProvider, TimeProvider? timeProvider,
        PostgresLeaderElectionContext? context, Func<string, DbConnection>? sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(pgOptions);
        ArgumentNullException.ThrowIfNull(electionOptions);
        ArgumentNullException.ThrowIfNull(logger);
        var supplied = pgOptions.Value;
        _pgOptions = new PostgresLeaderElectionOptions
        {
            ConnectionString = supplied.ConnectionString,
            LockKey = supplied.LockKey,
            CommandTimeoutSeconds = supplied.CommandTimeoutSeconds,
        };
        _pgOptions.Validate();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_pgOptions.CommandTimeoutSeconds, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(_pgOptions.CommandTimeoutSeconds, 300);
        _electionOptions = electionOptions.Value;
        var validation = new LeaderElectionOptionsValidator().Validate(null, _electionOptions);
        if (validation.Failed) { throw new OptionsValidationException(nameof(LeaderElectionOptions), typeof(LeaderElectionOptions), validation.Failures); }
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _failureClassifier = context?.FailureClassifier;
        _fencingTokenProvider = fencingTokenProvider;
        _connectionString = BuildLockConnectionString(_pgOptions.ConnectionString);
        _sessionFactory = sessionFactory ?? (connectionString => new NpgsqlConnection(connectionString));
        _resource = _pgOptions.LockKey.ToString(CultureInfo.InvariantCulture);
        CandidateId = LeaderCandidateIdentity.Resolve(_electionOptions.InstanceId);
        _notifications = new LeaderElectionNotifications(error => LogRenewalError(error, CandidateId));
    }

    private static string BuildLockConnectionString(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            Multiplexing = false,
            Enlist = false,
        }.ConnectionString;

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
                _notifications.EnqueueDiagnostic(LogStarting, CandidateId, _pgOptions.LockKey);
#pragma warning disable RS0030 // The owned admission worker cannot run consumer notification-drain code under the run gate; Stop joins this task.
                slot.Worker = LeaderElectionExecution.RunAsync(() => AdmitRunAsync(slot));
#pragma warning restore RS0030
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
#pragma warning disable RS0030 // A tracked protocol actor owns SDK result processing and is joined during Stop, separately from resource retirement.
                run.Actor = LeaderElectionExecution.RunAsync(() => RunActorAsync(run));
#pragma warning restore RS0030
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
#pragma warning disable RS0030 // Memoized shutdown runs cancellation and owned joins outside the run gate, including notification-reentrant Stop.
            return slot.Shutdown = LeaderElectionExecution.RunAsync(() => StopSlotAsync(slot));
#pragma warning restore RS0030
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
        if (distinct.Length > 1) { throw new AggregateException("Postgres run shutdown failed.", distinct); }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Each Attempt is transferred to BeginAcquisition, whose retirement callback joins its SDK latch and disposes it; failed transfer disposes the attempt in the adjacent catch.")]
    private async Task RunActorAsync(Run run)
    {
        try
        {
            await run.Kernel.WaitForNotificationDrainAsync(run.Stop.Token).ConfigureAwait(false);
            while (!run.StopRequested && !run.Stop.IsCancellationRequested)
            {
                var attempt = new Attempt(_sessionFactory(_connectionString));
                long generation;
                try { generation = run.Kernel.BeginAcquisition(attempt, attempt.Acquire); }
                catch
                {
                    await attempt.DisposeAsync().ConfigureAwait(false);
                    if (run.StopRequested) { break; }
                    throw;
                }
                var start = _timeProvider.GetTimestamp();
                attempt.Acquire.TryLaunch(async () =>
                {
                    if (attempt.Connection.State != ConnectionState.Closed)
                    {
                        throw new InvalidOperationException("The Postgres session factory must transfer a new closed connection.");
                    }
                    await attempt.Connection.OpenAsync(run.Stop.Token).ConfigureAwait(false);
                    attempt.AcquisitionAttempted = true;
                    attempt.Acquired = await ExecuteBooleanAsync(attempt.Connection,
                        "SELECT pg_catalog.pg_try_advisory_lock(@lockKey)", run.Stop.Token).ConfigureAwait(false);
                    attempt.AcquisitionAnswered = true;
                    if (attempt.Acquired && _fencingTokenProvider is not null)
                    {
                        attempt.Fence = await FencingTokenMinting.MintWithRetryAsync(_fencingTokenProvider,
                            _resource, run.Stop.Token, FencingTokenMintMaxAttempts).ConfigureAwait(false);
                    }
                });
                try
                {
                    await attempt.Acquire.Completion.ConfigureAwait(false);
                    if (attempt.Acquired && !run.Stop.IsCancellationRequested)
                    {
                        if (!run.Kernel.Publish(generation, start, attempt, attempt.Fence, _timeProvider.GetUtcNow()))
                        {
                            run.Kernel.ReportObservation(generation, false, null, "acquisition proof expired before publication", null, _timeProvider.GetUtcNow());
                        }
                    }
                    else
                    {
                        _notifications.EnqueueDiagnostic(LogLockAcquisitionFailed, CandidateId, _pgOptions.LockKey);
                        run.Kernel.ReportObservation(generation, true, null, "lost the acquisition race", null, _timeProvider.GetUtcNow());
                        run.Kernel.Retire(generation, "acquisition race lost");
                    }
                }
                catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { run.Kernel.Retire(generation, "stopped"); }
                catch (Exception error)
                {
                    run.Kernel.Retire(generation, "acquisition failed", error);
                    if (attempt.Acquired && _fencingTokenProvider is not null)
                    {
                        _notifications.EnqueueDiagnostic(LogFencingTokenIssueError, error, CandidateId, _resource);
                    }
                    run.Kernel.ReportObservation(generation, false, null,
                        attempt.Acquired ? "fencing token mint failed" : "error during acquisition", error, _timeProvider.GetUtcNow());
                    _notifications.EnqueueDiagnostic(LogLockAcquisitionError, error, CandidateId);
                }
                finally { run.Initial.TrySetResult(); }

                var cadenceAnchor = start;
                var renewalDelay = _electionOptions.RenewInterval;
                while (!run.Stop.IsCancellationRequested && run.Kernel.Current is not null)
                {
                    await LeaderElectionDelay.WaitFromAsync(cadenceAnchor, renewalDelay, _timeProvider, run.Stop.Token).ConfigureAwait(false);
                    var operation = new OwnedOperationLatch();
                    if (!run.Kernel.TryRegisterOperation(generation, operation)) { break; }
                    var renewalStart = _timeProvider.GetTimestamp();
                    cadenceAnchor = renewalStart;
                    var renewed = false;
                    var backendAvailable = false;
                    operation.TryLaunch(async () =>
                    {
                        // No reconnect: a closed/broken dedicated session cannot retain its advisory lock.
                        backendAvailable = attempt.Connection.State == ConnectionState.Open;
                        renewed = backendAvailable &&
                            await ExecuteBooleanAsync(attempt.Connection, VerifyOwnershipSql, run.Stop.Token).ConfigureAwait(false);
                    });
                    try
                    {
                        await operation.Completion.ConfigureAwait(false);
                        if (renewed)
                        {
                            run.Kernel.Confirm(generation, renewalStart);
                            renewalDelay = _electionOptions.RenewInterval;
                        }
                        else { run.Kernel.Retire(generation, backendAvailable
                            ? "configured advisory lock ownership is absent"
                            : "dedicated backend is unavailable"); }
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
                        renewalDelay = _electionOptions.RetryInterval;
                        // Ambiguous/unrelated cancellation does not advance the deadline or stop the actor.
                    }
                }
                run.Kernel.Retire(generation, run.Stop.IsCancellationRequested ? "stopped" : "tenure ended");
                await run.Kernel.RetirementTask.ConfigureAwait(false);
                run.Kernel.CompleteRetirement(generation);
                await run.Kernel.WaitForNotificationDrainAsync(run.Stop.Token).ConfigureAwait(false);
                await LeaderElectionDelay.WaitFromAsync(cadenceAnchor, _electionOptions.RetryInterval, _timeProvider, run.Stop.Token).ConfigureAwait(false);
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
        var failures = new List<Exception>();
        Exception? operationFailure = null;
        Exception? unlockFailure = null;
        var unlocked = false;
        if (retirement.OperationCompletion is not null)
        {
            try { await retirement.OperationCompletion.ConfigureAwait(false); }
            catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { }
            catch (Exception error) { operationFailure = error; }
        }
        try
        {
            if (attempt.Connection.State == ConnectionState.Open &&
                (attempt.Acquired || (attempt.AcquisitionAttempted && !attempt.AcquisitionAnswered)))
            {
                _ = await ExecuteBooleanAsync(attempt.Connection,
                    "SELECT pg_catalog.pg_advisory_unlock(@lockKey)", CancellationToken.None).ConfigureAwait(false);
                unlocked = true;
            }
        }
        catch (Exception error) { unlockFailure = error; failures.Add(error); }
        try { await attempt.DisposeAsync().ConfigureAwait(false); }
        catch (Exception disposeFailure) { failures.Add(disposeFailure); }
        // Queue best-effort diagnostics after SDK join and owned-session disposal; their callbacks run independently.
        if (operationFailure is not null)
        {
            try { _notifications.EnqueueDiagnostic(LogRenewalError, operationFailure, CandidateId); }
            catch (Exception loggingFailure) { failures.Add(operationFailure); failures.Add(loggingFailure); }
        }
        if (unlockFailure is not null)
        {
            try { _notifications.EnqueueDiagnostic(LogLockReleaseError, unlockFailure, CandidateId); }
            catch (Exception loggingFailure) { failures.Add(loggingFailure); }
        }
        else if (unlocked)
        {
            try { _notifications.EnqueueDiagnostic(LogLockReleased, CandidateId); }
            catch (Exception loggingFailure) { failures.Add(loggingFailure); }
        }
        if (failures.Count == 1)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 1)
        {
            throw new AggregateException("Postgres owned session retirement failed.", failures);
        }
    }

    private const string VerifyOwnershipSql = """
        SELECT EXISTS (
            SELECT 1 FROM pg_catalog.pg_locks
            WHERE locktype = 'advisory' AND pid = pg_catalog.pg_backend_pid() AND granted
                AND mode = 'ExclusiveLock' AND objsubid = 1
                AND database = (SELECT oid FROM pg_catalog.pg_database WHERE datname = pg_catalog.current_database())
                AND classid = ((CAST(@lockKey AS bigint) >> 32) & 4294967295)::oid
                AND objid = (CAST(@lockKey AS bigint) & 4294967295)::oid)
        """;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Private callers supply only the three fixed advisory-lock SQL literals; the configured lock key is always bound as an Int64 parameter.")]
    private async Task<bool> ExecuteBooleanAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = _pgOptions.CommandTimeoutSeconds;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "lockKey";
        parameter.DbType = DbType.Int64;
        parameter.Value = _pgOptions.LockKey;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is bool value ? value : throw new InvalidOperationException("Postgres advisory-lock commands must return a boolean.");
    }

    private void Notify(RenewalTenureDeadline.Transition transition)
    {
        var args = new LeaderElectionEventArgs(CandidateId, _resource, timestamp: transition.Timestamp);
        InvokeEach(transition.Acquired ? BecameLeader : LostLeadership, args);
        InvokeEach(LeaderChanged, new LeaderChangedEventArgs(transition.Acquired ? transition.PreviousHolder : CandidateId,
            transition.Acquired ? CandidateId : null, _resource,
            fencingToken: transition.Acquired ? transition.FencingToken : null, timestamp: transition.Timestamp));
        // Foreign diagnostics cannot suppress either event family.
        if (transition.Acquired)
        {
            _notifications.EnqueueDiagnostic(LogBecameLeader, CandidateId, _resource);
            if (transition.FencingToken is { } fence) { _notifications.EnqueueDiagnostic(LogFencingTokenIssued, CandidateId, fence, _resource); }
        }
        else { _notifications.EnqueueDiagnostic(LogLostLeadership, CandidateId, _resource); }
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
                _resource, timestamp: observation.Timestamp));
        }
        if (observation.FailureReason is not null)
        {
            InvokeEach(AcquisitionFailed, new LeaderElectionAcquisitionFailedEventArgs(CandidateId, _resource,
                observation.FailureReason, observation.Timestamp, observation.Cause));
        }
    }

    private sealed class Attempt(DbConnection connection) : IAsyncDisposable
    {
        private readonly Lock _disposeGate = new();
        private Task? _disposal;
        internal DbConnection Connection { get; } = connection ?? throw new ArgumentNullException(nameof(connection));
        internal OwnedOperationLatch Acquire { get; } = new();
        internal bool AcquisitionAttempted { get; set; }
        internal bool AcquisitionAnswered { get; set; }
        internal bool Acquired { get; set; }
        internal long? Fence { get; set; }
        public ValueTask DisposeAsync()
        {
            lock (_disposeGate) { return new ValueTask(_disposal ??= Connection.DisposeAsync().AsTask()); }
        }
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
        internal Run(PostgresLeaderElection owner, StartSlot slot)
        {
            Slot = slot;
            Kernel = new RenewalTenureDeadline(owner._timeProvider, owner._electionOptions.RenewInterval + owner._electionOptions.GracePeriod,
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
	[LoggerMessage(LeaderElectionPostgresEventId.LeaderElectionStarted, LogLevel.Information,
		"Starting Postgres leader election for candidate {CandidateId} on lock key {LockKey}")]
	private partial void LogStarting(string candidateId, long lockKey);

	[LoggerMessage(LeaderElectionPostgresEventId.LeaderElectionStopped, LogLevel.Information,
		"Stopping Postgres leader election for candidate {CandidateId}")]
	private partial void LogStopping(string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.LockAcquisitionFailed, LogLevel.Debug,
		"Failed to acquire advisory lock for {CandidateId}, lock key: {LockKey}")]
	private partial void LogLockAcquisitionFailed(string candidateId, long lockKey);

	[LoggerMessage(LeaderElectionPostgresEventId.LockAcquisitionError, LogLevel.Error,
		"Error acquiring advisory lock for {CandidateId}")]
	private partial void LogLockAcquisitionError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.LockReleased, LogLevel.Debug,
		"Released advisory lock for {CandidateId}")]
	private partial void LogLockReleased(string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.LockReleaseError, LogLevel.Warning,
		"Error releasing advisory lock for {CandidateId}")]
	private partial void LogLockReleaseError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.LeaderElectionError, LogLevel.Error,
		"Error in renewal loop for {CandidateId}")]
	private partial void LogRenewalError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.BecameLeader, LogLevel.Information,
		"Candidate {CandidateId} became leader for resource {Resource}")]
	private partial void LogBecameLeader(string candidateId, string resource);

	[LoggerMessage(LeaderElectionPostgresEventId.LostLeadership, LogLevel.Warning,
		"Candidate {CandidateId} lost leadership for resource {Resource}")]
	private partial void LogLostLeadership(string candidateId, string resource);

	[LoggerMessage(LeaderElectionPostgresEventId.LeaderElectionDisposeError, LogLevel.Warning,
		"Error during leader election dispose for {CandidateId}")]
	private partial void LogDisposeError(Exception ex, string candidateId);

	[LoggerMessage(LeaderElectionPostgresEventId.FencingTokenIssued, LogLevel.Information,
		"Candidate {CandidateId} issued fencing token {Token} for resource {Resource}")]
	private partial void LogFencingTokenIssued(string candidateId, long token, string resource);

	[LoggerMessage(LeaderElectionPostgresEventId.FencingTokenIssueError, LogLevel.Error,
		"Candidate {CandidateId} failed to mint a fencing token for resource {Resource} after bounded retries; relinquishing leadership (fail-closed) rather than leading with an un-advanced fence")]
	private partial void LogFencingTokenIssueError(Exception ex, string candidateId, string resource);
}
