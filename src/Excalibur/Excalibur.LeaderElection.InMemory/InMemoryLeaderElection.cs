// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;

using Excalibur.LeaderElection.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.InMemory;

/// <summary>
/// In-memory implementation of leader election for single-process scenarios.
/// </summary>
public sealed partial class InMemoryLeaderElection : IHealthBasedLeaderElection, IDisposable, IAsyncDisposable
{

    private readonly Lock _lifecycleGate = new();
    private readonly LeaderElectionProducerLifetime _producers = new();
    private readonly LeaderElectionNotifications _notifications;
    private Task? _disposalTask;
    private Task? _startTask;
    private Task? _stopTask;

    /// <inheritdoc />
    public Task NotificationsCompleted => _notifications.NotificationsCompleted;

    private async Task RunProducerAsync(Func<Task> action)
    {
        if (!_producers.TryEnter(out var lease)) { throw new ObjectDisposedException(GetType().Name); }
        using (lease) { await LeaderElectionExecution.RunAsync(action).ConfigureAwait(false); }
    }

    private void QueueEvent<T>(EventHandler<T>? subscribers, T args) where T : EventArgs
    {
        if (subscribers is null) { return; }
        _ = _notifications.Enqueue(() => _notifications.InvokeEach(subscribers, this, args));
    }

    private async Task WaitForStartAsync(Task start, CancellationToken cancellationToken)
    {
        try { await start.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Retain shutdown ownership when a canceled waiter leaves admitted startup work behind.
            try { _ = StopAsync(CancellationToken.None); }
            catch (ObjectDisposedException) when (_disposed) { }
            throw;
        }
    }

