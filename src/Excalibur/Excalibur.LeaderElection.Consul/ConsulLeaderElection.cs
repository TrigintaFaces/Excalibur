// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Collections.Concurrent;
using Consul;
using Lock = System.Threading.Lock;
using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Excalibur.LeaderElection.Consul;

/// <summary>Consul session/KV election with owned RPC lifetimes and an independent local deadline.</summary>
public sealed partial class ConsulLeaderElection : IHealthBasedLeaderElection, IDisposable, IAsyncDisposable
{
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(2);
    private readonly Lock _runGate = new();
    private readonly LeaderElectionNotifications _notifications;
    // No lifecycle-gate path may acquire this gate. It serializes only local proof
    // publication/conditional retirement and local health eligibility, never SDK I/O.
    private readonly Lock _proofGate = new();
    private readonly string _resourceName;
    private readonly ConsulLeaderElectionOptions _options;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
        Justification = "DisposeCoreAsync joins the lifecycle and public SDK leases, then disposes only a client constructed by this instance; supplied clients are borrowed.")]
    private readonly IConsulClient _consulClient;
    private readonly bool _ownsClient;
    private readonly ILogger<ConsulLeaderElection> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IFencingTokenIssuer? _fencingTokenProvider;
    private readonly IMessageFailureClassifier? _failureClassifier;
    private readonly ResiliencePipeline _retryPolicy;
    private readonly ConcurrentDictionary<string, CandidateHealth> _candidateHealthCache = new(StringComparer.Ordinal);
    private LocalHealth _localHealth = new(true, 1, new Dictionary<string, string>(StringComparer.Ordinal));
    private StartSlot? _slot;
    private volatile bool _disposed;
    private Task? _disposal;
    private int _publicOperations;
    private TaskCompletionSource? _publicDrain;

    /// <summary>Creates an election using the existing constructor contract.</summary>
    /// <param name="resourceName">Resource to coordinate.</param>
    /// <param name="options">Consul and timing settings, captured on construction.</param>
    /// <param name="consulClient">Borrowed client, or null to construct an owned client.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="fencingTokenProvider">Optional fencing token provider.</param>
    /// <param name="timeProvider">Optional monotonic clock.</param>
    public ConsulLeaderElection(string resourceName, IOptions<ConsulLeaderElectionOptions>? options,
        IConsulClient? consulClient, ILogger<ConsulLeaderElection>? logger,
        IFencingTokenIssuer? fencingTokenProvider = null, TimeProvider? timeProvider = null)
        : this(resourceName, options, consulClient, logger, fencingTokenProvider, timeProvider, null) { }

    /// <summary>Creates an election with optional permanent-failure classification.</summary>
    /// <param name="resourceName">Resource to coordinate.</param>
    /// <param name="options">Consul and timing settings, captured on construction.</param>
    /// <param name="consulClient">Borrowed client, or null to construct an owned client.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="fencingTokenProvider">Optional fencing token provider.</param>
    /// <param name="timeProvider">Optional monotonic clock.</param>
    /// <param name="failureClassifier">Optional classifier; permanent failures accelerate relinquishment.</param>
    public ConsulLeaderElection(string resourceName, IOptions<ConsulLeaderElectionOptions>? options,
        IConsulClient? consulClient, ILogger<ConsulLeaderElection>? logger,
        IFencingTokenIssuer? fencingTokenProvider, TimeProvider? timeProvider, IMessageFailureClassifier? failureClassifier)
    {
        _resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
        _options = ConsulElectionConfiguration.Capture(options?.Value ?? throw new ArgumentNullException(nameof(options)));
        _logger = ConsulElectionConfiguration.LoggerOrDefault(logger);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _fencingTokenProvider = fencingTokenProvider;
        _failureClassifier = failureClassifier;
        _ownsClient = consulClient is null;
        _consulClient = consulClient ?? ConsulElectionConfiguration.CreateClient(_options);
        CandidateId = LeaderCandidateIdentity.Resolve(_options.InstanceId);
        _notifications = new LeaderElectionNotifications(error => LogErrorDuringMonitoring(error, _resourceName));
        _retryPolicy = ConsulElectionConfiguration.CreateRetryPolicy(_options,
            (error, attempt, delay) => _notifications.EnqueueDiagnostic(LogRetryAttempt, error, attempt, delay, _resourceName));
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
        get { var held = CurrentRun()?.Kernel.Current; return held is null ? null : new Leadership(held.FencingToken, held.AcquiredAt); }
    }
    /// <inheritdoc/>
    public string? CurrentLeaderId
    {
        get { var run = CurrentRun(); if (run is null) { return null; } var status = run.Kernel.ReadStatus(); return status.Tenure is not null ? CandidateId : status.ObservedHolder; }
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
            if (_slot is { Shutdown: { } oldShutdown } && !oldShutdown.IsCompletedSuccessfully) { throw new InvalidOperationException("Previous Consul run has not reached successful quiescence."); }
            if (_slot is { Shutdown: null } running) { slot = running; }
            else
            {
                slot = new StartSlot(_slot?.Run?.Kernel.NotificationDrainTask ?? _slot?.PreviousNotifications ?? Task.CompletedTask);
                _slot = slot;
                slot.Worker = LeaderElectionExecution.RunAsync(() => AdmitRunAsync(slot));
            }
        }
        try { await slot.Initial.Task.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { _ = BeginStop(slot); throw; }
    }

    private async Task AdmitRunAsync(StartSlot slot)
    {
        try
        {
            await slot.PreviousNotifications.WaitAsync(slot.Stop.Token).ConfigureAwait(false);
            Run run;
            lock (_runGate)
            {
                if (slot.StopRequested || _disposed || !ReferenceEquals(_slot, slot)) { throw new OperationCanceledException(slot.Stop.Token); }
                run = new Run(this, slot); slot.Run = run;
                run.Actor = LeaderElectionExecution.RunAsync(() => RunActorAsync(run));
            }
            await run.Initial.Task.ConfigureAwait(false);
            slot.Initial.TrySetResult();
        }
        catch (OperationCanceledException) when (slot.StopRequested) { slot.Initial.TrySetCanceled(slot.Stop.Token); }
        catch (Exception error) { slot.Initial.TrySetException(error); throw; }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_runGate)
        {
            if (_disposed) { return (_disposal ?? Task.CompletedTask).WaitAsync(cancellationToken); }
            return _slot is null ? Task.CompletedTask : BeginStop(_slot).WaitAsync(cancellationToken);
        }
    }
    private Run? CurrentRun() { lock (_runGate) { return _slot?.Run; } }
    private Task BeginStop(StartSlot slot)
    {
        lock (_runGate)
        {
            if (slot.Shutdown is not null) { return slot.Shutdown; }
            slot.StopRequested = true;
            _notifications.EnqueueDiagnostic(LogStoppingElection, _resourceName);
            slot.Run?.Kernel.RequestStop();
            return slot.Shutdown = LeaderElectionExecution.RunAsync(() => StopSlotAsync(slot));
        }
    }
    private async Task StopSlotAsync(StartSlot slot)
    {
        var failures = new List<Exception>();
        await CaptureAsync(() => slot.Stop.CancelAsync(), failures).ConfigureAwait(false);
        await CaptureAsync(() => slot.Worker, failures).ConfigureAwait(false);
        if (slot.Run is { } run)
        {
            await CaptureAsync(() => run.Actor, failures).ConfigureAwait(false);
            await CaptureAsync(() => run.Kernel.DisposeAsync().AsTask(), failures).ConfigureAwait(false);
            if (run.ActorFailure is not null) { failures.Add(run.ActorFailure); }
            await WriteCurrentHealthAsync(isHealthy: false, CancellationToken.None).ConfigureAwait(false);
        }
        slot.Stop.Dispose();
        ConsulElectionFailures.Throw(failures);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
#pragma warning disable RS0030 // Existing synchronous disposal joins the memoized ownership cleanup; no consumer callback is joined.
        BeginDispose().GetAwaiter().GetResult();
#pragma warning restore RS0030
        GC.SuppressFinalize(this);
    }
    /// <inheritdoc/>
    public async ValueTask DisposeAsync() { await BeginDispose().ConfigureAwait(false); GC.SuppressFinalize(this); }
    private Task BeginDispose()
    {
        lock (_runGate)
        {
            _disposed = true;
            // Permanent disposal seals the same admitted start slot before its async stop worker runs.
            if (_slot is { } slot) { slot.StopRequested = true; }
            return _disposal ??= LeaderElectionExecution.RunAsync(DisposeCoreAsync);
        }
    }
    private async Task DisposeCoreAsync()
    {
        try
        {
            var failures = new List<Exception>();
            StartSlot? slot; Task publicDrain;
            lock (_runGate) { slot = _slot; publicDrain = _publicDrain?.Task ?? Task.CompletedTask; }
            if (slot is not null) { await CaptureAsync(() => BeginStop(slot), failures).ConfigureAwait(false); }
            await CaptureAsync(() => publicDrain, failures).ConfigureAwait(false);
            if (_ownsClient)
            {
                try { _consulClient.Dispose(); }
                catch (Exception error) { failures.Add(error); }
            }
            ConsulElectionFailures.Throw(failures);
        }
        finally { _notifications.Seal(); }
    }

    private async Task RunActorAsync(Run run)
    {
        try
        {
            _notifications.EnqueueDiagnostic(LogStartingElection, _resourceName, CandidateId);
            while (!run.Stop.IsCancellationRequested)
            {
                bool eligible; lock (_proofGate) { eligible = CanLeadUnderProofGate(); }
                if (!eligible)
                {
                    run.Initial.TrySetResult();
                    await LeaderElectionDelay.WaitAsync(_options.RetryInterval, _timeProvider, run.Stop.Token).ConfigureAwait(false);
                    continue;
                }
                Attempt attempt; long generation;
                // Stop and acquisition admission share the lifecycle gate. A normal stop
                // cannot turn a loop-boundary race into an unrelated actor failure.
                lock (_runGate)
                {
                    if (run.StopRequested) { break; }
                    attempt = new Attempt();
                    generation = run.Kernel.BeginAcquisition(attempt, attempt.Acquire);
                }
                attempt.Generation = generation;
                attempt.Acquire.TryLaunch(() => AcquireAsync(run, attempt));
                try
                {
                    await attempt.Acquire.Completion.ConfigureAwait(false);
                    bool published;
                    lock (_proofGate)
                    {
                        published = attempt.Acquired && CanLeadUnderProofGate() && !run.Stop.IsCancellationRequested &&
                            run.Kernel.Publish(generation, attempt.ProofRequestStart, attempt, attempt.Fence, _timeProvider.GetUtcNow());
                    }
                    if (published)
                    {
                        _notifications.EnqueueDiagnostic(LogAcquiredLeadership, _resourceName);
                        attempt.Monitor.TryLaunch(() => MonitorAsync(run, attempt));
                    }
                    else
                    {
                        run.Kernel.ReportObservation(generation, true, attempt.OtherLeader, "Consul acquisition did not establish a usable tenure", null, _timeProvider.GetUtcNow());
                        run.Kernel.Retire(generation, "acquisition not published");
                    }
                }
                catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { run.Kernel.Retire(generation, "stopped"); }
                catch (Exception error)
                {
                    run.Kernel.Retire(generation, "Consul acquisition failed", error);
                    run.Kernel.ReportObservation(generation, false, null, attempt.Acquired ? "fencing or ownership proof failed" : "error during acquisition", error, _timeProvider.GetUtcNow());
                    _notifications.EnqueueDiagnostic(LogErrorAcquiringLeadership, error, _resourceName);
                    if (attempt.SessionId is null && !run.Initial.Task.IsCompleted)
                    {
                        // Preserve startup propagation after session-create retries exhaust.
                        run.Initial.TrySetException(error);
                        _ = BeginStop(run.Slot);
                    }
                }
                finally { run.Initial.TrySetResult(); }

                var lastRequestStart = attempt.ProofRequestStart;
                var interval = _options.RenewInterval;
                while (!run.Stop.IsCancellationRequested && run.Kernel.Current is not null)
                {
                    await LeaderElectionDelay.WaitFromAsync(lastRequestStart, interval, _timeProvider, run.Stop.Token).ConfigureAwait(false);
                    var operation = new OwnedOperationLatch();
                    if (!run.Kernel.TryRegisterOperation(generation, operation)) { break; }
                    lastRequestStart = _timeProvider.GetTimestamp();
                    var renewalStart = lastRequestStart;
                    var confirmed = false;
                    operation.TryLaunch(async () =>
                    {
                        if (!await ConsulElectionSessions.RenewAsync(_consulClient, _options, attempt.SessionId!, run.Stop.Token).ConfigureAwait(false)) { return; }
                        var pair = await ReadLeaderAsync(run.Stop.Token).ConfigureAwait(false);
                        confirmed = Owns(pair, attempt);
                    });
                    try
                    {
                        await operation.Completion.ConfigureAwait(false);
                        lock (_proofGate)
                        {
                            if (confirmed && CanLeadUnderProofGate()) { run.Kernel.Confirm(generation, renewalStart); }
                            else { run.Kernel.Retire(generation, "Consul session or KV ownership proof no longer matches"); }
                        }
                        interval = _options.RenewInterval;
                    }
                    catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { break; }
                    catch (Exception error)
                    {
                        _notifications.EnqueueDiagnostic(LogErrorDuringRenewal, error, _resourceName);
                        // The pinned SDK maps a missing/expired server session to this
                        // exception. It is definitive loss, even without a classifier.
                        if (ConsulElectionFailures.IsSessionExpired(error)) { run.Kernel.Retire(generation, "Consul session expired", error); }
                        else if (ConsulElectionFailures.IsPermanent(_failureClassifier, error)) { run.Kernel.Retire(generation, "permanent Consul renewal failure", error); }
                        interval = _options.RetryInterval;
                    }
                }
                run.Kernel.Retire(generation, run.Stop.IsCancellationRequested ? "stopped" : "tenure ended");
                await run.Kernel.RetirementTask.ConfigureAwait(false);
                run.Kernel.CompleteRetirement(generation);
                await run.Kernel.WaitForNotificationDrainAsync(run.Stop.Token).ConfigureAwait(false);
                await LeaderElectionDelay.WaitAsync(_options.RetryInterval, _timeProvider, run.Stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { }
        catch (Exception error) { run.ActorFailure = error; run.Initial.TrySetException(error); _ = BeginStop(run.Slot); }
        finally { run.Initial.TrySetResult(); }
    }
    private async Task AcquireAsync(Run run, Attempt attempt)
    {
        attempt.SessionId = await ConsulElectionSessions.CreateAsync(_consulClient, _options, _retryPolicy,
            _resourceName, CandidateId, attempt.Nonce, run.Stop.Token).ConfigureAwait(false);
        _notifications.EnqueueDiagnostic(LogCreatedSession, attempt.SessionId, _resourceName);
        await WriteCurrentHealthAsync(null, run.Stop.Token).ConfigureAwait(false);
        // Create/metadata retries are not TTL confirmation. Renew immediately before acquire;
        // anchor before that request, then verify actual KV.Session and private payload proof.
        attempt.ProofRequestStart = _timeProvider.GetTimestamp();
        if (!await ConsulElectionSessions.RenewAsync(_consulClient, _options, attempt.SessionId, run.Stop.Token).ConfigureAwait(false)) { return; }
        var information = new LeaderInfo
        {
            CandidateId = CandidateId, SessionId = attempt.SessionId, OwnerNonce = attempt.Nonce,
            AcquiredAt = _timeProvider.GetUtcNow(), Metadata = new Dictionary<string, string>(_options.CandidateMetadata, StringComparer.Ordinal)
        };
        attempt.Payload = ConsulElectionPayloads.EncodeLeader(information);
        var acquired = await _retryPolicy.ExecuteAsync(async ct =>
            await _consulClient.KV.Acquire(new KVPair(GetLeaderKeyPath()) { Session = attempt.SessionId, Value = attempt.Payload }, ConsulElectionConfiguration.WriteOptions(_options), ct).ConfigureAwait(false), run.Stop.Token).ConfigureAwait(false);
        attempt.Acquired = acquired.Response;
        var current = await ReadLeaderAsync(run.Stop.Token).ConfigureAwait(false);
        attempt.OtherLeader = Display(current);
        if (!attempt.Acquired || !Owns(current, attempt)) { attempt.Acquired = false; return; }
        if (_fencingTokenProvider is not null)
        {
            attempt.Fence = await FencingTokenMinting.MintWithRetryAsync(_fencingTokenProvider, _resourceName, run.Stop.Token, 3).ConfigureAwait(false);
            _notifications.EnqueueDiagnostic(LogFencingTokenIssued, CandidateId, attempt.Fence.Value, _resourceName);
        }
    }

    private async Task MonitorAsync(Run run, Attempt attempt)
    {
        try
        {
            var previousStart = _timeProvider.GetTimestamp();
            while (!attempt.MonitorStop.IsCancellationRequested)
            {
                await LeaderElectionDelay.WaitFromAsync(previousStart, MonitorInterval, _timeProvider, attempt.MonitorStop.Token).ConfigureAwait(false);
                var expected = run.Kernel.Current;
                if (expected is null) { return; }
                previousStart = _timeProvider.GetTimestamp();
                try
                {
                    var current = await ReadLeaderAsync(attempt.MonitorStop.Token).ConfigureAwait(false);
                    lock (_proofGate)
                    {
                        if (!ReferenceEquals(expected, run.Kernel.Current)) { continue; }
                        // A consistent read accelerates definitive loss; it never refreshes TTL.
                        // Matching the already-published private proof is not a new holder transition.
                        if (!Owns(current, attempt))
                        {
                            run.Kernel.ReportObservation(attempt.Generation, true, Display(current), null, null, _timeProvider.GetUtcNow());
                            run.Kernel.Retire(attempt.Generation, "consistent Consul monitor no longer proves ownership");
                        }
                    }
                }
                catch (OperationCanceledException) when (attempt.MonitorStop.IsCancellationRequested) { return; }
                catch (Exception error)
                {
                    _notifications.EnqueueDiagnostic(LogErrorDuringMonitoring, error, _resourceName);
                    if (ConsulElectionFailures.IsPermanent(_failureClassifier, error))
                    {
                        lock (_proofGate) { if (ReferenceEquals(expected, run.Kernel.Current)) { run.Kernel.Retire(attempt.Generation, "permanent Consul monitoring failure", error); } }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (attempt.MonitorStop.IsCancellationRequested) { }
        catch (Exception error) { attempt.MonitorFailure = error; run.Kernel.Retire(attempt.Generation, "Consul monitor worker failed", error); }
    }

    private async Task RetireAsync(Run run, RenewalTenureDeadline.Retirement retirement)
    {
        var attempt = (Attempt)retirement.AcquisitionOwner;
        var failures = new List<Exception>();
        // The read-only lane has no authority to allocate/confirm a tenure. Its fixed latch
        // may launch before this seal, but both outcomes are joined before session cleanup.
        await CaptureAsync(() => attempt.MonitorStop.CancelAsync(), failures).ConfigureAwait(false);
        await CaptureMonitorCompletionAsync(attempt.Monitor.SealAndJoinAsync(), attempt, failures).ConfigureAwait(false);
        if (retirement.OperationCompletion is not null)
        {
            try { await retirement.OperationCompletion.ConfigureAwait(false); }
            catch (OperationCanceledException) when (run.Stop.IsCancellationRequested) { }
            catch (Exception error) { _notifications.EnqueueDiagnostic(LogErrorDuringRenewal, error, _resourceName); }
        }
        if (attempt.MonitorFailure is not null) { failures.Add(attempt.MonitorFailure); }
        if (attempt.SessionId is { } session)
        {
            await CaptureAsync(async () =>
            {
                _ = await _consulClient.KV.Release(new KVPair(GetLeaderKeyPath()) { Session = session, Value = attempt.Payload }, ConsulElectionConfiguration.WriteOptions(_options), CancellationToken.None).ConfigureAwait(false);
                _notifications.EnqueueDiagnostic(LogReleasedLeadership, _resourceName);
            }, failures).ConfigureAwait(false);
            await CaptureAsync(async () =>
            {
                await ConsulElectionSessions.DestroyAsync(_consulClient, _options, session, CancellationToken.None).ConfigureAwait(false);
                _notifications.EnqueueDiagnostic(LogDestroyedSession, session, _resourceName);
            }, failures).ConfigureAwait(false);
        }
        attempt.MonitorStop.Dispose();
        ConsulElectionFailures.Throw(failures);
    }

    private static async Task CaptureMonitorCompletionAsync(Task task, Attempt attempt, List<Exception> failures)
    {
        try { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) when (attempt.MonitorStop.IsCancellationRequested) { }
        catch (Exception error) { failures.Add(error); }
    }
    private async Task<KVPair?> ReadLeaderAsync(CancellationToken token) =>
        (await _consulClient.KV.Get(GetLeaderKeyPath(), ConsulElectionConfiguration.LeaderQueryOptions(_options), token).ConfigureAwait(false)).Response;
    private bool Owns(KVPair? pair, Attempt attempt)
    {
        if (pair?.Value is null || !string.Equals(pair.Session, attempt.SessionId, StringComparison.Ordinal)) { return false; }
        var information = Parse(pair);
        return information is not null && string.Equals(information.SessionId, attempt.SessionId, StringComparison.Ordinal) &&
            string.Equals(information.OwnerNonce, attempt.Nonce, StringComparison.Ordinal) && string.Equals(information.CandidateId, CandidateId, StringComparison.Ordinal);
    }
    private static LeaderInfo? Parse(KVPair? pair)
    {
        if (pair?.Value is null || string.IsNullOrEmpty(pair.Session)) { return null; }
        return ConsulElectionPayloads.ReadLeader(pair.Value);
    }
    private static string? Display(KVPair? pair) => Parse(pair)?.CandidateId;
    private void Notify(RenewalTenureDeadline.Transition transition)
    {
        var args = new LeaderElectionEventArgs(CandidateId, _resourceName, timestamp: transition.Timestamp);
        InvokeEach(transition.Acquired ? BecameLeader : LostLeadership, args);
        InvokeEach(LeaderChanged, new LeaderChangedEventArgs(transition.Acquired ? transition.PreviousHolder : CandidateId,
            transition.Acquired ? CandidateId : null, _resourceName, transition.Acquired ? transition.FencingToken : null, timestamp: transition.Timestamp));
    }
    private void NotifyObservation(RenewalTenureDeadline.Observation observation)
    {
        if (observation.HasHolder && !string.Equals(observation.PreviousHolder, observation.Holder, StringComparison.Ordinal))
        {
            InvokeEach(LeaderChanged, new LeaderChangedEventArgs(observation.PreviousHolder, observation.Holder, _resourceName, timestamp: observation.Timestamp));
        }
        if (observation.FailureReason is not null)
        {
            InvokeEach(AcquisitionFailed, new LeaderElectionAcquisitionFailedEventArgs(CandidateId, _resourceName, observation.FailureReason, observation.Timestamp, observation.Cause));
        }
    }
    private void InvokeEach<T>(EventHandler<T>? subscribers, T args) where T : EventArgs
    {
        _notifications.InvokeEach(subscribers, this, args);
    }

    /// <inheritdoc/>
    public async Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var lease = EnterPublicOperation();
        Task? retirement = null;
        CandidateHealth health;
        lock (_proofGate)
        {
            _localHealth = new LocalHealth(isHealthy, isHealthy ? 1 : 0, metadata is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(metadata, StringComparer.Ordinal));
            var run = CurrentRun();
            if (!CanLeadUnderProofGate() && run?.Kernel.Current is { } held)
            {
                run.Kernel.Retire(held.Generation, "local candidate unhealthy");
                retirement = run.Kernel.RetirementTask;
            }
            health = CreateHealth(_localHealth);
            _candidateHealthCache[CandidateId] = health;
        }
        await WriteHealthAsync(health, cancellationToken).ConfigureAwait(false);
        if (retirement is not null) { await retirement.WaitAsync(cancellationToken).ConfigureAwait(false); }
    }
    /// <inheritdoc/>
    public async Task<IEnumerable<CandidateHealth>> GetCandidateHealthAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var lease = EnterPublicOperation();
        try
        {
            var response = await _consulClient.KV.List(GetHealthKeyPrefix(), ConsulElectionConfiguration.HealthQueryOptions(_options), cancellationToken).ConfigureAwait(false);
            if (response.Response is null) { return _candidateHealthCache.Values.ToArray(); }
            var result = new List<CandidateHealth>();
            foreach (var entry in response.Response)
            {
                if (entry.Value is null) { continue; }
                var health = ConsulElectionPayloads.ReadHealth(entry.Value, out var error);
                if (error is not null) { _notifications.EnqueueDiagnostic(LogFailedToDeserializeHealth, error, entry.Key); continue; }
                if (health is null || string.IsNullOrEmpty(health.CandidateId)) { continue; }
                _candidateHealthCache[health.CandidateId] = health; result.Add(health);
            }
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) { _notifications.EnqueueDiagnostic(LogErrorGettingCandidateHealth, error, _resourceName); return _candidateHealthCache.Values.ToArray(); }
    }
    private bool CanLeadUnderProofGate() => !_options.StepDownWhenUnhealthy || (_localHealth.Healthy && _localHealth.Score >= _options.MinimumHealthScore);
    private CandidateHealth CreateHealth(LocalHealth local) => new()
    {
        CandidateId = CandidateId, IsHealthy = local.Healthy, HealthScore = local.Score,
        LastUpdated = _timeProvider.GetUtcNow(), IsLeader = IsLeader,
        Metadata = new Dictionary<string, string>(local.Metadata, StringComparer.Ordinal)
    };
    private async Task WriteCurrentHealthAsync(bool? isHealthy, CancellationToken token)
    {
        CandidateHealth health;
        lock (_proofGate)
        {
            var snapshot = isHealthy.HasValue ? new LocalHealth(isHealthy.Value, isHealthy.Value ? 1 : 0, _localHealth.Metadata) : _localHealth;
            health = CreateHealth(snapshot); _candidateHealthCache[CandidateId] = health;
        }
        await WriteHealthAsync(health, token).ConfigureAwait(false);
    }
    private async Task WriteHealthAsync(CandidateHealth health, CancellationToken token)
    {
        var payload = ConsulElectionPayloads.EncodeHealth(health);
        try
        {
            await _retryPolicy.ExecuteAsync(async ct => { _ = await _consulClient.KV.Put(new KVPair(GetHealthKeyPath(CandidateId)) { Value = payload }, ConsulElectionConfiguration.WriteOptions(_options), ct).ConfigureAwait(false); }, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error) { _notifications.EnqueueDiagnostic(LogFailedToUpdateHealth, error, _resourceName); }
    }
    private PublicOperation EnterPublicOperation()
    {
        lock (_runGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_publicOperations++ == 0) { _publicDrain = new(TaskCreationOptions.RunContinuationsAsynchronously); }
            return new PublicOperation(this);
        }
    }
    private void LeavePublicOperation()
    {
        lock (_runGate) { if (--_publicOperations == 0) { _publicDrain!.TrySetResult(); } }
    }
    private sealed class PublicOperation(ConsulLeaderElection owner) : IDisposable
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
            Justification = "This lease borrows the parent; disposing it only releases the public-operation count. Disposing the parent here would join this same lease and deadlock.")]
        private ConsulLeaderElection? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.LeavePublicOperation();
    }
    private sealed record LocalHealth(bool Healthy, double Score, IDictionary<string, string> Metadata);
    private sealed class Attempt
    {
        internal string Nonce { get; } = ConsulElectionPayloads.CreateOwnerNonce();
        internal string? SessionId;
        internal byte[]? Payload;
        internal long Generation;
        internal long ProofRequestStart;
        internal bool Acquired;
        internal string? OtherLeader;
        internal long? Fence;
        internal Exception? MonitorFailure;
        internal OwnedOperationLatch Acquire { get; } = new();
        internal OwnedOperationLatch Monitor { get; } = new();
        internal CancellationTokenSource MonitorStop { get; } = new();
    }
    private sealed class StartSlot(Task previousNotifications)
    {
        internal Task PreviousNotifications { get; } = previousNotifications;
        internal CancellationTokenSource Stop { get; } = new();
        internal TaskCompletionSource Initial { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Worker { get; set; } = Task.CompletedTask;
        internal Run? Run;
        internal Task? Shutdown;
        internal volatile bool StopRequested;
    }
    private sealed class Run
    {
        internal Run(ConsulLeaderElection owner, StartSlot slot)
        {
            Slot = slot;
            Kernel = new RenewalTenureDeadline(owner._timeProvider, owner._options.RenewInterval + owner._options.GracePeriod,
                retirement => owner.RetireAsync(this, retirement), owner.Notify, observe: owner.NotifyObservation, notifications: owner._notifications);
        }
        internal StartSlot Slot { get; }
        internal CancellationTokenSource Stop => Slot.Stop;
        internal bool StopRequested => Slot.StopRequested;
        internal RenewalTenureDeadline Kernel { get; }
        internal TaskCompletionSource Initial { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Actor { get; set; } = Task.CompletedTask;
        internal Exception? ActorFailure;
    }
    private static async Task CaptureAsync(Func<Task> action, List<Exception> failures)
    {
        try { await action().ConfigureAwait(false); }
        catch (Exception error) { failures.Add(error); }
    }
    private string GetLeaderKeyPath() => $"{_options.KeyPrefix}/leader/{_resourceName}";
    private string GetHealthKeyPrefix() => $"{_options.KeyPrefix}/health/{_resourceName}/";
    private string GetHealthKeyPath(string candidateId) => GetHealthKeyPrefix() + candidateId;
	// LoggerMessage delegates
	[LoggerMessage(LeaderElectionEventId.ConsulRetryAttempt, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Retry {RetryCount} after {TimeSpan}s for resource '{Resource}'")]
	partial void LogRetryAttempt(Exception exception, int retryCount, double timeSpan, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulAlreadyRunning, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Leader election for resource '{Resource}' is already running")]
	partial void LogAlreadyRunning(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulStartingElection, Microsoft.Extensions.Logging.LogLevel.Information,
		"Starting Consul leader election for resource '{Resource}' with candidate ID '{CandidateId}'")]
	partial void LogStartingElection(string resource, string candidateId);

	[LoggerMessage(LeaderElectionEventId.ConsulStoppingElection, Microsoft.Extensions.Logging.LogLevel.Information,
		"Stopping Consul leader election for resource '{Resource}'")]
	partial void LogStoppingElection(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorGettingCurrentLeader, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error getting current leader for resource '{Resource}'")]
	partial void LogErrorGettingCurrentLeader(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFailedToUpdateHealth, Microsoft.Extensions.Logging.LogLevel.Error,
		"Failed to update health status in Consul for resource '{Resource}'")]
	partial void LogFailedToUpdateHealth(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulSteppingDownUnhealthy, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Leader is unhealthy, stepping down from leadership for resource '{Resource}'")]
	partial void LogSteppingDownUnhealthy(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFailedToDeserializeHealth, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Failed to deserialize health data from key '{Key}'")]
	partial void LogFailedToDeserializeHealth(Exception ex, string key);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorGettingCandidateHealth, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error getting candidate health from Consul for resource '{Resource}'")]
	partial void LogErrorGettingCandidateHealth(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulCreatedSession, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Created Consul session '{SessionId}' for resource '{Resource}'")]
	partial void LogCreatedSession(string sessionId, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFailedToCreateSession, Microsoft.Extensions.Logging.LogLevel.Error,
		"Failed to create Consul session for resource '{Resource}'")]
	partial void LogFailedToCreateSession(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulDestroyedSession, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Destroyed Consul session '{SessionId}' for resource '{Resource}'")]
	partial void LogDestroyedSession(string sessionId, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFailedToDestroySession, Microsoft.Extensions.Logging.LogLevel.Error,
		"Failed to destroy Consul session for resource '{Resource}'")]
	partial void LogFailedToDestroySession(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulCannotAcquireWithoutSession, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Cannot acquire leadership without a valid session for resource '{Resource}'")]
	partial void LogCannotAcquireWithoutSession(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulAcquiredLeadership, Microsoft.Extensions.Logging.LogLevel.Information,
		"Acquired leadership for resource '{Resource}'")]
	partial void LogAcquiredLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFailedToAcquireLeadership, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Failed to acquire leadership for resource '{Resource}' - another leader exists")]
	partial void LogFailedToAcquireLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFencingTokenIssued, Microsoft.Extensions.Logging.LogLevel.Information,
		"Candidate {CandidateId} minted fencing token {Token} for resource {Resource}")]
	partial void LogFencingTokenIssued(string candidateId, long token, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulFencingTokenMintFailed, Microsoft.Extensions.Logging.LogLevel.Critical,
		"Candidate {CandidateId} could not mint a fencing token for resource {Resource}; releasing the lock and relinquishing leadership (fail-closed) rather than leading with an un-advanced fence")]
	partial void LogFencingTokenIssueError(Exception exception, string candidateId, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorAcquiringLeadership, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error trying to acquire leadership for resource '{Resource}'")]
	partial void LogErrorAcquiringLeadership(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulReleasedLeadership, Microsoft.Extensions.Logging.LogLevel.Information,
		"Released leadership for resource '{Resource}'")]
	partial void LogReleasedLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorReleasingLeadership, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error releasing leadership for resource '{Resource}'")]
	partial void LogErrorReleasingLeadership(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulSessionRenewalFailed, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Session renewal failed for resource '{Resource}', recreating session")]
	partial void LogSessionRenewalFailed(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulRenewedSession, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Renewed Consul session for resource '{Resource}'")]
	partial void LogRenewedSession(string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorDuringRenewal, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error during session renewal for resource '{Resource}'")]
	partial void LogErrorDuringRenewal(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulErrorDuringMonitoring, Microsoft.Extensions.Logging.LogLevel.Error,
		"Error during leadership monitoring for resource '{Resource}'")]
	partial void LogErrorDuringMonitoring(Exception ex, string resource);

	[LoggerMessage(LeaderElectionEventId.ConsulGracePeriodElapsed, Microsoft.Extensions.Logging.LogLevel.Warning,
		"Relinquishing leadership for resource '{Resource}': {Elapsed} elapsed since the last confirmed " +
		"renewal, exceeding the configured grace period of {GracePeriod}")]
	partial void LogGracePeriodElapsed(string resource, TimeSpan elapsed, TimeSpan gracePeriod);



	internal sealed class LeaderInfo
	{
		/// <summary>
		/// Gets or sets the unique identifier of the candidate that holds leadership.
		/// </summary>
		/// <value> The candidate identifier. </value>
		public string CandidateId { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the Consul session identifier associated with the leader lock.
		/// </summary>
		/// <value> The session identifier. </value>
		public string SessionId { get; set; } = string.Empty;

        /// <summary>Gets or sets the private per-tenure payload proof.</summary>
        public string OwnerNonce { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the timestamp when leadership was acquired.
		/// </summary>
		/// <value> The leadership acquisition timestamp. </value>
		public DateTimeOffset AcquiredAt { get; set; }

		/// <summary>
		/// Gets or sets additional metadata associated with the leader.
		/// </summary>
		/// <value> The leader metadata dictionary. </value>
		public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
	}
}
