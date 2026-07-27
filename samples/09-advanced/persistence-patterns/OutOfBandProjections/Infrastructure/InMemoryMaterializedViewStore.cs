// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text.Json;

using Excalibur.EventSourcing;

namespace OutOfBandProjections.Infrastructure;

/// <summary>
/// Simple in-memory implementation of <see cref="IAtomicMaterializedViewStore"/> for demonstration.
/// In production, use the SqlServer or Postgres store, or MongoDB with transactions enabled.
/// </summary>
/// <remarks>
/// Implements <see cref="IAtomicMaterializedViewStore"/> because an exactly-once projection requires a store
/// that commits the view and its checkpoint together. A store that only implements
/// <see cref="IMaterializedViewStore"/> is refused at startup rather than silently degrading to at-least-once.
/// </remarks>
public sealed class InMemoryMaterializedViewStore : IAtomicMaterializedViewStore
{
    private readonly ConcurrentDictionary<string, byte[]> _views = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _positions = new(StringComparer.Ordinal);

    /// <summary>Serialises the view+position write so the pair is observed all-or-nothing.</summary>
    private readonly Lock _atomicWriteGate = new();

    /// <inheritdoc />
    public bool SupportsAtomicWrites => true;

    /// <inheritdoc />
    public ValueTask SaveViewAndPositionAsync<TView>(
        string viewName,
        string viewId,
        TView view,
        long position,
        CancellationToken cancellationToken)
        where TView : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewId);
        ArgumentNullException.ThrowIfNull(view);

        var key = $"{viewName}:{viewId}";
        var payload = JsonSerializer.SerializeToUtf8Bytes(view);

        // Both writes happen under one lock, so no reader observes the view advanced without its checkpoint.
        // The position advance is monotonic: a delayed write never rewinds an already-higher checkpoint.
        lock (_atomicWriteGate)
        {
            _views[key] = payload;
            _ = _positions.AddOrUpdate(viewName, position, (_, existing) => Math.Max(existing, position));
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<TView?> GetAsync<TView>(string viewName, string viewId, CancellationToken cancellationToken)
        where TView : class
    {
        var key = $"{viewName}:{viewId}";
        if (_views.TryGetValue(key, out var data))
        {
            return new ValueTask<TView?>(JsonSerializer.Deserialize<TView>(data));
        }

        return new ValueTask<TView?>((TView?)null);
    }

    /// <inheritdoc />
    public ValueTask SaveAsync<TView>(string viewName, string viewId, TView view, CancellationToken cancellationToken)
        where TView : class
    {
        var key = $"{viewName}:{viewId}";
        _views[key] = JsonSerializer.SerializeToUtf8Bytes(view);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(string viewName, string viewId, CancellationToken cancellationToken)
    {
        var key = $"{viewName}:{viewId}";
        _views.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<long?> GetPositionAsync(string viewName, CancellationToken cancellationToken)
    {
        if (_positions.TryGetValue(viewName, out var position))
        {
            return new ValueTask<long?>((long?)position);
        }

        return new ValueTask<long?>((long?)null);
    }

    /// <inheritdoc />
    public ValueTask SavePositionAsync(string viewName, long position, CancellationToken cancellationToken)
    {
        _positions[viewName] = position;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets all stored views for display purposes.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> GetAllViews() =>
        new Dictionary<string, byte[]>(_views, StringComparer.Ordinal);
}
