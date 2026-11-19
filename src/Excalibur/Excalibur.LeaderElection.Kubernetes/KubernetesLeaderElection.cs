// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;
using k8s;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>Lease API election with independent monotonic local authority and joined native operations.</summary>
/// <remarks>LeaseTransitions is scoped to a retained Lease UID. Holder-authored takeover timestamps do not prove arbitrary cross-clock safety.</remarks>
public sealed partial class KubernetesLeaderElection : IHealthBasedLeaderElection, IDisposable, IAsyncDisposable
{
    private readonly Lock _runGate = new();
    private readonly TimeProvider _timeProvider;
    private readonly KubernetesLeaderElectionOptions _options;
    private readonly ILogger<KubernetesLeaderElection> _logger;
    private readonly string _resourceName;
    private readonly string _leaseName;
    private readonly string _namespace;
    private readonly LeaderElectionNotifications _notifications;
    private readonly LeaderElectionProducerLifetime _producers = new();
#pragma warning disable CA2213 // DisposeAsync memoizes DisposeResourcesAsync; it cancels and joins all actual producers/public SDK work before disposing this source. Synchronous Dispose joins that same resource task.
    private readonly CancellationTokenSource _lifetimeStop = new();
#pragma warning restore CA2213
    private readonly HashSet<PublicWork> _publicWork = [];
    private readonly KubernetesLeaseProtocol _protocol;
    private StartSlot? _slot;
    private volatile bool _disposed;
    private Task? _disposal;

