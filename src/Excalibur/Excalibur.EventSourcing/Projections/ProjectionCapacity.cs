// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.RateLimiting;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.EventSourcing.Projections;

internal enum ProjectionCapacityKind { Read, Write, Rebuild }

/// <summary>Owns stable FIFO budgets. A lease lives until its actual storage operation completes.</summary>
internal sealed class ProjectionCapacity : IDisposable
{
    internal static void Register(IServiceCollection services)
    {
        services.TryAddSingleton<ProjectionCapacity>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ProjectionCapacityOptions>, ProjectionCapacityOptionsValidator>());
        _ = services.AddOptions<ProjectionCapacityOptions>().ValidateOnStart();
    }

    private static readonly AsyncLocal<AdmissionFrame?> CurrentStorage = new();
    private static readonly AsyncLocal<AdmissionFrame?> CurrentRebuild = new();
    private static readonly AsyncLocal<AdmissionFrame?> CurrentObservation = new();
    private readonly Lock _sync = new();
    private readonly Dictionary<string, ConcurrencyLimiter[]> _resources = new(StringComparer.Ordinal);
    private readonly int[] _limits;
    private readonly int _queueLimit;
    private readonly int _maxResources;
    private readonly ConcurrencyLimiter _processGate;
    private readonly Meter _meter = new("Excalibur.EventSourcing.ProjectionCapacity");
    private readonly UpDownCounter<long> _waiting;
    private readonly UpDownCounter<long> _active;
    private readonly Counter<long> _rejected;
    private readonly Histogram<double> _waitDuration;
    private bool _disposed;

    public ProjectionCapacity(IOptions<ProjectionCapacityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;
        ArgumentOutOfRangeException.ThrowIfLessThan(value.MaxConcurrentOperations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(value.MaxConcurrentReads, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(value.MaxConcurrentWrites, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(value.MaxConcurrentRebuilds, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(value.QueueLimit);
        ArgumentOutOfRangeException.ThrowIfLessThan(value.MaxResources, 1);
        _limits = [value.MaxConcurrentReads, value.MaxConcurrentWrites, value.MaxConcurrentRebuilds];
        _queueLimit = value.QueueLimit;
        _maxResources = value.MaxResources;
        _processGate = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = value.MaxConcurrentOperations, QueueLimit = _queueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
        _waiting = _meter.CreateUpDownCounter<long>("projection.capacity.waiting", "{operation}");
        _active = _meter.CreateUpDownCounter<long>("projection.capacity.active", "{operation}");
        _rejected = _meter.CreateCounter<long>("projection.capacity.rejected", "{operation}");
        _waitDuration = _meter.CreateHistogram<double>("projection.capacity.wait.duration", "s");
    }

    internal async ValueTask<string?> ResolveResourceAsync(IServiceProvider store, CancellationToken cancellationToken)
    {
        // Metadata resolution may itself perform I/O. Admit it conservatively, then release before
        // acquiring the resolved resource. Routing is evaluated for every operation, never per type.
        var scope = await ReadScopeAsync(store, cancellationToken).ConfigureAwait(false);
        return Normalize(scope);
    }

    internal async ValueTask<string?> ReadScopeAsync(IServiceProvider store, CancellationToken cancellationToken)
    {
        return store.GetService(typeof(IAsyncProjectionDestinationScope)) is IAsyncProjectionDestinationScope asynchronous
            ? await ExecuteAsync(null, ProjectionCapacityKind.Read, asynchronous.GetScopeAsync, cancellationToken).ConfigureAwait(false)
            : (store.GetService(typeof(IProjectionDestinationScope)) as IProjectionDestinationScope)?.Scope;
    }

    internal async ValueTask<T> ExecuteStoreAsync<T>(IServiceProvider store, ProjectionCapacityKind kind,
        Func<CancellationToken, ValueTask<T>> operation, CancellationToken cancellationToken)
    {
        var resource = await ResolveResourceAsync(store, cancellationToken).ConfigureAwait(false);
        return await ExecuteAsync(resource, kind, async token =>
        {
            // Client configuration and tenant routing must remain stable during an operation.
            // Recheck after waiting so a route changed while queued fails before the data call.
            var current = store.GetService(typeof(IAsyncProjectionDestinationScope)) is IAsyncProjectionDestinationScope asynchronous
                ? await asynchronous.GetScopeAsync(token).ConfigureAwait(false)
                : (store.GetService(typeof(IProjectionDestinationScope)) as IProjectionDestinationScope)?.Scope;
            if (!string.Equals(resource, Normalize(current), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The projection storage resource changed while waiting for capacity. Retry with stable tenant routing and client configuration.");
            }
            return await operation(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static string? Normalize(string? resource) =>
        resource?.StartsWith("inmemory/", StringComparison.Ordinal) == true ? "inmemory" : resource;

    internal async ValueTask<T> ExecuteAsync<T>(string? resource, ProjectionCapacityKind kind,
        Func<CancellationToken, ValueTask<T>> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateAdmission(kind);

        var gate = GetGate(resource, kind);
        var tag = new KeyValuePair<string, object?>("operation", kind switch
        {
            ProjectionCapacityKind.Read => "read", ProjectionCapacityKind.Write => "write", _ => "rebuild",
        });
        using (var lease = await AcquireAsync(gate, tag, "resource", cancellationToken).ConfigureAwait(false))
        {
            // One acquisition order everywhere: resource, then process. Rebuild cells never hold
            // this storage permit while invoking handlers or waiting for their own storage calls.
            using var processLease = kind == ProjectionCapacityKind.Rebuild ? null
                : await AcquireAsync(_processGate, tag, "process", cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var context = kind == ProjectionCapacityKind.Rebuild ? CurrentRebuild : CurrentStorage;
            var previous = context.Value;
            var frame = new AdmissionFrame();
            context.Value = frame;
            Observe(_active, 1, tag);
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                frame.Active = false;
                context.Value = previous;
                Observe(_active, -1, tag);
            }
        }
    }

    internal static void ValidateAdmission(ProjectionCapacityKind kind)
    {
        // Check before any local gate as well as before shared admission. Rebuild callbacks must
        // never queue recursively behind the local rebuild semaphore they already own.
        if (CurrentObservation.Value is { Active: true })
        {
            throw new InvalidOperationException("A projection capacity metric listener cannot recursively invoke governed storage.");
        }
        if (CurrentStorage.Value is { Active: true })
        {
            throw new InvalidOperationException("A storage callback or serializer cannot recursively invoke governed projection storage. Perform dependent storage work in the projection handler instead.");
        }
        if (kind == ProjectionCapacityKind.Rebuild && CurrentRebuild.Value is { Active: true })
        {
            throw new InvalidOperationException("A projection rebuild cannot recursively start another rebuild. Schedule dependent rebuilds after the current rebuild completes.");
        }
    }

    private async ValueTask<RateLimitLease> AcquireAsync(ConcurrencyLimiter gate,
        KeyValuePair<string, object?> operation, string budget, CancellationToken token)
    {
        var tags = new TagList { operation, new("budget", budget) };
        var started = Stopwatch.GetTimestamp();
        Observe(_waiting, 1, tags);
        try
        {
            var lease = await gate.AcquireAsync(1, token).ConfigureAwait(false);
            if (lease.IsAcquired) { return lease; }
            lease.Dispose();
            if (_rejected.Enabled) { Observe((_rejected, tags), static state => state._rejected.Add(1, state.tags)); }
            throw new InvalidOperationException("Projection storage capacity is unavailable because its bounded queue is full or the service is stopping. Retry the operation; no storage callback has run.");
        }
        finally
        {
            Observe(_waiting, -1, tags);
            if (_waitDuration.Enabled)
            { Observe((_waitDuration, started, tags), static state => state._waitDuration.Record(Stopwatch.GetElapsedTime(state.started).TotalSeconds, state.tags)); }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) { return; }
            _disposed = true;
            _processGate.Dispose();
            foreach (var gates in _resources.Values)
            {
                foreach (var gate in gates) { gate.Dispose(); }
            }
        }
        _meter.Dispose();
    }

    private ConcurrencyLimiter GetGate(string? resource, ProjectionCapacityKind kind)
    {
        ArgumentOutOfRangeException.ThrowIfNegative((int)kind);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)kind, 2);
        if (resource?.StartsWith("inmemory/", StringComparison.Ordinal) == true) { resource = "inmemory"; }
        var key = string.IsNullOrWhiteSpace(resource) ? "unknown" : "identified:" + resource;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_resources.TryGetValue(key, out var gates))
            {
                if (_resources.Count >= _maxResources)
                {
                    throw new InvalidOperationException("The projection capacity resource limit was reached. Resource identities must describe stable storage resources, not tenants, projection keys, or generations.");
                }
                gates = new ConcurrencyLimiter[3];
                for (var index = 0; index < gates.Length; index++)
                {
                    gates[index] = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
                    {
                        PermitLimit = _limits[index], QueueLimit = _queueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    });
                }
                _resources.Add(key, gates);
            }
            return gates[(int)kind];
        }
    }

    private sealed class AdmissionFrame
    {
        internal volatile bool Active = true;
    }

    private static void Observe(UpDownCounter<long> counter, long value, KeyValuePair<string, object?> tag)
    {
        if (!counter.Enabled) { return; }
        Observe((counter, value, tag), static state => state.counter.Add(state.value, state.tag));
    }

    private static void Observe(UpDownCounter<long> counter, long value, TagList tags)
    {
        if (!counter.Enabled) { return; }
        Observe((counter, value, tags), static state => state.counter.Add(state.value, state.tags));
    }

    private static void Observe<TState>(TState state, Action<TState> observation)
    {
        var previous = CurrentObservation.Value;
        var frame = new AdmissionFrame();
        CurrentObservation.Value = frame;
        try { observation(state); }
        catch (Exception) { /* User-installed metric listeners cannot participate in storage correctness. */ }
        finally { frame.Active = false; CurrentObservation.Value = previous; }
    }
}