    private static void ThrowFailures(List<Exception> failures)
    {
        if (failures.Count == 1) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw(); }
        if (failures.Count > 1) { throw new AggregateException("Leader election resource cleanup failed.", failures); }
    }

    private Task _acquisitionTask = Task.CompletedTask;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Owned field is disposed by memoized DisposeCoreAsync after producer seal and join.")]
    private CancellationTokenSource? _runStop;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, InMemoryLeaderElectionSharedState.TenureOwnership> _leaders;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CandidateHealth>> _candidates;
    private readonly InMemoryLeaderElectionSharedState _sharedState;
    private readonly InMemoryLeaderElectionSharedState.CandidateBucket _candidateBucket;
    private bool _candidateLeaseActive;
    private readonly string _resourceName;
    private readonly LeaderElectionOptions _options;
    private readonly ILogger<InMemoryLeaderElection> _logger;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Owned field is disposed by memoized DisposeCoreAsync after producer seal and join.")]
    private readonly ITimer _leaseRenewalTimer;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Owned field is disposed by memoized DisposeCoreAsync after producer seal and join.")]
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly IFencingTokenIssuer? _fencingTokenProvider;
    private volatile int _state; // 0 = stopped, 1 = running
    private volatile bool _disposed;
    private volatile InMemoryLeaderElectionSharedState.TenureOwnership? _publishedTenure;
    private CandidateHealth? _lastCandidateHealth;
    // UTC ticks of the instant this candidate most recently acquired leadership, accessed via
    // Interlocked lock-free.
    private long _leadershipAcquiredAtTicks;

    // Sentinel distinguishing "no fencing token provider configured" from a real token, so CurrentLeadership
    // reports null (never an in-band 0) when fencing is unavailable — an in-band 0 would read to a consumer
    // as a valid low token and could be presented to a fencing store, defeating split-brain protection.
    private const long NoFencingToken = long.MinValue;
    private long _currentFencingToken = NoFencingToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryLeaderElection" /> class.
    /// </summary>
    /// <param name="resourceName"> The name of the resource to elect a leader for. </param>
    /// <param name="options"> The leader election options. </param>
    /// <param name="logger"> Optional logger for diagnostic output. </param>
    /// <param name="sharedState"> Optional shared state for coordinating multiple instances in the same process. </param>
    /// <param name="timeProvider">
    /// Optional time provider used for event timestamps. Defaults to <see cref="TimeProvider.System"/>.
    /// Inject a controllable provider to make emitted timestamps deterministic in tests.
    /// </param>
    /// <param name="fencingTokenProvider">
    /// Optional fencing-token provider. When absent this election runs in non-fencing mode (see
    /// <see cref="TryMintFencingTokenAsync"/>) — <c>Add{Store}LeaderElection()</c> registers one by default;
    /// <c>WithoutFencingTokens()</c> opts out.
    /// </param>
    public InMemoryLeaderElection(
        string resourceName,
        IOptions<LeaderElectionOptions> options,
        ILogger<InMemoryLeaderElection>? logger,
        InMemoryLeaderElectionSharedState? sharedState = null,
        TimeProvider? timeProvider = null,
        IFencingTokenIssuer? fencingTokenProvider = null)
    {
        _resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<InMemoryLeaderElection>.Instance;
        _notifications = new LeaderElectionNotifications(error => LogRenewalError(error, _resourceName));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _fencingTokenProvider = fencingTokenProvider;
        var state = sharedState ?? InMemoryLeaderElectionSharedState.Default;
        _leaders = state.Leaders;
        _candidates = state.Candidates;
        _sharedState = state;

        CandidateId = Excalibur.Dispatch.LeaderElection.LeaderCandidateIdentity.Resolve(_options.InstanceId);

        _leaseRenewalTimer = _timeProvider.CreateTimer(RenewLeaseCallback, state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        // Stopped instances retain their lifetime pin so they can restart; disposal releases it.
        try
        {
            lock (_sharedState.CandidateGate)
            {
                if (!_sharedState.CandidateBuckets.TryGetValue(_resourceName, out var bucket))
                {
                    var members = _candidates.GetOrAdd(_resourceName,
                        static _ => new ConcurrentDictionary<string, CandidateHealth>(StringComparer.Ordinal));
                    bucket = new InMemoryLeaderElectionSharedState.CandidateBucket(members);
                    _sharedState.CandidateBuckets.Add(_resourceName, bucket);
                }
                if (_candidates.TryGetValue(_resourceName, out var published) && !ReferenceEquals(published, bucket.Members))
                {
                    throw new InvalidOperationException("Candidate bucket publication differs from its active lease.");
                }
                _candidates.TryAdd(_resourceName, bucket.Members);
                checked { bucket.Owners++; }
                _candidateBucket = bucket;
                _candidateLeaseActive = true;
            }
        }
        catch (Exception initializationFailure)
        {
            try { _leaseRenewalTimer.Dispose(); }
            catch (Exception cleanupFailure) { throw new AggregateException(initializationFailure, cleanupFailure); }
            throw;
        }
    }

    /// <inheritdoc />
    public event EventHandler<LeaderElectionEventArgs>? BecameLeader;

    /// <inheritdoc />
    public event EventHandler<LeaderElectionEventArgs>? LostLeadership;

    /// <inheritdoc />
    public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

    /// <inheritdoc />
    public event EventHandler<LeaderElectionAcquisitionFailedEventArgs>? AcquisitionFailed;

    /// <inheritdoc />
    public string CandidateId { get; }

    /// <inheritdoc />
    public bool IsLeader => !_disposed && _state == 1 && _publishedTenure is { } published && _leaders.TryGetValue(_resourceName, out var owner) && ReferenceEquals(owner, published);

    /// <inheritdoc />
    public Leadership? CurrentLeadership
    {
        get
        {
            lock (_sharedState.CandidateGate)
            {
                if (!IsLeader) { return null; }
                var token = Interlocked.Read(ref _currentFencingToken);
                var acquiredAtTicks = Interlocked.Read(ref _leadershipAcquiredAtTicks);
                return new Leadership(token == NoFencingToken ? null : token, new DateTimeOffset(acquiredAtTicks, TimeSpan.Zero));
            }
        }
    }

    /// <inheritdoc />
    public string? CurrentLeaderId => _leaders.GetValueOrDefault(_resourceName)?.CandidateId;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _notifications.ThrowIfReentrant();
        Task start;
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_state == 1) { return WaitForStartAsync(_startTask ?? Task.CompletedTask, cancellationToken); }
            if (_stopTask is { IsCompletedSuccessfully: false }) { throw new InvalidOperationException("Previous stop has not completed successfully."); }
            _state = 1;
            _runStop = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            _stopTask = null;
            _startTask = start = RunProducerAsync(() => StartCoreAsync(cancellationToken));
        }
        return WaitForStartAsync(start, cancellationToken);
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        using var linkedStart = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _runStop!.Token);
        cancellationToken = linkedStart.Token;
        await _notifications.DrainTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        var metadata = _options.CandidateMetadata ?? new Dictionary<string, string>(StringComparer.Ordinal);
        lock (_sharedState.CandidateGate)
        {
            if (_disposed || _state == 0 || !TryGetLeasedCandidates(out var members, allowRepublish: true)) { return; }
            members.TryGetValue(CandidateId, out var existing);
            members[CandidateId] = _lastCandidateHealth = new CandidateHealth
            {
                CandidateId = CandidateId,
                IsHealthy = existing?.IsHealthy ?? true,
                HealthScore = existing?.HealthScore ?? 1.0,
                LastUpdated = now,
                Metadata = metadata,
            };
        }
        await AcquireTrackedAsync().ConfigureAwait(false);
        lock (_lifecycleGate)
        {
            if (!_disposed && _state == 1) { _ = _leaseRenewalTimer.Change(_options.RenewInterval, _options.RenewInterval); }
        }
        _notifications.EnqueueDiagnostic(LogStarted, _resourceName, CandidateId);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Task stop;
        lock (_lifecycleGate)
        {
            if (_disposed) { return (_disposalTask ?? Task.CompletedTask).WaitAsync(cancellationToken); }
            if (_stopTask is not null) { return _stopTask.WaitAsync(cancellationToken); }
            if (_state == 0) { return Task.CompletedTask; }
            _state = 0;
            _stopTask = stop = RunProducerAsync(StopCoreAsync);
        }
        return stop.WaitAsync(cancellationToken);
    }

    private async Task StopCoreAsync()
    {
        var failures = new List<Exception>();
        if (_runStop is not null)
        {
            try { await _runStop.CancelAsync().ConfigureAwait(false); }
            catch (Exception error) { failures.Add(error); }
        }
        try { _ = _leaseRenewalTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan); }
        catch (Exception error) { failures.Add(error); }
        Task? start; Task acquisition;
        lock (_lifecycleGate) { start = _startTask; acquisition = _acquisitionTask; }
        if (start is not null)
        {
            try { await start.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception error) { failures.Add(error); }
        }
        try { await acquisition.ConfigureAwait(false); }
        catch (OperationCanceledException) when (_runStop?.IsCancellationRequested == true) { }
        catch (Exception error) { failures.Add(error); }
        try { StopForDisposal(releaseLeadership: true, logStopped: true); }
        catch (Exception error) { failures.Add(error); }
        finally { RemoveCandidateMembership(); }
        try { _runStop?.Dispose(); _runStop = null; }
        catch (Exception error) { failures.Add(error); }
        ThrowFailures(failures);
    }

    private Task AcquireTrackedAsync()
    {
        lock (_lifecycleGate)
        {
            if (_disposed || _state == 0) { return Task.CompletedTask; }
            if (!_acquisitionTask.IsCompleted) { return _acquisitionTask; }
            return _acquisitionTask = RunProducerAsync(TryAcquireLeadershipAsync);
        }
    }
    /// <inheritdoc />
    public Task UpdateHealthAsync(bool isHealthy, IDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        if (!_producers.TryEnter(out var operation)) { return Task.CompletedTask; }
        using var healthOperation = operation;
        if (_state == 0 || _disposed) { return Task.CompletedTask; }
        var combinedMetadata = new Dictionary<string, string>(
            _options.CandidateMetadata ?? new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);
        if (metadata is not null)
        {
            foreach (var pair in metadata) { combinedMetadata[pair.Key] = pair.Value; }
        }
        var now = _timeProvider.GetUtcNow();
        lock (_sharedState.CandidateGate)
        {
            if (_state == 0 || !TryGetLeasedCandidates(out var members)) { return Task.CompletedTask; }
            members[CandidateId] = _lastCandidateHealth = new CandidateHealth
            {
                CandidateId = CandidateId,
                IsHealthy = isHealthy,
                HealthScore = isHealthy ? 1.0 : 0.0,
                LastUpdated = now,
                Metadata = combinedMetadata,
            };
        }
        _notifications.EnqueueDiagnostic(LogHealthUpdated, CandidateId, isHealthy);
        if (!isHealthy && _options.StepDownWhenUnhealthy)
        {
            // Capture the clock outside CandidateGate; release and its owed loss publication
            // then share the state-commit gate, so a successor cannot enqueue Became first.
            var lostAt = _timeProvider.GetUtcNow();
            if (ReleaseLeadershipIfHeld(lostAt))
            {
                _notifications.EnqueueDiagnostic(LogSteppedDownUnhealthy);
            }
        }
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public async Task<IEnumerable<CandidateHealth>> GetCandidateHealthAsync(CancellationToken cancellationToken)
    {
        if (_candidates.TryGetValue(_resourceName, out var candidateDict))
        {
            return candidateDict.Values.ToList();
        }

        return await Task.FromResult(Enumerable.Empty<CandidateHealth>()).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return new(BeginDispose());
    }

    private Task BeginDispose()
    {
        lock (_lifecycleGate)
        {
            if (_disposalTask is not null) { return _disposalTask; }
            _disposed = true;
            _state = 0;
            return _disposalTask = LeaderElectionExecution.RunAsync(DisposeCoreAsync);
        }
    }

    private async Task DisposeCoreAsync()
    {
        var failures = new List<Exception>();
        var producers = _producers.Seal();
        try
        {
            try { await _cancellationTokenSource.CancelAsync().ConfigureAwait(false); }
            catch (Exception error) { failures.Add(error); }
            try { _ = _leaseRenewalTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan); }
            catch (Exception error) { failures.Add(error); }
            try { await _leaseRenewalTimer.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { failures.Add(error); }
            try { await producers.ConfigureAwait(false); }
            catch (Exception error) { failures.Add(error); }
            try { StopForDisposal(releaseLeadership: true, logStopped: true, disarmTimer: false); }
            catch (Exception error) { failures.Add(error); }
            try { _runStop?.Dispose(); _cancellationTokenSource.Dispose(); }
            catch (Exception error) { failures.Add(error); }
            try { ReleaseCandidateLease(removeMembership: true); }
            catch (Exception error) { failures.Add(error); }
        }
        finally { _notifications.Seal(); }
        ThrowFailures(failures);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
#pragma warning disable RS0030 // Synchronous disposal joins owned resource work, never the independent notification lane.
        BeginDispose().GetAwaiter().GetResult();
#pragma warning restore RS0030
    }

    private static Exception CombineFailures(Exception? earlier, Exception later) =>
        earlier is null ? later : new AggregateException(earlier, later);

    private void StopForDisposal(bool releaseLeadership, bool logStopped, bool disarmTimer = true)
    {
        // Async disposal of a stopped/never-started instance preserves StopAsync's former no-op.
        if (!releaseLeadership) { return; }
        Exception? failure = null;
        if (disarmTimer)
        {
            try { _ = _leaseRenewalTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan); }
            catch (Exception error) { failure = error; }
        }
        var wasLeader = false;
        var lostAt = _timeProvider.GetUtcNow();
        try { wasLeader = ReleaseLeadershipIfHeld(lostAt); }
        catch (Exception error) { failure = CombineFailures(failure, error); }
        if (logStopped)
        {
            try { _notifications.EnqueueDiagnostic(LogStopped, _resourceName, wasLeader); }
            catch (Exception error) { failure = CombineFailures(failure, error); }
        }
        if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }
    // Every caller holds CandidateGate. The private lease cannot mutate a detached/retired bucket.
    private bool TryGetLeasedCandidates(out ConcurrentDictionary<string, CandidateHealth> members, bool allowDisposing = false,
        bool allowRepublish = false)
    {
        members = null!;
        if (!_candidateLeaseActive || (_disposed && !allowDisposing) ||
            !_sharedState.CandidateBuckets.TryGetValue(_resourceName, out var bucket) ||
            !ReferenceEquals(bucket, _candidateBucket)) { return false; }
        if (_candidates.TryGetValue(_resourceName, out var published))
        {
            if (!ReferenceEquals(published, bucket.Members)) { return false; }
        }
        else
        {
            // Startup may restore its live bucket; health updates and disposal never republish it.
            if (!allowRepublish || !_candidates.TryAdd(_resourceName, bucket.Members)) { return false; }
        }
        members = bucket.Members;
        return true;
    }

    private void RemoveCandidateMembership()
    {
        lock (_sharedState.CandidateGate)
        {
            if (TryGetLeasedCandidates(out var members, allowDisposing: true)) { RemoveOwnedMembership(members); }
        }
    }

    // CandidateGate is held by both callers; a same-display instance owns only its last publication.
    private void RemoveOwnedMembership(ConcurrentDictionary<string, CandidateHealth> members)
    {
        if (_lastCandidateHealth is { } owned && members.TryGetValue(CandidateId, out var current)
            && ReferenceEquals(current, owned))
        {
            _ = members.TryRemove(new KeyValuePair<string, CandidateHealth>(CandidateId, owned));
        }
        _lastCandidateHealth = null;
    }

    private void ReleaseCandidateLease(bool removeMembership)
    {
        lock (_sharedState.CandidateGate)
        {
            if (!_candidateLeaseActive) { return; }
            if (removeMembership && TryGetLeasedCandidates(out var members, allowDisposing: true)) { RemoveOwnedMembership(members); }
            _candidateLeaseActive = false;
            _candidateBucket.Owners--;
            if (_candidateBucket.Owners == 0)
            {
                _sharedState.CandidateBuckets.Remove(_resourceName);
                _ = _candidates.TryRemove(new KeyValuePair<string, ConcurrentDictionary<string, CandidateHealth>>(
                    _resourceName, _candidateBucket.Members));
            }
        }
    }
    /// <summary>
    /// Relinquishes leadership of this resource if — and only if — this candidate still holds it.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if this candidate held leadership and it was released by this call;
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The check and the removal are one atomic compare-and-remove, so a candidate can never delete a
    /// leadership record belonging to a successor. Splitting them into a read followed by a remove-by-key
    /// would let a candidate that observed itself leader, and then lost the resource to a successor,
    /// delete the successor's record — leaving that successor believing it still leads while the
    /// resource is free for a third candidate to acquire. This provider has no lease expiry, so nothing
    /// downstream would ever correct that.
    /// </remarks>
    private bool ReleaseLeadershipIfHeld(DateTimeOffset lostAt)
    {
        lock (_sharedState.CandidateGate)
        {
            var published = _publishedTenure;
            if (published is null) { return false; }
            var released = _leaders.TryRemove(new KeyValuePair<string, InMemoryLeaderElectionSharedState.TenureOwnership>(_resourceName, published));
            _publishedTenure = null;
            _ = Interlocked.Exchange(ref _currentFencingToken, NoFencingToken);
            if (released)
            {
                QueueEvent(LostLeadership, new LeaderElectionEventArgs(CandidateId, _resourceName, timestamp: lostAt));
                QueueEvent(LeaderChanged, new LeaderChangedEventArgs(CandidateId, null, _resourceName, timestamp: lostAt));
            }
            return released;
        }
    }

    /// <summary>
    /// Attempts to take the resource for this candidate, and gives it straight back if this candidate
    /// is no longer running by the time it has it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The invariant: <b>a candidate holds the resource only while it is running.</b> Both callers can
    /// be racing a shutdown -- the renewal callback because a callback already dispatched to the thread
    /// pool keeps running after <c>Timer.Change(Timeout.Infinite, ...)</c>, and <c>StartAsync</c>
    /// because a host may start and stop concurrently -- so an entry check cannot establish it. The
    /// state can go stopped between the check and the <c>TryAdd</c>, and the acquisition would then
    /// place a stopped candidate on the resource: <c>StopAsync</c> has already run its release and found
    /// nothing to release, and this provider has no lease expiry, so no later event corrects it. The
    /// candidate is deregistered and every other candidate fails forever.
    /// </para>
    /// <para>
    /// The re-check therefore comes <b>after</b> the acquisition, and the interleaving is closed by the
    /// order on both sides. Shutdown writes <c>_state = 0</c> and then releases; acquisition adds and
    /// then reads <c>_state</c>. Either the add lands before the shutdown's release -- which is a
    /// compare-and-remove, so it takes this candidate's own record away -- or it lands after, in which
    /// case the shutdown's <c>_state</c> write preceded its release and so precedes the add, and the
    /// read here sees the stopped state and hands the resource back. <b>Swapping either pair reopens
    /// the window.</b> The <c>Interlocked</c> exchange on <c>_state</c> and the dictionary mutation are
    /// both full fences, so neither pair can be reordered.
    /// </para>
    /// </remarks>
    private async Task TryAcquireLeadershipAsync()
    {
        await _notifications.DrainTask.WaitAsync(_runStop?.Token ?? _cancellationTokenSource.Token).ConfigureAwait(false);
        if (_disposed || _state == 0 || IsLeader) { return; }
        var currentLeader = CurrentLeaderId;
        var reservation = new InMemoryLeaderElectionSharedState.TenureOwnership(CandidateId);
        if (!_leaders.TryAdd(_resourceName, reservation))
        {
            RaiseAcquisitionFailed("lost the acquisition race", exception: null);
            return;
        }
        var published = false;
        try
        {
            if (_disposed || _state == 0) { return; }
            var mintFailure = await TryMintFencingTokenAsync().ConfigureAwait(false);
            if (mintFailure is not null)
            {
                RaiseAcquisitionFailed("fencing token mint failed", mintFailure);
                return;
            }
            // A foreign clock can fail here after minting; the reservation still belongs to finally.
            var acquiredAt = _timeProvider.GetUtcNow();
            lock (_sharedState.CandidateGate)
            {
                if (_disposed || _state == 0 || !_leaders.TryGetValue(_resourceName, out var current)
                    || !ReferenceEquals(current, reservation)) { return; }
                Interlocked.Exchange(ref _leadershipAcquiredAtTicks, acquiredAt.UtcTicks);
                _publishedTenure = reservation;
                published = true;
                var fence = Interlocked.Read(ref _currentFencingToken);
                QueueEvent(BecameLeader, new LeaderElectionEventArgs(CandidateId, _resourceName, timestamp: acquiredAt));
                QueueEvent(LeaderChanged, new LeaderChangedEventArgs(currentLeader, CandidateId, _resourceName,
                    fence == NoFencingToken ? null : fence, timestamp: acquiredAt));
            }
            _notifications.EnqueueDiagnostic(LogAcquiredLeadership, _resourceName);
        }
        finally
        {
            if (!published)
            {
                lock (_sharedState.CandidateGate)
                {
                    _ = _leaders.TryRemove(new KeyValuePair<string, InMemoryLeaderElectionSharedState.TenureOwnership>(_resourceName, reservation));
                    if (_publishedTenure is null) { _ = Interlocked.Exchange(ref _currentFencingToken, NoFencingToken); }
                }
            }
        }
    }

    /// <summary>
    /// Mints a monotonic fencing token on acquisition (when a provider is configured), returning
    /// <see langword="true"/> if leadership may proceed. Returns <see langword="false"/> only when the token
    /// domain is exhausted (<see cref="FencingTokenExhaustedException"/>) — the fail-closed relinquish path.
    /// </summary>
    private async Task<Exception?> TryMintFencingTokenAsync()
    {
        if (_fencingTokenProvider is null)
        {
            // No provider configured => this election runs in NON-FENCING mode by design, not "minted
            // nothing, proceed anyway". A consumer who needs fencing must configure a provider (or accept
            // the default one Add{Store}LeaderElection() registers); the fenced-resource seam
            // (GuardActiveGateHasFencingToken) is what guards against a fenced store being used without
            // one, not this method.
            return null;
        }

        try
        {
            // No single cancellation token spans every caller here (StartAsync's token is not carried
            // through, and the renewal timer callback has none at all) — mirrors the rest of this
            // acquisition path, which is already synchronous/uncancellable end to end.
            var token = await _fencingTokenProvider.IssueTokenAsync(_resourceName, CancellationToken.None).ConfigureAwait(false);
            _ = Interlocked.Exchange(ref _currentFencingToken, token);
            _notifications.EnqueueDiagnostic(LogFencingTokenIssued, CandidateId, token, _resourceName);
            return null;
        }
        catch (Exception ex)
        {
            // No cancellation clause, unlike the sibling providers: this path carries no token (see the
            // comment above), so there is no shutdown signal here to distinguish from a mint failure, and
            // giving the resource back is the right answer either way.
            _notifications.EnqueueDiagnostic(LogFencingTokenMintFailed, ex, CandidateId, _resourceName);
            return ex;
        }
    }

    /// <summary>
    /// Raises <see cref="AcquisitionFailed"/>, guarding the invocation so a throwing subscriber
    /// can never break the acquisition/renewal loop.
    /// </summary>
    private void RaiseAcquisitionFailed(string reason, Exception? exception)
    {
        try
        {
            QueueEvent(AcquisitionFailed, new LeaderElectionAcquisitionFailedEventArgs(CandidateId, _resourceName, reason, _timeProvider.GetUtcNow(), exception));
        }
        catch (Exception)
        {
            // A throwing subscriber must never break the acquire loop.
        }
    }

    private void RenewLeaseCallback(object? state)
    {
        if (_state == 0 || _disposed)
        {
            return;
        }

        try
        {
            // In a real implementation, this would renew a lease in external storage. For in-memory, we just verify we're still the leader
            if (IsLeader)
            {
                _notifications.EnqueueDiagnostic(LogRenewedLease, _resourceName);
            }
            else if (!(_options.StepDownWhenUnhealthy && IsCurrentCandidateUnhealthy()))
            {
                // Try to acquire leadership if no one has it,
                // but not if we stepped down due to being unhealthy
                _ = AcquireTrackedAsync();
            }
        }
        catch (Exception ex)
        {
            _notifications.EnqueueDiagnostic(LogRenewalError, ex, _resourceName);
        }
    }

    private bool IsCurrentCandidateUnhealthy()
    {
        return _candidates.TryGetValue(_resourceName, out var candidateDict) &&
            candidateDict.TryGetValue(CandidateId, out var health) &&
            !health.IsHealthy;
    }

    // LoggerMessage delegates
    [LoggerMessage(LeaderElectionEventId.InMemoryStarted, LogLevel.Information, "Started leader election for resource '{ResourceName}' with candidate ID '{CandidateId}'")]
    partial void LogStarted(string resourceName, string candidateId);

    [LoggerMessage(LeaderElectionEventId.InMemoryStopped, LogLevel.Information, "Stopped leader election for resource '{ResourceName}', was leader: {WasLeader}")]
    partial void LogStopped(string resourceName, bool wasLeader);

    [LoggerMessage(LeaderElectionEventId.InMemoryHealthUpdated, LogLevel.Debug, "Updated health status for candidate '{CandidateId}': {IsHealthy}")]
    partial void LogHealthUpdated(string candidateId, bool isHealthy);

    [LoggerMessage(LeaderElectionEventId.InMemorySteppedDownUnhealthy, LogLevel.Warning, "Stepped down from leadership due to unhealthy status")]
    partial void LogSteppedDownUnhealthy();

    [LoggerMessage(LeaderElectionEventId.InMemoryAcquiredLeadership, LogLevel.Information, "Acquired leadership for resource '{ResourceName}'")]
    partial void LogAcquiredLeadership(string resourceName);

    [LoggerMessage(LeaderElectionEventId.InMemoryRenewedLease, LogLevel.Trace, "Renewed leadership lease for resource '{ResourceName}'")]
    partial void LogRenewedLease(string resourceName);

    [LoggerMessage(LeaderElectionEventId.InMemoryRenewalError, LogLevel.Error, "Error during lease renewal for resource '{ResourceName}'")]
    partial void LogRenewalError(Exception ex, string resourceName);

    [LoggerMessage(LeaderElectionEventId.InMemoryFencingTokenIssued, LogLevel.Information, "Fencing token {Token} issued to candidate '{CandidateId}' for resource '{ResourceName}'")]
    partial void LogFencingTokenIssued(string candidateId, long token, string resourceName);

    [LoggerMessage(LeaderElectionEventId.InMemoryFencingTokenMintFailed, LogLevel.Critical, "Could not mint a fencing token for candidate '{CandidateId}' on resource '{ResourceName}'; the resource was given back and leadership relinquished")]
    partial void LogFencingTokenMintFailed(Exception exception, string candidateId, string resourceName);

    [LoggerMessage(LeaderElectionEventId.InMemoryDisposalCleanupFailed, LogLevel.Error, "Cleanup failed while disposing leader election for resource '{ResourceName}', candidate '{CandidateId}'; disposal completed and the failure was not propagated")]
    partial void LogDisposalCleanupFailed(Exception exception, string resourceName, string candidateId);
}
