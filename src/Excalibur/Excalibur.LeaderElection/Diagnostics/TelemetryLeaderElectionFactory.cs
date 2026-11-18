// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Diagnostics;

/// <summary>Creates telemetry wrappers with terminal-notification-scoped instrument leases.</summary>
public sealed class TelemetryLeaderElectionFactory : ILeaderElectionFactory, IDisposable
{
    private readonly Lock _creationGate = new();
    private readonly ILeaderElectionFactory _inner;
    private readonly LeaderElectionInstrumentation _instrumentation;
    private readonly string _providerName;
    private readonly TimeProvider _timeProvider;
    private volatile bool _disposed;

    /// <summary>Initializes a telemetry factory that owns the supplied instruments.</summary>
    /// <param name="inner">Borrowed underlying factory.</param>
    /// <param name="meter">Owned meter, closed after the factory and all wrapper leases finish.</param>
    /// <param name="activitySource">Owned activity source with the same lifetime.</param>
    /// <param name="providerName">Provider label.</param>
    /// <param name="timeProvider">Monotonic metric clock.</param>
    public TelemetryLeaderElectionFactory(ILeaderElectionFactory inner, Meter meter, ActivitySource activitySource,
        string providerName, TimeProvider? timeProvider = null)
        : this(inner, new LeaderElectionInstrumentation(
            meter ?? throw new ArgumentNullException(nameof(meter)),
            activitySource ?? throw new ArgumentNullException(nameof(activitySource)), true, true), providerName, timeProvider) { }

    private TelemetryLeaderElectionFactory(ILeaderElectionFactory inner, LeaderElectionInstrumentation instrumentation,
        string providerName, TimeProvider? timeProvider)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        _instrumentation = instrumentation;
        _providerName = providerName;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets permanent completion of the instruments owned by this factory.</summary>
    /// <remarks>
    /// Dispose seals creation but never waits for a child's callback. This stable task settles after
    /// every admitted wrapper's notifications and proxy activities finish and owned instruments close.
    /// Do not await it from one of this factory's child callbacks. Borrowed IMeterFactory meters remain
    /// subject to their external owner's lifetime; this task cannot extend that owner's lifetime.
    /// </remarks>
    public Task InstrumentationCompleted => _instrumentation.Completion;

    internal static TelemetryLeaderElectionFactory CreateOwned(ILeaderElectionFactory inner, IMeterFactory? meterFactory,
        string providerName, TimeProvider? timeProvider = null)
    {
        var instruments = LeaderElectionInstrumentation.Create(meterFactory);
        try { return new TelemetryLeaderElectionFactory(inner, instruments, providerName, timeProvider); }
        catch { instruments.Dispose(); throw; }
    }

    /// <inheritdoc/>
    public ILeaderElection CreateElection(string resourceName, string? candidateId)
    {
        LeaderElectionInstrumentation.InstrumentLease lease;
        lock (_creationGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            lease = _instrumentation.Acquire();
        }
        ILeaderElection? inner = null;
        try
        {
            inner = _inner.CreateElection(resourceName, candidateId);
            return TelemetryLeaderElection.CreateLeased(inner, _instrumentation, lease, _providerName, _timeProvider);
        }
        catch
        {
            if (inner is not null) { lease.RetainFailedCreationCleanup(inner); }
            else { lease.Dispose(); }
            throw;
        }
    }

    /// <inheritdoc/>
    public IHealthBasedLeaderElection CreateHealthBasedElection(string resourceName, string? candidateId)
    {
        LeaderElectionInstrumentation.InstrumentLease lease;
        lock (_creationGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            lease = _instrumentation.Acquire();
        }
        // Preserve the health interface; these returned provider-specific instances are not telemetry wrappers.
        using (lease) { return _inner.CreateHealthBasedElection(resourceName, candidateId); }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_creationGate) { _disposed = true; }
        _instrumentation.Dispose();
    }
}
