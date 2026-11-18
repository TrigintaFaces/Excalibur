// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Diagnostics;

/// <summary>Decorates leader election with metrics and tracing without owning consumer callbacks.</summary>
public sealed class TelemetryLeaderElection : ILeaderElection, IAsyncDisposable
{
    private readonly ILeaderElection _inner;
    private readonly Counter<long> _acquisitionsCounter;
    private readonly Histogram<double> _leaseDurationHistogram;
    private readonly ActivitySource _activitySource;
    private readonly string _providerName;
    private readonly TagCardinalityGuard _instanceGuard;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _leaseTimestampLock = new();
    private readonly Lock _disposalGate = new();
    private readonly LeaderElectionProducerLifetime _instrumentOperations = new();
    private readonly TaskCompletionSource _terminal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<Task> _resourcePublication = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _resourceDisposal;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "ObserveTerminalAsync disposes the registration after admitted callbacks and instrument operations drain; resource Dispose must not self-join callbacks.")]
    private readonly IDisposable _gaugeRegistration;
    private readonly LeaderElectionInstrumentation.InstrumentLease? _instrumentLease;
    private long? _leaseStartTimestamp;
    private bool _admissionClosed;
    private bool _disposeStarted;
    private Exception? _firstNotificationFailure;

    /// <summary>Initializes telemetry around an election.</summary>
    /// <param name="inner">The election whose resource disposal is owned by this decorator.</param>
    /// <param name="meter">Borrowed meter; its owner must keep it alive through terminal notification delivery.</param>
    /// <param name="activitySource">Borrowed activity source with the same lifetime requirement.</param>
    /// <param name="providerName">Provider label.</param>
    /// <param name="timeProvider">Clock used for monotonic lease-duration measurements.</param>
    public TelemetryLeaderElection(ILeaderElection inner, Meter meter, ActivitySource activitySource,
        string providerName, TimeProvider? timeProvider = null)
        : this(inner, meter, activitySource, providerName, timeProvider, null) { }

    private TelemetryLeaderElection(ILeaderElection inner, Meter meter, ActivitySource activitySource,
        string providerName, TimeProvider? timeProvider, LeaderElectionInstrumentation.InstrumentLease? instrumentLease)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ArgumentNullException.ThrowIfNull(meter);
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));
        _instanceGuard = new TagCardinalityGuard(maxCardinality: 100);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _instrumentLease = instrumentLease;
        _resourceDisposal = _resourcePublication.Task.Unwrap();
        _acquisitionsCounter = meter.CreateCounter<long>(LeaderElectionTelemetryConstants.MetricNames.Acquisitions,
            "{attempts}", "Lease acquisition attempts");
        _leaseDurationHistogram = meter.CreateHistogram<double>(LeaderElectionTelemetryConstants.MetricNames.LeaseDuration,
            "s", "Duration leader holds lease in seconds");
        _gaugeRegistration = LeaderElectionGaugeRegistry.Register(meter, ObserveLeadership, RecordNotificationFailure);
        try
        {
            _inner.BecameLeader += HandleBecameLeader;
            _inner.LostLeadership += HandleLostLeadership;
            _inner.LeaderChanged += HandleLeaderChanged;
            _inner.AcquisitionFailed += HandleAcquisitionFailed;
        }
        catch
        {
            Exception? ignored = null;
            DetachAll(ref ignored);
            _gaugeRegistration.Dispose();
            throw;
        }
        // All fields and ALL subscriptions precede observation of an already-terminal inner.
        _ = ObserveTerminalAsync();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The lease transfers to the decorator on success and to RetainFailedCreationCleanup on failure, retaining instruments until actual inner cleanup and delivery finish.")]
    internal static TelemetryLeaderElection CreateOwned(ILeaderElection inner, IMeterFactory? meterFactory,
        string providerName, TimeProvider? timeProvider = null)
    {
        var owner = LeaderElectionInstrumentation.Create(meterFactory);
        var lease = owner.Acquire();
        try { return new TelemetryLeaderElection(inner, owner.Meter, owner.Activity, providerName, timeProvider, lease); }
        catch { lease.RetainFailedCreationCleanup(inner); throw; }
        finally { owner.Dispose(); }
    }

    internal static TelemetryLeaderElection CreateLeased(ILeaderElection inner, LeaderElectionInstrumentation owner,
        LeaderElectionInstrumentation.InstrumentLease lease, string providerName, TimeProvider timeProvider) =>
        new(inner, owner.Meter, owner.Activity, providerName, timeProvider, lease);

    /// <inheritdoc/>
    public Task NotificationsCompleted => _terminal.Task;
    /// <inheritdoc/>
    public event EventHandler<LeaderElectionEventArgs>? BecameLeader;
    /// <inheritdoc/>
    public event EventHandler<LeaderElectionEventArgs>? LostLeadership;
    /// <inheritdoc/>
    public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;
    /// <inheritdoc/>
    public event EventHandler<LeaderElectionAcquisitionFailedEventArgs>? AcquisitionFailed;
    /// <inheritdoc/>
    public string CandidateId => _inner.CandidateId;
    /// <inheritdoc/>
    public bool IsLeader => _inner.IsLeader;
    /// <inheritdoc/>
    public Leadership? CurrentLeadership => _inner.CurrentLeadership;
    /// <inheritdoc/>
    public string? CurrentLeaderId => _inner.CurrentLeaderId;

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LeaderElectionNotifications.ThrowIfNotificationReentrant(NotificationsCompleted);
        await RunInstrumentedAsync(start: true, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await RunInstrumentedAsync(start: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunInstrumentedAsync(bool start, CancellationToken cancellationToken)
    {
        using var operation = AdmitInstrumentOperation();
        Activity? activity = null;
        var previous = Activity.Current;
        Exception? coreFailure = null;
        try
        {
            try
            {
                activity = _activitySource.StartActivity(start ? "leader_election.start" : "leader_election.stop");
                activity?.SetTag(LeaderElectionTelemetryConstants.TagNames.Instance, _instanceGuard.Guard(_inner.CandidateId));
                activity?.SetTag(LeaderElectionTelemetryConstants.TagNames.Provider, _providerName);
            }
            catch (Exception failure)
            {
                RecordNotificationFailure(failure);
                // ActivityStarted may throw after creating our Activity but before returning it.
                var current = Activity.Current;
                if (activity is null && current is not null && !ReferenceEquals(current, previous)
                    && ReferenceEquals(current.Source, _activitySource) && ReferenceEquals(current.Parent, previous))
                {
                    activity = current;
                }
            }
            try
            {
                if (start) { await _inner.StartAsync(cancellationToken).ConfigureAwait(false); }
                else { await _inner.StopAsync(cancellationToken).ConfigureAwait(false); }
            }
            catch (Exception failure)
            {
                coreFailure = failure;
                try { activity?.SetStatus(ActivityStatusCode.Error, failure.Message); }
                catch (Exception instrumentationFailure) { RecordNotificationFailure(instrumentationFailure); }
            }
            if (!start && coreFailure is null)
            {
                try { RecordLeaseDurationIfActive(); }
                catch (Exception failure) { RecordNotificationFailure(failure); }
            }
        }
        finally
        {
            try { activity?.Dispose(); }
            catch (Exception failure) { RecordNotificationFailure(failure); }
            finally
            {
                try
                {
                    if (activity is not null && ReferenceEquals(Activity.Current, activity)) { Activity.Current = previous; }
                }
                catch (Exception failure) { RecordNotificationFailure(failure); }
            }
        }
        // Optional instrumentation does not replace the operation's outcome. Its first failure
        // remains observable through the permanent notification/instrumentation completion task.
        LeaderElectionNotifications.ThrowFirst(coreFailure);
    }

    private IDisposable AdmitInstrumentOperation()
    {
        lock (_disposalGate)
        {
            ObjectDisposedException.ThrowIf(_admissionClosed, this);
            if (!_instrumentOperations.TryEnter(out var operation)) { throw new ObjectDisposedException(nameof(TelemetryLeaderElection)); }
            return operation;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        lock (_disposalGate)
        {
            _admissionClosed = true;
            _ = _instrumentOperations.Seal();
            if (!_disposeStarted)
            {
                _disposeStarted = true;
                _ = PublishInnerDisposalAsync();
            }
            return new ValueTask(_resourceDisposal);
        }
    }

    private async Task PublishInnerDisposalAsync()
    {
        try
        {
            // Observe the complete worker, including transaction-suppression setup and teardown,
            // before publishing resource completion. Notification delivery has its own join.
            await LeaderElectionExecution.RunAsync(async () => await _inner.DisposeAsync().ConfigureAwait(false)).ConfigureAwait(false);
            _resourcePublication.TrySetResult(Task.CompletedTask);
        }
        catch (Exception failure) { _resourcePublication.TrySetResult(Task.FromException(failure)); }
    }

    private async Task ObserveTerminalAsync()
    {
        Exception? failure = null;
        try { await _inner.NotificationsCompleted.ConfigureAwait(false); }
        catch (Exception error) { failure = error; }
        Task instrumentDrain;
        lock (_disposalGate)
        {
            _admissionClosed = true;
            instrumentDrain = _instrumentOperations.Seal();
        }
        try { await instrumentDrain.ConfigureAwait(false); }
        catch (Exception error) { failure ??= error; }
        failure ??= Volatile.Read(ref _firstNotificationFailure);
        DetachAll(ref failure);
        try { _gaugeRegistration.Dispose(); }
        catch (Exception error) { failure ??= error; }
        try { _instrumentLease?.Dispose(); }
        catch (Exception error) { failure ??= error; }
        if (failure is null) { _terminal.TrySetResult(); }
        else { _terminal.TrySetException(failure); }
    }

    private void DetachAll(ref Exception? failure)
    {
        try { _inner.BecameLeader -= HandleBecameLeader; } catch (Exception error) { failure ??= error; }
        try { _inner.LostLeadership -= HandleLostLeadership; } catch (Exception error) { failure ??= error; }
        try { _inner.LeaderChanged -= HandleLeaderChanged; } catch (Exception error) { failure ??= error; }
        try { _inner.AcquisitionFailed -= HandleAcquisitionFailed; } catch (Exception error) { failure ??= error; }
    }

    private TagList CreateTags(string candidateId, string? result = null)
    {
        var tags = new TagList
        {
            { LeaderElectionTelemetryConstants.TagNames.Instance, _instanceGuard.Guard(candidateId) },
            { LeaderElectionTelemetryConstants.TagNames.Provider, _providerName },
        };
        if (result is not null) { tags.Add(LeaderElectionTelemetryConstants.TagNames.Result, result); }
        return tags;
    }

    private void RecordNotificationFailure(Exception failure) =>
        Interlocked.CompareExchange(ref _firstNotificationFailure, failure, null);

    private Measurement<int>? ObserveLeadership()
    {
        IDisposable? operation = null;
        try
        {
            lock (_disposalGate)
            {
                if (_admissionClosed || !_instrumentOperations.TryEnter(out operation)) { return null; }
            }
            return new Measurement<int>(_inner.IsLeader ? 1 : 0, CreateTags(_inner.CandidateId));
        }
        catch (Exception failure) { RecordNotificationFailure(failure); return null; }
        finally { operation?.Dispose(); }
    }

    private void Forward<T>(EventHandler<T>? subscribers, T args, Exception? failure = null) where T : EventArgs
    {
        using var scope = LeaderElectionNotifications.EnterNotificationScope(NotificationsCompleted);
        var consumerFailure = LeaderElectionNotifications.InvokeAll(subscribers, this, args);
        failure ??= consumerFailure;
        if (failure is not null)
        {
            Interlocked.CompareExchange(ref _firstNotificationFailure, failure, null);
            LeaderElectionNotifications.ThrowFirst(failure);
        }
    }

    private void HandleBecameLeader(object? sender, LeaderElectionEventArgs args)
    {
        Exception? failure = null;
        try
        {
            var timestamp = _timeProvider.GetTimestamp();
            lock (_leaseTimestampLock) { _leaseStartTimestamp = timestamp; }
        }
        catch (Exception error) { failure = error; }
        try { _acquisitionsCounter.Add(1, CreateTags(args.CandidateId, "acquired")); }
        catch (Exception error) { failure ??= error; }
        Forward(BecameLeader, args, failure);
    }

    private void HandleLostLeadership(object? sender, LeaderElectionEventArgs args)
    {
        Exception? failure = null;
        try { RecordLeaseDurationIfActive(); } catch (Exception error) { failure = error; }
        try { _acquisitionsCounter.Add(1, CreateTags(args.CandidateId, "lost")); } catch (Exception error) { failure ??= error; }
        Forward(LostLeadership, args, failure);
    }

    private void HandleLeaderChanged(object? sender, LeaderChangedEventArgs args) => Forward(LeaderChanged, args);

    private void HandleAcquisitionFailed(object? sender, LeaderElectionAcquisitionFailedEventArgs args)
    {
        Exception? failure = null;
        try { _acquisitionsCounter.Add(1, CreateTags(args.CandidateId, "failed")); }
        catch (Exception error) { failure = error; }
        Forward(AcquisitionFailed, args, failure);
    }

    private void RecordLeaseDurationIfActive()
    {
        long? start;
        lock (_leaseTimestampLock) { start = _leaseStartTimestamp; _leaseStartTimestamp = null; }
        if (start.HasValue)
        {
            _leaseDurationHistogram.Record(_timeProvider.GetElapsedTime(start.Value).TotalSeconds, CreateTags(_inner.CandidateId));
        }
    }
}