    /// <summary>Constructs an election borrowing the supplied Kubernetes client.</summary>
    /// <param name="kubernetesClient">Borrowed SDK client; never disposed or reconfigured.</param>
    /// <param name="resourceName">Display resource.</param>
    /// <param name="options">Election configuration copied at construction.</param>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="timeProvider">Monotonic clock and deterministic timers.</param>
    public KubernetesLeaderElection(IKubernetes kubernetesClient, string resourceName,
        IOptions<KubernetesLeaderElectionOptions>? options, ILogger<KubernetesLeaderElection>? logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(kubernetesClient);
        _resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
        _options = KubernetesElectionConfiguration.Snapshot(options?.Value ?? throw new ArgumentNullException(nameof(options)));
        _logger = logger ?? NullLogger<KubernetesLeaderElection>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _leaseName = _options.LeaseName ?? (resourceName + "-leader-election");
        _notifications = new LeaderElectionNotifications(error => LogRenewalError(error, _leaseName));
        _namespace = KubernetesElectionConfiguration.DetermineNamespace(_options.Namespace,
            (error, path) => _notifications.EnqueueDiagnostic(LogNamespaceReadFailed, error, path));
        CandidateId = LeaderCandidateIdentity.Resolve(_options.InstanceId);
        var retry = KubernetesLeaderElectionRetry.Create(_options, _timeProvider,
            (error, attempt, delay) => _notifications.EnqueueDiagnostic(LogRetryWarning, error, attempt, delay, _leaseName));
        _protocol = new KubernetesLeaseProtocol(kubernetesClient, _options, _leaseName, _namespace, CandidateId, _resourceName, _timeProvider, retry);
        _notifications.EnqueueDiagnostic(LogInitialized, _resourceName, _leaseName, _namespace);
    }

#pragma warning disable CA2000 // TryEnter preregisters the lease before scheduling. The scheduled delegate owns it lexically with using through actual action completion; a synchronous scheduling failure disposes it in catch.
    private Task RunProducerAsync(Func<Task> action)
    {
        ObjectDisposedException.ThrowIf(!_producers.TryEnter(out var producer), this);
        try { return LeaderElectionExecution.RunAsync(async () => { using (producer) { await action().ConfigureAwait(false); } }); }
        catch { producer.Dispose(); throw; }
    }
#pragma warning restore CA2000

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
        var callerStart = _timeProvider.GetTimestamp();
        KubernetesNativeOperation.ThrowIfActive(this);
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
                _notifications.EnqueueDiagnostic(LogStarting, _resourceName, CandidateId);
                slot.Worker = RunProducerAsync(() => AdmitRunAsync(slot));
            }
        }
        try { await KubernetesCallerWait.WaitAsync(slot.Initial.Task, callerStart, CallerBudget, _timeProvider, cancellationToken).ConfigureAwait(false); }
        catch (TimeoutException)
        {
            _ = BeginStop(slot); // Actual operation and shutdown remain owned after this wait.
            throw;
        }
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
                run.Actor = RunProducerAsync(() => RunActorAsync(run));
            }
            await run.Initial.Task.ConfigureAwait(false);
            slot.Initial.TrySetResult();
        }
        catch (OperationCanceledException) when (slot.StopRequested)
        {
            slot.Initial.TrySetCanceled(slot.Stop.Token);
        }
        catch (TimeoutException failure)
        {
            slot.Initial.TrySetException(failure); // Logical start failure is not physical cleanup failure.
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
        var callerStart = _timeProvider.GetTimestamp();
        KubernetesNativeOperation.ThrowIfActive(this);
        StartSlot? slot;
        lock (_runGate) { ObjectDisposedException.ThrowIf(_disposed, this); slot = _slot; }
        return slot is null ? Task.CompletedTask : KubernetesCallerWait.WaitAsync(
            BeginStop(slot), callerStart, CallerBudget, _timeProvider, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        KubernetesNativeOperation.ThrowIfActive(this);
        lock (_runGate)
        {
            if (_disposal is not null) { return new ValueTask(_disposal); }
            _disposed = true;
            var publicWork = _publicWork.ToArray();
            if (_slot is { } slot) { _ = BeginStop(slot); }
            foreach (var work in publicWork) { work.Latch.SealAndJoinAsync(); }
            return new ValueTask(_disposal = LeaderElectionExecution.RunAsync(() => DisposeResourcesAsync(publicWork)));
        }
    }

    private async Task DisposeResourcesAsync(PublicWork[] publicWork)
    {
        var errors = new List<Exception>();
        var producers = _producers.Seal();
        try
        {
            try { await _lifetimeStop.CancelAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
            StartSlot? slot; lock (_runGate) { slot = _slot; }
            if (slot is not null) { try { await BeginStop(slot).ConfigureAwait(false); } catch (Exception error) { errors.Add(error); } }
            try { await producers.ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
            foreach (var work in publicWork)
            {
                try { await work.Completion.Task.ConfigureAwait(false); }
                catch (OperationCanceledException error) when (work.CancelledBeforeLaunch || (work.Token.IsCancellationRequested && error.CancellationToken == work.Token)) { }
                catch (Exception error) { errors.Add(error); }
            }
            try { _lifetimeStop.Dispose(); } catch (Exception error) { errors.Add(error); }
        }
        finally { _notifications.Seal(); }
        KubernetesElectionFailures.Throw(errors, "Kubernetes owned shutdown failed.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
#pragma warning disable RS0030 // IDisposable must join the same memoized RESOURCE cleanup and observe faults; notification delivery is a separate lane.
        DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore RS0030
    }

    // Kubernetes-local caller policy, not a network-completion or lease-safety theorem.
    private TimeSpan CallerBudget => _options.RenewInterval + _options.GracePeriod;

    private void AdmitOwnershipPhase(Run run, Attempt attempt, long requestStart, bool renewal)
    {
        lock (_runGate)
        {
            if (run.StopRequested || !ReferenceEquals(_slot, run.Slot) || attempt.Retiring)
            { throw new OperationCanceledException(attempt.Token); }
            if (_timeProvider.GetElapsedTime(requestStart) >= CallerBudget)
            { throw new TimeoutException("Kubernetes logical ownership attempt exceeded its total caller budget."); }
            if (renewal && !ReferenceEquals(run.Kernel.Current?.OwnershipProof, attempt))
            { throw new OperationCanceledException(attempt.Token); }
            // Phase admission is the boundary. Its SDK prefix runs outside this gate,
            // may physically start late, and remains covered by the actual operation owner.
        }
    }

    private Run? CurrentRun() { lock (_runGate) { return _slot?.Run; } }

    private Task BeginStop(StartSlot slot)
    {
        lock (_runGate)
        {
            if (slot.Shutdown is not null) { return slot.Shutdown; }
            slot.StopRequested = true;
            _notifications.EnqueueDiagnostic(LogStopping, _resourceName);
            // No new run can be installed after this seal: admission checks under this gate.
            // The common kernel never invokes external user code inline under its gate.
            slot.Run?.Kernel.RequestStop();
            var publicWork = _publicWork.ToArray();
            foreach (var work in publicWork) { work.Latch.SealAndJoinAsync(); }
            return slot.Shutdown = LeaderElectionExecution.RunAsync(() => StopSlotAsync(slot, publicWork));
        }
    }

    private async Task StopSlotAsync(StartSlot slot, PublicWork[] publicWork)
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
        foreach (var work in publicWork)
        {
            try { await work.CancelAsync().ConfigureAwait(false); } catch (Exception error) { failures.Add(error); }
            try { await work.Completion.Task.ConfigureAwait(false); }
            catch (OperationCanceledException error) when (work.CancelledBeforeLaunch || (work.Token.IsCancellationRequested && error.CancellationToken == work.Token)) { }
            catch (Exception error) { failures.Add(error); }
        }
        slot.Stop.Dispose();
        KubernetesElectionFailures.Throw(failures, "Kubernetes run shutdown failed.");
    }


    private async Task RunActorAsync(Run run)
    {
        try
        {
            await run.Kernel.WaitForNotificationDrainAsync(run.Stop.Token).ConfigureAwait(false);
            while (!run.StopRequested && !run.Stop.IsCancellationRequested)
            {
                var attempt = new Attempt(Guid.NewGuid().ToString("N"), run.Stop.Token);
                long generation;
                try { generation = run.Kernel.BeginAcquisition(attempt, attempt.Acquire); }
                catch { attempt.Stop.Dispose(); if (run.StopRequested) { break; } throw; }
                var start = _timeProvider.GetTimestamp();
                KubernetesLeaseProtocol.Result? result = null;
                // Deliberately discarded. A refusal means a stop already sealed the latch, and on this
                // provider both downstream effects are already suppressed by the kernel --
                // ReportObservation returns on _stopped and Retire is a no-op once the phase is
                // Retiring -- so a guard arm here would change nothing a consumer or operator sees.
                _ = attempt.Acquire.TryLaunch(async () => { result = await KubernetesNativeOperation.RunAsync(this,
                    ct => _protocol.AcquireAsync(attempt.Native, ct), attempt.Token, error => attempt.SdkCleanupFailure = error,
                    () => AdmitOwnershipPhase(run, attempt, start, false)).ConfigureAwait(false); });
                try
                {
                    await KubernetesCallerWait.WaitAsync(attempt.Acquire.Completion, start, CallerBudget, _timeProvider, attempt.Token).ConfigureAwait(false);
                    if (result is { Confirmed: true, Proof: { } proof } && !run.Stop.IsCancellationRequested)
                    {
                        attempt.Proof = proof;
                        if (!run.Kernel.Publish(generation, start, attempt, proof.Token, _timeProvider.GetUtcNow()))
                        {
                            run.Kernel.ReportObservation(generation, false, null, "acquisition proof expired before publication", null, _timeProvider.GetUtcNow());
                        }
                    }
                    else
                    {
                        var reason = FenceFailureReason(result?.Failure ?? KubernetesLeaseProtocol.FenceFailure.None) ?? "lost the acquisition race";
                        run.Kernel.ReportObservation(generation, true, result?.OtherHolder, reason, null, _timeProvider.GetUtcNow());
                        run.Kernel.Retire(generation, "acquisition race lost");
                    }
                }
                catch (OperationCanceledException error) when (attempt.Token.IsCancellationRequested && error.CancellationToken == attempt.Token)
                { run.Initial.TrySetCanceled(attempt.Token); run.Kernel.Retire(generation, "stopped"); }
                catch (TimeoutException error)
                {
                    run.Initial.TrySetException(error); // Must precede finally's generic TrySetResult.
                    run.Kernel.Retire(generation, "logical acquisition timed out", error);
                    _ = BeginStop(run.Slot); // A failed initial start cannot autonomously reacquire.
                }
                catch (Exception error)
                {
                    run.Kernel.Retire(generation, "acquisition failed", error);
                    run.Kernel.ReportObservation(generation, false, null, "error during acquisition", error, _timeProvider.GetUtcNow());
                    _notifications.EnqueueDiagnostic(LogElectionLoopError, error, _leaseName);
                }
                finally { run.Initial.TrySetResult(); }
                var cadenceStart = start;
                var delay = _options.RenewInterval;
                while (!run.Stop.IsCancellationRequested && run.Kernel.Current is not null)
                {
                    await LeaderElectionDelay.WaitFromAsync(cadenceStart, delay, _timeProvider, run.Stop.Token).ConfigureAwait(false);
                    var operation = new OwnedOperationLatch();
                    if (!run.Kernel.TryRegisterOperation(generation, operation)) { break; }
                    var proofStart = run.Kernel.Current?.ProofRequestStart;
                    if (proofStart is null) { await operation.SealAndJoinAsync().ConfigureAwait(false); break; }
                    var renewalStart = _timeProvider.GetTimestamp(); cadenceStart = renewalStart;
                    KubernetesLeaseProtocol.Result? renewal = null;
                    // Deliberately discarded: see the acquisition site above.
                    _ = operation.TryLaunch(async () => { renewal = await KubernetesNativeOperation.RunAsync(this,
                        ct => _protocol.RenewAsync(attempt.Proof!, ct), attempt.Token, error => attempt.SdkCleanupFailure = error,
                        () => AdmitOwnershipPhase(run, attempt, renewalStart, true)).ConfigureAwait(false); });
                    try
                    {
                        await KubernetesCallerWait.WaitAsync(operation.Completion, proofStart.Value, CallerBudget, _timeProvider, attempt.Token).ConfigureAwait(false);
                        if (renewal is { Confirmed: true }) { run.Kernel.Confirm(generation, renewalStart); delay = _options.RenewInterval; }
                        else if (renewal is { Lost: true })
                        {
                            if (FenceFailureReason(renewal.Failure) is { } failure) { run.Kernel.ReportObservation(generation, false, null, failure, null, _timeProvider.GetUtcNow()); }
                            run.Kernel.Retire(generation, "native UID/private owner proof changed");
                            run.Kernel.ReportObservation(generation, true, renewal.OtherHolder, null, null, _timeProvider.GetUtcNow());
                        }
                        else { delay = _options.RetryInterval; }
                    }
                    catch (OperationCanceledException error) when (attempt.Token.IsCancellationRequested && error.CancellationToken == attempt.Token) { break; }
                    catch (TimeoutException error)
                    {
                        run.Kernel.Retire(generation, "logical renewal timed out", error);
                        _notifications.EnqueueDiagnostic(LogRenewalError, error, _leaseName);
                    }
                    catch (Exception error)
                    {
                        _notifications.EnqueueDiagnostic(LogRenewalError, error, _leaseName);
                        if (attempt.SdkCleanupFailure is not null) { run.Kernel.Retire(generation, "owned SDK cleanup failed", error); }
                        delay = _options.RetryInterval; // Ambiguity never refreshes the original proof.
                    }
                }
                run.Kernel.Retire(generation, run.Stop.IsCancellationRequested ? "stopped" : "tenure ended");
                await run.Kernel.RetirementTask.ConfigureAwait(false);
                run.Kernel.CompleteRetirement(generation);
                await run.Kernel.WaitForNotificationDrainAsync(run.Stop.Token).ConfigureAwait(false);
                await LeaderElectionDelay.WaitFromAsync(cadenceStart, _options.RetryInterval, _timeProvider, run.Stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException error) when (run.Stop.IsCancellationRequested && error.CancellationToken == run.Stop.Token) { }
        catch (Exception error)
        {
            run.ActorFailure = error; run.Initial.TrySetException(error); _ = BeginStop(run.Slot);
        }
        finally { run.Initial.TrySetResult(); }
    }

    private async Task RetireAsync(Run run, RenewalTenureDeadline.Retirement retirement)
    {
        var attempt = (Attempt)retirement.AcquisitionOwner;
        PublicWork[] health;
        lock (_runGate)
        {
            attempt.Retiring = true;
            health = _publicWork.Where(work => ReferenceEquals(work.Attempt, attempt)).ToArray();
            foreach (var work in health) { if (!work.PermitRetiring) { work.Latch.SealAndJoinAsync(); } }
        }
        var errors = new List<Exception>();
        try { await attempt.Stop.CancelAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
        if (retirement.OperationCompletion is { } completion)
        {
            try { await completion.ConfigureAwait(false); }
            catch (OperationCanceledException error) when (attempt.Token.IsCancellationRequested && error.CancellationToken == attempt.Token) { }
            catch (Exception error) { _notifications.EnqueueDiagnostic(LogRenewalError, error, _leaseName); }
        }
        if (attempt.SdkCleanupFailure is not null) { errors.Add(attempt.SdkCleanupFailure); }
        foreach (var work in health)
        {
            try { await work.Completion.Task.ConfigureAwait(false); }
            catch (OperationCanceledException error) when (work.Token.IsCancellationRequested && error.CancellationToken == work.Token) { }
            catch (Exception error) { errors.Add(error); }
        }
        // Prepared proof also covers an ambiguous write that committed before its response failed.
        if (attempt.Native.Prepared is { } proof)
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await KubernetesNativeOperation.RunAsync(this, async ct =>
                { await _protocol.ReleaseAsync(proof, ct).ConfigureAwait(false); return true; }, cleanup.Token).ConfigureAwait(false);
            }
            catch (Exception error) { errors.Add(error); }
        }
        try { attempt.Stop.Dispose(); } catch (Exception error) { errors.Add(error); }
        KubernetesElectionFailures.Throw(errors, "Kubernetes owned shutdown failed.");
    }

    private sealed class Attempt
    {
        internal Attempt(string nonce, CancellationToken stop)
        { Native = new(nonce); Stop = CancellationTokenSource.CreateLinkedTokenSource(stop); Token = Stop.Token; }
        internal KubernetesLeaseProtocol.Acquisition Native { get; }
        internal OwnedOperationLatch Acquire { get; } = new();
        internal CancellationTokenSource Stop { get; }
        internal CancellationToken Token { get; }
        internal KubernetesLeaseProtocol.Owner? Proof { get; set; }
        internal bool Retiring;
        internal Exception? SdkCleanupFailure { get; set; }
    }

    private void Notify(RenewalTenureDeadline.Transition transition)
    {
        if (transition.Acquired) { _notifications.EnqueueDiagnostic(LogAcquiredLeadership, _resourceName); }
        else { _notifications.EnqueueDiagnostic(LogLostLeadership, _resourceName); }
        var args = new LeaderElectionEventArgs(CandidateId, _resourceName, timestamp: transition.Timestamp);
        InvokeEach(transition.Acquired ? BecameLeader : LostLeadership, args);
        InvokeEach(LeaderChanged, new LeaderChangedEventArgs(transition.Acquired ? transition.PreviousHolder : CandidateId,
            transition.Acquired ? CandidateId : null, _resourceName,
            fencingToken: transition.Acquired ? transition.FencingToken : null, timestamp: transition.Timestamp));
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
                _resourceName, timestamp: observation.Timestamp));
        }
        if (observation.FailureReason is not null)
        {
            InvokeEach(AcquisitionFailed, new LeaderElectionAcquisitionFailedEventArgs(CandidateId, _resourceName,
                observation.FailureReason, observation.Timestamp, observation.Cause));
        }
    }


    /// <inheritdoc/>
    public async Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var annotation = KubernetesLeaseHealthCodec.SerializeHealth(CandidateId, isHealthy, _timeProvider.GetUtcNow(), metadata);
        var work = await AdmitPublicAsync(true, !isHealthy && _options.StepDownWhenUnhealthy, cancellationToken).ConfigureAwait(false);
        if (work is null) { return; }
        work.Launch(async () =>
        {
            var matched = await KubernetesNativeOperation.RunAsync(this,
                ct => _protocol.WriteHealthAsync(work.Attempt!.Proof!, annotation, ct), work.Token).ConfigureAwait(false);
            if (!matched) { work.Run!.Kernel.Retire(work.Generation, "health read observed a different native owner"); }
        });
        await work.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<CandidateHealth>> GetCandidateHealthAsync(CancellationToken cancellationToken)
    {
        var work = await AdmitPublicAsync(false, false, cancellationToken).ConfigureAwait(false);
        if (work is null) { return []; }
        IReadOnlyList<CandidateHealth> result = [];
        work.Launch(async () =>
        {
            try
            {
                var lease = await KubernetesNativeOperation.RunAsync(this, _protocol.ReadAsync, work.Token).ConfigureAwait(false);
                result = KubernetesLeaseHealthCodec.ReadCandidates(lease,
                    (error, key) => _notifications.EnqueueDiagnostic(LogHealthAnnotationParseFailed, error, key));
            }
            catch (OperationCanceledException error) when (work.Token.IsCancellationRequested && error.CancellationToken == work.Token) { throw; }
            catch (Exception error) { _notifications.EnqueueDiagnostic(LogGetHealthFailed, error, _leaseName); }
        });
        await work.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<PublicWork?> AdmitPublicAsync(bool mutation, bool unhealthy, CancellationToken caller)
    {
        while (true)
        {
            Task? wait; PublicWork? work = null;
            lock (_runGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                wait = _slot?.Shutdown is { IsCompletedSuccessfully: false } stopping ? stopping : null;
                if (wait is null)
                {
                    var run = _slot is { Shutdown: null } ? _slot.Run : null; var current = run?.Kernel.Current;
                    var attempt = current?.OwnershipProof as Attempt;
                    if (mutation && (attempt is null || attempt.Retiring)) { return null; }
                    var parent = mutation && !unhealthy ? attempt!.Token : run?.Stop.Token ?? _lifetimeStop.Token;
                    work = new PublicWork(this, run, mutation ? attempt : null, current?.Generation ?? 0, caller, parent);
                    work.PermitRetiring = unhealthy;
                    _publicWork.Add(work);
                    work.Worker = RunProducerAsync(() => ExecutePublicAsync(work));
                    if (unhealthy) { _notifications.EnqueueDiagnostic(LogSteppingDownUnhealthy, _resourceName); run!.Kernel.Retire(current!.Generation, "unhealthy candidate"); }
                }
            }
            if (work is not null) { return work; }
            // Stop joins admitted SDK callbacks. That same SDK context cannot join Stop in return.
            // Check outside the state gate and only on this pending-shutdown wait path.
            KubernetesNativeOperation.ThrowIfActive(this);
            await wait!.WaitAsync(caller).ConfigureAwait(false);
        }
    }

    private async Task ExecutePublicAsync(PublicWork work)
    {
        Exception? failure = null;
        try
        {
            await work.Ready.Task.ConfigureAwait(false);
            work.Token.ThrowIfCancellationRequested();
            if (!work.Latch.TryLaunch(work.Operation!)) { work.CancelledBeforeLaunch = true; throw new OperationCanceledException(work.Token); }
            await work.Latch.Completion.ConfigureAwait(false);
        }
        catch (Exception error) { failure = error; }
        try { await work.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { failure = KubernetesElectionFailures.Combine(failure, error); }
        lock (_runGate)
        {
            _publicWork.Remove(work);
            if (failure is OperationCanceledException canceled && canceled.CancellationToken == work.Token) { work.Completion.TrySetCanceled(canceled.CancellationToken); }
            else if (failure is not null) { work.Completion.TrySetException(failure); }
            else { work.Completion.TrySetResult(); }
        }
    }

    private sealed class PublicWork : IAsyncDisposable
    {
        internal PublicWork(KubernetesLeaderElection owner, Run? run, Attempt? attempt, long generation, CancellationToken caller, CancellationToken parent)
        {
            Run = run; Attempt = attempt; Generation = generation;
            Source = CancellationTokenSource.CreateLinkedTokenSource(caller, parent, owner._lifetimeStop.Token); Token = Source.Token;
        }
        internal Run? Run { get; }
        internal Attempt? Attempt { get; }
        internal long Generation { get; }
        private CancellationTokenSource Source { get; }
        internal CancellationToken Token { get; }
        internal bool PermitRetiring;
        internal bool CancelledBeforeLaunch;
        private readonly Lock _sourceGate = new();
        private bool _finishing;
        private Task? _cancellation;
        internal OwnedOperationLatch Latch { get; } = new();
        internal TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Func<Task>? Operation { get; private set; }
        internal Task Worker { get; set; } = Task.CompletedTask;
        internal void Launch(Func<Task> operation) { Operation = operation; Ready.TrySetResult(); }
        internal Task CancelAsync()
        {
            lock (_sourceGate)
            {
                if (_finishing) { return Task.CompletedTask; }
                return _cancellation ??= Source.CancelAsync();
            }
        }
        public async ValueTask DisposeAsync()
        {
            Task? cancellation;
            lock (_sourceGate) { _finishing = true; cancellation = _cancellation; }
            try { if (cancellation is not null) { await cancellation.ConfigureAwait(false); } }
            finally { Source.Dispose(); }
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
        internal Run(KubernetesLeaderElection owner, StartSlot slot)
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

    private string? FenceFailureReason(KubernetesLeaseProtocol.FenceFailure failure)
    {
        if (failure == KubernetesLeaseProtocol.FenceFailure.Exhausted)
        { _notifications.EnqueueDiagnostic(LogFencingTokenExhausted, CandidateId, _leaseName); return "fencing token (leaseTransitions) domain exhausted"; }
        if (failure == KubernetesLeaseProtocol.FenceFailure.Unset)
        { _notifications.EnqueueDiagnostic(LogUnusableFencingToken, CandidateId, _leaseName); return "no usable fencing token"; }
        return null;
    }




	// LoggerMessage delegates
	[LoggerMessage(LeaderElectionEventId.KubernetesRetryWarning, LogLevel.Warning,
		"Retry {RetryCount} after {TimeSpan}ms for Kubernetes operation on lease '{LeaseName}'")]
	partial void LogRetryWarning(Exception exception, int retryCount, double timeSpan, string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesInitialized, LogLevel.Information,
		"Initialized Kubernetes leader election for resource '{Resource}' with lease '{LeaseName}' in namespace '{Namespace}'")]
	partial void LogInitialized(string resource, string leaseName, string @namespace);

	[LoggerMessage(LeaderElectionEventId.KubernetesAlreadyRunning, LogLevel.Warning,
		"Leader election for resource '{Resource}' is already running")]
	partial void LogAlreadyRunning(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesStarting, LogLevel.Information,
		"Starting leader election for resource '{Resource}' with candidate ID '{CandidateId}'")]
	partial void LogStarting(string resource, string candidateId);

	[LoggerMessage(LeaderElectionEventId.KubernetesStopping, LogLevel.Information, "Stopping leader election for resource '{Resource}'")]
	partial void LogStopping(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesStoppedNotLeader, LogLevel.Warning,
		"Leader election for resource '{Resource}' stopped but candidate '{CandidateId}' was not the leader")]
	partial void LogStoppedNotLeader(string resource, string candidateId);

	[LoggerMessage(LeaderElectionEventId.KubernetesSteppingDownUnhealthy, LogLevel.Warning,
		"Leader is unhealthy, stepping down from leadership for resource '{Resource}'")]
	partial void LogSteppingDownUnhealthy(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesHealthAnnotationParseFailed, LogLevel.Warning,
		"Failed to parse health annotation: {Annotation}")]
	partial void LogHealthAnnotationParseFailed(Exception ex, string annotation);

	[LoggerMessage(LeaderElectionEventId.KubernetesGetHealthFailed, LogLevel.Error,
		"Failed to get candidate health for lease '{LeaseName}'")]
	partial void LogGetHealthFailed(Exception ex, string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesNamespaceReadFailed, LogLevel.Warning, "Failed to read namespace from {File}")]
	partial void LogNamespaceReadFailed(Exception ex, string file);

	[LoggerMessage(LeaderElectionEventId.KubernetesLeaseExists, LogLevel.Debug,
		"Lease '{LeaseName}' already exists in namespace '{Namespace}'")]
	partial void LogLeaseExists(string leaseName, string @namespace);

	[LoggerMessage(LeaderElectionEventId.KubernetesCreatingLease, LogLevel.Information,
		"Creating lease '{LeaseName}' in namespace '{Namespace}'")]
	partial void LogCreatingLease(string leaseName, string @namespace);

	[LoggerMessage(LeaderElectionEventId.KubernetesElectionLoopError, LogLevel.Error, "Error in election loop for lease '{LeaseName}'")]
	partial void LogElectionLoopError(Exception ex, string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesAttemptingAcquire, LogLevel.Information,
		"Lease '{LeaseName}' has no holder, attempting to acquire")]
	partial void LogAttemptingAcquire(string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesRenewingLease, LogLevel.Debug, "Renewing lease '{LeaseName}' as current holder")]
	partial void LogRenewingLease(string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesLeaseExpired, LogLevel.Information,
		"Lease '{LeaseName}' held by '{Holder}' has expired (last renewed: {RenewTime}, expiry: {Expiry})")]
	partial void LogLeaseExpired(string leaseName, string? holder, DateTime? renewTime, DateTime? expiry);

	[LoggerMessage(LeaderElectionEventId.KubernetesRenewedLease, LogLevel.Information,
		"Successfully renewed lease '{LeaseName}' as leader of '{Resource}'")]
	partial void LogRenewedLease(string leaseName, string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesAcquiredLeadership, LogLevel.Information,
		"Acquired leadership for resource '{Resource}'")]
	partial void LogAcquiredLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesLostRace, LogLevel.Debug, "Lost race to acquire lease '{LeaseName}'")]
	partial void LogLostRace(string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesFencingTokenExhausted, LogLevel.Critical,
		"Candidate {CandidateId} cannot take leadership of lease {LeaseName} â€” leaseTransitions (int32 fencing token) is exhausted; relinquishing (fail-closed) rather than wrapping to a non-monotonic token")]
	partial void LogFencingTokenExhausted(string candidateId, string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesLostLeadership, LogLevel.Warning, "Lost leadership for resource '{Resource}'")]
	partial void LogLostLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesLeaderChanged, LogLevel.Information,
		"Leader changed from '{Previous}' to '{Current}' for resource '{Resource}'")]
	partial void LogLeaderChanged(string? previous, string? current, string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesRenewalFailed, LogLevel.Information,
		"Failed to renew lease '{LeaseName}', will retry in election loop")]
	partial void LogRenewalFailed(string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesRenewalError, LogLevel.Error, "Error renewing leadership for lease '{LeaseName}'")]
	partial void LogRenewalError(Exception ex, string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesReleasingLeadership, LogLevel.Information,
		"Releasing leadership for resource '{Resource}'")]
	partial void LogReleasingLeadership(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesUnusableFencingToken, LogLevel.Critical,
		"Candidate {CandidateId} holds lease {LeaseName} but it records no leadership transition, so no strictly-positive fencing token exists; giving the lease back and relinquishing (fail-closed) rather than leading a tenure whose every fenced write would be refused")]
	partial void LogUnusableFencingToken(string candidateId, string leaseName);

	[LoggerMessage(LeaderElectionEventId.KubernetesReleaseError, LogLevel.Error, "Failed to release leadership for lease '{LeaseName}'")]
	partial void LogReleaseError(Exception ex, string leaseName);
}
