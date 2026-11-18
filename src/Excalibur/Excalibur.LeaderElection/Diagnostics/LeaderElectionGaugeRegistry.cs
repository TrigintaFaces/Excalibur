// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace Excalibur.LeaderElection.Diagnostics;

/// <summary>One observable gauge per meter, with removable strong registrations for live wrappers.</summary>
internal sealed class LeaderElectionGaugeRegistry
{
    private static readonly ConditionalWeakTable<Meter, LeaderElectionGaugeRegistry> Registries = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<long, Entry> _entries = [];
    private long _next;
    private bool _initialized;
    private Exception? _initializationFailure;

    internal static IDisposable Register(Meter meter, Func<Measurement<int>?> observe, Action<Exception> failure)
    {
        // Publish the registry before instrument creation, which can invoke foreign listeners.
        // A reentrant registration therefore sees the same initialization attempt without self-waiting.
        var registry = Registries.GetValue(meter, static _ => new LeaderElectionGaugeRegistry());
        long id;
        lock (registry._gate)
        {
            id = checked(++registry._next);
            registry._entries.Add(id, new Entry(observe, failure));
        }
        registry.EnsureInitialized(meter);
        return new Registration(registry, id);
    }

    internal static int RegistrationCount(Meter meter)
    {
        if (!Registries.TryGetValue(meter, out var registry)) { return 0; }
        lock (registry._gate) { return registry._entries.Count; }
    }

    private void EnsureInitialized(Meter meter)
    {
        Exception? prior;
        bool initialize;
        lock (_gate)
        {
            prior = _initializationFailure;
            initialize = !_initialized;
            _initialized = true;
        }
        if (!initialize)
        {
            if (prior is not null) { RecordInitializationFailure(prior); }
            return;
        }
        try
        {
            _ = meter.CreateObservableGauge<int>(LeaderElectionTelemetryConstants.MetricNames.IsLeader,
                Observe, "{status}", "Current leadership status (0=follower, 1=leader)");
        }
        catch (Exception failure)
        {
            lock (_gate) { _initializationFailure ??= failure; }
            RecordInitializationFailure(failure);
        }
    }

    private void RecordInitializationFailure(Exception failure)
    {
        Entry[] entries;
        lock (_gate) { entries = [.. _entries.Values]; }
        foreach (var entry in entries) { entry.Failure(failure); }
    }

    private IEnumerable<Measurement<int>> Observe()
    {
        Entry[] entries;
        lock (_gate) { entries = [.. _entries.Values]; }
        var values = new List<Measurement<int>>(entries.Length);
        foreach (var entry in entries)
        {
            try { if (entry.Observe() is { } value) { values.Add(value); } }
            catch (Exception failure) { entry.Failure(failure); }
        }
        return values;
    }

    private sealed record Entry(Func<Measurement<int>?> Observe, Action<Exception> Failure);

    private sealed class Registration(LeaderElectionGaugeRegistry owner, long id) : IDisposable
    {
        private LeaderElectionGaugeRegistry? _owner = owner;
        public void Dispose()
        {
            var registry = Interlocked.Exchange(ref _owner, null);
            if (registry is not null) { lock (registry._gate) { registry._entries.Remove(id); } }
        }
    }
}
