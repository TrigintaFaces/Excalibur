// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Excalibur.Data.Sharding;

namespace Excalibur.EventSourcing.Sharding;

/// <summary>Owns completed physical bindings; cache hits do not acquire the construction gate.</summary>
/// <remarks>Disposal stops publication and detaches ownership; it does not join active store operations.</remarks>
internal sealed class TenantShardBindingCache : IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ShardReadinessBinding> _bindings = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private volatile bool _disposed;
    private Task? _disposeTask;

    internal ShardReadinessBinding Get(ShardInfo shard, Func<ShardInfo, ShardReadinessBinding> factory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_bindings.TryGetValue(shard.ShardId, out var existing)) { return existing; }
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_bindings.TryGetValue(shard.ShardId, out existing)) { return existing; }
            var binding = factory(shard);
            _bindings[shard.ShardId] = binding;
            return binding;
        }
    }


    // SQL bindings retain only a non-disposable store, reader and connection factory. Connections
    // belong to individual operations and are disposed there, not by this cache. This guarded path
    // preserves ordinary synchronous ServiceProvider.Dispose without blocking an async operation.
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }
            if (_bindings.Values.Any(binding => binding.OwnsDisposableResources))
            {
                throw new InvalidOperationException("Owned resources require asynchronous binding disposal.");
            }
            _disposed = true;
            _bindings.Clear();
            _disposeTask = Task.CompletedTask;
        }
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource? completion = null;
        ShardReadinessBinding[] detached = [];
        Task task;
        lock (_gate)
        {
            if (_disposeTask is null)
            {
                _disposed = true;
                detached = _bindings.Values.ToArray();
                _bindings.Clear();
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposeTask = completion.Task;
            }
            task = _disposeTask;
        }
        if (completion is not null) { _ = DisposeDetachedAsync(detached, completion); }
        return new(task);
    }

    private static async Task DisposeDetachedAsync(ShardReadinessBinding[] bindings, TaskCompletionSource completion)
    {
        List<Exception>? errors = null;
        foreach (var binding in bindings)
        {
            try { await binding.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        if (errors is null) { completion.TrySetResult(); }
        else { completion.TrySetException(errors); }
    }
}

/// <summary>A store and readiness delegate sharing the exact same owned physical resources.</summary>
internal sealed class ShardReadinessBinding(IEventStore store, Func<CancellationToken, Task> probe,
    IReadOnlyList<IDisposable> resources) : IAsyncDisposable
{
    internal bool OwnsDisposableResources => resources.Count != 0 || Store is IDisposable or IAsyncDisposable;
    internal IEventStore Store { get; } = store;
    internal Func<CancellationToken, Task> Probe { get; } = probe;

    public async ValueTask DisposeAsync()
    {
        List<Exception>? errors = null;
        async ValueTask ReleaseAsync(object value)
        {
            try
            {
                if (value is IAsyncDisposable asynchronous) { await asynchronous.DisposeAsync().ConfigureAwait(false); }
                else if (value is IDisposable synchronous) { synchronous.Dispose(); }
            }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        await ReleaseAsync(Store).ConfigureAwait(false);
        for (var i = resources.Count - 1; i >= 0; i--) { await ReleaseAsync(resources[i]).ConfigureAwait(false); }
        if (errors is { Count: 1 }) { ExceptionDispatchInfo.Capture(errors[0]).Throw(); }
        if (errors is not null) { throw new AggregateException(errors); }
    }

    // Every factory validates coordinates/options before allocating resources. This stage owns each
    // allocated client immediately and rolls it back if later construction fails, preserving causes.
    internal static ShardReadinessBinding Construct(Func<Construction, ShardReadinessBinding> factory)
    {
        var construction = new Construction();
        try { return factory(construction); }
        catch (Exception original)
        {
            List<Exception> failures = [original];
            foreach (var resource in construction.Resources.AsEnumerable().Reverse())
            {
                try { resource.Dispose(); }
                catch (Exception cleanup) { failures.Add(cleanup); }
            }
            if (failures.Count > 1) { throw new AggregateException(failures); }
            throw;
        }
    }

    internal sealed class Construction
    {
        internal List<IDisposable> Resources { get; } = [];
        internal T Own<T>(T resource) where T : IDisposable { Resources.Add(resource); return resource; }
        internal ShardReadinessBinding Complete(IEventStore store, Func<CancellationToken, Task> probe)
            => new(store, probe, Resources);
    }
}
